using Microsoft.Extensions.Logging;

namespace ACS.Service.Infrastructure;

public class TenantConfiguration
{
    public virtual string TenantId { get; set; } = string.Empty;
    public virtual string DatabaseConnectionString { get; set; } = string.Empty;
    public virtual string DisplayName { get; set; } = string.Empty;
    public virtual DateTime CreatedAt { get; set; }
    public virtual bool IsActive { get; set; } = true;
    public virtual Dictionary<string, string> Settings { get; set; } = new();
}

public interface ITenantConfigurationProvider
{
    Task<TenantConfiguration?> GetTenantConfigurationAsync(string tenantId);
    Task<IEnumerable<TenantConfiguration>> GetAllTenantsAsync();
    Task<bool> CreateTenantAsync(TenantConfiguration configuration);
    Task<bool> UpdateTenantAsync(TenantConfiguration configuration);
    Task<bool> DeleteTenantAsync(string tenantId);
}

public class TenantConfigurationProvider : ITenantConfigurationProvider
{
    private readonly ILogger<TenantConfigurationProvider> _logger;
    private readonly Dictionary<string, TenantConfiguration> _tenants = new();

    public TenantConfigurationProvider(ILogger<TenantConfigurationProvider> logger)
    {
        _logger = logger;
        
        // Initialize with some default tenants for development
        InitializeDefaultTenants();
    }

    private void InitializeDefaultTenants()
    {
        var defaultTenants = new[]
        {
            new TenantConfiguration
            {
                TenantId = "tenant-a",
                DisplayName = "Tenant A",
                DatabaseConnectionString = "Server=(localdb)\\mssqllocaldb;Database=ACS_TenantA;Trusted_Connection=true;MultipleActiveResultSets=true",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new TenantConfiguration
            {
                TenantId = "tenant-b", 
                DisplayName = "Tenant B",
                DatabaseConnectionString = "Server=(localdb)\\mssqllocaldb;Database=ACS_TenantB;Trusted_Connection=true;MultipleActiveResultSets=true",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }
        };

        foreach (var tenant in defaultTenants)
        {
            _tenants[tenant.TenantId] = tenant;
        }

        _logger.LogInformation("Initialized {Count} default tenants", defaultTenants.Length);
    }

    public Task<TenantConfiguration?> GetTenantConfigurationAsync(string tenantId)
    {
        _tenants.TryGetValue(tenantId, out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<IEnumerable<TenantConfiguration>> GetAllTenantsAsync()
    {
        return Task.FromResult(_tenants.Values.AsEnumerable());
    }

    public Task<bool> CreateTenantAsync(TenantConfiguration configuration)
    {
        if (_tenants.ContainsKey(configuration.TenantId))
        {
            _logger.LogWarning("Tenant {TenantId} already exists", configuration.TenantId);
            return Task.FromResult(false);
        }

        configuration.CreatedAt = DateTime.UtcNow;
        _tenants[configuration.TenantId] = configuration;
        
        _logger.LogInformation("Created tenant {TenantId}: {DisplayName}", 
            configuration.TenantId, configuration.DisplayName);
        
        return Task.FromResult(true);
    }

    public Task<bool> UpdateTenantAsync(TenantConfiguration configuration)
    {
        if (!_tenants.ContainsKey(configuration.TenantId))
        {
            _logger.LogWarning("Tenant {TenantId} not found for update", configuration.TenantId);
            return Task.FromResult(false);
        }

        _tenants[configuration.TenantId] = configuration;
        
        _logger.LogInformation("Updated tenant {TenantId}: {DisplayName}", 
            configuration.TenantId, configuration.DisplayName);
        
        return Task.FromResult(true);
    }

    public Task<bool> DeleteTenantAsync(string tenantId)
    {
        var removed = _tenants.Remove(tenantId);
        
        if (removed)
        {
            _logger.LogInformation("Deleted tenant {TenantId}", tenantId);
        }
        else
        {
            _logger.LogWarning("Tenant {TenantId} not found for deletion", tenantId);
        }
        
        return Task.FromResult(removed);
    }
}