using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace ACS.Infrastructure.Configuration;

/// <summary>
/// Service for validating configuration at application startup
/// </summary>
public interface IConfigurationValidationService
{
    ValidationResult ValidateConfiguration();
    Task<ValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default);
    void ValidateAndThrow();
    Task ValidateAndThrowAsync(CancellationToken cancellationToken = default);
}

public class ConfigurationValidationService : IConfigurationValidationService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConfigurationValidationService> _logger;
    private readonly List<IConfigurationValidator> _validators;

    public ConfigurationValidationService(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<ConfigurationValidationService> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _validators = new List<IConfigurationValidator>();
        
        RegisterDefaultValidators();
    }

    private void RegisterDefaultValidators()
    {
        _validators.Add(new RequiredSettingsValidator());
        _validators.Add(new ConnectionStringValidator());
        _validators.Add(new JwtConfigurationValidator());
        _validators.Add(new PathConfigurationValidator());
        _validators.Add(new EmailConfigurationValidator());
        _validators.Add(new RateLimitConfigurationValidator());
        _validators.Add(new DatabaseConfigurationValidator());
    }

    public ValidationResult ValidateConfiguration()
    {
        var result = new ValidationResult { IsValid = true };
        
        _logger.LogInformation("Starting configuration validation");

        foreach (var validator in _validators)
        {
            try
            {
                var validationResult = validator.Validate(_configuration, _serviceProvider);
                
                if (!validationResult.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(validationResult.Errors);
                }
                
                result.Warnings.AddRange(validationResult.Warnings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during validation with {Validator}", validator.GetType().Name);
                result.IsValid = false;
                result.Errors.Add($"Validation error in {validator.GetType().Name}: {ex.Message}");
            }
        }

        // Validate options using data annotations
        ValidateOptionsWithDataAnnotations(result);

        if (result.IsValid)
        {
            _logger.LogInformation("Configuration validation passed");
        }
        else
        {
            _logger.LogError("Configuration validation failed with {ErrorCount} errors", result.Errors.Count);
            foreach (var error in result.Errors)
            {
                _logger.LogError("Configuration error: {Error}", error);
            }
        }

        if (result.Warnings.Any())
        {
            _logger.LogWarning("Configuration validation completed with {WarningCount} warnings", result.Warnings.Count);
            foreach (var warning in result.Warnings)
            {
                _logger.LogWarning("Configuration warning: {Warning}", warning);
            }
        }

        return result;
    }

    public async Task<ValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var result = ValidateConfiguration();

        // Perform async validations
        foreach (var validator in _validators.OfType<IAsyncConfigurationValidator>())
        {
            try
            {
                var asyncResult = await validator.ValidateAsync(_configuration, _serviceProvider, cancellationToken);
                
                if (!asyncResult.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(asyncResult.Errors);
                }
                
                result.Warnings.AddRange(asyncResult.Warnings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during async validation with {Validator}", validator.GetType().Name);
                result.IsValid = false;
                result.Errors.Add($"Async validation error in {validator.GetType().Name}: {ex.Message}");
            }
        }

        return result;
    }

    public void ValidateAndThrow()
    {
        var result = ValidateConfiguration();
        
        if (!result.IsValid)
        {
            var errors = string.Join(Environment.NewLine, result.Errors);
            throw new ConfigurationValidationException($"Configuration validation failed:{Environment.NewLine}{errors}");
        }
    }

    public async Task ValidateAndThrowAsync(CancellationToken cancellationToken = default)
    {
        var result = await ValidateConfigurationAsync(cancellationToken);
        
        if (!result.IsValid)
        {
            var errors = string.Join(Environment.NewLine, result.Errors);
            throw new ConfigurationValidationException($"Configuration validation failed:{Environment.NewLine}{errors}");
        }
    }

    private void ValidateOptionsWithDataAnnotations(ValidationResult result)
    {
        // Validate all registered options
        var optionsTypes = _serviceProvider.GetServices<IOptions<object>>()
            .Select(o => o.GetType())
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IOptions<>))
            .Select(t => t.GetGenericArguments()[0])
            .Distinct();

        foreach (var optionsType in optionsTypes)
        {
            try
            {
                var options = _serviceProvider.GetService(typeof(IOptions<>).MakeGenericType(optionsType));
                if (options != null)
                {
                    var value = options.GetType().GetProperty("Value")?.GetValue(options);
                    if (value != null)
                    {
                        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(value);
                        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
                        
                        if (!Validator.TryValidateObject(value, validationContext, validationResults, true))
                        {
                            result.IsValid = false;
                            foreach (var validationResult in validationResults)
                            {
                                result.Errors.Add($"{optionsType.Name}: {validationResult.ErrorMessage}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not validate options type {OptionsType}", optionsType.Name);
            }
        }
    }
}

/// <summary>
/// Base interface for configuration validators
/// </summary>
public interface IConfigurationValidator
{
    ValidationResult Validate(IConfiguration configuration, IServiceProvider serviceProvider);
}

/// <summary>
/// Interface for async configuration validators
/// </summary>
public interface IAsyncConfigurationValidator : IConfigurationValidator
{
    Task<ValidationResult> ValidateAsync(IConfiguration configuration, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Exception thrown when configuration validation fails
/// </summary>
public class ConfigurationValidationException : Exception
{
    public ConfigurationValidationException(string message) : base(message) { }
    public ConfigurationValidationException(string message, Exception innerException) : base(message, innerException) { }
}

#region Validators

public class RequiredSettingsValidator : IConfigurationValidator
{
    private readonly List<string> _requiredSettings = new()
    {
        "ConnectionStrings:DefaultConnection",
        "Authentication:Jwt:SecretKey",
        "Logging:LogLevel:Default"
    };

    private readonly Dictionary<string, List<string>> _environmentRequiredSettings = new()
    {
        ["Production"] = new List<string>
        {
            "ConnectionStrings:Redis",
            "KeyVault:VaultUri",
            "OpenTelemetry:OtlpEndpoint"
        },
        ["Development"] = new List<string>()
    };

    public ValidationResult Validate(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        var result = new ValidationResult { IsValid = true };
        var environment = configuration["Environment"] ?? "Production";

        // Check required settings
        foreach (var setting in _requiredSettings)
        {
            var value = configuration[setting];
            if (string.IsNullOrWhiteSpace(value))
            {
                result.IsValid = false;
                result.Errors.Add($"Required configuration setting '{setting}' is missing or empty");
            }
        }

        // Check environment-specific required settings
        if (_environmentRequiredSettings.ContainsKey(environment))
        {
            foreach (var setting in _environmentRequiredSettings[environment])
            {
                var value = configuration[setting];
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Required configuration setting '{setting}' for {environment} environment is missing or empty");
                }
            }
        }

        return result;
    }
}

public class ConnectionStringValidator : IConfigurationValidator
{
    public ValidationResult Validate(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        var result = new ValidationResult { IsValid = true };

        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(defaultConnection))
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(defaultConnection);
                
                // Validate connection string components
                if (string.IsNullOrEmpty(builder.DataSource))
                {
                    result.IsValid = false;
                    result.Errors.Add("Database connection string is missing server/data source");
                }
                
                if (string.IsNullOrEmpty(builder.InitialCatalog))
                {
                    result.IsValid = false;
                    result.Errors.Add("Database connection string is missing database name");
                }
                
                // Security warnings
                if (builder.IntegratedSecurity && configuration["Environment"] == "Production")
                {
                    result.Warnings.Add("Using integrated security in production is not recommended");
                }
                
                if (!string.IsNullOrEmpty(builder.Password) && builder.Password.Length < 12)
                {
                    result.Warnings.Add("Database password appears to be weak (less than 12 characters)");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid database connection string: {ex.Message}");
            }
        }

        // Validate Redis connection
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            // Basic Redis connection string validation
            if (!redisConnection.Contains(":") && !redisConnection.Contains(","))
            {
                result.Warnings.Add("Redis connection string may be invalid");
            }
        }

        return result;
    }
}

public class JwtConfigurationValidator : IConfigurationValidator
{
    public ValidationResult Validate(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        var result = new ValidationResult { IsValid = true };

        var jwtSecret = configuration["Authentication:Jwt:SecretKey"];
        if (!string.IsNullOrEmpty(jwtSecret))
        {
            // Check minimum key length (256 bits = 32 bytes)
            if (System.Text.Encoding.UTF8.GetByteCount(jwtSecret) < 32)
            {
                result.IsValid = false;
                result.Errors.Add("JWT secret key must be at least 256 bits (32 bytes) long");
            }

            // Check for default/weak keys
            var weakKeys = new[] 
            { 
                "development", 
                "secret", 
                "password", 
                "12345", 
                "default",
                "your-super-secret-key"
            };
            
            if (weakKeys.Any(weak => jwtSecret.ToLower().Contains(weak)) && 
                configuration["Environment"] == "Production")
            {
                result.IsValid = false;
                result.Errors.Add("JWT secret key appears to be a default or weak key in production");
            }
        }

        // Validate expiration
        var expiration = configuration.GetValue<int?>("Authentication:Jwt:ExpirationHours");
        if (expiration.HasValue)
        {
            if (expiration.Value < 1)
            {
                result.Errors.Add("JWT expiration hours must be at least 1");
                result.IsValid = false;
            }
            else if (expiration.Value > 168) // 1 week
            {
                result.Warnings.Add("JWT expiration is set to more than 1 week, consider shorter duration for security");
            }
        }

        return result;
    }
}

public class PathConfigurationValidator : IConfigurationValidator
{
    public ValidationResult Validate(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        var result = new ValidationResult { IsValid = true };

        var pathSettings = new Dictionary<string, string>
        {
            ["Database:Backup:DefaultPath"] = "Backup",
            ["DataArchiving:ArchivePath"] = "Archive",
            ["Logging:FilePath"] = "Log"
        };

        foreach (var (setting, name) in pathSettings)
        {
            var path = configuration[setting];
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    // Check if path is absolute
                    if (!Path.IsPathRooted(path))
                    {
                        result.Warnings.Add($"{name} path '{path}' is relative, consider using absolute path");
                    }

                    // Try to create directory if it doesn't exist
                    if (!Directory.Exists(path))
                    {
                        try
                        {
                            Directory.CreateDirectory(path);
                            result.Warnings.Add($"{name} directory '{path}' did not exist and was created");
                        }
                        catch
                        {
                            result.Warnings.Add($"{name} directory '{path}' does not exist and could not be created");
                        }
                    }

                    // Check write permissions
                    if (Directory.Exists(path))
                    {
                        try
                        {
                            var testFile = Path.Combine(path, $".write_test_{Guid.NewGuid()}.tmp");
                            File.WriteAllText(testFile, "test");
                            File.Delete(testFile);
                        }
                        catch
                        {
                            result.IsValid = false;
                            result.Errors.Add($"No write permission for {name} directory '{path}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Could not validate {name} path '{path}': {ex.Message}");
                }
            }
        }

        return result;
    }
}

public class EmailConfigurationValidator : IConfigurationValidator
{
    public ValidationResult Validate(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        var result = new ValidationResult { IsValid = true };

        var smtpHost = configuration["Alerting:Channels:Email:SmtpHost"];
        if (!string.IsNullOrEmpty(smtpHost))
        {
            var smtpPort = configuration.GetValue<int?>("Alerting:Channels:Email:SmtpPort");
            var username = configuration["Alerting:Channels:Email:Username"];
            var password = configuration["Alerting:Channels:Email:Password"];
            var fromAddress = configuration["Alerting:Channels:Email:FromAddress"];

            if (!smtpPort.HasValue || smtpPort.Value < 1 || smtpPort.Value > 65535)
            {
                result.Errors.Add("SMTP port is invalid");
                result.IsValid = false;
            }

            if (string.IsNullOrEmpty(fromAddress))
            {
                result.Errors.Add("SMTP from address is required when SMTP host is configured");
                result.IsValid = false;
            }
            else if (!IsValidEmail(fromAddress))
            {
                result.Errors.Add($"SMTP from address '{fromAddress}' is not a valid email");
                result.IsValid = false;
            }

            if (!string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
            {
                result.Warnings.Add("SMTP username is configured but password is missing");
            }
        }

        return result;
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

public class RateLimitConfigurationValidator : IConfigurationValidator
{
    public ValidationResult Validate(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        var result = new ValidationResult { IsValid = true };

        if (configuration.GetValue<bool>("RateLimit:Enabled"))
        {
            var defaultLimit = configuration.GetValue<int?>("RateLimit:DefaultPolicy:RequestLimit");
            var windowSize = configuration.GetValue<int?>("RateLimit:DefaultPolicy:WindowSizeSeconds");

            if (!defaultLimit.HasValue || defaultLimit.Value < 1)
            {
                result.Errors.Add("Rate limit default request limit must be at least 1");
                result.IsValid = false;
            }

            if (!windowSize.HasValue || windowSize.Value < 1)
            {
                result.Errors.Add("Rate limit window size must be at least 1 second");
                result.IsValid = false;
            }

            if (configuration.GetValue<bool>("RateLimit:UseDistributedStorage"))
            {
                var storageType = configuration["RateLimit:Storage:Type"];
                if (storageType == "Redis" && string.IsNullOrEmpty(configuration.GetConnectionString("Redis")))
                {
                    result.Errors.Add("Redis connection string is required when using Redis for rate limit storage");
                    result.IsValid = false;
                }
            }
        }

        return result;
    }
}

public class DatabaseConfigurationValidator : IConfigurationValidator
{
    public ValidationResult Validate(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        var result = new ValidationResult { IsValid = true };

        var minPoolSize = configuration.GetValue<int?>("Database:Performance:MinPoolSize");
        var maxPoolSize = configuration.GetValue<int?>("Database:Performance:MaxPoolSize");

        if (minPoolSize.HasValue && maxPoolSize.HasValue)
        {
            if (minPoolSize.Value > maxPoolSize.Value)
            {
                result.Errors.Add("Database minimum pool size cannot be greater than maximum pool size");
                result.IsValid = false;
            }

            if (maxPoolSize.Value > 500)
            {
                result.Warnings.Add("Database maximum pool size is very high (>500), this may cause issues");
            }
        }

        // Validate backup configuration
        if (configuration.GetValue<bool>("Database:Backup:Schedule:Enabled"))
        {
            var backupPath = configuration["Database:Backup:DefaultPath"];
            if (string.IsNullOrEmpty(backupPath))
            {
                result.Errors.Add("Database backup path is required when backups are enabled");
                result.IsValid = false;
            }
        }

        // Validate archiving configuration
        if (configuration.GetValue<bool>("DataArchiving:Schedule:Enabled"))
        {
            var retentionDays = configuration.GetValue<int?>("DataArchiving:Schedule:ArchiveSchedule:RetentionDays");
            if (!retentionDays.HasValue || retentionDays.Value < 1)
            {
                result.Errors.Add("Archive retention days must be at least 1");
                result.IsValid = false;
            }

            var purgeRetentionDays = configuration.GetValue<int?>("DataArchiving:Schedule:PurgeSchedule:RetentionDays");
            if (purgeRetentionDays.HasValue && retentionDays.HasValue && 
                purgeRetentionDays.Value <= retentionDays.Value)
            {
                result.Warnings.Add("Purge retention days should be greater than archive retention days");
            }
        }

        return result;
    }
}

#endregion

/// <summary>
/// Hosted service to validate configuration at startup
/// </summary>
public class ConfigurationValidationHostedService : IHostedService
{
    private readonly IConfigurationValidationService _validationService;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ConfigurationValidationHostedService> _logger;
    private readonly bool _failOnError;

    public ConfigurationValidationHostedService(
        IConfigurationValidationService validationService,
        IHostApplicationLifetime lifetime,
        ILogger<ConfigurationValidationHostedService> logger,
        IConfiguration configuration)
    {
        _validationService = validationService;
        _lifetime = lifetime;
        _logger = logger;
        _failOnError = configuration.GetValue<bool>("Configuration:FailOnValidationError", true);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating configuration at startup");

        try
        {
            var result = await _validationService.ValidateConfigurationAsync(cancellationToken);

            if (!result.IsValid && _failOnError)
            {
                _logger.LogCritical("Configuration validation failed. Stopping application");
                
                // Log all errors
                foreach (var error in result.Errors)
                {
                    _logger.LogCritical("Configuration error: {Error}", error);
                }

                // Stop the application
                _lifetime.StopApplication();
                
                var errors = string.Join(Environment.NewLine, result.Errors);
                throw new ConfigurationValidationException($"Configuration validation failed:{Environment.NewLine}{errors}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to validate configuration");
            
            if (_failOnError)
            {
                _lifetime.StopApplication();
                throw;
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Extension methods for configuration validation
/// </summary>
public static class ConfigurationValidationExtensions
{
    public static IServiceCollection AddConfigurationValidation(
        this IServiceCollection services,
        bool validateAtStartup = true)
    {
        services.AddSingleton<IConfigurationValidationService, ConfigurationValidationService>();

        if (validateAtStartup)
        {
            services.AddHostedService<ConfigurationValidationHostedService>();
        }

        return services;
    }
}