using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.RateLimiting;

/// <summary>
/// Background service for monitoring rate limiting health and performance
/// Provides alerts, cleanup, and health checks for the rate limiting system
/// </summary>
public class RateLimitingMonitoringService : BackgroundService
{
    private readonly ILogger<RateLimitingMonitoringService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    
    private readonly TimeSpan _monitoringInterval;
    private readonly TimeSpan _cleanupInterval;
    private readonly double _alertThreshold;
    private DateTime _lastCleanup = DateTime.UtcNow;

    public RateLimitingMonitoringService(
        ILogger<RateLimitingMonitoringService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        
        _monitoringInterval = TimeSpan.FromMinutes(
            configuration.GetValue<int>("RateLimit:Monitoring:IntervalMinutes", 5));
        _cleanupInterval = TimeSpan.FromMinutes(
            configuration.GetValue<int>("RateLimit:Monitoring:CleanupIntervalMinutes", 15));
        _alertThreshold = configuration.GetValue<double>("RateLimit:Monitoring:AlertThreshold", 0.8);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rate limiting monitoring service started with {Interval} minute intervals", 
            _monitoringInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformMonitoringCycleAsync(stoppingToken);
                await Task.Delay(_monitoringInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in rate limiting monitoring cycle");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Short delay on error
            }
        }

        _logger.LogInformation("Rate limiting monitoring service stopped");
    }

    private async Task PerformMonitoringCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        var rateLimitService = scope.ServiceProvider.GetRequiredService<IRateLimitingService>();
        var metricsService = scope.ServiceProvider.GetRequiredService<RateLimitingMetricsService>();
        var storage = scope.ServiceProvider.GetRequiredService<IRateLimitStorage>();

        // Collect current metrics and health status
        await CollectMetricsAsync(metricsService, storage, cancellationToken);
        
        // Check for alerts and anomalies
        await CheckAlertsAsync(metricsService, cancellationToken);
        
        // Perform cleanup if needed
        if (DateTime.UtcNow - _lastCleanup >= _cleanupInterval)
        {
            await PerformCleanupAsync(storage, cancellationToken);
            _lastCleanup = DateTime.UtcNow;
        }
        
        // Validate system health
        await ValidateSystemHealthAsync(rateLimitService, storage, cancellationToken);
    }

    private async Task CollectMetricsAsync(
        RateLimitingMetricsService metricsService, 
        IRateLimitStorage storage, 
        CancellationToken cancellationToken)
    {
        try
        {
            var storageStats = await storage.GetStatsAsync(cancellationToken);
            var aggregatedMetrics = metricsService.GetAggregatedMetrics();
            
            _logger.LogDebug("Rate limiting metrics - Storage: {TotalEntries} entries, " +
                           "Requests: {TotalRequests} total, {BlockRate:P1} blocked",
                storageStats.TotalEntries, aggregatedMetrics.TotalRequests, aggregatedMetrics.BlockRate);
            
            // Log detailed metrics periodically
            if (DateTime.UtcNow.Minute % 10 == 0) // Every 10 minutes
            {
                _logger.LogInformation("Rate limiting summary - Active tenants: {ActiveTenants}, " +
                                     "Active policies: {ActivePolicies}, Block rate: {BlockRate:P2}",
                    aggregatedMetrics.ActiveTenants, aggregatedMetrics.ActivePolicies, aggregatedMetrics.BlockRate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting rate limiting metrics");
        }
    }

    private async Task CheckAlertsAsync(RateLimitingMetricsService metricsService, CancellationToken cancellationToken)
    {
        try
        {
            var aggregatedMetrics = metricsService.GetAggregatedMetrics();
            
            // Check overall block rate
            if (aggregatedMetrics.BlockRate > _alertThreshold)
            {
                _logger.LogWarning("HIGH RATE LIMITING ACTIVITY: Block rate is {BlockRate:P1} (threshold: {Threshold:P1}). " +
                                 "Total blocked: {TotalBlocked}/{TotalRequests}",
                    aggregatedMetrics.BlockRate, _alertThreshold, 
                    aggregatedMetrics.TotalBlocked, aggregatedMetrics.TotalRequests);
            }
            
            // Check for tenants with unusual activity - simplified to avoid dynamic dispatch issues
            try
            {
                var topBlockedCount = aggregatedMetrics.TopBlockedTenants?.GetType()?.GetProperty("Count")?.GetValue(aggregatedMetrics.TopBlockedTenants) ?? 0;
                if (Convert.ToInt32(topBlockedCount) > 0)
                {
                    _logger.LogInformation("High rate limiting activity detected across {ActiveTenants} tenants with {TotalBlocked} blocked requests",
                        aggregatedMetrics.ActiveTenants, aggregatedMetrics.TotalBlocked);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking top blocked tenants");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limiting alerts");
        }
    }

    private async Task PerformCleanupAsync(IRateLimitStorage storage, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Starting rate limiting storage cleanup");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await storage.CleanupExpiredAsync(cancellationToken);
            stopwatch.Stop();
            
            _logger.LogDebug("Rate limiting cleanup completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rate limiting storage cleanup");
        }
    }

    private async Task ValidateSystemHealthAsync(
        IRateLimitingService rateLimitService,
        IRateLimitStorage storage, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Perform a test rate limit check to validate system health
            var testPolicy = new RateLimitPolicy
            {
                RequestLimit = 1000,
                WindowSizeSeconds = 60,
                PolicyName = "health_check"
            };
            
            var healthCheckResult = await rateLimitService.CheckRateLimitAsync(
                "health_check_tenant", 
                "health_check_key", 
                testPolicy, 
                cancellationToken);
            
            if (!healthCheckResult.IsAllowed)
            {
                _logger.LogWarning("Rate limiting health check failed - test request was blocked");
            }
            
            // Test storage health
            var storageStats = await storage.GetStatsAsync(cancellationToken);
            if (storageStats.AverageResponseTime > TimeSpan.FromSeconds(1))
            {
                _logger.LogWarning("Rate limiting storage showing slow response times: {ResponseTime}ms",
                    storageStats.AverageResponseTime.TotalMilliseconds);
            }
            
            // Clean up health check data
            await rateLimitService.ResetRateLimitAsync("health_check_tenant", "health_check_key", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating rate limiting system health");
        }
    }

    /// <summary>
    /// Get current system health status
    /// </summary>
    public async Task<RateLimitingHealthStatus> GetHealthStatusAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IRateLimitStorage>();
            var metricsService = scope.ServiceProvider.GetRequiredService<RateLimitingMetricsService>();
            
            var storageStats = await storage.GetStatsAsync();
            var aggregatedMetrics = metricsService.GetAggregatedMetrics();
            
            var status = new RateLimitingHealthStatus
            {
                IsHealthy = true,
                LastCheck = DateTime.UtcNow,
                StorageResponseTime = storageStats.AverageResponseTime,
                TotalActiveEntries = storageStats.TotalEntries,
                ExpiredEntries = storageStats.ExpiredEntries,
                BlockRate = aggregatedMetrics.BlockRate,
                ActiveTenants = aggregatedMetrics.ActiveTenants,
                Issues = new List<string>()
            };
            
            // Check for health issues
            if (storageStats.AverageResponseTime > TimeSpan.FromMilliseconds(500))
            {
                status.Issues.Add("Storage response time is high");
            }
            
            if (aggregatedMetrics.BlockRate > _alertThreshold)
            {
                status.Issues.Add($"Block rate ({aggregatedMetrics.BlockRate:P1}) exceeds threshold");
            }
            
            if (storageStats.ExpiredEntries > storageStats.TotalEntries * 0.3)
            {
                status.Issues.Add("High number of expired entries - cleanup may be needed");
            }
            
            status.IsHealthy = !status.Issues.Any();
            
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limiting health status");
            
            return new RateLimitingHealthStatus
            {
                IsHealthy = false,
                LastCheck = DateTime.UtcNow,
                Issues = new List<string> { $"Health check failed: {ex.Message}" }
            };
        }
    }
}

/// <summary>
/// Health status information for rate limiting system
/// </summary>
public class RateLimitingHealthStatus
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