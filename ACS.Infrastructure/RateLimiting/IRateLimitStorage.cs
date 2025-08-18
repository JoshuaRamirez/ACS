namespace ACS.Infrastructure.RateLimiting;

/// <summary>
/// Storage interface for rate limiting data
/// Supports both in-memory and distributed storage implementations
/// </summary>
public interface IRateLimitStorage
{
    /// <summary>
    /// Get rate limit data for a specific key
    /// </summary>
    Task<RateLimitStorageData?> GetAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set rate limit data for a specific key
    /// </summary>
    Task SetAsync(string key, RateLimitStorageData data, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove rate limit data for a specific key
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all keys with a specific prefix (for tenant queries)
    /// </summary>
    Task<IEnumerable<RateLimitStorageData>> GetByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove expired entries (cleanup operation)
    /// </summary>
    Task CleanupExpiredAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get storage statistics
    /// </summary>
    Task<RateLimitStorageStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Data structure for storing rate limit information
/// </summary>
public class RateLimitStorageData
{
    /// <summary>
    /// Composite key (tenant:identifier)
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// List of request timestamps within the current window
    /// </summary>
    public List<DateTime> Timestamps { get; set; } = new();
    
    /// <summary>
    /// Last time this entry was updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this entry expires and can be cleaned up
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(1);
    
    /// <summary>
    /// Additional metadata for the rate limit entry
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Storage statistics for monitoring
/// </summary>
public class RateLimitStorageStats
{
    public long TotalEntries { get; set; }
    public long ExpiredEntries { get; set; }
    public long TotalRequests { get; set; }
    public DateTime LastCleanup { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public Dictionary<string, long> TenantCounts { get; set; } = new();
}