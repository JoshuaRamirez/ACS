namespace ACS.Infrastructure.Caching;

/// <summary>
/// Interface for cache strategy management
/// </summary>
public interface ICacheStrategy
{
    /// <summary>
    /// Gets cache expiration time for a specific cache type
    /// </summary>
    TimeSpan GetExpiration(CacheType cacheType);
    
    /// <summary>
    /// Gets sliding expiration time for a specific cache type
    /// </summary>
    TimeSpan GetSlidingExpiration(CacheType cacheType);
    
    /// <summary>
    /// Determines if the cache item should be compressed
    /// </summary>
    bool ShouldCompress(CacheType cacheType, object item);
    
    /// <summary>
    /// Gets cache priority for a specific cache type
    /// </summary>
    CachePriority GetPriority(CacheType cacheType);
    
    /// <summary>
    /// Gets cache invalidation dependencies for an item
    /// </summary>
    string[] GetInvalidationKeys(CacheType cacheType, object item, object key);
}

/// <summary>
/// Types of cached data
/// </summary>
public enum CacheType
{
    User,
    Group,
    Role,
    Permission,
    UserGroups,
    UserRoles,
    PermissionEvaluation,
    Resource,
    AuditLog,
    Session,
    Configuration,
    Metadata
}

/// <summary>
/// Cache priority levels
/// </summary>
public enum CachePriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Cache invalidation events
/// </summary>
public class CacheInvalidationEvent
{
    public string Key { get; set; } = string.Empty;
    public CacheType Type { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
    public string[] DependentKeys { get; set; } = Array.Empty<string>();
}