using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.Security.KeyVault;

/// <summary>
/// Configuration provider that loads secrets from Key Vault
/// </summary>
public class KeyVaultConfigurationProvider : ConfigurationProvider
{
    private readonly IKeyVaultService _keyVaultService;
    private readonly KeyVaultConfigurationOptions _options;
    private readonly ILogger<KeyVaultConfigurationProvider> _logger;

    public KeyVaultConfigurationProvider(
        IKeyVaultService keyVaultService,
        KeyVaultConfigurationOptions options,
        ILogger<KeyVaultConfigurationProvider> logger)
    {
        _keyVaultService = keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();
    }

    private async Task LoadAsync()
    {
        try
        {
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            // Load specified secrets
            foreach (var mapping in _options.SecretMappings)
            {
                try
                {
                    var secretValue = await _keyVaultService.GetSecretAsync(mapping.SecretName);
                    if (secretValue != null)
                    {
                        data[mapping.ConfigurationKey] = secretValue;
                        _logger.LogDebug("Loaded secret {SecretName} into configuration key {ConfigKey}",
                            mapping.SecretName, mapping.ConfigurationKey);
                    }
                    else
                    {
                        _logger.LogWarning("Secret {SecretName} not found in Key Vault", mapping.SecretName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading secret {SecretName}", mapping.SecretName);
                    if (!_options.Optional)
                    {
                        throw;
                    }
                }
            }

            // Load connection strings
            foreach (var connectionStringName in _options.ConnectionStringNames)
            {
                try
                {
                    var connectionString = await _keyVaultService.GetConnectionStringAsync(connectionStringName);
                    if (connectionString != null)
                    {
                        data[$"ConnectionStrings:{connectionStringName}"] = connectionString;
                        _logger.LogDebug("Loaded connection string {Name} from Key Vault", connectionStringName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading connection string {Name}", connectionStringName);
                    if (!_options.Optional)
                    {
                        throw;
                    }
                }
            }

            // Load API keys
            foreach (var apiKeyName in _options.ApiKeyNames)
            {
                try
                {
                    var apiKey = await _keyVaultService.GetApiKeyAsync(apiKeyName);
                    if (apiKey != null)
                    {
                        data[$"ApiKeys:{apiKeyName}"] = apiKey;
                        _logger.LogDebug("Loaded API key {Name} from Key Vault", apiKeyName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading API key {Name}", apiKeyName);
                    if (!_options.Optional)
                    {
                        throw;
                    }
                }
            }

            // Load all secrets if specified
            if (_options.LoadAllSecrets)
            {
                try
                {
                    var secretNames = await _keyVaultService.ListSecretNamesAsync();
                    foreach (var secretName in secretNames)
                    {
                        if (!ShouldLoadSecret(secretName))
                            continue;

                        var secretValue = await _keyVaultService.GetSecretAsync(secretName);
                        if (secretValue != null)
                        {
                            var configKey = TransformSecretNameToConfigKey(secretName);
                            data[configKey] = secretValue;
                            _logger.LogDebug("Loaded secret {SecretName} as {ConfigKey}", secretName, configKey);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading all secrets");
                    if (!_options.Optional)
                    {
                        throw;
                    }
                }
            }

            Data = data!;
            _logger.LogInformation("Successfully loaded {Count} configuration values from Key Vault", data.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration from Key Vault");
            if (!_options.Optional)
            {
                throw;
            }
        }
    }

    private bool ShouldLoadSecret(string secretName)
    {
        // Check if secret is in exclusion list
        if (_options.ExcludeSecrets.Contains(secretName, StringComparer.OrdinalIgnoreCase))
            return false;

        // Check prefix filter
        if (_options.SecretNamePrefixes.Any())
        {
            return _options.SecretNamePrefixes.Any(prefix =>
                secretName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    private string TransformSecretNameToConfigKey(string secretName)
    {
        // Transform secret name to configuration key
        // Example: "ConnectionString-Database" -> "ConnectionStrings:Database"
        // Example: "ApiKey-External" -> "ApiKeys:External"
        // Example: "App-Setting-Value" -> "App:Setting:Value"

        var configKey = secretName;

        // Handle connection strings
        if (configKey.StartsWith("ConnectionString-", StringComparison.OrdinalIgnoreCase))
        {
            configKey = $"ConnectionStrings:{configKey.Substring(17)}";
        }
        // Handle API keys
        else if (configKey.StartsWith("ApiKey-", StringComparison.OrdinalIgnoreCase))
        {
            configKey = $"ApiKeys:{configKey.Substring(7)}";
        }
        // Replace dashes with colons for nested configuration
        else
        {
            configKey = configKey.Replace("-", ":");
        }

        return configKey;
    }
}

/// <summary>
/// Configuration source for Key Vault
/// </summary>
public class KeyVaultConfigurationSource : IConfigurationSource
{
    public IKeyVaultService? KeyVaultService { get; set; }
    public KeyVaultConfigurationOptions Options { get; set; } = new();
    public ILoggerFactory? LoggerFactory { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        if (KeyVaultService == null)
            throw new InvalidOperationException("KeyVaultService must be provided");

        var logger = LoggerFactory?.CreateLogger<KeyVaultConfigurationProvider>()
            ?? new LoggerFactory().CreateLogger<KeyVaultConfigurationProvider>();

        return new KeyVaultConfigurationProvider(KeyVaultService, Options, logger);
    }
}

/// <summary>
/// Options for Key Vault configuration provider
/// </summary>
public class KeyVaultConfigurationOptions
{
    /// <summary>
    /// Whether the configuration provider is optional
    /// </summary>
    public bool Optional { get; set; } = true;

    /// <summary>
    /// Reload configuration on changes
    /// </summary>
    public bool ReloadOnChange { get; set; } = false;

    /// <summary>
    /// Reload interval in seconds
    /// </summary>
    public int ReloadIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Load all secrets from vault
    /// </summary>
    public bool LoadAllSecrets { get; set; } = false;

    /// <summary>
    /// Secret name prefixes to include
    /// </summary>
    public List<string> SecretNamePrefixes { get; set; } = new();

    /// <summary>
    /// Secrets to exclude
    /// </summary>
    public HashSet<string> ExcludeSecrets { get; set; } = new();

    /// <summary>
    /// Explicit secret to configuration key mappings
    /// </summary>
    public List<SecretMapping> SecretMappings { get; set; } = new();

    /// <summary>
    /// Connection string names to load
    /// </summary>
    public List<string> ConnectionStringNames { get; set; } = new();

    /// <summary>
    /// API key names to load
    /// </summary>
    public List<string> ApiKeyNames { get; set; } = new();
}

/// <summary>
/// Maps a Key Vault secret to a configuration key
/// </summary>
public class SecretMapping
{
    public string SecretName { get; set; } = string.Empty;
    public string ConfigurationKey { get; set; } = string.Empty;
}

/// <summary>
/// Extension methods for adding Key Vault configuration
/// </summary>
public static class KeyVaultConfigurationExtensions
{
    /// <summary>
    /// Add Key Vault as a configuration source
    /// </summary>
    public static IConfigurationBuilder AddKeyVault(
        this IConfigurationBuilder builder,
        IKeyVaultService keyVaultService,
        Action<KeyVaultConfigurationOptions>? configureOptions = null)
    {
        var options = new KeyVaultConfigurationOptions();
        configureOptions?.Invoke(options);

        var source = new KeyVaultConfigurationSource
        {
            KeyVaultService = keyVaultService,
            Options = options
        };

        builder.Add(source);
        return builder;
    }

    /// <summary>
    /// Add Key Vault as a configuration source with automatic setup
    /// </summary>
    public static IConfigurationBuilder AddKeyVault(
        this IConfigurationBuilder builder,
        string vaultUri,
        Action<KeyVaultConfigurationOptions>? configureOptions = null)
    {
        var options = new KeyVaultConfigurationOptions();
        configureOptions?.Invoke(options);

        // Build temporary configuration to get Key Vault settings
        var tempConfig = builder.Build();
        var kvOptions = new KeyVaultOptions
        {
            VaultUri = vaultUri,
            UseManagedIdentity = tempConfig.GetValue<bool>("KeyVault:UseManagedIdentity"),
            ClientId = tempConfig["KeyVault:ClientId"],
            ClientSecret = tempConfig["KeyVault:ClientSecret"],
            TenantId = tempConfig["KeyVault:TenantId"]
        };

        var loggerFactory = new LoggerFactory();
        var keyVaultService = new KeyVaultService(
            Microsoft.Extensions.Options.Options.Create(kvOptions),
            loggerFactory.CreateLogger<KeyVaultService>(),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())));

        return builder.AddKeyVault(keyVaultService, configureOptions);
    }
}