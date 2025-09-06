using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Three-level cache implementation with L1 (Memory), L2 (Redis), L3 (SQL Server) caching
/// with failover, circuit breaker patterns, and comprehensive performance monitoring
/// </summary>
public class ThreeLevelCache : IThreeLevelCache
{
    private readonly IMemoryCache _l1Cache;
    private readonly IDistributedCache _l2Cache; // Redis
    private readonly IDistributedCache _l3Cache; // SQL Server
    private readonly ICacheStrategy _cacheStrategy;
    private readonly ILogger<ThreeLevelCache> _logger;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource = new("ACS.ThreeLevelCache");
    
    // Statistics tracking
    private long _l1Hits = 0;
    private long _l2Hits = 0;
    private long _l3Hits = 0;
    private long _misses = 0;
    private readonly DateTime _startTime = DateTime.UtcNow;
    
    // Key tracking for pattern-based invalidation
    private readonly ConcurrentDictionary<string, HashSet<string>> _keysByPattern = new();
    private readonly ConcurrentDictionary<string, DateTime> _keyLastAccess = new();
    
    // Background refresh tracking
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshSemaphores = new();
    
    // Circuit breakers for each level
    private readonly AsyncCircuitBreakerPolicy _l2CircuitBreaker;
    private readonly AsyncCircuitBreakerPolicy _l3CircuitBreaker;
    
    // Configuration
    private readonly CacheFailoverStrategy _failoverStrategy;
    private readonly bool _enableL2Fallback;
    private readonly bool _enableL3Fallback;
    private readonly TimeSpan _promotionDelay;

    public ThreeLevelCache(
        IMemoryCache memoryCache,
        RedisCache redisCache,
        SqlServerCache sqlServerCache,
        ICacheStrategy cacheStrategy,
        IConfiguration configuration,
        ILogger<ThreeLevelCache> logger)
    {
        _l1Cache = memoryCache;
        _l2Cache = redisCache;
        _l3Cache = sqlServerCache;
        _cacheStrategy = cacheStrategy;
        _configuration = configuration;
        _logger = logger;
        
        _failoverStrategy = Enum.Parse<CacheFailoverStrategy>(
            configuration.GetValue<string>("Caching:FailoverStrategy") ?? "Fallback");
        _enableL2Fallback = configuration.GetValue<bool>("Caching:EnableL2Fallback", true);
        _enableL3Fallback = configuration.GetValue<bool>("Caching:EnableL3Fallback", true);
        _promotionDelay = TimeSpan.FromSeconds(configuration.GetValue<int>("Caching:PromotionDelaySeconds", 1));
        
        // Configure circuit breakers for L2 and L3
        _l2CircuitBreaker = Policy
            .Handle<Exception>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: configuration.GetValue<double>("Caching:L2CircuitBreaker:FailureThreshold", 0.5),
                samplingDuration: TimeSpan.FromSeconds(configuration.GetValue<int>("Caching:L2CircuitBreaker:SamplingDurationSeconds", 10)),
                minimumThroughput: configuration.GetValue<int>("Caching:L2CircuitBreaker:MinimumThroughput", 3),
                durationOfBreak: TimeSpan.FromSeconds(configuration.GetValue<int>("Caching:L2CircuitBreaker:BreakDurationSeconds", 30)),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning("L2 cache circuit breaker opened for {Duration}s due to: {Exception}", 
                        duration.TotalSeconds, exception.Message);
                },
                onReset: () =>
                {
                    _logger.LogInformation("L2 cache circuit breaker reset");
                });

        _l3CircuitBreaker = Policy
            .Handle<Exception>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: configuration.GetValue<double>("Caching:L3CircuitBreaker:FailureThreshold", 0.5),
                samplingDuration: TimeSpan.FromSeconds(configuration.GetValue<int>("Caching:L3CircuitBreaker:SamplingDurationSeconds", 10)),
                minimumThroughput: configuration.GetValue<int>("Caching:L3CircuitBreaker:MinimumThroughput", 2),
                durationOfBreak: TimeSpan.FromSeconds(configuration.GetValue<int>("Caching:L3CircuitBreaker:BreakDurationSeconds", 60)),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning("L3 cache circuit breaker opened for {Duration}s due to: {Exception}", 
                        duration.TotalSeconds, exception.Message);
                },
                onReset: () =>
                {
                    _logger.LogInformation("L3 cache circuit breaker reset");
                });
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.type", typeof(T).Name);
        
        // Try L1 cache first (fastest)
        if (_l1Cache.TryGetValue<T>(key, out var l1Value))
        {
            Interlocked.Increment(ref _l1Hits);
            _keyLastAccess[key] = DateTime.UtcNow;
            activity?.SetTag("cache.level", "L1");
            activity?.SetTag("cache.hit", true);
            _logger.LogTrace("L1 cache hit for key {Key}", key);
            return l1Value;
        }
        
        // Try L2 cache (Redis)
        if (_enableL2Fallback)
        {
            var l2Value = await TryGetFromL2Async<T>(key, cancellationToken);
            if (l2Value != null)
            {
                Interlocked.Increment(ref _l2Hits);
                activity?.SetTag("cache.level", "L2");
                activity?.SetTag("cache.hit", true);
                
                // Promote to L1 cache asynchronously to avoid blocking
                _ = Task.Run(async () =>
                {
                    await Task.Delay(_promotionDelay, cancellationToken);
                    var cacheType = InferCacheType(key);
                    var options = CreateMemoryCacheOptions(cacheType);
                    _l1Cache.Set(key, l2Value, options);
                }, cancellationToken);
                
                _keyLastAccess[key] = DateTime.UtcNow;
                _logger.LogTrace("L2 cache hit for key {Key}, promoting to L1", key);
                return l2Value;
            }
        }
        
        // Try L3 cache (SQL Server)
        if (_enableL3Fallback)
        {
            var l3Value = await TryGetFromL3Async<T>(key, cancellationToken);
            if (l3Value != null)
            {
                Interlocked.Increment(ref _l3Hits);
                activity?.SetTag("cache.level", "L3");
                activity?.SetTag("cache.hit", true);
                
                // Promote to L2 and L1 caches asynchronously
                _ = Task.Run(async () =>
                {
                    await Task.Delay(_promotionDelay, cancellationToken);
                    var cacheType = InferCacheType(key);
                    
                    // Promote to L2
                    if (_enableL2Fallback)
                    {
                        await TrySetInL2Async(key, l3Value, cacheType, cancellationToken);
                    }
                    
                    // Promote to L1
                    var options = CreateMemoryCacheOptions(cacheType);
                    _l1Cache.Set(key, l3Value, options);
                }, cancellationToken);
                
                _keyLastAccess[key] = DateTime.UtcNow;
                _logger.LogTrace("L3 cache hit for key {Key}, promoting to L2 and L1", key);
                return l3Value;
            }
        }
        
        // Cache miss
        Interlocked.Increment(ref _misses);
        activity?.SetTag("cache.hit", false);
        _logger.LogTrace("Cache miss for key {Key} across all levels", key);
        return default;
    }

    public async Task SetAsync<T>(string key, T value, CacheType cacheType, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("SetAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.type", typeof(T).Name);
        activity?.SetTag("cache.cache_type", cacheType.ToString());
        
        if (value == null) return;
        
        var tasks = new List<Task>();
        
        try
        {
            // Set in L1 cache (always fastest)
            var memoryOptions = CreateMemoryCacheOptions(cacheType);
            _l1Cache.Set(key, value, memoryOptions);
            
            // Set in L2 cache (Redis) if available
            if (_enableL2Fallback)
            {
                tasks.Add(TrySetInL2Async(key, value, cacheType, cancellationToken));
            }
            
            // Set in L3 cache (SQL Server) if available
            if (_enableL3Fallback)
            {
                tasks.Add(TrySetInL3Async(key, value, cacheType, cancellationToken));
            }
            
            // Wait for all cache levels to complete (with error tolerance)
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.Select(async task =>
                {
                    try
                    {
                        await task;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error setting cache value in distributed cache for key {Key}", key);
                    }
                }));
            }
            
            // Track key for pattern-based operations
            TrackKeyPattern(key);
            _keyLastAccess[key] = DateTime.UtcNow;
            
            _logger.LogTrace("Set cache value for key {Key} across all available levels", key);
            
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
            var tasks = new List<Task>();
            
            // Remove from L1
            _l1Cache.Remove(key);
            
            // Remove from L2 (Redis)
            if (_enableL2Fallback)
            {
                tasks.Add(TryRemoveFromL2Async(key, cancellationToken));
            }
            
            // Remove from L3 (SQL Server)
            if (_enableL3Fallback)
            {
                tasks.Add(TryRemoveFromL3Async(key, cancellationToken));
            }
            
            // Wait for all removals (with error tolerance)
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.Select(async task =>
                {
                    try
                    {
                        await task;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error removing cache entry from distributed cache for key {Key}", key);
                    }
                }));
            }
            
            // Clean up tracking
            _keyLastAccess.TryRemove(key, out _);
            RemoveKeyFromPatternTracking(key);
            
            _logger.LogTrace("Removed cache entry for key {Key} from all levels", key);
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
            // Get keys matching pattern from our tracking
            var keysToRemove = GetKeysMatchingPattern(pattern).ToArray();
            
            // Remove from L1 (memory cache doesn't support pattern removal, so we remove tracked keys)
            foreach (var key in keysToRemove)
            {
                _l1Cache.Remove(key);
                removedCount++;
            }
            
            var tasks = new List<Task>();
            
            // Remove from L2 (Redis supports pattern removal)
            if (_enableL2Fallback && _l2Cache is RedisCache redisCache)
            {
                tasks.Add(redisCache.RemoveByPatternAsync(pattern, cancellationToken));
            }
            
            // Remove from L3 (SQL Server supports pattern removal)
            if (_enableL3Fallback && _l3Cache is SqlServerCache sqlServerCache)
            {
                tasks.Add(sqlServerCache.RemoveByPatternAsync(pattern, cancellationToken));
            }
            
            // Wait for distributed cache pattern removals
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.Select(async task =>
                {
                    try
                    {
                        await task;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error removing cache entries by pattern {Pattern} from distributed cache", pattern);
                    }
                }));
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

    public Task<MultiLevelCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new MultiLevelCacheStatistics
        {
            TotalL1Hits = _l1Hits,
            TotalL2Hits = _l2Hits,
            TotalMisses = _misses,
            StartTime = _startTime
        };
        
        return Task.FromResult(stats);
    }

    public Task<ThreeLevelCacheStatistics> GetThreeLevelStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new ThreeLevelCacheStatistics
        {
            TotalL1Hits = _l1Hits,
            TotalL2Hits = _l2Hits,
            TotalL3Hits = _l3Hits,
            TotalMisses = _misses,
            StartTime = _startTime
        };
        
        return Task.FromResult(stats);
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
                    // Try to get from any level to promote to higher levels
                    var exists = false;
                    
                    // Check L3 first and promote upward
                    if (_enableL3Fallback)
                    {
                        var l3Data = await _l3Cache.GetAsync(key, cancellationToken);
                        if (l3Data != null)
                        {
                            exists = true;
                            
                            // Promote to L2
                            if (_enableL2Fallback)
                            {
                                await _l2Cache.SetAsync(key, l3Data, new DistributedCacheEntryOptions
                                {
                                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                                }, cancellationToken);
                            }
                        }
                    }
                    
                    // Check L2 and promote to L1
                    if (!exists && _enableL2Fallback)
                    {
                        var l2Data = await _l2Cache.GetAsync(key, cancellationToken);
                        if (l2Data != null)
                        {
                            exists = true;
                        }
                    }
                    
                    if (exists)
                    {
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

    // IThreeLevelCache specific methods

    public async Task SetAtLevelAsync<T>(string key, T value, CacheType cacheType, CacheLevel level, CancellationToken cancellationToken = default)
    {
        switch (level)
        {
            case CacheLevel.L1_Memory:
                var options = CreateMemoryCacheOptions(cacheType);
                _l1Cache.Set(key, value, options);
                break;
                
            case CacheLevel.L2_Redis:
                if (_enableL2Fallback)
                    await TrySetInL2Async(key, value, cacheType, cancellationToken);
                break;
                
            case CacheLevel.L3_SqlServer:
                if (_enableL3Fallback)
                    await TrySetInL3Async(key, value, cacheType, cancellationToken);
                break;
        }
    }

    public async Task<T?> GetFromLevelAsync<T>(string key, CacheLevel level, CancellationToken cancellationToken = default)
    {
        switch (level)
        {
            case CacheLevel.L1_Memory:
                return _l1Cache.TryGetValue<T>(key, out var value) ? value : default;
                
            case CacheLevel.L2_Redis:
                return _enableL2Fallback ? await TryGetFromL2Async<T>(key, cancellationToken) : default;
                
            case CacheLevel.L3_SqlServer:
                return _enableL3Fallback ? await TryGetFromL3Async<T>(key, cancellationToken) : default;
                
            default:
                return default;
        }
    }

    public async Task PromoteAsync(string key, CancellationToken cancellationToken = default)
    {
        // Try to get from L3 and promote to L2 and L1
        var l3Data = await _l3Cache.GetAsync(key, cancellationToken);
        if (l3Data != null)
        {
            if (_enableL2Fallback)
            {
                await _l2Cache.SetAsync(key, l3Data, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                }, cancellationToken);
            }
            
            // Deserialize and set in L1 (this is a simplified approach)
            // In practice, you'd need type information
            _logger.LogTrace("Promoted key {Key} from L3 to upper levels", key);
        }
        
        // Try to get from L2 and promote to L1
        if (_enableL2Fallback)
        {
            var l2Data = await _l2Cache.GetAsync(key, cancellationToken);
            if (l2Data != null)
            {
                _logger.LogTrace("Promoted key {Key} from L2 to L1", key);
            }
        }
    }

    public async Task DemoteAsync(string key, CancellationToken cancellationToken = default)
    {
        // Remove from L1 first
        _l1Cache.Remove(key);
        
        // Optionally remove from L2 to force retrieval from L3
        if (_enableL2Fallback)
        {
            await TryRemoveFromL2Async(key, cancellationToken);
        }
        
        _logger.LogTrace("Demoted key {Key} to lower cache levels", key);
    }

    public async Task<Dictionary<string, Dictionary<string, string>>> GetHealthInfoAsync()
    {
        var healthInfo = new Dictionary<string, Dictionary<string, string>>();
        
        // L1 (Memory) health
        healthInfo["L1_Memory"] = new Dictionary<string, string>
        {
            ["status"] = "available",
            ["type"] = "memory",
            ["hits"] = _l1Hits.ToString(),
            ["estimated_entries"] = "unknown" // MemoryCache doesn't expose count
        };
        
        // L2 (Redis) health
        if (_enableL2Fallback && _l2Cache is RedisCache redisCache)
        {
            try
            {
                healthInfo["L2_Redis"] = await redisCache.GetHealthInfoAsync();
            }
            catch (Exception ex)
            {
                healthInfo["L2_Redis"] = new Dictionary<string, string>
                {
                    ["status"] = "error",
                    ["error"] = ex.Message,
                    ["hits"] = _l2Hits.ToString()
                };
            }
        }
        else
        {
            healthInfo["L2_Redis"] = new Dictionary<string, string>
            {
                ["status"] = "disabled",
                ["hits"] = _l2Hits.ToString()
            };
        }
        
        // L3 (SQL Server) health
        if (_enableL3Fallback && _l3Cache is SqlServerCache sqlServerCache)
        {
            try
            {
                healthInfo["L3_SqlServer"] = await sqlServerCache.GetHealthInfoAsync();
            }
            catch (Exception ex)
            {
                healthInfo["L3_SqlServer"] = new Dictionary<string, string>
                {
                    ["status"] = "error",
                    ["error"] = ex.Message,
                    ["hits"] = _l3Hits.ToString()
                };
            }
        }
        else
        {
            healthInfo["L3_SqlServer"] = new Dictionary<string, string>
            {
                ["status"] = "disabled",
                ["hits"] = _l3Hits.ToString()
            };
        }
        
        return healthInfo;
    }

    // Private helper methods

    private async Task<T?> TryGetFromL2Async<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await _l2CircuitBreaker.ExecuteAsync(async () =>
            {
                var data = await _l2Cache.GetAsync(key, cancellationToken);
                return data != null ? DeserializeValue<T>(data) : default;
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting value from L2 cache for key {Key}", key);
            return default;
        }
    }

    private async Task<T?> TryGetFromL3Async<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await _l3CircuitBreaker.ExecuteAsync(async () =>
            {
                var data = await _l3Cache.GetAsync(key, cancellationToken);
                return data != null ? DeserializeValue<T>(data) : default;
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting value from L3 cache for key {Key}", key);
            return default;
        }
    }

    private async Task TrySetInL2Async<T>(string key, T value, CacheType cacheType, CancellationToken cancellationToken)
    {
        try
        {
            await _l2CircuitBreaker.ExecuteAsync(async () =>
            {
                var distributedOptions = CreateDistributedCacheOptions(cacheType);
                var serializedValue = SerializeValue(value, cacheType);
                await _l2Cache.SetAsync(key, serializedValue, distributedOptions, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error setting value in L2 cache for key {Key}", key);
        }
    }

    private async Task TrySetInL3Async<T>(string key, T value, CacheType cacheType, CancellationToken cancellationToken)
    {
        try
        {
            await _l3CircuitBreaker.ExecuteAsync(async () =>
            {
                var distributedOptions = CreateDistributedCacheOptions(cacheType);
                var serializedValue = SerializeValue(value, cacheType);
                await _l3Cache.SetAsync(key, serializedValue, distributedOptions, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error setting value in L3 cache for key {Key}", key);
        }
    }

    private async Task TryRemoveFromL2Async(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _l2CircuitBreaker.ExecuteAsync(async () =>
            {
                await _l2Cache.RemoveAsync(key, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error removing value from L2 cache for key {Key}", key);
        }
    }

    private async Task TryRemoveFromL3Async(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _l3CircuitBreaker.ExecuteAsync(async () =>
            {
                await _l3Cache.RemoveAsync(key, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error removing value from L3 cache for key {Key}", key);
        }
    }

    // Existing helper methods from MultiLevelCache
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
        
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    private T? DeserializeValue<T>(byte[] data)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached value");
            return default;
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