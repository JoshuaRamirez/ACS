namespace ACS.Infrastructure.RateLimiting;

/// <summary>
/// Configuration for rate limiting middleware and services
/// </summary>
public class RateLimitingConfiguration
{
    /// <summary>
    /// Whether rate limiting is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Strategy for generating rate limit keys
    /// </summary>
    public RateLimitKeyStrategy KeyStrategy { get; set; } = RateLimitKeyStrategy.Combined;

    /// <summary>
    /// Default rate limiting policy
    /// </summary>
    public RateLimitPolicyConfig DefaultPolicy { get; set; } = new()
    {
        RequestLimit = 100,
        WindowSizeSeconds = 60,
        PolicyName = "default"
    };

    /// <summary>
    /// Tenant-specific rate limiting policies
    /// </summary>
    public Dictionary<string, RateLimitPolicyConfig> TenantPolicies { get; set; } = new();

    /// <summary>
    /// Endpoint-specific rate limiting policies
    /// </summary>
    public List<EndpointRateLimitPolicy> EndpointPolicies { get; set; } = new();

    /// <summary>
    /// Paths to exclude from rate limiting
    /// </summary>
    public List<string> ExcludePaths { get; set; } = new()
    {
        "/health",
        "/metrics",
        "/swagger"
    };

    /// <summary>
    /// Storage configuration
    /// </summary>
    public RateLimitStorageConfig Storage { get; set; } = new();
}

/// <summary>
/// Rate limit key generation strategies
/// </summary>
public enum RateLimitKeyStrategy
{
    /// <summary>
    /// Use client IP address only
    /// </summary>
    IpAddress,

    /// <summary>
    /// Use authenticated user ID only
    /// </summary>
    User,

    /// <summary>
    /// Use user ID and endpoint combination
    /// </summary>
    UserAndEndpoint,

    /// <summary>
    /// Use API key for identification
    /// </summary>
    ApiKey,

    /// <summary>
    /// Combine IP, user, and endpoint (most restrictive)
    /// </summary>
    Combined
}

/// <summary>
/// Rate limiting policy configuration
/// </summary>
public class RateLimitPolicyConfig
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
    /// Whether to use distributed storage
    /// </summary>
    public bool UseDistributedStorage { get; set; } = true;

    /// <summary>
    /// Policy name for identification
    /// </summary>
    public string PolicyName { get; set; } = "default";

    /// <summary>
    /// Priority level for this policy
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Custom headers to include in responses
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}

/// <summary>
/// Endpoint-specific rate limiting policy
/// </summary>
public class EndpointRateLimitPolicy : RateLimitPolicyConfig
{
    /// <summary>
    /// Path pattern to match (supports wildcards)
    /// </summary>
    public string PathPattern { get; set; } = "*";

    /// <summary>
    /// HTTP methods this policy applies to
    /// </summary>
    public List<string> HttpMethods { get; set; } = new() { "*" };

    /// <summary>
    /// Description of this endpoint policy
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Storage configuration for rate limiting
/// </summary>
public class RateLimitStorageConfig
{
    /// <summary>
    /// Storage type to use
    /// </summary>
    public RateLimitStorageType Type { get; set; } = RateLimitStorageType.InMemory;

    /// <summary>
    /// Connection string for distributed storage
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Key prefix for storage entries
    /// </summary>
    public string KeyPrefix { get; set; } = "rl:";

    /// <summary>
    /// How often to run cleanup of expired entries (minutes)
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum number of entries to keep in memory cache
    /// </summary>
    public int MaxCacheEntries { get; set; } = 10000;
}

/// <summary>
/// Available storage types for rate limiting
/// </summary>
public enum RateLimitStorageType
{
    /// <summary>
    /// In-memory storage (single instance)
    /// </summary>
    InMemory,

    /// <summary>
    /// Redis distributed storage
    /// </summary>
    Redis,

    /// <summary>
    /// SQL Server storage
    /// </summary>
    SqlServer
}