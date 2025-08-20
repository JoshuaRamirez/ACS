namespace ACS.Infrastructure.Caching;

/// <summary>
/// Interface for multi-level caching (L1: Memory, L2: Distributed)
/// </summary>
public interface IMultiLevelCache
{
    /// <summary>
    /// Gets a value from cache, checking L1 first, then L2
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets a value in both L1 and L2 cache
    /// </summary>
    Task SetAsync<T>(string key, T value, CacheType cacheType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a value from both L1 and L2 cache
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes multiple keys matching a pattern from both levels
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Invalidates cache entries based on dependencies
    /// </summary>
    Task InvalidateAsync(CacheInvalidationEvent invalidationEvent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets cache statistics for both levels
    /// </summary>
    Task<MultiLevelCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Preloads frequently accessed data into cache
    /// </summary>
    Task WarmupAsync(string[] keys, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refreshes cache entry in background
    /// </summary>
    Task RefreshAsync<T>(string key, Func<Task<T>> refreshFunction, CacheType cacheType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for multi-level cache
/// </summary>
public class MultiLevelCacheStatistics
{
    public CacheStatistics Level1Statistics { get; set; } = new();
    public CacheStatistics Level2Statistics { get; set; } = new();
    public long TotalL1Hits { get; set; }
    public long TotalL2Hits { get; set; }
    public long TotalMisses { get; set; }
    public double OverallHitRate => (TotalL1Hits + TotalL2Hits + TotalMisses) > 0 
        ? (TotalL1Hits + TotalL2Hits) / (double)(TotalL1Hits + TotalL2Hits + TotalMisses) 
        : 0;
    public double L1HitRate => (TotalL1Hits + TotalL2Hits + TotalMisses) > 0 
        ? TotalL1Hits / (double)(TotalL1Hits + TotalL2Hits + TotalMisses) 
        : 0;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public TimeSpan Uptime => DateTime.UtcNow - StartTime;
}