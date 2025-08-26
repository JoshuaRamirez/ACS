using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace ACS.Infrastructure.Configuration;

/// <summary>
/// Custom configuration provider for environment variables with enhanced mapping
/// </summary>
public class EnvironmentVariableProvider : ConfigurationProvider, IConfigurationSource
{
    private readonly string _prefix;
    private readonly Dictionary<string, string> _mappings;
    private readonly bool _includeSystemVariables;

    public EnvironmentVariableProvider(string prefix = "ACS_", bool includeSystemVariables = false)
    {
        _prefix = prefix;
        _includeSystemVariables = includeSystemVariables;
        _mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        InitializeMappings();
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return this;
    }

    public override void Load()
    {
        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Load environment variables
        var envVars = Environment.GetEnvironmentVariables();
        
        foreach (var key in envVars.Keys)
        {
            if (key == null) continue;
            
            var keyStr = key.ToString();
            var value = envVars[key]?.ToString();

            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(keyStr))
                continue;

            // Check if it's an ACS-specific variable
            if (keyStr.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
            {
                var configKey = TransformKey(keyStr.Substring(_prefix.Length));
                Data[configKey] = value;
            }
            // Check if it's a mapped variable
            else if (_mappings.ContainsKey(keyStr))
            {
                Data[_mappings[keyStr]] = value;
            }
            // Include system variables if requested
            else if (_includeSystemVariables)
            {
                Data[$"System:{keyStr}"] = value;
            }
        }

        // Apply variable substitution
        ApplyVariableSubstitution();
    }

    private void InitializeMappings()
    {
        // Map common environment variables to configuration keys
        
        // Database
        _mappings["DATABASE_URL"] = "ConnectionStrings:DefaultConnection";
        _mappings["DB_CONNECTION"] = "ConnectionStrings:DefaultConnection";
        _mappings["REDIS_URL"] = "ConnectionStrings:Redis";
        _mappings["REDIS_CONNECTION"] = "ConnectionStrings:Redis";
        
        // Authentication
        _mappings["JWT_SECRET"] = "Authentication:Jwt:SecretKey";
        _mappings["JWT_ISSUER"] = "Authentication:Jwt:Issuer";
        _mappings["JWT_AUDIENCE"] = "Authentication:Jwt:Audience";
        _mappings["JWT_EXPIRATION_HOURS"] = "Authentication:Jwt:ExpirationHours";
        
        // Azure Key Vault
        _mappings["AZURE_KEY_VAULT_URI"] = "KeyVault:VaultUri";
        _mappings["AZURE_CLIENT_ID"] = "KeyVault:ClientId";
        _mappings["AZURE_CLIENT_SECRET"] = "KeyVault:ClientSecret";
        _mappings["AZURE_TENANT_ID"] = "KeyVault:TenantId";
        
        // Application Insights
        _mappings["APPINSIGHTS_INSTRUMENTATIONKEY"] = "ApplicationInsights:InstrumentationKey";
        _mappings["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "ApplicationInsights:ConnectionString";
        
        // OpenTelemetry
        _mappings["OTEL_EXPORTER_OTLP_ENDPOINT"] = "OpenTelemetry:OtlpEndpoint";
        _mappings["OTEL_SERVICE_NAME"] = "OpenTelemetry:ServiceName";
        
        // SMTP
        _mappings["SMTP_HOST"] = "Alerting:Channels:Email:SmtpHost";
        _mappings["SMTP_PORT"] = "Alerting:Channels:Email:SmtpPort";
        _mappings["SMTP_USERNAME"] = "Alerting:Channels:Email:Username";
        _mappings["SMTP_PASSWORD"] = "Alerting:Channels:Email:Password";
        _mappings["SMTP_FROM"] = "Alerting:Channels:Email:FromAddress";
        
        // Slack
        _mappings["SLACK_WEBHOOK_URL"] = "Alerting:Channels:Slack:DefaultWebhookUrl";
        
        // Feature Flags
        _mappings["FEATURE_RATE_LIMITING"] = "RateLimit:Enabled";
        _mappings["FEATURE_HEALTH_CHECKS"] = "HealthChecks:EnablePeriodicChecks";
        _mappings["FEATURE_METRICS"] = "Monitoring:EnableMetrics";
        _mappings["FEATURE_ARCHIVING"] = "DataArchiving:Schedule:Enabled";
        
        // Paths
        _mappings["BACKUP_PATH"] = "Database:Backup:DefaultPath";
        _mappings["ARCHIVE_PATH"] = "DataArchiving:ArchivePath";
        _mappings["LOG_PATH"] = "Logging:FilePath";
        
        // Kubernetes/Docker
        _mappings["ASPNETCORE_ENVIRONMENT"] = "Environment";
        _mappings["ASPNETCORE_URLS"] = "Urls";
        _mappings["POD_NAME"] = "Kubernetes:PodName";
        _mappings["POD_NAMESPACE"] = "Kubernetes:Namespace";
        _mappings["NODE_NAME"] = "Kubernetes:NodeName";
    }

    private string TransformKey(string key)
    {
        // Transform ACS_SECTION__SUBSECTION__KEY to Section:Subsection:Key
        // Double underscore becomes colon, single underscore becomes nothing
        
        var transformed = key.Replace("__", ":");
        
        // Convert to PascalCase for each section
        var parts = transformed.Split(':');
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = ToPascalCase(parts[i]);
        }
        
        return string.Join(":", parts);
    }

    private string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Handle snake_case to PascalCase
        var parts = input.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (!string.IsNullOrEmpty(parts[i]))
            {
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1).ToLower();
            }
        }
        
        return string.Join("", parts);
    }

    private void ApplyVariableSubstitution()
    {
        // Support variable substitution like ${VAR_NAME} or ${VAR_NAME:default}
        var regex = new Regex(@"\$\{([^}:]+)(?::([^}]*))?\}", RegexOptions.Compiled);
        
        var keysToUpdate = new Dictionary<string, string>();
        
        foreach (var kvp in Data)
        {
            var value = kvp.Value;
            if (string.IsNullOrEmpty(value))
                continue;
            
            var matches = regex.Matches(value);
            
            foreach (Match match in matches)
            {
                var varName = match.Groups[1].Value;
                var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : "";
                
                // Look for the variable in environment
                var envValue = Environment.GetEnvironmentVariable(varName);
                
                // If not found, check our data
                if (string.IsNullOrEmpty(envValue) && Data.ContainsKey(varName))
                {
                    envValue = Data[varName];
                }
                
                // Use default if still not found
                if (string.IsNullOrEmpty(envValue))
                {
                    envValue = defaultValue;
                }
                
                value = value.Replace(match.Value, envValue);
            }
            
            if (value != kvp.Value)
            {
                keysToUpdate[kvp.Key] = value;
            }
        }
        
        // Apply updates
        foreach (var update in keysToUpdate)
        {
            Data[update.Key] = update.Value;
        }
    }
}

/// <summary>
/// Extension methods for adding environment variable configuration
/// </summary>
public static class EnvironmentVariableExtensions
{
    public static IConfigurationBuilder AddEnvironmentVariables(
        this IConfigurationBuilder builder,
        string prefix = "ACS_",
        bool includeSystemVariables = false)
    {
        return builder.Add(new EnvironmentVariableProvider(prefix, includeSystemVariables));
    }

    public static IConfigurationBuilder AddEnvironmentVariablesFromFile(
        this IConfigurationBuilder builder,
        string envFilePath,
        bool optional = true)
    {
        if (!File.Exists(envFilePath))
        {
            if (!optional)
                throw new FileNotFoundException($"Environment file not found: {envFilePath}");
            return builder;
        }

        // Load .env file
        var lines = File.ReadAllLines(envFilePath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var index = line.IndexOf('=');
            if (index > 0)
            {
                var key = line.Substring(0, index).Trim();
                var value = line.Substring(index + 1).Trim();
                
                // Remove quotes if present
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        return builder.AddEnvironmentVariables();
    }
}