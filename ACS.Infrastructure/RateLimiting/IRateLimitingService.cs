using System.Net;

namespace ACS.Infrastructure.RateLimiting;

/// <summary>
/// Core rate limiting service interface with tenant isolation
/// </summary>
public interface IRateLimitingService
{
    /// <summary>
    /// Check if a request should be allowed for the given tenant and key
    /// </summary>
    Task<RateLimitResult> CheckRateLimitAsync(string tenantId, string key, RateLimitPolicy policy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get current rate limit status for a tenant and key
    /// </summary>
    Task<RateLimitStatus> GetRateLimitStatusAsync(string tenantId, string key, RateLimitPolicy policy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reset rate limit for a specific tenant and key (admin operation)
    /// </summary>
    Task ResetRateLimitAsync(string tenantId, string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all active rate limit entries for a tenant (monitoring)
    /// </summary>
    Task<IEnumerable<RateLimitEntry>> GetActiveLimitsAsync(string tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Rate limiting policy configuration
/// </summary>
public class RateLimitPolicy
{
    /// <summary>
    /// Maximum number of requests allowed in the time window
    /// </summary>
    public int RequestLimit { get; set; } = 100;
    
    /// <summary>
    /// Time window duration in seconds
    /// </summary>
    public int WindowSizeSeconds { get; set; } = 60;
    
    /// <summary>
    /// Algorithm to use for rate limiting
    /// </summary>
    public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.SlidingWindow;
    
    /// <summary>
    /// Whether to use distributed storage (Redis) or in-memory
    /// </summary>
    public bool UseDistributedStorage { get; set; } = true;
    
    /// <summary>
    /// Policy name for identification and logging
    /// </summary>
    public string PolicyName { get; set; } = "default";
    
    /// <summary>
    /// Priority level for this policy (higher = more important)
    /// </summary>
    public int Priority { get; set; } = 1;
    
    /// <summary>
    /// Custom headers to include in rate limit responses
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}

/// <summary>
/// Rate limiting algorithms supported
/// </summary>
public enum RateLimitAlgorithm
{
    /// <summary>
    /// Fixed window algorithm - simple but less accurate
    /// </summary>
    FixedWindow,
    
    /// <summary>
    /// Sliding window algorithm - more accurate but more complex
    /// </summary>
    SlidingWindow,
    
    /// <summary>
    /// Token bucket algorithm - allows bursts
    /// </summary>
    TokenBucket,
    
    /// <summary>
    /// Leaky bucket algorithm - smooth rate limiting
    /// </summary>
    LeakyBucket
}

/// <summary>
/// Result of a rate limit check
/// </summary>
public class RateLimitResult
{
    /// <summary>
    /// Whether the request is allowed
    /// </summary>
    public bool IsAllowed { get; set; }
    
    /// <summary>
    /// Number of requests remaining in current window
    /// </summary>
    public int RemainingRequests { get; set; }
    
    /// <summary>
    /// Total request limit for the policy
    /// </summary>
    public int RequestLimit { get; set; }
    
    /// <summary>
    /// Time until rate limit resets (in seconds)
    /// </summary>
    public int ResetTimeSeconds { get; set; }
    
    /// <summary>
    /// Time until next request is allowed (if blocked)
    /// </summary>
    public TimeSpan? RetryAfter { get; set; }
    
    /// <summary>
    /// HTTP status code to return
    /// </summary>
    public HttpStatusCode StatusCode => IsAllowed ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests;
    
    /// <summary>
    /// Policy that was applied
    /// </summary>
    public string PolicyName { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional metadata about the rate limit check
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Current status of rate limiting for a key
/// </summary>
public class RateLimitStatus
{
    public string TenantId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int RequestLimit { get; set; }
    public DateTime WindowStartTime { get; set; }
    public DateTime WindowEndTime { get; set; }
    public RateLimitAlgorithm Algorithm { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    
    public int RemainingRequests => Math.Max(0, RequestLimit - RequestCount);
    public bool IsNearLimit => RemainingRequests <= (RequestLimit * 0.1); // 10% threshold
}

/// <summary>
/// Active rate limit entry for monitoring
/// </summary>
public class RateLimitEntry
{
    public string TenantId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int RequestLimit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public RateLimitAlgorithm Algorithm { get; set; }
    public string ClientInfo { get; set; } = string.Empty;
}