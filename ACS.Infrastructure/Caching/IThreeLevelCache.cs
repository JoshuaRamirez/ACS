namespace ACS.Infrastructure.Caching;

/// <summary>
/// Interface for three-level caching (L1: Memory, L2: Redis, L3: SQL Server)
/// </summary>
public interface IThreeLevelCache : IMultiLevelCache
{
    /// <summary>
    /// Gets cache statistics for all three levels
    /// </summary>
    Task<ThreeLevelCacheStatistics> GetThreeLevelStatisticsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Forces a value to a specific cache level for testing or debugging
    /// </summary>
    Task SetAtLevelAsync<T>(string key, T value, CacheType cacheType, CacheLevel level, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a value from a specific cache level
    /// </summary>
    Task<T?> GetFromLevelAsync<T>(string key, CacheLevel level, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Promotes a cache entry from lower levels to higher levels
    /// </summary>
    Task PromoteAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Demotes a cache entry to lower levels (for memory pressure scenarios)
    /// </summary>
    Task DemoteAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets health information for all cache levels
    /// </summary>
    Task<Dictionary<string, Dictionary<string, string>>> GetHealthInfoAsync();
}

/// <summary>
/// Cache levels in the three-level hierarchy
/// </summary>
public enum CacheLevel
{
    L1_Memory = 1,
    L2_Redis = 2,
    L3_SqlServer = 3
}

/// <summary>
/// Statistics for three-level cache
/// </summary>
public class ThreeLevelCacheStatistics : MultiLevelCacheStatistics
{
    public CacheStatistics Level3Statistics { get; set; } = new();
    public long TotalL3Hits { get; set; }
    
    public double L2HitRate => (TotalL1Hits + TotalL2Hits + TotalL3Hits + TotalMisses) > 0 
        ? TotalL2Hits / (double)(TotalL1Hits + TotalL2Hits + TotalL3Hits + TotalMisses) 
        : 0;
        
    public double L3HitRate => (TotalL1Hits + TotalL2Hits + TotalL3Hits + TotalMisses) > 0 
        ? TotalL3Hits / (double)(TotalL1Hits + TotalL2Hits + TotalL3Hits + TotalMisses) 
        : 0;
        
    public new double OverallHitRate => (TotalL1Hits + TotalL2Hits + TotalL3Hits + TotalMisses) > 0 
        ? (TotalL1Hits + TotalL2Hits + TotalL3Hits) / (double)(TotalL1Hits + TotalL2Hits + TotalL3Hits + TotalMisses) 
        : 0;
        
    public new double L1HitRate => (TotalL1Hits + TotalL2Hits + TotalL3Hits + TotalMisses) > 0 
        ? TotalL1Hits / (double)(TotalL1Hits + TotalL2Hits + TotalL3Hits + TotalMisses) 
        : 0;
}

/// <summary>
/// Cache failover strategy
/// </summary>
public enum CacheFailoverStrategy
{
    /// <summary>
    /// Fail immediately if preferred cache level is unavailable
    /// </summary>
    FailFast,
    
    /// <summary>
    /// Fall back to next available cache level
    /// </summary>
    Fallback,
    
    /// <summary>
    /// Skip failed levels and continue to next available level
    /// </summary>
    SkipFailed,
    
    /// <summary>
    /// Try all levels and return first successful result
    /// </summary>
    BestEffort
}