using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace ACS.Infrastructure.RateLimiting;

/// <summary>
/// Service for collecting and exposing rate limiting metrics
/// Integrates with OpenTelemetry for distributed observability
/// </summary>
public class RateLimitingMetricsService : IDisposable
{
    private readonly ILogger<RateLimitingMetricsService> _logger;
    private readonly Meter _meter;
    
    // OpenTelemetry instruments
    private readonly Counter<long> _requestsAllowedCounter;
    private readonly Counter<long> _requestsBlockedCounter;
    private readonly Histogram<double> _rateLimitCheckDuration;
    private readonly ObservableGauge<long> _activeLimitsGauge;
    private readonly Counter<long> _rateLimitResetCounter;
    private readonly Histogram<int> _remainingRequestsHistogram;
    
    // Internal metrics storage
    private readonly ConcurrentDictionary<string, TenantRateLimitMetrics> _tenantMetrics = new();
    private readonly ConcurrentDictionary<string, PolicyMetrics> _policyMetrics = new();

    public RateLimitingMetricsService(ILogger<RateLimitingMetricsService> logger)
    {
        _logger = logger;
        _meter = new Meter("ACS.RateLimiting", "1.0.0");
        
        // Initialize OpenTelemetry instruments
        _requestsAllowedCounter = _meter.CreateCounter<long>(
            "acs_rate_limit_requests_allowed_total",
            description: "Total number of requests allowed by rate limiting");
            
        _requestsBlockedCounter = _meter.CreateCounter<long>(
            "acs_rate_limit_requests_blocked_total", 
            description: "Total number of requests blocked by rate limiting");
            
        _rateLimitCheckDuration = _meter.CreateHistogram<double>(
            "acs_rate_limit_check_duration_seconds",
            description: "Duration of rate limit checks in seconds");
            
        _activeLimitsGauge = _meter.CreateObservableGauge<long>(
            "acs_rate_limit_active_limits",
            description: "Number of active rate limits by tenant",
            observeValues: ObserveActiveLimits);
            
        _rateLimitResetCounter = _meter.CreateCounter<long>(
            "acs_rate_limit_resets_total",
            description: "Total number of rate limit resets");
            
        _remainingRequestsHistogram = _meter.CreateHistogram<int>(
            "acs_rate_limit_remaining_requests",
            description: "Distribution of remaining requests in rate limit windows");
    }

    /// <summary>
    /// Record a rate limit check result
    /// </summary>
    public void RecordRateLimitCheck(
        string tenantId, 
        string policyName, 
        RateLimitResult result, 
        TimeSpan duration)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("tenant_id", tenantId),
            new("policy", policyName),
            new("allowed", result.IsAllowed.ToString().ToLower())
        };

        // Record OpenTelemetry metrics
        if (result.IsAllowed)
        {
            _requestsAllowedCounter.Add(1, tags);
        }
        else
        {
            _requestsBlockedCounter.Add(1, tags);
        }
        
        _rateLimitCheckDuration.Record(duration.TotalSeconds, tags);
        _remainingRequestsHistogram.Record(result.RemainingRequests, tags);

        // Update internal metrics
        UpdateTenantMetrics(tenantId, result);
        UpdatePolicyMetrics(policyName, result);

        _logger.LogTrace("Recorded rate limit check for tenant {TenantId}, policy {Policy}, allowed: {Allowed}",
            tenantId, policyName, result.IsAllowed);
    }

    /// <summary>
    /// Record a rate limit reset operation
    /// </summary>
    public void RecordRateLimitReset(string tenantId, string key, string reason = "manual")
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("tenant_id", tenantId),
            new("reason", reason)
        };

        _rateLimitResetCounter.Add(1, tags);
        
        _logger.LogInformation("Recorded rate limit reset for tenant {TenantId}, key {Key}, reason: {Reason}",
            tenantId, key, reason);
    }

    /// <summary>
    /// Get current metrics for a specific tenant
    /// </summary>
    public TenantRateLimitMetrics GetTenantMetrics(string tenantId)
    {
        return _tenantMetrics.GetOrAdd(tenantId, _ => new TenantRateLimitMetrics
        {
            TenantId = tenantId
        });
    }

    /// <summary>
    /// Get current metrics for a specific policy
    /// </summary>
    public PolicyMetrics GetPolicyMetrics(string policyName)
    {
        return _policyMetrics.GetOrAdd(policyName, _ => new PolicyMetrics
        {
            PolicyName = policyName
        });
    }

    /// <summary>
    /// Get aggregated metrics across all tenants and policies
    /// </summary>
    public AggregatedRateLimitMetrics GetAggregatedMetrics()
    {
        var totalAllowed = _tenantMetrics.Values.Sum(m => m.RequestsAllowed);
        var totalBlocked = _tenantMetrics.Values.Sum(m => m.RequestsBlocked);
        var totalRequests = totalAllowed + totalBlocked;
        
        return new AggregatedRateLimitMetrics
        {
            TotalRequests = totalRequests,
            TotalAllowed = totalAllowed,
            TotalBlocked = totalBlocked,
            BlockRate = totalRequests > 0 ? (double)totalBlocked / totalRequests : 0,
            ActiveTenants = _tenantMetrics.Count,
            ActivePolicies = _policyMetrics.Count,
            TopBlockedTenants = _tenantMetrics.Values
                .OrderByDescending(m => m.RequestsBlocked)
                .Take(10)
                .Select(m => new { m.TenantId, m.RequestsBlocked })
                .ToList(),
            TopActivePolicies = _policyMetrics.Values
                .OrderByDescending(m => m.TotalRequests)
                .Take(10)
                .Select(m => new { m.PolicyName, m.TotalRequests })
                .ToList()
        };
    }

    /// <summary>
    /// Clear metrics for a specific tenant (cleanup operation)
    /// </summary>
    public void ClearTenantMetrics(string tenantId)
    {
        _tenantMetrics.TryRemove(tenantId, out _);
        _logger.LogDebug("Cleared metrics for tenant {TenantId}", tenantId);
    }

    /// <summary>
    /// Reset all metrics (admin operation)
    /// </summary>
    public void ResetAllMetrics()
    {
        _tenantMetrics.Clear();
        _policyMetrics.Clear();
        _logger.LogInformation("Reset all rate limiting metrics");
    }

    private void UpdateTenantMetrics(string tenantId, RateLimitResult result)
    {
        var metrics = GetTenantMetrics(tenantId);
        
        lock (metrics)
        {
            if (result.IsAllowed)
            {
                metrics.RequestsAllowed++;
            }
            else
            {
                metrics.RequestsBlocked++;
            }
            
            metrics.LastActivity = DateTime.UtcNow;
            
            // Update average remaining requests (exponential moving average)
            var alpha = 0.1; // Smoothing factor
            if (metrics.AverageRemainingRequests == 0)
            {
                metrics.AverageRemainingRequests = result.RemainingRequests;
            }
            else
            {
                metrics.AverageRemainingRequests = 
                    alpha * result.RemainingRequests + (1 - alpha) * metrics.AverageRemainingRequests;
            }
        }
    }

    private void UpdatePolicyMetrics(string policyName, RateLimitResult result)
    {
        var metrics = GetPolicyMetrics(policyName);
        
        lock (metrics)
        {
            metrics.TotalRequests++;
            
            if (result.IsAllowed)
            {
                metrics.RequestsAllowed++;
            }
            else
            {
                metrics.RequestsBlocked++;
            }
            
            metrics.LastActivity = DateTime.UtcNow;
        }
    }

    private IEnumerable<Measurement<long>> ObserveActiveLimits()
    {
        foreach (var tenantMetrics in _tenantMetrics.Values)
        {
            yield return new Measurement<long>(
                1, // Each tenant has at least one active limit
                new KeyValuePair<string, object?>("tenant_id", tenantMetrics.TenantId));
        }
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}

/// <summary>
/// Metrics for a specific tenant
/// </summary>
public class TenantRateLimitMetrics
{
    public string TenantId { get; set; } = string.Empty;
    public long RequestsAllowed { get; set; }
    public long RequestsBlocked { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public double AverageRemainingRequests { get; set; }
    
    public long TotalRequests => RequestsAllowed + RequestsBlocked;
    public double BlockRate => TotalRequests > 0 ? (double)RequestsBlocked / TotalRequests : 0;
}

/// <summary>
/// Metrics for a specific policy
/// </summary>
public class PolicyMetrics
{
    public string PolicyName { get; set; } = string.Empty;
    public long TotalRequests { get; set; }
    public long RequestsAllowed { get; set; }
    public long RequestsBlocked { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    
    public double BlockRate => TotalRequests > 0 ? (double)RequestsBlocked / TotalRequests : 0;
}

/// <summary>
/// Aggregated metrics across all tenants and policies
/// </summary>
public class AggregatedRateLimitMetrics
{
    public long TotalRequests { get; set; }
    public long TotalAllowed { get; set; }
    public long TotalBlocked { get; set; }
    public double BlockRate { get; set; }
    public int ActiveTenants { get; set; }
    public int ActivePolicies { get; set; }
    public object TopBlockedTenants { get; set; } = new();
    public object TopActivePolicies { get; set; } = new();
}