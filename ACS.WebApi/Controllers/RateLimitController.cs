using ACS.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for rate limiting monitoring and management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for rate limiting management
public class RateLimitController : ControllerBase
{
    private readonly IRateLimitingService _rateLimitingService;
    private readonly RateLimitingMetricsService _metricsService;
    private readonly RateLimitingMonitoringService _monitoringService;
    private readonly ILogger<RateLimitController> _logger;

    public RateLimitController(
        IRateLimitingService rateLimitingService,
        RateLimitingMetricsService metricsService,
        RateLimitingMonitoringService monitoringService,
        ILogger<RateLimitController> logger)
    {
        _rateLimitingService = rateLimitingService;
        _metricsService = metricsService;
        _monitoringService = monitoringService;
        _logger = logger;
    }

    /// <summary>
    /// Get current rate limit status for the requesting tenant
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetCurrentStatus()
    {
        try
        {
            var tenantId = GetTenantIdFromContext();
            var userId = User.Identity?.Name ?? "anonymous";
            var key = $"{tenantId}:{userId}";
            
            var policy = new RateLimitPolicy
            {
                RequestLimit = 100,
                WindowSizeSeconds = 60,
                PolicyName = "status_check"
            };
            
            var status = await _rateLimitingService.GetRateLimitStatusAsync(tenantId, key, policy);
            
            return Ok(new
            {
                tenantId = status.TenantId,
                requestCount = status.RequestCount,
                requestLimit = status.RequestLimit,
                remainingRequests = status.RemainingRequests,
                windowStart = status.WindowStartTime,
                windowEnd = status.WindowEndTime,
                algorithm = status.Algorithm.ToString(),
                policy = status.PolicyName,
                isNearLimit = status.IsNearLimit
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limit status");
            return StatusCode(500, new { error = "Failed to get rate limit status" });
        }
    }

    /// <summary>
    /// Get active rate limits for the current tenant
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveLimits()
    {
        try
        {
            var tenantId = GetTenantIdFromContext();
            var activeLimits = await _rateLimitingService.GetActiveLimitsAsync(tenantId);
            
            return Ok(activeLimits.Select(limit => new
            {
                key = limit.Key,
                requestCount = limit.RequestCount,
                requestLimit = limit.RequestLimit,
                createdAt = limit.CreatedAt,
                expiresAt = limit.ExpiresAt,
                policy = limit.PolicyName,
                algorithm = limit.Algorithm.ToString(),
                clientInfo = limit.ClientInfo
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active rate limits");
            return StatusCode(500, new { error = "Failed to get active rate limits" });
        }
    }

    /// <summary>
    /// Get rate limiting metrics for the current tenant
    /// </summary>
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        try
        {
            var tenantId = GetTenantIdFromContext();
            var tenantMetrics = _metricsService.GetTenantMetrics(tenantId);
            
            return Ok(new
            {
                tenantId = tenantMetrics.TenantId,
                totalRequests = tenantMetrics.TotalRequests,
                requestsAllowed = tenantMetrics.RequestsAllowed,
                requestsBlocked = tenantMetrics.RequestsBlocked,
                blockRate = tenantMetrics.BlockRate,
                averageRemainingRequests = tenantMetrics.AverageRemainingRequests,
                lastActivity = tenantMetrics.LastActivity
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limit metrics");
            return StatusCode(500, new { error = "Failed to get rate limit metrics" });
        }
    }

    /// <summary>
    /// Get aggregated rate limiting metrics (admin only)
    /// </summary>
    [HttpGet("metrics/aggregated")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetAggregatedMetrics()
    {
        try
        {
            var metrics = _metricsService.GetAggregatedMetrics();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting aggregated rate limit metrics");
            return StatusCode(500, new { error = "Failed to get aggregated metrics" });
        }
    }

    /// <summary>
    /// Get rate limiting system health status (admin only)
    /// </summary>
    [HttpGet("health")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetHealthStatus()
    {
        try
        {
            var health = await _monitoringService.GetHealthStatusAsync();
            
            var statusCode = health.IsHealthy ? 200 : 503;
            return StatusCode(statusCode, new
            {
                isHealthy = health.IsHealthy,
                lastCheck = health.LastCheck,
                storageResponseTime = health.StorageResponseTime.TotalMilliseconds,
                totalActiveEntries = health.TotalActiveEntries,
                expiredEntries = health.ExpiredEntries,
                blockRate = health.BlockRate,
                activeTenants = health.ActiveTenants,
                issues = health.Issues
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limiting health status");
            return StatusCode(500, new { error = "Failed to get health status" });
        }
    }

    /// <summary>
    /// Reset rate limit for the current user (emergency use)
    /// </summary>
    [HttpPost("reset")]
    [Authorize]
    public async Task<IActionResult> ResetRateLimit([FromBody] ResetRateLimitRequest request)
    {
        try
        {
            var tenantId = GetTenantIdFromContext();
            var userId = User.Identity?.Name ?? "anonymous";
            
            // Only allow users to reset their own limits or admins to reset any
            if (!User.IsInRole("Admin") && request.UserId != userId)
            {
                return Forbid("You can only reset your own rate limits");
            }
            
            var key = $"{tenantId}:{request.UserId ?? userId}";
            await _rateLimitingService.ResetRateLimitAsync(tenantId, key);
            
            _metricsService.RecordRateLimitReset(tenantId, key, "manual_reset");
            
            _logger.LogInformation("Rate limit reset for tenant {TenantId}, user {UserId} by {RequestedBy}",
                tenantId, request.UserId ?? userId, userId);
            
            return Ok(new { message = "Rate limit reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting rate limit");
            return StatusCode(500, new { error = "Failed to reset rate limit" });
        }
    }

    /// <summary>
    /// Test rate limiting (useful for debugging)
    /// </summary>
    [HttpPost("test")]
    [Authorize]
    public async Task<IActionResult> TestRateLimit([FromBody] TestRateLimitRequest request)
    {
        try
        {
            var tenantId = GetTenantIdFromContext();
            var key = $"test_{tenantId}_{Guid.NewGuid():N}";
            
            var policy = new RateLimitPolicy
            {
                RequestLimit = request.RequestLimit,
                WindowSizeSeconds = request.WindowSizeSeconds,
                PolicyName = "test"
            };
            
            var results = new List<object>();
            
            // Perform multiple test requests
            for (int i = 0; i < request.NumberOfRequests; i++)
            {
                var result = await _rateLimitingService.CheckRateLimitAsync(tenantId, key, policy);
                results.Add(new
                {
                    requestNumber = i + 1,
                    isAllowed = result.IsAllowed,
                    remaining = result.RemainingRequests,
                    resetTime = result.ResetTimeSeconds
                });
                
                if (request.DelayBetweenRequests > 0)
                {
                    await Task.Delay(request.DelayBetweenRequests);
                }
            }
            
            // Clean up test data
            await _rateLimitingService.ResetRateLimitAsync(tenantId, key);
            
            return Ok(new
            {
                testConfiguration = new
                {
                    requestLimit = request.RequestLimit,
                    windowSizeSeconds = request.WindowSizeSeconds,
                    numberOfRequests = request.NumberOfRequests
                },
                results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing rate limit");
            return StatusCode(500, new { error = "Failed to test rate limit" });
        }
    }

    private string GetTenantIdFromContext()
    {
        // Try to get tenant ID from various sources
        if (HttpContext.Items.TryGetValue("TenantId", out var tenantIdObj))
        {
            return tenantIdObj.ToString() ?? "unknown";
        }
        
        var tenantClaim = User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantClaim))
        {
            return tenantClaim;
        }
        
        return "default";
    }
}

/// <summary>
/// Request model for resetting rate limits
/// </summary>
public class ResetRateLimitRequest
{
    /// <summary>
    /// User ID to reset (optional, defaults to current user)
    /// </summary>
    public string? UserId { get; set; }
}

/// <summary>
/// Request model for testing rate limits
/// </summary>
public class TestRateLimitRequest
{
    /// <summary>
    /// Request limit to test with
    /// </summary>
    public int RequestLimit { get; set; } = 10;
    
    /// <summary>
    /// Window size in seconds
    /// </summary>
    public int WindowSizeSeconds { get; set; } = 60;
    
    /// <summary>
    /// Number of test requests to make
    /// </summary>
    public int NumberOfRequests { get; set; } = 5;
    
    /// <summary>
    /// Delay between requests in milliseconds
    /// </summary>
    public int DelayBetweenRequests { get; set; } = 0;
}