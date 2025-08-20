using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.IO.Compression;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Multi-level cache implementation with L1 (Memory) and L2 (Distributed) caching
/// </summary>
public class MultiLevelCache : IMultiLevelCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ICacheStrategy _cacheStrategy;
    private readonly ILogger<MultiLevelCache> _logger;
    private readonly ActivitySource _activitySource = new("ACS.MultiLevelCache");
    
    // Statistics tracking
    private long _l1Hits = 0;
    private long _l2Hits = 0;
    private long _misses = 0;
    private readonly DateTime _startTime = DateTime.UtcNow;
    
    // Key tracking for pattern-based invalidation
    private readonly ConcurrentDictionary<string, HashSet<string>> _keysByPattern = new();
    private readonly ConcurrentDictionary<string, DateTime> _keyLastAccess = new();
    
    // Background refresh tracking
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshSemaphores = new();
    
    public MultiLevelCache(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ICacheStrategy cacheStrategy,
        ILogger<MultiLevelCache> logger)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _cacheStrategy = cacheStrategy;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.type", typeof(T).Name);
        
        // Try L1 cache first
        if (_memoryCache.TryGetValue<T>(key, out var l1Value))
        {
            Interlocked.Increment(ref _l1Hits);
            _keyLastAccess[key] = DateTime.UtcNow;
            activity?.SetTag("cache.level", "L1");
            activity?.SetTag("cache.hit", true);
            _logger.LogTrace("L1 cache hit for key {Key}", key);
            return l1Value;
        }
        
        // Try L2 cache
        try
        {
            var l2Data = await _distributedCache.GetAsync(key, cancellationToken);
            if (l2Data != null)
            {
                Interlocked.Increment(ref _l2Hits);
                activity?.SetTag("cache.level", "L2");
                activity?.SetTag("cache.hit", true);
                
                var l2Value = DeserializeValue<T>(l2Data);
                if (l2Value != null)
                {
                    // Promote to L1 cache
                    var cacheType = InferCacheType(key);
                    var options = CreateMemoryCacheOptions(cacheType);
                    _memoryCache.Set(key, l2Value, options);
                    
                    _keyLastAccess[key] = DateTime.UtcNow;
                    _logger.LogTrace("L2 cache hit for key {Key}, promoted to L1", key);
                    return l2Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error accessing L2 cache for key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
        }
        
        // Cache miss
        Interlocked.Increment(ref _misses);
        activity?.SetTag("cache.hit", false);
        _logger.LogTrace("Cache miss for key {Key}", key);
        return default;
    }

    public async Task SetAsync<T>(string key, T value, CacheType cacheType, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("SetAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.type", typeof(T).Name);
        activity?.SetTag("cache.cache_type", cacheType.ToString());
        
        if (value == null) return;
        
        try
        {
            // Set in L1 cache
            var memoryOptions = CreateMemoryCacheOptions(cacheType);
            _memoryCache.Set(key, value, memoryOptions);
            
            // Set in L2 cache
            var distributedOptions = CreateDistributedCacheOptions(cacheType);
            var serializedValue = SerializeValue(value, cacheType);
            await _distributedCache.SetAsync(key, serializedValue, distributedOptions, cancellationToken);
            
            // Track key for pattern-based operations
            TrackKeyPattern(key);
            _keyLastAccess[key] = DateTime.UtcNow;
            
            _logger.LogTrace("Set cache value for key {Key} in both L1 and L2", key);
            
            // Handle cache dependencies/invalidation
            var dependencies = _cacheStrategy.GetInvalidationKeys(cacheType, value, key);
            foreach (var dependentKey in dependencies)
            {
                await RemoveAsync(dependentKey, cancellationToken);
            }
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
            // Remove from L1
            _memoryCache.Remove(key);
            
            // Remove from L2
            await _distributedCache.RemoveAsync(key, cancellationToken);
            
            // Clean up tracking
            _keyLastAccess.TryRemove(key, out _);
            RemoveKeyFromPatternTracking(key);
            
            _logger.LogTrace("Removed cache entry for key {Key} from both levels", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache entry for key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("RemoveByPatternAsync");
        activity?.SetTag("cache.pattern", pattern);
        
        var removedCount = 0;
        
        try
        {
            // Get keys matching pattern
            var keysToRemove = GetKeysMatchingPattern(pattern);
            
            foreach (var key in keysToRemove)
            {
                await RemoveAsync(key, cancellationToken);
                removedCount++;
            }
            
            activity?.SetTag("cache.removed_count", removedCount);
            _logger.LogDebug("Removed {Count} cache entries matching pattern {Pattern}", removedCount, pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache entries by pattern {Pattern}", pattern);
            activity?.SetTag("cache.error", ex.Message);
        }
    }

    public async Task InvalidateAsync(CacheInvalidationEvent invalidationEvent, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("InvalidateAsync");
        activity?.SetTag("cache.invalidation_key", invalidationEvent.Key);
        activity?.SetTag("cache.invalidation_type", invalidationEvent.Type.ToString());
        activity?.SetTag("cache.tenant_id", invalidationEvent.TenantId);
        
        try
        {
            // Remove the primary key
            await RemoveAsync(invalidationEvent.Key, cancellationToken);
            
            // Remove dependent keys
            foreach (var dependentKey in invalidationEvent.DependentKeys)
            {
                if (dependentKey.EndsWith("*"))
                {
                    // Pattern-based removal
                    var pattern = dependentKey.TrimEnd('*');
                    await RemoveByPatternAsync(pattern, cancellationToken);
                }
                else
                {
                    // Exact key removal
                    await RemoveAsync(dependentKey, cancellationToken);
                }
            }
            
            _logger.LogDebug("Processed cache invalidation for {Key} with {DependentCount} dependencies", 
                invalidationEvent.Key, invalidationEvent.DependentKeys.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cache invalidation for {Key}", invalidationEvent.Key);
            activity?.SetTag("cache.error", ex.Message);
        }
    }

    public async Task<MultiLevelCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new MultiLevelCacheStatistics
        {
            TotalL1Hits = _l1Hits,
            TotalL2Hits = _l2Hits,
            TotalMisses = _misses,
            StartTime = _startTime
        };
        
        // Get additional statistics if available
        // Note: MemoryCache and IDistributedCache don't expose detailed stats by default
        
        return stats;
    }

    public async Task WarmupAsync(string[] keys, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("WarmupAsync");
        activity?.SetTag("cache.warmup_keys_count", keys.Length);
        
        var warmedUp = 0;
        
        try
        {
            var tasks = keys.Select(async key =>
            {
                try
                {
                    // Check if key exists in L2 and promote to L1
                    var data = await _distributedCache.GetAsync(key, cancellationToken);
                    if (data != null)
                    {
                        // We don't know the type here, so just check if it exists
                        Interlocked.Increment(ref warmedUp);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error warming up cache key {Key}", key);
                }
            });
            
            await Task.WhenAll(tasks);
            
            activity?.SetTag("cache.warmed_up_count", warmedUp);
            _logger.LogInformation("Cache warmup completed: {WarmedUp}/{Total} keys", warmedUp, keys.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache warmup");
            activity?.SetTag("cache.error", ex.Message);
        }
    }

    public async Task RefreshAsync<T>(string key, Func<Task<T>> refreshFunction, CacheType cacheType, CancellationToken cancellationToken = default)
    {
        var semaphore = _refreshSemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            // Another refresh is in progress
            _logger.LogTrace("Refresh already in progress for key {Key}", key);
            return;
        }
        
        try
        {
            using var activity = _activitySource.StartActivity("RefreshAsync");
            activity?.SetTag("cache.key", key);
            activity?.SetTag("cache.type", typeof(T).Name);
            
            var newValue = await refreshFunction();
            if (newValue != null)
            {
                await SetAsync(key, newValue, cacheType, cancellationToken);
                _logger.LogTrace("Successfully refreshed cache for key {Key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing cache for key {Key}", key);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private MemoryCacheEntryOptions CreateMemoryCacheOptions(CacheType cacheType)
    {
        var priority = _cacheStrategy.GetPriority(cacheType) switch
        {
            CachePriority.Low => Microsoft.Extensions.Caching.Memory.CacheItemPriority.Low,
            CachePriority.Normal => Microsoft.Extensions.Caching.Memory.CacheItemPriority.Normal,
            CachePriority.High => Microsoft.Extensions.Caching.Memory.CacheItemPriority.High,
            CachePriority.Critical => Microsoft.Extensions.Caching.Memory.CacheItemPriority.NeverRemove,
            _ => Microsoft.Extensions.Caching.Memory.CacheItemPriority.Normal
        };
        
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheStrategy.GetExpiration(cacheType),
            SlidingExpiration = _cacheStrategy.GetSlidingExpiration(cacheType),
            Priority = priority
        };
    }

    private DistributedCacheEntryOptions CreateDistributedCacheOptions(CacheType cacheType)
    {
        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheStrategy.GetExpiration(cacheType),
            SlidingExpiration = _cacheStrategy.GetSlidingExpiration(cacheType)
        };
    }

    private byte[] SerializeValue<T>(T value, CacheType cacheType)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        });
        
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        
        // Compress if strategy suggests it
        if (_cacheStrategy.ShouldCompress(cacheType, value))
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
            {
                gzip.Write(bytes);
            }
            return output.ToArray();
        }
        
        return bytes;
    }

    private T? DeserializeValue<T>(byte[] data)
    {
        try
        {
            // Try to decompress first
            var decompressedData = TryDecompress(data) ?? data;
            var json = System.Text.Encoding.UTF8.GetString(decompressedData);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached value");
            return default;
        }
    }

    private byte[]? TryDecompress(byte[] data)
    {
        try
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            // Not compressed or invalid compression
            return null;
        }
    }

    private CacheType InferCacheType(string key)
    {
        // Simple heuristic to infer cache type from key
        if (key.StartsWith("user:")) return CacheType.User;
        if (key.StartsWith("group:")) return CacheType.Group;
        if (key.StartsWith("role:")) return CacheType.Role;
        if (key.StartsWith("permission")) return CacheType.Permission;
        if (key.StartsWith("user_groups:")) return CacheType.UserGroups;
        if (key.StartsWith("user_roles:")) return CacheType.UserRoles;
        if (key.StartsWith("resource:")) return CacheType.Resource;
        if (key.StartsWith("session:")) return CacheType.Session;
        if (key.StartsWith("config:")) return CacheType.Configuration;
        return CacheType.Metadata;
    }

    private void TrackKeyPattern(string key)
    {
        // Extract patterns for efficient pattern-based operations
        var parts = key.Split(':');
        for (int i = 1; i <= parts.Length; i++)
        {
            var pattern = string.Join(":", parts.Take(i));
            _keysByPattern.AddOrUpdate(pattern, 
                new HashSet<string> { key }, 
                (_, set) => { set.Add(key); return set; });
        }
    }

    private void RemoveKeyFromPatternTracking(string key)
    {
        var parts = key.Split(':');
        for (int i = 1; i <= parts.Length; i++)
        {
            var pattern = string.Join(":", parts.Take(i));
            if (_keysByPattern.TryGetValue(pattern, out var set))
            {
                set.Remove(key);
                if (set.Count == 0)
                {
                    _keysByPattern.TryRemove(pattern, out _);
                }
            }
        }
    }

    private IEnumerable<string> GetKeysMatchingPattern(string pattern)
    {
        var matchingKeys = new HashSet<string>();
        
        foreach (var kvp in _keysByPattern)
        {
            if (kvp.Key.StartsWith(pattern))
            {
                foreach (var key in kvp.Value)
                {
                    matchingKeys.Add(key);
                }
            }
        }
        
        return matchingKeys;
    }
}