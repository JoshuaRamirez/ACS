using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ACS.Infrastructure.RateLimiting;

/// <summary>
/// In-memory implementation of rate limit storage
/// Suitable for single-instance scenarios or development
/// </summary>
public class InMemoryRateLimitStorage : IRateLimitStorage, IDisposable
{
    private readonly ILogger<InMemoryRateLimitStorage> _logger;
    private readonly ConcurrentDictionary<string, RateLimitStorageData> _storage = new();
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
    
    // Statistics tracking
    private long _totalRequests = 0;
    private DateTime _lastCleanup = DateTime.UtcNow;
    private readonly ConcurrentDictionary<string, long> _tenantRequestCounts = new();

    public InMemoryRateLimitStorage(ILogger<InMemoryRateLimitStorage> logger)
    {
        _logger = logger;
        
        // Setup periodic cleanup every 5 minutes
        _cleanupTimer = new Timer(async _ => await CleanupExpiredAsync(), 
            null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task<RateLimitStorageData?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        if (_storage.TryGetValue(key, out var data))
        {
            // Check if data has expired
            if (data.ExpiresAt > DateTime.UtcNow)
            {
                _logger.LogTrace("Retrieved rate limit data for key {Key}", key);
                return CloneStorageData(data);
            }
            else
            {
                // Remove expired data
                _storage.TryRemove(key, out _);
                _logger.LogTrace("Expired rate limit data removed for key {Key}", key);
            }
        }
        
        return null;
    }

    public async Task SetAsync(string key, RateLimitStorageData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(data);
        
        var clonedData = CloneStorageData(data);
        clonedData.LastUpdated = DateTime.UtcNow;
        
        _storage.AddOrUpdate(key, clonedData, (k, existing) => clonedData);
        
        // Update statistics
        Interlocked.Increment(ref _totalRequests);
        UpdateTenantStats(key, 1);
        
        _logger.LogTrace("Stored rate limit data for key {Key} with {TimestampCount} timestamps", 
            key, data.Timestamps.Count);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        if (_storage.TryRemove(key, out var removed))
        {
            UpdateTenantStats(key, -1);
            _logger.LogTrace("Removed rate limit data for key {Key}", key);
        }
    }

    public async Task<IEnumerable<RateLimitStorageData>> GetByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        
        var results = new List<RateLimitStorageData>();
        var now = DateTime.UtcNow;
        
        foreach (var kvp in _storage)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (kvp.Value.ExpiresAt > now)
                {
                    results.Add(CloneStorageData(kvp.Value));
                }
                else
                {
                    // Remove expired entry during scan
                    _storage.TryRemove(kvp.Key, out _);
                }
            }
        }
        
        _logger.LogTrace("Found {Count} entries with prefix {Prefix}", results.Count, prefix);
        return results;
    }

    public async Task CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        if (!await _cleanupSemaphore.WaitAsync(100, cancellationToken))
        {
            _logger.LogTrace("Cleanup already in progress, skipping");
            return;
        }
        
        try
        {
            var now = DateTime.UtcNow;
            var expiredKeys = new List<string>();
            
            foreach (var kvp in _storage)
            {
                if (kvp.Value.ExpiresAt <= now)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
            
            foreach (var key in expiredKeys)
            {
                if (_storage.TryRemove(key, out _))
                {
                    UpdateTenantStats(key, -1);
                }
            }
            
            _lastCleanup = now;
            
            if (expiredKeys.Any())
            {
                _logger.LogDebug("Cleaned up {Count} expired rate limit entries", expiredKeys.Count);
            }
            else
            {
                _logger.LogTrace("No expired entries found during cleanup");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rate limit storage cleanup");
        }
        finally
        {
            _cleanupSemaphore.Release();
        }
    }

    public async Task<RateLimitStorageStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var totalEntries = _storage.Count;
        var expiredEntries = _storage.Values.Count(v => v.ExpiresAt <= now);
        
        return new RateLimitStorageStats
        {
            TotalEntries = totalEntries,
            ExpiredEntries = expiredEntries,
            TotalRequests = _totalRequests,
            LastCleanup = _lastCleanup,
            AverageResponseTime = TimeSpan.FromMilliseconds(1), // In-memory is fast
            TenantCounts = new Dictionary<string, long>(_tenantRequestCounts)
        };
    }

    private RateLimitStorageData CloneStorageData(RateLimitStorageData original)
    {
        return new RateLimitStorageData
        {
            Key = original.Key,
            Timestamps = new List<DateTime>(original.Timestamps),
            LastUpdated = original.LastUpdated,
            ExpiresAt = original.ExpiresAt,
            Metadata = new Dictionary<string, object>(original.Metadata)
        };
    }

    private void UpdateTenantStats(string key, int delta)
    {
        // Extract tenant ID from composite key (format: "tenantId:identifier")
        var colonIndex = key.IndexOf(':');
        if (colonIndex > 0)
        {
            var tenantId = key[..colonIndex];
            _tenantRequestCounts.AddOrUpdate(tenantId, 
                delta, 
                (k, existing) => Math.Max(0, existing + delta));
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _cleanupSemaphore?.Dispose();
        
        _logger.LogInformation("Disposed in-memory rate limit storage with {Count} entries", _storage.Count);
    }
}