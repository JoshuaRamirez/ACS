using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.Security;

/// <summary>
/// Extension methods for configuring encryption services
/// </summary>
public static class EncryptionExtensions
{
    /// <summary>
    /// Add encryption services to the service collection
    /// </summary>
    public static IServiceCollection AddEncryptionServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register key management service
        services.AddSingleton<IKeyManagementService, FileBasedKeyManagementService>();
        
        // Register encryption service
        services.AddSingleton<IEncryptionService, AesEncryptionService>();
        
        // Register encryption configuration service
        services.AddSingleton<EncryptionConfigurationService>();
        
        // Register background key rotation service
        services.AddHostedService<KeyRotationService>();
        
        return services;
    }
}

/// <summary>
/// Background service for automated key rotation
/// </summary>
public class KeyRotationService : BackgroundService
{
    private readonly ILogger<KeyRotationService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _rotationInterval;

    public KeyRotationService(
        ILogger<KeyRotationService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _rotationInterval = TimeSpan.FromDays(configuration.GetValue<int>("Encryption:KeyRotationIntervalDays", 90));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Key rotation service started with interval: {Interval} days", _rotationInterval.TotalDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformKeyRotationCheckAsync();
                await Task.Delay(_rotationInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in key rotation service");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken); // Retry in 1 hour
            }
        }

        _logger.LogInformation("Key rotation service stopped");
    }

    private async Task PerformKeyRotationCheckAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var configService = scope.ServiceProvider.GetRequiredService<EncryptionConfigurationService>();
        
        // Get all tenants that need key rotation
        var tenants = await configService.GetTenantsRequiringKeyRotationAsync();
        
        foreach (var tenantId in tenants)
        {
            try
            {
                _logger.LogInformation("Rotating keys for tenant {TenantId}", tenantId);
                await encryptionService.RotateKeysAsync(tenantId);
                await configService.UpdateLastKeyRotationAsync(tenantId, DateTime.UtcNow);
                _logger.LogInformation("Successfully rotated keys for tenant {TenantId}", tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rotate keys for tenant {TenantId}", tenantId);
            }
        }
    }
}

/// <summary>
/// Service for managing encryption configuration per tenant
/// </summary>
public class EncryptionConfigurationService
{
    private readonly ILogger<EncryptionConfigurationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, TenantEncryptionConfiguration> _tenantConfigs = new();
    private readonly SemaphoreSlim _configLock = new(1, 1);

    public EncryptionConfigurationService(
        ILogger<EncryptionConfigurationService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<TenantEncryptionConfiguration> GetConfigurationAsync(string tenantId)
    {
        await _configLock.WaitAsync();
        try
        {
            if (_tenantConfigs.TryGetValue(tenantId, out var config))
            {
                return config;
            }

            // Load configuration for tenant (from database or configuration files)
            config = await LoadTenantConfigurationAsync(tenantId);
            _tenantConfigs[tenantId] = config;
            
            return config;
        }
        finally
        {
            _configLock.Release();
        }
    }

    public async Task<IEnumerable<string>> GetTenantsRequiringKeyRotationAsync()
    {
        var tenantsForRotation = new List<string>();
        
        // In a real implementation, this would query a database
        // For now, we'll check configured tenants
        var configuredTenants = _configuration.GetSection("Tenants").GetChildren().Select(c => c.Key);
        
        foreach (var tenantId in configuredTenants)
        {
            var config = await GetConfigurationAsync(tenantId);
            var daysSinceRotation = (DateTime.UtcNow - config.LastKeyRotation).TotalDays;
            
            if (daysSinceRotation >= config.KeyRotationIntervalDays)
            {
                tenantsForRotation.Add(tenantId);
            }
        }
        
        return tenantsForRotation;
    }

    public async Task UpdateLastKeyRotationAsync(string tenantId, DateTime rotationTime)
    {
        await _configLock.WaitAsync();
        try
        {
            if (_tenantConfigs.TryGetValue(tenantId, out var config))
            {
                config.LastKeyRotation = rotationTime;
                // In a real implementation, persist this to database
                _logger.LogDebug("Updated last key rotation time for tenant {TenantId} to {RotationTime}", tenantId, rotationTime);
            }
        }
        finally
        {
            _configLock.Release();
        }
    }

    private Task<TenantEncryptionConfiguration> LoadTenantConfigurationAsync(string tenantId)
    {
        // In a real implementation, this would load from database
        // For now, return default configuration with tenant-specific overrides
        var config = new TenantEncryptionConfiguration
        {
            TenantId = tenantId,
            Algorithm = _configuration.GetValue<string>($"Tenants:{tenantId}:Encryption:Algorithm", "AES-256-GCM"),
            KeyRotationIntervalDays = _configuration.GetValue<int>($"Tenants:{tenantId}:Encryption:KeyRotationIntervalDays", 90),
            EnableFieldLevelEncryption = _configuration.GetValue<bool>($"Tenants:{tenantId}:Encryption:EnableFieldLevelEncryption", true),
            EnableDatabaseLevelEncryption = _configuration.GetValue<bool>($"Tenants:{tenantId}:Encryption:EnableDatabaseLevelEncryption", true),
            EncryptedFields = _configuration.GetSection($"Tenants:{tenantId}:Encryption:EncryptedFields").Get<string[]>() ?? Array.Empty<string>(),
            LastKeyRotation = DateTime.UtcNow.AddDays(-30) // Default to 30 days ago
        };

        _logger.LogDebug("Loaded encryption configuration for tenant {TenantId}", tenantId);
        return Task.FromResult(config);
    }
}