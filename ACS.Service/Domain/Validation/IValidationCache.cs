namespace ACS.Service.Domain.Validation;

/// <summary>
/// Cache interface for validation operations
/// </summary>
public interface IValidationCache
{
    /// <summary>
    /// Gets cached validation result
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;
    
    /// <summary>
    /// Sets cached validation result
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    
    /// <summary>
    /// Removes cached validation result
    /// </summary>
    Task RemoveAsync(string key);
    
    /// <summary>
    /// Removes all cached results matching pattern
    /// </summary>
    Task RemovePatternAsync(string pattern);
    
    /// <summary>
    /// Clears all validation cache
    /// </summary>
    Task ClearAsync();
    
    /// <summary>
    /// Gets cache statistics
    /// </summary>
    Task<ValidationCacheStatistics> GetStatisticsAsync();
}

/// <summary>
/// Statistics for validation cache performance
/// </summary>
public class ValidationCacheStatistics
{
    public int TotalEntries { get; set; }
    public int HitCount { get; set; }
    public int MissCount { get; set; }
    public double HitRate => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0;
    public int TotalRequests => HitCount + MissCount;
    public long TotalMemoryUsage { get; set; }
    public Dictionary<string, int> EntriesByType { get; set; } = new();
    public DateTime LastReset { get; set; }
}