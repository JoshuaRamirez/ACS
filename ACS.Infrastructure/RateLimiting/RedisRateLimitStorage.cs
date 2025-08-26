using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace ACS.Infrastructure.RateLimiting;

/// <summary>
/// Redis-based distributed rate limit storage implementation
/// Suitable for multi-instance deployments requiring shared rate limiting
/// </summary>
public class RedisRateLimitStorage : IRateLimitStorage, IDisposable
{
    private readonly ILogger<RedisRateLimitStorage> _logger;
    private readonly IDatabase _database;
    private readonly ConnectionMultiplexer _redis;
    private readonly string _keyPrefix;
    private readonly Timer _cleanupTimer;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisRateLimitStorage(
        ILogger<RedisRateLimitStorage> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        
        var connectionString = configuration.GetConnectionString("Redis") 
                              ?? throw new InvalidOperationException("Redis connection string not configured");
        
        _keyPrefix = configuration.GetValue<string>("RateLimit:Redis:KeyPrefix") ?? "rl:";
        
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _database = _redis.GetDatabase();
        
        // Setup periodic cleanup every 10 minutes
        _cleanupTimer = new Timer(async _ => await CleanupExpiredAsync(), 
            null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        
        _logger.LogInformation("Initialized Redis rate limit storage with prefix {KeyPrefix}", _keyPrefix);
    }

    public async Task<RateLimitStorageData?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        try
        {
            var redisKey = GetRedisKey(key);
            var value = await _database.StringGetAsync(redisKey);
            
            if (!value.HasValue)
            {
                return null;
            }
            
            var data = JsonSerializer.Deserialize<RateLimitStorageData>(value!, JsonOptions);
            
            // Check if data has expired
            if (data != null && data.ExpiresAt > DateTime.UtcNow)
            {
                _logger.LogTrace("Retrieved rate limit data from Redis for key {Key}", key);
                return data;
            }
            else if (data != null)
            {
                // Remove expired data
                await _database.KeyDeleteAsync(redisKey);
                _logger.LogTrace("Expired rate limit data removed from Redis for key {Key}", key);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rate limit data from Redis for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync(string key, RateLimitStorageData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(data);
        
        try
        {
            var redisKey = GetRedisKey(key);
            data.LastUpdated = DateTime.UtcNow;
            
            var json = JsonSerializer.Serialize(data, JsonOptions);
            var expiry = data.ExpiresAt - DateTime.UtcNow;
            
            if (expiry > TimeSpan.Zero)
            {
                await _database.StringSetAsync(redisKey, json, expiry);
                
                // Also set in a sorted set for efficient cleanup and querying
                var score = data.ExpiresAt.Ticks;
                await _database.SortedSetAddAsync(GetCleanupSetKey(), redisKey, score);
                
                _logger.LogTrace("Stored rate limit data in Redis for key {Key} with expiry {Expiry}", 
                    key, expiry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing rate limit data in Redis for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        try
        {
            var redisKey = GetRedisKey(key);
            
            // Remove from both the main storage and cleanup set
            var tasks = new Task[]
            {
                _database.KeyDeleteAsync(redisKey),
                _database.SortedSetRemoveAsync(GetCleanupSetKey(), redisKey)
            };
            
            await Task.WhenAll(tasks);
            
            _logger.LogTrace("Removed rate limit data from Redis for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing rate limit data from Redis for key {Key}", key);
        }
    }

    public async Task<IEnumerable<RateLimitStorageData>> GetByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        
        try
        {
            var results = new List<RateLimitStorageData>();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            var pattern = GetRedisKey(prefix + "*");
            var keys = server.Keys(pattern: pattern);
            
            var tasks = keys.Select(async key =>
            {
                try
                {
                    var value = await _database.StringGetAsync(key);
                    if (value.HasValue)
                    {
                        var data = JsonSerializer.Deserialize<RateLimitStorageData>(value!, JsonOptions);
                        if (data != null && data.ExpiresAt > DateTime.UtcNow)
                        {
                            return data;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deserializing rate limit data for key {Key}", key);
                }
                return null;
            }).ToArray();
            
            var loadedData = await Task.WhenAll(tasks);
            results.AddRange(loadedData.Where(d => d != null)!);
            
            _logger.LogTrace("Found {Count} entries with prefix {Prefix} in Redis", results.Count, prefix);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying rate limit data by prefix {Prefix} in Redis", prefix);
            return Enumerable.Empty<RateLimitStorageData>();
        }
    }

    public async Task CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cleanupSetKey = GetCleanupSetKey();
            var now = DateTime.UtcNow.Ticks;
            
            // Get expired entries from sorted set
            var expiredEntries = await _database.SortedSetRangeByScoreAsync(
                cleanupSetKey, 0, now);
            
            if (expiredEntries.Length == 0)
            {
                _logger.LogTrace("No expired entries found during Redis cleanup");
                return;
            }
            
            // Remove expired entries in batches
            const int batchSize = 100;
            var tasks = new List<Task>();
            
            for (int i = 0; i < expiredEntries.Length; i += batchSize)
            {
                var batch = expiredEntries.Skip(i).Take(batchSize);
                tasks.Add(RemoveExpiredBatchAsync(batch, cleanupSetKey));
            }
            
            await Task.WhenAll(tasks);
            
            _logger.LogDebug("Cleaned up {Count} expired rate limit entries from Redis", expiredEntries.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Redis rate limit storage cleanup");
        }
    }

    public async Task<RateLimitStorageStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var info = await server.InfoAsync();
            
            // Count entries with our prefix
            var pattern = GetRedisKey("*");
            var keys = server.Keys(pattern: pattern);
            var totalEntries = keys.LongCount();
            
            // Count expired entries using cleanup set
            var cleanupSetKey = GetCleanupSetKey();
            var now = DateTime.UtcNow.Ticks;
            var expiredEntries = await _database.SortedSetLengthByValueAsync(cleanupSetKey, 0, now);
            
            // Get tenant counts (this is expensive, so we'll sample)
            var tenantCounts = await GetTenantCountsAsync(keys.Take(1000));
            
            return new RateLimitStorageStats
            {
                TotalEntries = totalEntries,
                ExpiredEntries = expiredEntries,
                TotalRequests = totalEntries, // Approximation
                LastCleanup = DateTime.UtcNow, // We don't track this in Redis
                AverageResponseTime = TimeSpan.FromMilliseconds(5), // Typical Redis response time
                TenantCounts = tenantCounts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Redis rate limit storage stats");
            return new RateLimitStorageStats();
        }
    }

    private async Task RemoveExpiredBatchAsync(IEnumerable<RedisValue> expiredKeys, string cleanupSetKey)
    {
        var batch = _database.CreateBatch();
        var tasks = new List<Task>();
        
        foreach (var keyValue in expiredKeys)
        {
            // Convert RedisValue to string to RedisKey for key operations
            string keyString = keyValue.ToString();
            RedisKey redisKey = keyString;
            
            tasks.Add(batch.KeyDeleteAsync(redisKey));
            tasks.Add(batch.SortedSetRemoveAsync(cleanupSetKey, keyValue)); // Use original RedisValue for sorted set
        }
        
        batch.Execute();
        await Task.WhenAll(tasks);
    }

    private Task<Dictionary<string, long>> GetTenantCountsAsync(IEnumerable<RedisKey> keys)
    {
        var tenantCounts = new Dictionary<string, long>();
        
        foreach (var key in keys)
        {
            var keyString = key.ToString();
            if (keyString.StartsWith(_keyPrefix))
            {
                var unprefixedKey = keyString[_keyPrefix.Length..];
                var colonIndex = unprefixedKey.IndexOf(':');
                if (colonIndex > 0)
                {
                    var tenantId = unprefixedKey[..colonIndex];
                    tenantCounts.TryGetValue(tenantId, out var count);
                    tenantCounts[tenantId] = count + 1;
                }
            }
        }
        
        return Task.FromResult(tenantCounts);
    }

    private string GetRedisKey(string key) => $"{_keyPrefix}{key}";
    private string GetCleanupSetKey() => $"{_keyPrefix}cleanup_set";

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _redis?.Dispose();
        
        _logger.LogInformation("Disposed Redis rate limit storage");
    }
}