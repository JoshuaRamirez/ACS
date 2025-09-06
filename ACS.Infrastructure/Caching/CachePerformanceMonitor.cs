using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Comprehensive performance monitoring and health checking service for all cache levels
/// </summary>
public class CachePerformanceMonitor : IHostedService, IHealthCheck
{
    private readonly IThreeLevelCache _cache;
    private readonly ICacheInvalidationService _invalidationService;
    private readonly ILogger<CachePerformanceMonitor> _logger;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource = new("ACS.CachePerformanceMonitor");
    
    // Performance tracking
    private readonly ConcurrentDictionary<string, PerformanceCounter> _performanceCounters = new();
    private readonly Timer _monitoringTimer;
    private readonly Timer _healthCheckTimer;
    
    // Alerting thresholds
    private readonly double _hitRateThreshold;
    private readonly double _responseTimeThreshold;
    private readonly double _errorRateThreshold;
    private readonly int _capacityThreshold;
    
    // Health status tracking
    private readonly ConcurrentDictionary<string, CacheHealthStatus> _healthStatus = new();
    private HealthCheckResult _lastHealthCheckResult = HealthCheckResult.Healthy("Not yet checked");
    
    // SLA tracking
    private readonly SlaMetrics _slaMetrics = new();

    public CachePerformanceMonitor(
        IThreeLevelCache cache,
        ICacheInvalidationService invalidationService,
        IConfiguration configuration,
        ILogger<CachePerformanceMonitor> logger)
    {
        _cache = cache;
        _invalidationService = invalidationService;
        _configuration = configuration;
        _logger = logger;
        
        // Configure thresholds
        _hitRateThreshold = configuration.GetValue<double>("CacheMonitoring:HitRateThreshold", 0.85);
        _responseTimeThreshold = configuration.GetValue<double>("CacheMonitoring:ResponseTimeThresholdMs", 100);
        _errorRateThreshold = configuration.GetValue<double>("CacheMonitoring:ErrorRateThreshold", 0.05);
        _capacityThreshold = configuration.GetValue<int>("CacheMonitoring:CapacityThresholdPercent", 90);
        
        // Initialize timers
        var monitoringInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("CacheMonitoring:MonitoringIntervalSeconds", 30));
        var healthCheckInterval = TimeSpan.FromMinutes(configuration.GetValue<int>("CacheMonitoring:HealthCheckIntervalMinutes", 1));
        
        _monitoringTimer = new Timer(CollectMetrics, null, monitoringInterval, monitoringInterval);
        _healthCheckTimer = new Timer(async _ => await PerformHealthCheckAsync(), null, healthCheckInterval, healthCheckInterval);
        
        _logger.LogInformation("Initialized cache performance monitor with hit rate threshold {HitRateThreshold}% and response time threshold {ResponseTimeThreshold}ms",
            _hitRateThreshold * 100, _responseTimeThreshold);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Started cache performance monitor");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _monitoringTimer?.Dispose();
        _healthCheckTimer?.Dispose();
        _activitySource?.Dispose();
        
        _logger.LogInformation("Stopped cache performance monitor");
        
        return Task.CompletedTask;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = _activitySource.StartActivity("HealthCheck");
            
            var healthInfo = await _cache.GetHealthInfoAsync();
            var stats = await _cache.GetThreeLevelStatisticsAsync(cancellationToken);
            var invalidationStats = await _invalidationService.GetStatisticsAsync();
            
            var issues = new List<string>();
            var warnings = new List<string>();
            var data = new Dictionary<string, object>();
            
            // Check overall hit rate
            if (stats.OverallHitRate < _hitRateThreshold)
            {
                issues.Add($"Overall cache hit rate ({stats.OverallHitRate:P1}) is below threshold ({_hitRateThreshold:P1})");
            }
            
            // Check each cache level
            foreach (var (level, info) in healthInfo)
            {
                data[level] = info;
                
                if (info.ContainsKey("status"))
                {
                    var status = info["status"];
                    if (status == "error" || status == "disconnected")
                    {
                        issues.Add($"{level} is {status}: {info.GetValueOrDefault("error", "Unknown error")}");
                    }
                    else if (status == "degraded")
                    {
                        warnings.Add($"{level} is degraded");
                    }
                }
                
                // Check response times
                if (info.ContainsKey("ping_ms") && double.TryParse(info["ping_ms"], out var pingMs))
                {
                    if (pingMs > _responseTimeThreshold)
                    {
                        warnings.Add($"{level} response time ({pingMs:F1}ms) is above threshold ({_responseTimeThreshold}ms)");
                    }
                }
            }
            
            // Check invalidation service health
            var deadLetterCount = invalidationStats.GetValueOrDefault("dead_letter_count", 0L);
            if (deadLetterCount > 100)
            {
                warnings.Add($"High number of dead letter invalidations: {deadLetterCount}");
            }
            
            // Add performance metrics
            data["overall_hit_rate"] = stats.OverallHitRate;
            data["l1_hit_rate"] = stats.L1HitRate;
            data["l2_hit_rate"] = stats.L2HitRate;
            data["l3_hit_rate"] = stats.L3HitRate;
            data["uptime"] = stats.Uptime;
            data["sla_metrics"] = _slaMetrics;
            
            // Determine overall health status
            if (issues.Count > 0)
            {
                var message = $"Cache health issues detected: {string.Join("; ", issues)}";
                if (warnings.Count > 0)
                {
                    message += $". Warnings: {string.Join("; ", warnings)}";
                }
                
                _lastHealthCheckResult = HealthCheckResult.Unhealthy(message, data: data);
                _logger.LogWarning("Cache health check failed: {Message}", message);
            }
            else if (warnings.Count > 0)
            {
                var message = $"Cache performance warnings: {string.Join("; ", warnings)}";
                _lastHealthCheckResult = HealthCheckResult.Degraded(message, data: data);
                _logger.LogInformation("Cache health check degraded: {Message}", message);
            }
            else
            {
                _lastHealthCheckResult = HealthCheckResult.Healthy("All cache levels are operating normally", data);
                _logger.LogDebug("Cache health check passed");
            }
            
            return _lastHealthCheckResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache health check");
            _lastHealthCheckResult = HealthCheckResult.Unhealthy($"Health check failed: {ex.Message}", ex);
            return _lastHealthCheckResult;
        }
    }

    public async Task<CachePerformanceReport> GetPerformanceReportAsync()
    {
        var stats = await _cache.GetThreeLevelStatisticsAsync();
        var healthInfo = await _cache.GetHealthInfoAsync();
        var invalidationStats = await _invalidationService.GetStatisticsAsync();
        
        var report = new CachePerformanceReport
        {
            Timestamp = DateTime.UtcNow,
            OverallStats = stats,
            LevelHealthInfo = healthInfo,
            InvalidationStats = invalidationStats,
            SlaMetrics = _slaMetrics,
            PerformanceCounters = _performanceCounters.ToDictionary(kv => kv.Key, kv => kv.Value),
            HealthStatus = _lastHealthCheckResult.Status,
            Recommendations = GenerateRecommendations(stats, healthInfo)
        };
        
        return report;
    }

    private async void CollectMetrics(object? state)
    {
        try
        {
            using var activity = _activitySource.StartActivity("CollectMetrics");
            
            var stats = await _cache.GetThreeLevelStatisticsAsync();
            var healthInfo = await _cache.GetHealthInfoAsync();
            
            // Update SLA metrics
            _slaMetrics.UpdateHitRate(stats.OverallHitRate);
            _slaMetrics.UpdateUptime(stats.Uptime);
            
            // Collect per-level metrics
            foreach (var (level, info) in healthInfo)
            {
                var counter = _performanceCounters.GetOrAdd(level, _ => new PerformanceCounter());
                
                // Update response time
                if (info.ContainsKey("ping_ms") && double.TryParse(info["ping_ms"], out var pingMs))
                {
                    counter.RecordResponseTime(pingMs);
                    _slaMetrics.UpdateResponseTime(pingMs);
                }
                
                // Update status
                var status = info.GetValueOrDefault("status", "unknown");
                counter.RecordStatus(status);
                
                // Update hit counts
                var hitMetricKey = level switch
                {
                    "L1_Memory" => "hits",
                    "L2_Redis" => "metric_cache_hit",
                    "L3_SqlServer" => "metric_cache_hit",
                    _ => "hits"
                };
                
                if (info.ContainsKey(hitMetricKey) && long.TryParse(info[hitMetricKey], out var hits))
                {
                    counter.RecordHits(hits);
                }
            }
            
            // Check for threshold violations and alert
            CheckThresholdsAndAlert(stats, healthInfo);
            
            _logger.LogTrace("Collected cache performance metrics");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting cache performance metrics");
        }
    }

    private async Task PerformHealthCheckAsync()
    {
        try
        {
            var context = new HealthCheckContext();
            await CheckHealthAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic health check");
        }
    }

    private void CheckThresholdsAndAlert(ThreeLevelCacheStatistics stats, Dictionary<string, Dictionary<string, string>> healthInfo)
    {
        var alerts = new List<string>();
        
        // Check overall hit rate
        if (stats.OverallHitRate < _hitRateThreshold)
        {
            alerts.Add($"ALERT: Overall cache hit rate ({stats.OverallHitRate:P1}) is below threshold ({_hitRateThreshold:P1})");
        }
        
        // Check response times
        foreach (var (level, info) in healthInfo)
        {
            if (info.ContainsKey("ping_ms") && double.TryParse(info["ping_ms"], out var pingMs))
            {
                if (pingMs > _responseTimeThreshold)
                {
                    alerts.Add($"ALERT: {level} response time ({pingMs:F1}ms) exceeds threshold ({_responseTimeThreshold}ms)");
                }
            }
        }
        
        // Check error rates
        foreach (var counter in _performanceCounters.Values)
        {
            var errorRate = counter.GetErrorRate();
            if (errorRate > _errorRateThreshold)
            {
                alerts.Add($"ALERT: Error rate ({errorRate:P1}) exceeds threshold ({_errorRateThreshold:P1})");
            }
        }
        
        // Log alerts
        foreach (var alert in alerts)
        {
            _logger.LogWarning(alert);
        }
        
        // Update SLA violations
        if (alerts.Count > 0)
        {
            _slaMetrics.RecordViolation();
        }
    }

    private List<string> GenerateRecommendations(ThreeLevelCacheStatistics stats, Dictionary<string, Dictionary<string, string>> healthInfo)
    {
        var recommendations = new List<string>();
        
        // Hit rate recommendations
        if (stats.OverallHitRate < 0.9)
        {
            recommendations.Add("Consider increasing cache expiration times or implementing cache warming strategies");
        }
        
        if (stats.L1HitRate < 0.5)
        {
            recommendations.Add("L1 cache hit rate is low - consider increasing memory cache size or adjusting eviction policies");
        }
        
        // Response time recommendations
        foreach (var (level, info) in healthInfo)
        {
            if (info.ContainsKey("ping_ms") && double.TryParse(info["ping_ms"], out var pingMs))
            {
                if (pingMs > 50 && level == "L2_Redis")
                {
                    recommendations.Add("Redis response time is high - check network connectivity and Redis server performance");
                }
                else if (pingMs > 100 && level == "L3_SqlServer")
                {
                    recommendations.Add("SQL Server cache response time is high - consider query optimization or indexing");
                }
            }
        }
        
        // Capacity recommendations
        foreach (var (level, info) in healthInfo)
        {
            if (info.ContainsKey("used_memory") && info.ContainsKey("max_memory"))
            {
                if (long.TryParse(info["used_memory"], out var used) && 
                    long.TryParse(info["max_memory"], out var max) && max > 0)
                {
                    var utilizationPercent = (double)used / max * 100;
                    if (utilizationPercent > _capacityThreshold)
                    {
                        recommendations.Add($"{level} memory utilization ({utilizationPercent:F1}%) is high - consider scaling up or implementing more aggressive eviction policies");
                    }
                }
            }
        }
        
        return recommendations;
    }

    public void Dispose()
    {
        _monitoringTimer?.Dispose();
        _healthCheckTimer?.Dispose();
        _activitySource?.Dispose();
    }
}

/// <summary>
/// Performance counter for tracking cache metrics
/// </summary>
public class PerformanceCounter
{
    private readonly ConcurrentQueue<double> _responseTimes = new();
    private readonly ConcurrentDictionary<string, long> _statusCounts = new();
    private long _totalHits = 0;
    private readonly object _lock = new();
    
    public void RecordResponseTime(double milliseconds)
    {
        _responseTimes.Enqueue(milliseconds);
        
        // Keep only recent measurements (last 1000)
        while (_responseTimes.Count > 1000)
        {
            _responseTimes.TryDequeue(out _);
        }
    }
    
    public void RecordStatus(string status)
    {
        _statusCounts.AddOrUpdate(status, 1, (key, value) => value + 1);
    }
    
    public void RecordHits(long hits)
    {
        Interlocked.Exchange(ref _totalHits, hits);
    }
    
    public double GetAverageResponseTime()
    {
        var times = _responseTimes.ToArray();
        return times.Length > 0 ? times.Average() : 0;
    }
    
    public double GetErrorRate()
    {
        var total = _statusCounts.Values.Sum();
        var errors = _statusCounts.Where(kv => kv.Key == "error" || kv.Key == "disconnected").Sum(kv => kv.Value);
        return total > 0 ? (double)errors / total : 0;
    }
    
    public long GetTotalHits() => _totalHits;
}

/// <summary>
/// SLA metrics tracking
/// </summary>
public class SlaMetrics
{
    private readonly List<double> _hitRates = new();
    private readonly List<double> _responseTimes = new();
    private readonly List<TimeSpan> _uptimes = new();
    private long _violations = 0;
    private readonly object _lock = new();
    
    public void UpdateHitRate(double hitRate)
    {
        lock (_lock)
        {
            _hitRates.Add(hitRate);
            if (_hitRates.Count > 1000) _hitRates.RemoveAt(0);
        }
    }
    
    public void UpdateResponseTime(double milliseconds)
    {
        lock (_lock)
        {
            _responseTimes.Add(milliseconds);
            if (_responseTimes.Count > 1000) _responseTimes.RemoveAt(0);
        }
    }
    
    public void UpdateUptime(TimeSpan uptime)
    {
        lock (_lock)
        {
            _uptimes.Add(uptime);
            if (_uptimes.Count > 100) _uptimes.RemoveAt(0);
        }
    }
    
    public void RecordViolation()
    {
        Interlocked.Increment(ref _violations);
    }
    
    public double GetAverageHitRate()
    {
        lock (_lock)
        {
            return _hitRates.Count > 0 ? _hitRates.Average() : 0;
        }
    }
    
    public double GetAverageResponseTime()
    {
        lock (_lock)
        {
            return _responseTimes.Count > 0 ? _responseTimes.Average() : 0;
        }
    }
    
    public double GetAvailabilityPercent()
    {
        // Simplified availability calculation
        var totalChecks = _violations + Math.Max(100, _hitRates.Count);
        return totalChecks > 0 ? (1.0 - (double)_violations / totalChecks) * 100 : 100;
    }
    
    public long GetViolations() => _violations;
}

/// <summary>
/// Cache health status for individual levels
/// </summary>
public class CacheHealthStatus
{
    public string Level { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Comprehensive performance report
/// </summary>
public class CachePerformanceReport
{
    public DateTime Timestamp { get; set; }
    public ThreeLevelCacheStatistics OverallStats { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> LevelHealthInfo { get; set; } = new();
    public Dictionary<string, long> InvalidationStats { get; set; } = new();
    public SlaMetrics SlaMetrics { get; set; } = new();
    public Dictionary<string, PerformanceCounter> PerformanceCounters { get; set; } = new();
    public HealthStatus HealthStatus { get; set; }
    public List<string> Recommendations { get; set; } = new();
}