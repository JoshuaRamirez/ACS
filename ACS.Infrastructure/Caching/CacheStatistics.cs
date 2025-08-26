namespace ACS.Infrastructure.Caching;

/// <summary>
/// Cache statistics for monitoring and diagnostics
/// </summary>
public class CacheStatistics
{
    public long TotalRequests { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public long TotalEntries { get; set; }
    public long EvictedEntries { get; set; }
    public long ExpiredEntries { get; set; }
    public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
    public double MissRate => TotalRequests > 0 ? (double)CacheMisses / TotalRequests : 0;
    public TimeSpan AverageAccessTime { get; set; }
    public long MemoryUsageBytes { get; set; }
    public DateTime LastResetTime { get; set; } = DateTime.UtcNow;
}