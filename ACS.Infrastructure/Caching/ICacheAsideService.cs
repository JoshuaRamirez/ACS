namespace ACS.Infrastructure.Caching;

/// <summary>
/// Cache-aside pattern implementation for entity caching
/// </summary>
public interface ICacheAsideService
{
    /// <summary>
    /// Gets a value from cache or loads it using the provided function
    /// </summary>
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, CacheType cacheType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a value from cache or loads it using the provided function with expiration
    /// </summary>
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, CacheType cacheType, TimeSpan expiration, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets multiple values from cache or loads missing ones
    /// </summary>
    Task<Dictionary<string, T?>> GetOrSetManyAsync<T>(Dictionary<string, Func<Task<T?>>> keyFactoryPairs, CacheType cacheType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets a value in cache
    /// </summary>
    Task SetAsync<T>(string key, T value, CacheType cacheType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a value from cache
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes multiple values from cache
    /// </summary>
    Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refreshes cache entry with write-behind pattern
    /// </summary>
    Task RefreshAsync<T>(string key, Func<Task<T?>> factory, CacheType cacheType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a function and invalidates related cache entries
    /// </summary>
    Task<T> ExecuteWithInvalidationAsync<T>(Func<Task<T>> operation, CacheInvalidationEvent invalidationEvent, CancellationToken cancellationToken = default);
}