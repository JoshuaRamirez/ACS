using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Default implementation of cache strategy with configurable options
/// </summary>
public class DefaultCacheStrategy : ICacheStrategy
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DefaultCacheStrategy> _logger;
    private readonly Dictionary<CacheType, CacheConfiguration> _configurations;

    public DefaultCacheStrategy(IConfiguration configuration, ILogger<DefaultCacheStrategy> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _configurations = LoadCacheConfigurations();
    }

    public TimeSpan GetExpiration(CacheType cacheType)
    {
        if (_configurations.TryGetValue(cacheType, out var config))
        {
            return config.AbsoluteExpiration;
        }
        
        return GetDefaultExpiration(cacheType);
    }

    public TimeSpan GetSlidingExpiration(CacheType cacheType)
    {
        if (_configurations.TryGetValue(cacheType, out var config))
        {
            return config.SlidingExpiration;
        }
        
        return GetDefaultSlidingExpiration(cacheType);
    }

    public bool ShouldCompress(CacheType cacheType, object item)
    {
        if (_configurations.TryGetValue(cacheType, out var config))
        {
            return config.EnableCompression;
        }
        
        // Compress large objects by default
        return EstimateSize(item) > 1024; // 1KB threshold
    }

    public CachePriority GetPriority(CacheType cacheType)
    {
        if (_configurations.TryGetValue(cacheType, out var config))
        {
            return config.Priority;
        }
        
        return cacheType switch
        {
            CacheType.Session => CachePriority.Critical,
            CacheType.User => CachePriority.High,
            CacheType.Permission => CachePriority.High,
            CacheType.PermissionEvaluation => CachePriority.High,
            CacheType.Configuration => CachePriority.High,
            CacheType.Group => CachePriority.Normal,
            CacheType.Role => CachePriority.Normal,
            CacheType.Resource => CachePriority.Normal,
            CacheType.UserGroups => CachePriority.Normal,
            CacheType.UserRoles => CachePriority.Normal,
            CacheType.AuditLog => CachePriority.Low,
            CacheType.Metadata => CachePriority.Low,
            _ => CachePriority.Normal
        };
    }

    public string[] GetInvalidationKeys(CacheType cacheType, object item, object key)
    {
        var dependencies = new List<string>();
        
        switch (cacheType)
        {
            case CacheType.User:
                // When user changes, invalidate user groups, roles, and permissions
                dependencies.Add($"user_groups:{key}");
                dependencies.Add($"user_roles:{key}");
                dependencies.Add($"permissions:user:{key}");
                break;
                
            case CacheType.Group:
                // When group changes, invalidate all users in that group
                dependencies.Add($"group_users:{key}");
                dependencies.Add($"permissions:group:{key}");
                break;
                
            case CacheType.Role:
                // When role changes, invalidate all users with that role
                dependencies.Add($"role_users:{key}");
                dependencies.Add($"permissions:role:{key}");
                break;
                
            case CacheType.Permission:
                // When permissions change, invalidate all related evaluations
                dependencies.Add($"permission_eval:*");
                break;
                
            case CacheType.UserGroups:
                // When user groups change, invalidate user permissions
                dependencies.Add($"permissions:user:{key}");
                break;
                
            case CacheType.UserRoles:
                // When user roles change, invalidate user permissions
                dependencies.Add($"permissions:user:{key}");
                break;
        }
        
        return dependencies.ToArray();
    }

    private Dictionary<CacheType, CacheConfiguration> LoadCacheConfigurations()
    {
        var configurations = new Dictionary<CacheType, CacheConfiguration>();
        
        foreach (CacheType cacheType in Enum.GetValues<CacheType>())
        {
            var section = _configuration.GetSection($"Caching:{cacheType}");
            if (section.Exists())
            {
                var config = new CacheConfiguration();
                section.Bind(config);
                configurations[cacheType] = config;
                
                _logger.LogDebug("Loaded cache configuration for {CacheType}: {Config}", 
                    cacheType, System.Text.Json.JsonSerializer.Serialize(config));
            }
        }
        
        return configurations;
    }

    private TimeSpan GetDefaultExpiration(CacheType cacheType)
    {
        return cacheType switch
        {
            CacheType.Session => TimeSpan.FromHours(8),
            CacheType.User => TimeSpan.FromMinutes(30),
            CacheType.Group => TimeSpan.FromMinutes(30),
            CacheType.Role => TimeSpan.FromMinutes(30),
            CacheType.Permission => TimeSpan.FromMinutes(15),
            CacheType.PermissionEvaluation => TimeSpan.FromMinutes(10),
            CacheType.UserGroups => TimeSpan.FromMinutes(20),
            CacheType.UserRoles => TimeSpan.FromMinutes(20),
            CacheType.Resource => TimeSpan.FromHours(1),
            CacheType.Configuration => TimeSpan.FromHours(2),
            CacheType.Metadata => TimeSpan.FromHours(4),
            CacheType.AuditLog => TimeSpan.FromHours(24),
            _ => TimeSpan.FromMinutes(15)
        };
    }

    private TimeSpan GetDefaultSlidingExpiration(CacheType cacheType)
    {
        return cacheType switch
        {
            CacheType.Session => TimeSpan.FromMinutes(30),
            CacheType.User => TimeSpan.FromMinutes(10),
            CacheType.Group => TimeSpan.FromMinutes(10),
            CacheType.Role => TimeSpan.FromMinutes(10),
            CacheType.Permission => TimeSpan.FromMinutes(5),
            CacheType.PermissionEvaluation => TimeSpan.FromMinutes(3),
            CacheType.UserGroups => TimeSpan.FromMinutes(8),
            CacheType.UserRoles => TimeSpan.FromMinutes(8),
            CacheType.Resource => TimeSpan.FromMinutes(15),
            CacheType.Configuration => TimeSpan.FromMinutes(30),
            CacheType.Metadata => TimeSpan.FromMinutes(45),
            CacheType.AuditLog => TimeSpan.FromHours(2),
            _ => TimeSpan.FromMinutes(5)
        };
    }

    private int EstimateSize(object item)
    {
        // Simple size estimation - in production, use more sophisticated approach
        if (item is string str)
            return str.Length * 2; // Unicode chars
        
        if (item is System.Collections.ICollection collection)
            return collection.Count * 100; // Rough estimate
            
        return 256; // Default estimate
    }
}

/// <summary>
/// Configuration for a specific cache type
/// </summary>
public class CacheConfiguration
{
    public TimeSpan AbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromMinutes(5);
    public CachePriority Priority { get; set; } = CachePriority.Normal;
    public bool EnableCompression { get; set; } = false;
    public int MaxItems { get; set; } = 1000;
    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
}