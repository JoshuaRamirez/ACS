using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Implementation of cache-aside pattern with write-through and write-behind support
/// </summary>
public class CacheAsideService : ICacheAsideService
{
    private readonly IMultiLevelCache _cache;
    private readonly ILogger<CacheAsideService> _logger;
    private readonly ActivitySource _activitySource = new("ACS.CacheAside");
    
    // Semaphores for preventing cache stampede
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _loadingSemaphores = new();
    
    public CacheAsideService(
        IMultiLevelCache cache,
        ILogger<CacheAsideService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, CacheType cacheType, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetOrSetAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.type", typeof(T).Name);
        activity?.SetTag("cache.cache_type", cacheType.ToString());
        
        // Try to get from cache first
        var cachedValue = await _cache.GetAsync<T>(key, cancellationToken);
        if (cachedValue != null)
        {
            activity?.SetTag("cache.hit", true);
            return cachedValue;
        }
        
        activity?.SetTag("cache.hit", false);
        
        // Prevent cache stampede by using semaphores
        var semaphore = _loadingSemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check pattern - value might have been loaded by another thread
            cachedValue = await _cache.GetAsync<T>(key, cancellationToken);
            if (cachedValue != null)
            {
                activity?.SetTag("cache.hit_after_wait", true);
                return cachedValue;
            }
            
            // Load the value
            var stopwatch = Stopwatch.StartNew();
            var value = await factory();
            stopwatch.Stop();
            
            activity?.SetTag("cache.load_time_ms", stopwatch.ElapsedMilliseconds);
            _logger.LogTrace("Loaded value for key {Key} in {ElapsedMs}ms", key, stopwatch.ElapsedMilliseconds);
            
            // Cache the value if it's not null
            if (value != null)
            {
                await _cache.SetAsync(key, value, cacheType, cancellationToken);
                activity?.SetTag("cache.stored", true);
            }
            
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading value for cache key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
            throw;
        }
        finally
        {
            semaphore.Release();
            
            // Clean up old semaphores to prevent memory leaks
            if (semaphore.CurrentCount == 1)
            {
                _loadingSemaphores.TryRemove(key, out _);
            }
        }
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, CacheType cacheType, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        // For custom expiration, we'd need to extend the cache interface
        // For now, use the standard method and log the custom expiration request
        _logger.LogTrace("Custom expiration {Expiration} requested for key {Key} (using default strategy)", expiration, key);
        return await GetOrSetAsync(key, factory, cacheType, cancellationToken);
    }

    public async Task<Dictionary<string, T?>> GetOrSetManyAsync<T>(Dictionary<string, Func<Task<T?>>> keyFactoryPairs, CacheType cacheType, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetOrSetManyAsync");
        activity?.SetTag("cache.keys_count", keyFactoryPairs.Count);
        activity?.SetTag("cache.type", typeof(T).Name);
        activity?.SetTag("cache.cache_type", cacheType.ToString());
        
        var result = new Dictionary<string, T?>();
        var missedKeys = new List<string>();
        
        // First, try to get all values from cache
        foreach (var kvp in keyFactoryPairs)
        {
            var cachedValue = await _cache.GetAsync<T>(kvp.Key, cancellationToken);
            if (cachedValue != null)
            {
                result[kvp.Key] = cachedValue;
            }
            else
            {
                missedKeys.Add(kvp.Key);
            }
        }
        
        activity?.SetTag("cache.hits", result.Count);
        activity?.SetTag("cache.misses", missedKeys.Count);
        
        // Load missing values in parallel
        if (missedKeys.Count > 0)
        {
            var loadTasks = missedKeys.Select(async key =>
            {
                try
                {
                    var factory = keyFactoryPairs[key];
                    var value = await factory();
                    
                    if (value != null)
                    {
                        await _cache.SetAsync(key, value, cacheType, cancellationToken);
                    }
                    
                    return new { Key = key, Value = value };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading value for key {Key}", key);
                    return new { Key = key, Value = default(T) };
                }
            });
            
            var loadResults = await Task.WhenAll(loadTasks);
            
            foreach (var loadResult in loadResults)
            {
                result[loadResult.Key] = loadResult.Value;
            }
        }
        
        return result;
    }

    public async Task SetAsync<T>(string key, T value, CacheType cacheType, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("SetAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.type", typeof(T).Name);
        activity?.SetTag("cache.cache_type", cacheType.ToString());
        
        try
        {
            await _cache.SetAsync(key, value, cacheType, cancellationToken);
            _logger.LogTrace("Set cache value for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
            throw;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("RemoveAsync");
        activity?.SetTag("cache.key", key);
        
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            _logger.LogTrace("Removed cache entry for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache entry for key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
        }
    }

    public async Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("RemoveManyAsync");
        var keyList = keys.ToList();
        activity?.SetTag("cache.keys_count", keyList.Count);
        
        try
        {
            var removeTasks = keyList.Select(key => RemoveAsync(key, cancellationToken));
            await Task.WhenAll(removeTasks);
            
            _logger.LogTrace("Removed {Count} cache entries", keyList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing multiple cache entries");
            activity?.SetTag("cache.error", ex.Message);
        }
    }

    public async Task RefreshAsync<T>(string key, Func<Task<T?>> factory, CacheType cacheType, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("RefreshAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.type", typeof(T).Name);
        activity?.SetTag("cache.cache_type", cacheType.ToString());
        
        try
        {
            await _cache.RefreshAsync(key, factory, cacheType, cancellationToken);
            _logger.LogTrace("Refreshed cache for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing cache for key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
        }
    }

    public async Task<T> ExecuteWithInvalidationAsync<T>(Func<Task<T>> operation, CacheInvalidationEvent invalidationEvent, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ExecuteWithInvalidationAsync");
        activity?.SetTag("cache.invalidation_key", invalidationEvent.Key);
        activity?.SetTag("cache.invalidation_type", invalidationEvent.Type.ToString());
        activity?.SetTag("cache.tenant_id", invalidationEvent.TenantId);
        
        try
        {
            // Execute the operation first
            var result = await operation();
            
            // Then invalidate cache entries
            await _cache.InvalidateAsync(invalidationEvent, cancellationToken);
            
            _logger.LogTrace("Executed operation and invalidated cache for {Key}", invalidationEvent.Key);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing operation with cache invalidation for {Key}", invalidationEvent.Key);
            activity?.SetTag("cache.error", ex.Message);
            throw;
        }
    }
}