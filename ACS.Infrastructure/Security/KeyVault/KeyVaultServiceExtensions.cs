using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.Security.KeyVault;

/// <summary>
/// Extension methods for configuring Key Vault services
/// </summary>
public static class KeyVaultServiceExtensions
{
    /// <summary>
    /// Add Key Vault services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddKeyVault(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure Key Vault options
        services.Configure<KeyVaultOptions>(configuration.GetSection("KeyVault"));

        // Register Key Vault service
        services.AddSingleton<IKeyVaultService, KeyVaultService>();

        // Add hosted service for secret rotation if configured
        if (configuration.GetValue<bool>("KeyVault:EnableAutoRotation"))
        {
            services.AddHostedService<SecretRotationHostedService>();
        }

        return services;
    }

    /// <summary>
    /// Add Key Vault services with custom options
    /// </summary>
    public static IServiceCollection AddKeyVault(
        this IServiceCollection services,
        Action<KeyVaultOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IKeyVaultService, KeyVaultService>();
        return services;
    }

    /// <summary>
    /// Add Key Vault configuration to the host builder
    /// </summary>
    public static IHostBuilder ConfigureKeyVault(
        this IHostBuilder hostBuilder,
        Action<HostBuilderContext, KeyVaultConfigurationOptions>? configureOptions = null)
    {
        return hostBuilder.ConfigureAppConfiguration((context, config) =>
        {
            var builtConfig = config.Build();
            var vaultUri = builtConfig["KeyVault:VaultUri"];

            if (string.IsNullOrEmpty(vaultUri))
            {
                // Skip Key Vault configuration if no vault URI is provided
                return;
            }

            var options = new KeyVaultConfigurationOptions();
            configureOptions?.Invoke(context, options);

            // Set up default options based on environment
            if (context.HostingEnvironment.IsDevelopment())
            {
                options.Optional = true;
                options.LoadAllSecrets = false;
            }
            else
            {
                options.Optional = false;
                options.LoadAllSecrets = true;
            }

            config.AddKeyVault(vaultUri, opt =>
            {
                opt.Optional = options.Optional;
                opt.LoadAllSecrets = options.LoadAllSecrets;
                opt.SecretMappings = options.SecretMappings;
                opt.ConnectionStringNames = options.ConnectionStringNames;
                opt.ApiKeyNames = options.ApiKeyNames;
                opt.SecretNamePrefixes = options.SecretNamePrefixes;
                opt.ExcludeSecrets = options.ExcludeSecrets;
            });
        });
    }

    /// <summary>
    /// Use Key Vault for storing application secrets
    /// </summary>
    public static IServiceCollection AddSecretManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddKeyVault(configuration);

        // Register secret manager service
        services.AddScoped<ISecretManager, SecretManager>();

        // Register certificate manager
        services.AddScoped<ICertificateManager, CertificateManager>();

        return services;
    }
}

/// <summary>
/// Secret manager service for application-specific secret operations
/// </summary>
public interface ISecretManager
{
    Task<string?> GetDatabasePasswordAsync(string tenantId);
    Task<string?> GetApiKeyAsync(string serviceName);
    Task<string?> GetJwtSecretAsync();
    Task<string?> GetEncryptionKeyAsync();
    Task RotateAllSecretsAsync();
}

/// <summary>
/// Implementation of secret manager
/// </summary>
public class SecretManager : ISecretManager
{
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<SecretManager> _logger;

    public SecretManager(IKeyVaultService keyVaultService, ILogger<SecretManager> logger)
    {
        _keyVaultService = keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> GetDatabasePasswordAsync(string tenantId)
    {
        var secretName = $"DbPassword-{tenantId}";
        return await _keyVaultService.GetSecretAsync(secretName);
    }

    public async Task<string?> GetApiKeyAsync(string serviceName)
    {
        return await _keyVaultService.GetApiKeyAsync(serviceName);
    }

    public async Task<string?> GetJwtSecretAsync()
    {
        return await _keyVaultService.GetSecretAsync("JwtSecret");
    }

    public async Task<string?> GetEncryptionKeyAsync()
    {
        return await _keyVaultService.GetSecretAsync("EncryptionKey");
    }

    public async Task RotateAllSecretsAsync()
    {
        _logger.LogInformation("Starting secret rotation");

        var secretsToRotate = new[] { "JwtSecret", "EncryptionKey", "MasterKey" };
        foreach (var secretName in secretsToRotate)
        {
            try
            {
                await _keyVaultService.RotateSecretAsync(secretName);
                _logger.LogInformation("Rotated secret {SecretName}", secretName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rotate secret {SecretName}", secretName);
            }
        }

        _logger.LogInformation("Secret rotation completed");
    }
}

/// <summary>
/// Certificate manager for handling certificates from Key Vault
/// </summary>
public interface ICertificateManager
{
    Task<System.Security.Cryptography.X509Certificates.X509Certificate2?> GetSslCertificateAsync();
    Task<System.Security.Cryptography.X509Certificates.X509Certificate2?> GetSigningCertificateAsync();
    Task<bool> RenewCertificateAsync(string certificateName);
}

/// <summary>
/// Implementation of certificate manager
/// </summary>
public class CertificateManager : ICertificateManager
{
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<CertificateManager> _logger;

    public CertificateManager(IKeyVaultService keyVaultService, ILogger<CertificateManager> logger)
    {
        _keyVaultService = keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<System.Security.Cryptography.X509Certificates.X509Certificate2?> GetSslCertificateAsync()
    {
        return await _keyVaultService.GetCertificateAsync("SslCertificate");
    }

    public async Task<System.Security.Cryptography.X509Certificates.X509Certificate2?> GetSigningCertificateAsync()
    {
        return await _keyVaultService.GetCertificateAsync("SigningCertificate");
    }

    public async Task<bool> RenewCertificateAsync(string certificateName)
    {
        _logger.LogInformation("Renewing certificate {CertificateName}", certificateName);
        // Implementation would depend on certificate authority integration
        return await Task.FromResult(true);
    }
}

/// <summary>
/// Hosted service for automatic secret rotation
/// </summary>
public class SecretRotationHostedService : BackgroundService
{
    private readonly ISecretManager _secretManager;
    private readonly ILogger<SecretRotationHostedService> _logger;
    private readonly int _rotationIntervalHours;

    public SecretRotationHostedService(
        ISecretManager secretManager,
        ILogger<SecretRotationHostedService> logger,
        IConfiguration configuration)
    {
        _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rotationIntervalHours = configuration.GetValue<int>("KeyVault:RotationIntervalHours", 720); // 30 days default
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(_rotationIntervalHours), stoppingToken);
                
                _logger.LogInformation("Starting scheduled secret rotation");
                await _secretManager.RotateAllSecretsAsync();
                _logger.LogInformation("Scheduled secret rotation completed");
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled secret rotation");
            }
        }
    }
}