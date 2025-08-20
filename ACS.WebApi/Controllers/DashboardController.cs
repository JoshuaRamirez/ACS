using ACS.Alerting;
using ACS.Infrastructure.Monitoring;
using ACS.Infrastructure.Performance;
using ACS.Service.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Dashboard controller for monitoring and observability
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[ApiVersion("1.0")]
public class DashboardController : ControllerBase
{
    private readonly ILogger<DashboardController> _logger;
    private readonly HealthCheckService _healthCheckService;
    private readonly IMetricsCollectionService _metricsService;
    private readonly IPerformanceMetricsService _performanceMetrics;
    private readonly IAlertStorage _alertStorage;
    private readonly IAlertThrottler _alertThrottler;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public DashboardController(
        ILogger<DashboardController> logger,
        HealthCheckService healthCheckService,
        IMetricsCollectionService metricsService,
        IPerformanceMetricsService performanceMetrics,
        IAlertStorage alertStorage,
        IAlertThrottler alertThrottler,
        ApplicationDbContext dbContext,
        IConfiguration configuration)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
        _metricsService = metricsService;
        _performanceMetrics = performanceMetrics;
        _alertStorage = alertStorage;
        _alertThrottler = alertThrottler;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    /// <summary>
    /// Get comprehensive dashboard overview
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        try
        {
            var overview = new DashboardOverview
            {
                Timestamp = DateTime.UtcNow,
                SystemStatus = await GetSystemStatusAsync(),
                PerformanceMetrics = await GetPerformanceMetricsAsync(),
                ResourceUtilization = GetResourceUtilization(),
                DatabaseStatistics = await GetDatabaseStatisticsAsync(),
                TenantStatistics = await GetTenantStatisticsAsync(),
                AlertSummary = await GetAlertSummaryAsync(),
                RecentActivity = await GetRecentActivityAsync()
            };

            return Ok(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dashboard overview");
            return StatusCode(500, new { error = "Failed to generate dashboard overview" });
        }
    }

    /// <summary>
    /// Get real-time system metrics
    /// </summary>
    [HttpGet("metrics/realtime")]
    public async Task<IActionResult> GetRealtimeMetrics()
    {
        try
        {
            var metrics = new RealtimeMetrics
            {
                Timestamp = DateTime.UtcNow,
                RequestsPerSecond = _metricsService.GetRequestsPerSecond(),
                AverageResponseTime = _metricsService.GetAverageResponseTime(),
                ActiveConnections = _metricsService.GetActiveConnections(),
                ErrorRate = _metricsService.GetErrorRate(),
                ThroughputKbps = _metricsService.GetThroughput(),
                CpuUsagePercent = GetCpuUsage(),
                MemoryUsageMB = GetMemoryUsage(),
                ThreadCount = Process.GetCurrentProcess().Threads.Count,
                GCCollections = new
                {
                    Gen0 = GC.CollectionCount(0),
                    Gen1 = GC.CollectionCount(1),
                    Gen2 = GC.CollectionCount(2)
                }
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting realtime metrics");
            return StatusCode(500, new { error = "Failed to get realtime metrics" });
        }
    }

    /// <summary>
    /// Get health status of all components
    /// </summary>
    [HttpGet("health/detailed")]
    public async Task<IActionResult> GetDetailedHealth()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();
            
            var detailedHealth = new DetailedHealthStatus
            {
                OverallStatus = healthReport.Status.ToString(),
                TotalDuration = healthReport.TotalDuration,
                Components = healthReport.Entries.Select(e => new ComponentHealth
                {
                    Name = e.Key,
                    Status = e.Value.Status.ToString(),
                    Duration = e.Value.Duration,
                    Description = e.Value.Description,
                    Exception = e.Value.Exception?.Message,
                    Tags = e.Value.Tags?.ToList(),
                    Data = e.Value.Data?.ToDictionary(d => d.Key, d => d.Value?.ToString())
                }).ToList()
            };

            return Ok(detailedHealth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting detailed health status");
            return StatusCode(500, new { error = "Failed to get health status" });
        }
    }

    /// <summary>
    /// Get performance analytics
    /// </summary>
    [HttpGet("analytics/performance")]
    public async Task<IActionResult> GetPerformanceAnalytics(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null)
    {
        try
        {
            var start = startTime ?? DateTime.UtcNow.AddHours(-24);
            var end = endTime ?? DateTime.UtcNow;

            var analytics = new PerformanceAnalytics
            {
                Period = new { Start = start, End = end },
                EndpointMetrics = await GetEndpointMetricsAsync(start, end),
                DatabaseMetrics = await GetDatabaseMetricsAsync(start, end),
                CacheMetrics = GetCacheMetrics(),
                ErrorAnalysis = await GetErrorAnalysisAsync(start, end),
                TenantMetrics = await GetTenantMetricsAsync(start, end)
            };

            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance analytics");
            return StatusCode(500, new { error = "Failed to get performance analytics" });
        }
    }

    /// <summary>
    /// Get alert dashboard
    /// </summary>
    [HttpGet("alerts/dashboard")]
    public async Task<IActionResult> GetAlertDashboard()
    {
        try
        {
            var now = DateTime.UtcNow;
            var last24Hours = now.AddHours(-24);
            var last7Days = now.AddDays(-7);

            var dashboard = new AlertDashboard
            {
                ActiveAlerts = (await _alertStorage.GetActiveAlertsAsync()).ToList(),
                Statistics24Hours = await _alertStorage.GetAlertStatisticsAsync(last24Hours, now),
                Statistics7Days = await _alertStorage.GetAlertStatisticsAsync(last7Days, now),
                ThrottleStatistics = await _alertThrottler.GetThrottleStatisticsAsync(),
                RecentAlerts = (await _alertStorage.GetAlertHistoryAsync(last24Hours, now))
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(20)
                    .ToList(),
                AlertTrends = await GetAlertTrendsAsync()
            };

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert dashboard");
            return StatusCode(500, new { error = "Failed to get alert dashboard" });
        }
    }

    /// <summary>
    /// Get tenant usage dashboard
    /// </summary>
    [HttpGet("tenants/usage")]
    public async Task<IActionResult> GetTenantUsage()
    {
        try
        {
            var tenantUsage = await _dbContext.Set<TenantUsageStatistics>()
                .Where(t => t.RecordedAt >= DateTime.UtcNow.AddHours(-24))
                .GroupBy(t => t.TenantId)
                .Select(g => new TenantUsageSummary
                {
                    TenantId = g.Key,
                    RequestCount = g.Sum(t => t.RequestCount),
                    ErrorCount = g.Sum(t => t.ErrorCount),
                    AverageResponseTime = g.Average(t => t.AverageResponseTime),
                    DataTransferKB = g.Sum(t => t.DataTransferKB),
                    ActiveUsers = g.Max(t => t.ActiveUsers),
                    LastActivity = g.Max(t => t.RecordedAt)
                })
                .OrderByDescending(t => t.RequestCount)
                .ToListAsync();

            return Ok(tenantUsage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tenant usage");
            return StatusCode(500, new { error = "Failed to get tenant usage" });
        }
    }

    /// <summary>
    /// Get system capacity metrics
    /// </summary>
    [HttpGet("capacity")]
    public async Task<IActionResult> GetCapacityMetrics()
    {
        try
        {
            var capacity = new CapacityMetrics
            {
                Database = await GetDatabaseCapacityAsync(),
                Memory = GetMemoryCapacity(),
                Disk = GetDiskCapacity(),
                ConnectionPool = GetConnectionPoolCapacity(),
                ThreadPool = GetThreadPoolCapacity(),
                RateLimiting = GetRateLimitCapacity()
            };

            return Ok(capacity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting capacity metrics");
            return StatusCode(500, new { error = "Failed to get capacity metrics" });
        }
    }

    /// <summary>
    /// Get security dashboard metrics
    /// </summary>
    [HttpGet("security")]
    public async Task<IActionResult> GetSecurityMetrics()
    {
        try
        {
            var security = new SecurityDashboard
            {
                AuthenticationMetrics = await GetAuthenticationMetricsAsync(),
                AuthorizationMetrics = await GetAuthorizationMetricsAsync(),
                RateLimitingMetrics = GetRateLimitingMetrics(),
                SecurityAlerts = await GetSecurityAlertsAsync(),
                SuspiciousActivity = await GetSuspiciousActivityAsync()
            };

            return Ok(security);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security metrics");
            return StatusCode(500, new { error = "Failed to get security metrics" });
        }
    }

    #region Private Helper Methods

    private async Task<SystemStatus> GetSystemStatusAsync()
    {
        var healthReport = await _healthCheckService.CheckHealthAsync();
        
        return new SystemStatus
        {
            Health = healthReport.Status.ToString(),
            Uptime = GetUptime(),
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown",
            Environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Unknown",
            StartTime = Process.GetCurrentProcess().StartTime.ToUniversalTime()
        };
    }

    private async Task<PerformanceMetricsSummary> GetPerformanceMetricsAsync()
    {
        var metrics = await _performanceMetrics.GetCurrentMetricsAsync();
        
        return new PerformanceMetricsSummary
        {
            AverageResponseTimeMs = metrics.AverageResponseTime,
            RequestsPerSecond = metrics.RequestsPerSecond,
            ErrorRate = metrics.ErrorRate,
            P95ResponseTimeMs = metrics.P95ResponseTime,
            P99ResponseTimeMs = metrics.P99ResponseTime
        };
    }

    private ResourceUtilization GetResourceUtilization()
    {
        var process = Process.GetCurrentProcess();
        
        return new ResourceUtilization
        {
            CpuUsagePercent = GetCpuUsage(),
            MemoryUsageMB = GetMemoryUsage(),
            DiskUsagePercent = GetDiskUsage(),
            NetworkBandwidthKbps = _metricsService.GetNetworkBandwidth(),
            OpenFileHandles = process.HandleCount,
            ThreadCount = process.Threads.Count
        };
    }

    private async Task<DatabaseStatistics> GetDatabaseStatisticsAsync()
    {
        return new DatabaseStatistics
        {
            ConnectionCount = await _dbContext.Database.GetDbConnection().GetOpenConnectionsAsync(),
            ActiveQueries = await _dbContext.Database.GetActiveQueriesAsync(),
            AverageQueryTimeMs = await _dbContext.Database.GetAverageQueryTimeAsync(),
            DatabaseSizeMB = await _dbContext.Database.GetDatabaseSizeAsync(),
            TableCount = await _dbContext.Database.GetTableCountAsync(),
            IndexCount = await _dbContext.Database.GetIndexCountAsync()
        };
    }

    private async Task<TenantStatistics> GetTenantStatisticsAsync()
    {
        var tenantCount = await _dbContext.Set<Tenant>().CountAsync();
        var activeTenantsToday = await _dbContext.Set<TenantActivity>()
            .Where(a => a.LastActivity >= DateTime.UtcNow.Date)
            .Select(a => a.TenantId)
            .Distinct()
            .CountAsync();

        return new TenantStatistics
        {
            TotalTenants = tenantCount,
            ActiveTenantsToday = activeTenantsToday,
            NewTenantsThisMonth = await _dbContext.Set<Tenant>()
                .Where(t => t.CreatedAt >= DateTime.UtcNow.AddMonths(-1))
                .CountAsync(),
            AverageUsersPerTenant = tenantCount > 0 ? 
                await _dbContext.Set<User>().CountAsync() / (double)tenantCount : 0
        };
    }

    private async Task<AlertSummary> GetAlertSummaryAsync()
    {
        var activeAlerts = await _alertStorage.GetActiveAlertsAsync();
        var last24Hours = DateTime.UtcNow.AddHours(-24);
        
        return new AlertSummary
        {
            ActiveCount = activeAlerts.Count(),
            CriticalCount = activeAlerts.Count(a => a.Severity == AlertSeverity.Critical),
            WarningCount = activeAlerts.Count(a => a.Severity == AlertSeverity.Warning),
            Last24HoursCount = (await _alertStorage.GetAlertHistoryAsync(last24Hours, DateTime.UtcNow)).Count()
        };
    }

    private async Task<List<RecentActivity>> GetRecentActivityAsync()
    {
        var activities = new List<RecentActivity>();
        
        // Get recent alerts
        var recentAlerts = await _alertStorage.GetAlertHistoryAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        
        foreach (var alert in recentAlerts.Take(5))
        {
            activities.Add(new RecentActivity
            {
                Type = "Alert",
                Description = alert.Title,
                Timestamp = alert.CreatedAt,
                Severity = alert.Severity.ToString()
            });
        }

        // Get recent errors from logs
        var recentErrors = await GetRecentErrorsAsync();
        activities.AddRange(recentErrors);

        return activities.OrderByDescending(a => a.Timestamp).Take(10).ToList();
    }

    private async Task<List<EndpointMetric>> GetEndpointMetricsAsync(DateTime start, DateTime end)
    {
        return await _dbContext.Set<RequestMetrics>()
            .Where(r => r.Timestamp >= start && r.Timestamp <= end)
            .GroupBy(r => r.Endpoint)
            .Select(g => new EndpointMetric
            {
                Endpoint = g.Key,
                RequestCount = g.Count(),
                AverageResponseTime = g.Average(r => r.ResponseTime),
                ErrorCount = g.Count(r => r.StatusCode >= 400),
                P95ResponseTime = g.OrderBy(r => r.ResponseTime)
                    .Skip((int)(g.Count() * 0.95))
                    .FirstOrDefault().ResponseTime
            })
            .OrderByDescending(e => e.RequestCount)
            .Take(20)
            .ToListAsync();
    }

    private async Task<DatabaseMetricsSummary> GetDatabaseMetricsAsync(DateTime start, DateTime end)
    {
        return new DatabaseMetricsSummary
        {
            TotalQueries = await _dbContext.Set<QueryMetrics>()
                .Where(q => q.ExecutedAt >= start && q.ExecutedAt <= end)
                .CountAsync(),
            AverageQueryTime = await _dbContext.Set<QueryMetrics>()
                .Where(q => q.ExecutedAt >= start && q.ExecutedAt <= end)
                .AverageAsync(q => q.Duration),
            SlowQueries = await _dbContext.Set<QueryMetrics>()
                .Where(q => q.ExecutedAt >= start && q.ExecutedAt <= end && q.Duration > 1000)
                .CountAsync(),
            ConnectionErrors = await _dbContext.Set<DatabaseErrors>()
                .Where(e => e.OccurredAt >= start && e.OccurredAt <= end)
                .CountAsync()
        };
    }

    private CacheMetrics GetCacheMetrics()
    {
        return new CacheMetrics
        {
            HitRate = _metricsService.GetCacheHitRate(),
            MissRate = _metricsService.GetCacheMissRate(),
            EvictionCount = _metricsService.GetCacheEvictions(),
            CurrentSize = _metricsService.GetCacheSize(),
            MaxSize = _configuration.GetValue<long>("Cache:MaxSizeMB", 100) * 1024 * 1024
        };
    }

    private async Task<ErrorAnalysis> GetErrorAnalysisAsync(DateTime start, DateTime end)
    {
        var errors = await _dbContext.Set<ErrorLog>()
            .Where(e => e.Timestamp >= start && e.Timestamp <= end)
            .GroupBy(e => e.ErrorType)
            .Select(g => new ErrorTypeCount
            {
                ErrorType = g.Key,
                Count = g.Count(),
                MostRecentOccurrence = g.Max(e => e.Timestamp)
            })
            .OrderByDescending(e => e.Count)
            .ToListAsync();

        return new ErrorAnalysis
        {
            TotalErrors = errors.Sum(e => e.Count),
            ErrorsByType = errors,
            ErrorRate = await CalculateErrorRateAsync(start, end),
            TopFailingEndpoints = await GetTopFailingEndpointsAsync(start, end)
        };
    }

    private async Task<List<TenantMetric>> GetTenantMetricsAsync(DateTime start, DateTime end)
    {
        return await _dbContext.Set<TenantUsageStatistics>()
            .Where(t => t.RecordedAt >= start && t.RecordedAt <= end)
            .GroupBy(t => t.TenantId)
            .Select(g => new TenantMetric
            {
                TenantId = g.Key,
                RequestCount = g.Sum(t => t.RequestCount),
                ErrorCount = g.Sum(t => t.ErrorCount),
                DataTransferKB = g.Sum(t => t.DataTransferKB),
                CostEstimate = g.Sum(t => t.EstimatedCost)
            })
            .OrderByDescending(t => t.RequestCount)
            .Take(10)
            .ToListAsync();
    }

    private async Task<List<AlertTrend>> GetAlertTrendsAsync()
    {
        var last7Days = DateTime.UtcNow.AddDays(-7);
        var alerts = await _alertStorage.GetAlertHistoryAsync(last7Days, DateTime.UtcNow);
        
        return alerts
            .GroupBy(a => new { Date = a.CreatedAt.Date, a.Severity })
            .Select(g => new AlertTrend
            {
                Date = g.Key.Date,
                Severity = g.Key.Severity.ToString(),
                Count = g.Count()
            })
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Severity)
            .ToList();
    }

    private async Task<DatabaseCapacity> GetDatabaseCapacityAsync()
    {
        var dbSize = await _dbContext.Database.GetDatabaseSizeAsync();
        var maxSize = _configuration.GetValue<long>("Database:MaxSizeMB", 10240);
        
        return new DatabaseCapacity
        {
            CurrentSizeMB = dbSize,
            MaxSizeMB = maxSize,
            UsagePercent = (dbSize * 100.0) / maxSize,
            ConnectionsUsed = await _dbContext.Database.GetOpenConnectionsAsync(),
            MaxConnections = _configuration.GetValue<int>("Database:MaxConnections", 100)
        };
    }

    private MemoryCapacity GetMemoryCapacity()
    {
        var process = Process.GetCurrentProcess();
        var totalMemory = GC.GetTotalMemory(false);
        
        return new MemoryCapacity
        {
            UsedMB = totalMemory / (1024 * 1024),
            AvailableMB = (Environment.WorkingSet - totalMemory) / (1024 * 1024),
            TotalMB = Environment.WorkingSet / (1024 * 1024),
            GCHeapMB = GC.GetTotalMemory(false) / (1024 * 1024),
            WorkingSetMB = process.WorkingSet64 / (1024 * 1024)
        };
    }

    private double GetCpuUsage()
    {
        // This is a simplified implementation
        // In production, use performance counters or a more sophisticated approach
        return Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds / 
               Environment.ProcessorCount / 
               Environment.TickCount * 100;
    }

    private long GetMemoryUsage()
    {
        return Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
    }

    private double GetDiskUsage()
    {
        var drive = new DriveInfo("C");
        return ((drive.TotalSize - drive.AvailableFreeSpace) * 100.0) / drive.TotalSize;
    }

    private TimeSpan GetUptime()
    {
        return DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
    }

    #endregion
}

#region Dashboard Models

public class DashboardOverview
{
    public DateTime Timestamp { get; set; }
    public SystemStatus SystemStatus { get; set; }
    public PerformanceMetricsSummary PerformanceMetrics { get; set; }
    public ResourceUtilization ResourceUtilization { get; set; }
    public DatabaseStatistics DatabaseStatistics { get; set; }
    public TenantStatistics TenantStatistics { get; set; }
    public AlertSummary AlertSummary { get; set; }
    public List<RecentActivity> RecentActivity { get; set; }
}

public class SystemStatus
{
    public string Health { get; set; }
    public TimeSpan Uptime { get; set; }
    public string Version { get; set; }
    public string Environment { get; set; }
    public DateTime StartTime { get; set; }
}

public class PerformanceMetricsSummary
{
    public double AverageResponseTimeMs { get; set; }
    public double RequestsPerSecond { get; set; }
    public double ErrorRate { get; set; }
    public double P95ResponseTimeMs { get; set; }
    public double P99ResponseTimeMs { get; set; }
}

public class ResourceUtilization
{
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public double DiskUsagePercent { get; set; }
    public double NetworkBandwidthKbps { get; set; }
    public int OpenFileHandles { get; set; }
    public int ThreadCount { get; set; }
}

public class DatabaseStatistics
{
    public int ConnectionCount { get; set; }
    public int ActiveQueries { get; set; }
    public double AverageQueryTimeMs { get; set; }
    public long DatabaseSizeMB { get; set; }
    public int TableCount { get; set; }
    public int IndexCount { get; set; }
}

public class TenantStatistics
{
    public int TotalTenants { get; set; }
    public int ActiveTenantsToday { get; set; }
    public int NewTenantsThisMonth { get; set; }
    public double AverageUsersPerTenant { get; set; }
}

public class AlertSummary
{
    public int ActiveCount { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int Last24HoursCount { get; set; }
}

public class RecentActivity
{
    public string Type { get; set; }
    public string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public string Severity { get; set; }
}

public class RealtimeMetrics
{
    public DateTime Timestamp { get; set; }
    public double RequestsPerSecond { get; set; }
    public double AverageResponseTime { get; set; }
    public int ActiveConnections { get; set; }
    public double ErrorRate { get; set; }
    public double ThroughputKbps { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public int ThreadCount { get; set; }
    public object GCCollections { get; set; }
}

public class DetailedHealthStatus
{
    public string OverallStatus { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<ComponentHealth> Components { get; set; }
}

public class ComponentHealth
{
    public string Name { get; set; }
    public string Status { get; set; }
    public TimeSpan Duration { get; set; }
    public string Description { get; set; }
    public string Exception { get; set; }
    public List<string> Tags { get; set; }
    public Dictionary<string, string> Data { get; set; }
}

public class PerformanceAnalytics
{
    public object Period { get; set; }
    public List<EndpointMetric> EndpointMetrics { get; set; }
    public DatabaseMetricsSummary DatabaseMetrics { get; set; }
    public CacheMetrics CacheMetrics { get; set; }
    public ErrorAnalysis ErrorAnalysis { get; set; }
    public List<TenantMetric> TenantMetrics { get; set; }
}

public class AlertDashboard
{
    public List<AlertHistory> ActiveAlerts { get; set; }
    public AlertStatistics Statistics24Hours { get; set; }
    public AlertStatistics Statistics7Days { get; set; }
    public ThrottleStatistics ThrottleStatistics { get; set; }
    public List<AlertHistory> RecentAlerts { get; set; }
    public List<AlertTrend> AlertTrends { get; set; }
}

public class SecurityDashboard
{
    public object AuthenticationMetrics { get; set; }
    public object AuthorizationMetrics { get; set; }
    public object RateLimitingMetrics { get; set; }
    public object SecurityAlerts { get; set; }
    public object SuspiciousActivity { get; set; }
}

public class CapacityMetrics
{
    public DatabaseCapacity Database { get; set; }
    public MemoryCapacity Memory { get; set; }
    public object Disk { get; set; }
    public object ConnectionPool { get; set; }
    public object ThreadPool { get; set; }
    public object RateLimiting { get; set; }
}

#endregion