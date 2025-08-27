using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// Rate Limiting Commands
public class ResetRateLimitCommand : ICommand
{
    public string TenantId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class TestRateLimitCommand : ICommand<RateLimitTestResult>
{
    public string TenantId { get; set; } = string.Empty;
    public int RequestLimit { get; set; } = 10;
    public int WindowSizeSeconds { get; set; } = 60;
    public int NumberOfRequests { get; set; } = 5;
    public int DelayBetweenRequests { get; set; } = 0;
}

// Rate Limiting Queries
public class GetRateLimitStatusQuery : IQuery<RateLimitStatus>
{
    public string TenantId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public RateLimitPolicy Policy { get; set; } = new();
}

public class GetActiveLimitsQuery : IQuery<List<ActiveRateLimit>>
{
    public string TenantId { get; set; } = string.Empty;
}

public class GetRateLimitMetricsQuery : IQuery<RateLimitMetrics>
{
    public string TenantId { get; set; } = string.Empty;
}

public class GetAggregatedRateLimitMetricsQuery : IQuery<AggregatedRateLimitMetrics>
{
}

public class GetRateLimitHealthStatusQuery : IQuery<RateLimitHealthStatus>
{
}

// Result Types
public class RateLimitStatus
{
    public string TenantId { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public int RequestLimit { get; set; }
    public long RemainingRequests { get; set; }
    public DateTime WindowStartTime { get; set; }
    public DateTime WindowEndTime { get; set; }
    public RateLimitAlgorithm Algorithm { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public bool IsNearLimit { get; set; }
}

public class ActiveRateLimit
{
    public string Key { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public int RequestLimit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public RateLimitAlgorithm Algorithm { get; set; }
    public string ClientInfo { get; set; } = string.Empty;
}

public class RateLimitMetrics
{
    public string TenantId { get; set; } = string.Empty;
    public long TotalRequests { get; set; }
    public long RequestsAllowed { get; set; }
    public long RequestsBlocked { get; set; }
    public double BlockRate { get; set; }
    public double AverageRemainingRequests { get; set; }
    public DateTime? LastActivity { get; set; }
}

public class AggregatedRateLimitMetrics
{
    public long TotalTenants { get; set; }
    public long TotalRequests { get; set; }
    public long TotalBlocked { get; set; }
    public double OverallBlockRate { get; set; }
    public Dictionary<string, long> RequestsByTenant { get; set; } = new();
    public Dictionary<string, double> BlockRatesByTenant { get; set; } = new();
}

public class RateLimitHealthStatus
{
    public bool IsHealthy { get; set; }
    public DateTime LastCheck { get; set; }
    public TimeSpan StorageResponseTime { get; set; }
    public long TotalActiveEntries { get; set; }
    public long ExpiredEntries { get; set; }
    public double BlockRate { get; set; }
    public int ActiveTenants { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class RateLimitTestResult
{
    public RateLimitTestConfiguration TestConfiguration { get; set; } = new();
    public List<RateLimitTestRequestResult> Results { get; set; } = new();
}

public class RateLimitTestConfiguration
{
    public int RequestLimit { get; set; }
    public int WindowSizeSeconds { get; set; }
    public int NumberOfRequests { get; set; }
}

public class RateLimitTestRequestResult
{
    public int RequestNumber { get; set; }
    public bool IsAllowed { get; set; }
    public long Remaining { get; set; }
    public double ResetTime { get; set; }
}

public class RateLimitPolicy
{
    public int RequestLimit { get; set; }
    public int WindowSizeSeconds { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.SlidingWindow;
}

public enum RateLimitAlgorithm
{
    SlidingWindow = 0,
    FixedWindow = 1,
    TokenBucket = 2
}