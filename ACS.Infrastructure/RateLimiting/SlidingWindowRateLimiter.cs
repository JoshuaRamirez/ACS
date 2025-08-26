using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ACS.Infrastructure.RateLimiting;

/// <summary>
/// Sliding window rate limiter implementation with tenant isolation
/// Provides more accurate rate limiting compared to fixed windows
/// </summary>
public class SlidingWindowRateLimiter : IRateLimitingService
{
    private readonly ILogger<SlidingWindowRateLimiter> _logger;
    private readonly IRateLimitStorage _storage;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    // In-memory cache for frequently accessed rate limit data
    private readonly ConcurrentDictionary<string, CachedRateLimitData> _cache = new();
    private readonly Timer _cleanupTimer;

    public SlidingWindowRateLimiter(
        ILogger<SlidingWindowRateLimiter> logger,
        IRateLimitStorage storage)
    {
        _logger = logger;
        _storage = storage;
        
        // Setup periodic cleanup of expired cache entries
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(
        string tenantId, 
        string key, 
        RateLimitPolicy policy, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(policy);

        var compositeKey = GetCompositeKey(tenantId, key);
        var now = DateTime.UtcNow;
        
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            // Get current window data
            var windowData = await GetWindowDataAsync(compositeKey, policy, now, cancellationToken);
            
            // Apply sliding window algorithm
            var result = await ApplySlidingWindowAsync(windowData, policy, now, cancellationToken);
            
            // Update storage if request is allowed
            if (result.IsAllowed)
            {
                await UpdateStorageAsync(compositeKey, windowData, now, cancellationToken);
            }
            
            // Update cache
            UpdateCache(compositeKey, windowData, policy);
            
            _logger.LogDebug("Rate limit check for {TenantId}:{Key} - Allowed: {IsAllowed}, Remaining: {Remaining}",
                tenantId, key, result.IsAllowed, result.RemainingRequests);
                
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for {TenantId}:{Key}", tenantId, key);
            
            // On error, allow the request (fail open) but log the issue
            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingRequests = policy.RequestLimit,
                RequestLimit = policy.RequestLimit,
                ResetTimeSeconds = policy.WindowSizeSeconds,
                PolicyName = policy.PolicyName,
                Metadata = { ["error"] = "rate_limit_check_failed" }
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<RateLimitStatus> GetRateLimitStatusAsync(
        string tenantId, 
        string key, 
        RateLimitPolicy policy, 
        CancellationToken cancellationToken = default)
    {
        var compositeKey = GetCompositeKey(tenantId, key);
        var now = DateTime.UtcNow;
        
        var windowData = await GetWindowDataAsync(compositeKey, policy, now, cancellationToken);
        var activeRequests = CountActiveRequests(windowData.Timestamps, policy, now);
        
        return new RateLimitStatus
        {
            TenantId = tenantId,
            Key = key,
            RequestCount = activeRequests,
            RequestLimit = policy.RequestLimit,
            WindowStartTime = now.AddSeconds(-policy.WindowSizeSeconds),
            WindowEndTime = now,
            Algorithm = RateLimitAlgorithm.SlidingWindow,
            PolicyName = policy.PolicyName
        };
    }

    public async Task ResetRateLimitAsync(string tenantId, string key, CancellationToken cancellationToken = default)
    {
        var compositeKey = GetCompositeKey(tenantId, key);
        
        await _storage.RemoveAsync(compositeKey, cancellationToken);
        _cache.TryRemove(compositeKey, out _);
        
        _logger.LogInformation("Reset rate limit for {TenantId}:{Key}", tenantId, key);
    }

    public async Task<IEnumerable<RateLimitEntry>> GetActiveLimitsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var entries = new List<RateLimitEntry>();
        var now = DateTime.UtcNow;
        
        // Get all active entries for the tenant from storage
        var tenantPrefix = GetTenantPrefix(tenantId);
        var storageEntries = await _storage.GetByPrefixAsync(tenantPrefix, cancellationToken);
        
        foreach (var entry in storageEntries)
        {
            if (TryParseCompositeKey(entry.Key, out var parsedTenantId, out var parsedKey) &&
                parsedTenantId == tenantId)
            {
                var requestCount = CountActiveRequests(entry.Timestamps, GetDefaultPolicy(), now);
                
                if (requestCount > 0)
                {
                    entries.Add(new RateLimitEntry
                    {
                        TenantId = parsedTenantId,
                        Key = parsedKey,
                        RequestCount = requestCount,
                        RequestLimit = GetDefaultPolicy().RequestLimit,
                        CreatedAt = entry.Timestamps.Min(),
                        ExpiresAt = entry.Timestamps.Max().AddSeconds(GetDefaultPolicy().WindowSizeSeconds),
                        PolicyName = GetDefaultPolicy().PolicyName,
                        Algorithm = RateLimitAlgorithm.SlidingWindow
                    });
                }
            }
        }
        
        return entries;
    }

    private async Task<WindowData> GetWindowDataAsync(
        string compositeKey, 
        RateLimitPolicy policy, 
        DateTime now, 
        CancellationToken cancellationToken)
    {
        // Try cache first
        if (_cache.TryGetValue(compositeKey, out var cached) && 
            cached.ExpiresAt > now)
        {
            return cached.WindowData;
        }
        
        // Load from storage
        var storageData = await _storage.GetAsync(compositeKey, cancellationToken);
        return new WindowData
        {
            Key = compositeKey,
            Timestamps = storageData?.Timestamps ?? new List<DateTime>(),
            LastUpdated = storageData?.LastUpdated ?? now
        };
    }

    private Task<RateLimitResult> ApplySlidingWindowAsync(
        WindowData windowData, 
        RateLimitPolicy policy, 
        DateTime now, 
        CancellationToken cancellationToken)
    {
        // Remove timestamps outside the sliding window
        var windowStart = now.AddSeconds(-policy.WindowSizeSeconds);
        var activeTimestamps = windowData.Timestamps
            .Where(t => t >= windowStart)
            .ToList();
        
        windowData.Timestamps = activeTimestamps;
        
        var currentRequestCount = activeTimestamps.Count;
        var isAllowed = currentRequestCount < policy.RequestLimit;
        
        if (isAllowed)
        {
            // Add current request timestamp
            windowData.Timestamps.Add(now);
            windowData.LastUpdated = now;
        }
        
        var remainingRequests = Math.Max(0, policy.RequestLimit - currentRequestCount - (isAllowed ? 1 : 0));
        var resetTime = CalculateResetTime(activeTimestamps, policy, now);
        
        var result = new RateLimitResult
        {
            IsAllowed = isAllowed,
            RemainingRequests = remainingRequests,
            RequestLimit = policy.RequestLimit,
            ResetTimeSeconds = resetTime,
            RetryAfter = isAllowed ? null : CalculateRetryAfter(activeTimestamps, policy, now),
            PolicyName = policy.PolicyName,
            Metadata = 
            {
                ["algorithm"] = "sliding_window",
                ["window_start"] = windowStart,
                ["active_requests"] = currentRequestCount
            }
        };
        
        return Task.FromResult(result);
    }

    private async Task UpdateStorageAsync(
        string compositeKey, 
        WindowData windowData, 
        DateTime now, 
        CancellationToken cancellationToken)
    {
        var storageData = new RateLimitStorageData
        {
            Key = compositeKey,
            Timestamps = windowData.Timestamps,
            LastUpdated = now,
            ExpiresAt = now.AddSeconds(GetDefaultPolicy().WindowSizeSeconds * 2) // Keep data a bit longer for cleanup
        };
        
        await _storage.SetAsync(compositeKey, storageData, cancellationToken);
    }

    private void UpdateCache(string compositeKey, WindowData windowData, RateLimitPolicy policy)
    {
        var cached = new CachedRateLimitData
        {
            WindowData = windowData,
            ExpiresAt = DateTime.UtcNow.AddSeconds(30) // Cache for 30 seconds
        };
        
        _cache.AddOrUpdate(compositeKey, cached, (key, existing) => cached);
    }

    private int CountActiveRequests(List<DateTime> timestamps, RateLimitPolicy policy, DateTime now)
    {
        var windowStart = now.AddSeconds(-policy.WindowSizeSeconds);
        return timestamps.Count(t => t >= windowStart);
    }

    private int CalculateResetTime(List<DateTime> activeTimestamps, RateLimitPolicy policy, DateTime now)
    {
        if (!activeTimestamps.Any())
            return policy.WindowSizeSeconds;
        
        var oldestRequest = activeTimestamps.Min();
        var resetTime = oldestRequest.AddSeconds(policy.WindowSizeSeconds);
        return Math.Max(0, (int)(resetTime - now).TotalSeconds);
    }

    private TimeSpan? CalculateRetryAfter(List<DateTime> activeTimestamps, RateLimitPolicy policy, DateTime now)
    {
        if (!activeTimestamps.Any())
            return TimeSpan.Zero;
        
        // Calculate when the oldest request will expire
        var oldestRequest = activeTimestamps.Min();
        var retryTime = oldestRequest.AddSeconds(policy.WindowSizeSeconds);
        var retryAfter = retryTime - now;
        
        return retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero;
    }

    private void CleanupExpiredEntries(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
        
        if (expiredKeys.Any())
        {
            _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
        }
    }

    private string GetCompositeKey(string tenantId, string key) => $"{tenantId}:{key}";
    private string GetTenantPrefix(string tenantId) => $"{tenantId}:";
    
    private bool TryParseCompositeKey(string compositeKey, out string tenantId, out string key)
    {
        var parts = compositeKey.Split(':', 2);
        if (parts.Length == 2)
        {
            tenantId = parts[0];
            key = parts[1];
            return true;
        }
        
        tenantId = string.Empty;
        key = string.Empty;
        return false;
    }
    
    private RateLimitPolicy GetDefaultPolicy() => new()
    {
        RequestLimit = 100,
        WindowSizeSeconds = 60,
        PolicyName = "default"
    };

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _semaphore?.Dispose();
    }
}

/// <summary>
/// Window data for sliding window algorithm
/// </summary>
internal class WindowData
{
    public string Key { get; set; } = string.Empty;
    public List<DateTime> Timestamps { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Cached rate limit data with expiration
/// </summary>
internal class CachedRateLimitData
{
    public WindowData WindowData { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}