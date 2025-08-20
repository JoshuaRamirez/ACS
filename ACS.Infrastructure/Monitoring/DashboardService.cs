using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace ACS.Infrastructure.Monitoring;

/// <summary>
/// Implementation of dashboard service for monitoring visualization
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DashboardService> _logger;
    private readonly Dictionary<string, DashboardConfiguration> _dashboardConfigs;

    public DashboardService(
        IMetricsCollector metricsCollector,
        IConfiguration configuration,
        ILogger<DashboardService> logger)
    {
        _metricsCollector = metricsCollector;
        _configuration = configuration;
        _logger = logger;
        _dashboardConfigs = new Dictionary<string, DashboardConfiguration>();
        
        InitializeDefaultDashboards();
    }

    public async Task<DashboardData> GetDashboardAsync(string dashboardName, TimeRange? timeRange = null)
    {
        timeRange ??= new TimeRange();
        
        var config = await GetConfigurationAsync(dashboardName);
        var dashboard = new DashboardData
        {
            Name = dashboardName,
            GeneratedAt = DateTime.UtcNow,
            TimeRange = timeRange,
            Widgets = new List<Widget>()
        };

        foreach (var widgetConfig in config.Widgets)
        {
            var widget = await BuildWidgetAsync(widgetConfig, timeRange);
            dashboard.Widgets.Add(widget);
        }

        dashboard.Metadata = await GetDashboardMetadataAsync(dashboardName);
        
        return dashboard;
    }

    public async IAsyncEnumerable<MetricUpdate> GetRealTimeMetricsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var metrics = new[]
        {
            ApplicationMetrics.Api.RequestCount,
            ApplicationMetrics.Api.RequestDuration,
            ApplicationMetrics.Api.ActiveRequests,
            ApplicationMetrics.Performance.CpuUsage,
            ApplicationMetrics.Performance.MemoryUsage,
            ApplicationMetrics.Database.QueryCount,
            ApplicationMetrics.Cache.Hits,
            ApplicationMetrics.Cache.Misses
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = await _metricsCollector.GetSnapshotAsync();
            
            foreach (var metric in metrics)
            {
                double value = 0;
                
                if (snapshot.Counters.TryGetValue(metric, out var counter))
                    value = counter.Value;
                else if (snapshot.Gauges.TryGetValue(metric, out var gauge))
                    value = gauge.Value;
                else if (snapshot.Histograms.TryGetValue(metric, out var histogram))
                    value = histogram.Mean;

                yield return new MetricUpdate
                {
                    MetricName = metric,
                    Value = value,
                    Timestamp = DateTime.UtcNow
                };
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    public Task<DashboardConfiguration> GetConfigurationAsync(string dashboardName)
    {
        if (_dashboardConfigs.TryGetValue(dashboardName, out var config))
        {
            return Task.FromResult(config);
        }

        // Return default configuration if not found
        return Task.FromResult(GetDefaultConfiguration(dashboardName));
    }

    public Task SaveConfigurationAsync(string dashboardName, DashboardConfiguration configuration)
    {
        _dashboardConfigs[dashboardName] = configuration;
        _logger.LogInformation("Saved dashboard configuration for {Dashboard}", dashboardName);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DashboardInfo>> GetAvailableDashboardsAsync()
    {
        var dashboards = new List<DashboardInfo>
        {
            new() { Name = "overview", Description = "System Overview", Category = "System", IsDefault = true },
            new() { Name = "performance", Description = "Performance Metrics", Category = "System", IsDefault = true },
            new() { Name = "api", Description = "API Metrics", Category = "Application", IsDefault = true },
            new() { Name = "database", Description = "Database Metrics", Category = "Infrastructure", IsDefault = true },
            new() { Name = "security", Description = "Security Metrics", Category = "Security", IsDefault = true },
            new() { Name = "business", Description = "Business Metrics", Category = "Business", IsDefault = true },
            new() { Name = "errors", Description = "Error Tracking", Category = "Operations", IsDefault = true },
            new() { Name = "tenant", Description = "Tenant Metrics", Category = "Multi-tenancy", IsDefault = true }
        };

        // Add custom dashboards
        foreach (var (name, config) in _dashboardConfigs)
        {
            dashboards.Add(new DashboardInfo
            {
                Name = name,
                Description = config.Description,
                Category = "Custom",
                IsDefault = false,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            });
        }

        return Task.FromResult<IEnumerable<DashboardInfo>>(dashboards);
    }

    private void InitializeDefaultDashboards()
    {
        // Overview Dashboard
        _dashboardConfigs["overview"] = new DashboardConfiguration
        {
            Name = "overview",
            Description = "System Overview Dashboard",
            Widgets = new List<WidgetConfiguration>
            {
                new() { Id = "requests", Title = "Request Rate", Type = WidgetType.LineChart, 
                    Metrics = new() { ApplicationMetrics.Api.RequestCount } },
                new() { Id = "response_time", Title = "Response Time", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Api.RequestDuration } },
                new() { Id = "active_users", Title = "Active Users", Type = WidgetType.Counter,
                    Metrics = new() { ApplicationMetrics.Auth.ActiveSessions } },
                new() { Id = "error_rate", Title = "Error Rate", Type = WidgetType.Gauge,
                    Metrics = new() { ApplicationMetrics.Api.RequestErrors } },
                new() { Id = "cpu", Title = "CPU Usage", Type = WidgetType.Gauge,
                    Metrics = new() { ApplicationMetrics.Performance.CpuUsage } },
                new() { Id = "memory", Title = "Memory Usage", Type = WidgetType.Gauge,
                    Metrics = new() { ApplicationMetrics.Performance.MemoryUsage } }
            }
        };

        // Performance Dashboard
        _dashboardConfigs["performance"] = new DashboardConfiguration
        {
            Name = "performance",
            Description = "Performance Metrics Dashboard",
            Widgets = new List<WidgetConfiguration>
            {
                new() { Id = "response_time_histogram", Title = "Response Time Distribution", Type = WidgetType.BarChart,
                    Metrics = new() { ApplicationMetrics.Api.RequestDuration } },
                new() { Id = "throughput", Title = "Throughput", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Api.RequestRate } },
                new() { Id = "db_performance", Title = "Database Performance", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Database.QueryDuration } },
                new() { Id = "cache_hit_ratio", Title = "Cache Hit Ratio", Type = WidgetType.Gauge,
                    Metrics = new() { ApplicationMetrics.Cache.HitRatio } },
                new() { Id = "gc_collections", Title = "GC Collections", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Performance.GcCollections } },
                new() { Id = "thread_count", Title = "Thread Count", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Performance.ThreadCount } }
            }
        };

        // Security Dashboard
        _dashboardConfigs["security"] = new DashboardConfiguration
        {
            Name = "security",
            Description = "Security Metrics Dashboard",
            Widgets = new List<WidgetConfiguration>
            {
                new() { Id = "login_attempts", Title = "Login Attempts", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Auth.LoginAttempts } },
                new() { Id = "failed_logins", Title = "Failed Logins", Type = WidgetType.Counter,
                    Metrics = new() { ApplicationMetrics.Auth.LoginFailure } },
                new() { Id = "permission_denials", Title = "Permission Denials", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Business.PermissionsDenied } },
                new() { Id = "rate_limiting", Title = "Rate Limited Requests", Type = WidgetType.BarChart,
                    Metrics = new() { ApplicationMetrics.RateLimiting.RequestsThrottled } },
                new() { Id = "auth_errors", Title = "Authorization Errors", Type = WidgetType.Counter,
                    Metrics = new() { ApplicationMetrics.Errors.AuthorizationErrors } }
            }
        };
    }

    private DashboardConfiguration GetDefaultConfiguration(string dashboardName)
    {
        return dashboardName switch
        {
            "api" => CreateApiDashboard(),
            "database" => CreateDatabaseDashboard(),
            "business" => CreateBusinessDashboard(),
            "errors" => CreateErrorDashboard(),
            "tenant" => CreateTenantDashboard(),
            _ => _dashboardConfigs.GetValueOrDefault("overview") ?? new DashboardConfiguration()
        };
    }

    private DashboardConfiguration CreateApiDashboard()
    {
        return new DashboardConfiguration
        {
            Name = "api",
            Description = "API Metrics Dashboard",
            Widgets = new List<WidgetConfiguration>
            {
                new() { Id = "endpoints", Title = "Top Endpoints", Type = WidgetType.Table,
                    MetricQuery = "api.request.count BY endpoint" },
                new() { Id = "methods", Title = "HTTP Methods", Type = WidgetType.PieChart,
                    MetricQuery = "api.request.count BY method" },
                new() { Id = "status_codes", Title = "Status Codes", Type = WidgetType.BarChart,
                    MetricQuery = "api.request.count BY status_code" },
                new() { Id = "response_sizes", Title = "Response Sizes", Type = WidgetType.Heatmap,
                    Metrics = new() { ApplicationMetrics.Api.ResponseSize } }
            }
        };
    }

    private DashboardConfiguration CreateDatabaseDashboard()
    {
        return new DashboardConfiguration
        {
            Name = "database",
            Description = "Database Metrics Dashboard",
            Widgets = new List<WidgetConfiguration>
            {
                new() { Id = "query_rate", Title = "Query Rate", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Database.QueryCount } },
                new() { Id = "connection_pool", Title = "Connection Pool", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Database.ConnectionsActive, ApplicationMetrics.Database.ConnectionsIdle } },
                new() { Id = "slow_queries", Title = "Slow Queries", Type = WidgetType.Table,
                    MetricQuery = "db.query.duration > 1000" },
                new() { Id = "deadlocks", Title = "Deadlocks", Type = WidgetType.Counter,
                    Metrics = new() { ApplicationMetrics.Database.DeadlockCount } }
            }
        };
    }

    private DashboardConfiguration CreateBusinessDashboard()
    {
        return new DashboardConfiguration
        {
            Name = "business",
            Description = "Business Metrics Dashboard",
            Widgets = new List<WidgetConfiguration>
            {
                new() { Id = "user_growth", Title = "User Growth", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Business.UsersCreated } },
                new() { Id = "permission_checks", Title = "Permission Checks", Type = WidgetType.Counter,
                    Metrics = new() { ApplicationMetrics.Business.PermissionChecks } },
                new() { Id = "resource_access", Title = "Resource Access", Type = WidgetType.Timeline,
                    Metrics = new() { ApplicationMetrics.Business.ResourcesAccessed } },
                new() { Id = "role_assignments", Title = "Role Assignments", Type = WidgetType.BarChart,
                    Metrics = new() { ApplicationMetrics.Business.RolesAssigned } }
            }
        };
    }

    private DashboardConfiguration CreateErrorDashboard()
    {
        return new DashboardConfiguration
        {
            Name = "errors",
            Description = "Error Tracking Dashboard",
            Widgets = new List<WidgetConfiguration>
            {
                new() { Id = "error_rate", Title = "Error Rate", Type = WidgetType.LineChart,
                    Metrics = new() { ApplicationMetrics.Errors.TotalErrors } },
                new() { Id = "error_types", Title = "Error Types", Type = WidgetType.PieChart,
                    MetricQuery = "errors.* BY error_type" },
                new() { Id = "recent_errors", Title = "Recent Errors", Type = WidgetType.Table,
                    MetricQuery = "errors.unhandled LAST 100" },
                new() { Id = "error_heatmap", Title = "Error Heatmap", Type = WidgetType.Heatmap,
                    MetricQuery = "errors.* BY endpoint,hour" }
            }
        };
    }

    private DashboardConfiguration CreateTenantDashboard()
    {
        return new DashboardConfiguration
        {
            Name = "tenant",
            Description = "Tenant Metrics Dashboard",
            Widgets = new List<WidgetConfiguration>
            {
                new() { Id = "active_tenants", Title = "Active Tenants", Type = WidgetType.Counter,
                    Metrics = new() { ApplicationMetrics.Tenant.ActiveTenants } },
                new() { Id = "tenant_requests", Title = "Requests by Tenant", Type = WidgetType.BarChart,
                    MetricQuery = "tenant.requests BY tenant_id" },
                new() { Id = "tenant_storage", Title = "Storage by Tenant", Type = WidgetType.PieChart,
                    MetricQuery = "tenant.storage.bytes BY tenant_id" },
                new() { Id = "tenant_users", Title = "Users by Tenant", Type = WidgetType.Table,
                    MetricQuery = "tenant.users.count BY tenant_id" }
            }
        };
    }

    private async Task<Widget> BuildWidgetAsync(WidgetConfiguration config, TimeRange timeRange)
    {
        var widget = new Widget
        {
            Id = config.Id,
            Title = config.Title,
            Type = config.Type,
            Options = config.Options
        };

        // Fetch metric data based on configuration
        if (config.Metrics.Any())
        {
            var data = new List<object>();
            foreach (var metric in config.Metrics)
            {
                var points = await _metricsCollector.GetMetricsAsync(metric, timeRange.Start, timeRange.End);
                data.Add(new
                {
                    metric,
                    points = points.Select(p => new { p.Timestamp, p.Value })
                });
            }
            widget.Data = data;
        }
        else if (!string.IsNullOrEmpty(config.MetricQuery))
        {
            // Parse and execute metric query
            widget.Data = await ExecuteMetricQueryAsync(config.MetricQuery, timeRange);
        }

        return widget;
    }

    private async Task<object> ExecuteMetricQueryAsync(string query, TimeRange timeRange)
    {
        // Simplified query execution - in production this would be more sophisticated
        await Task.CompletedTask;
        
        return new
        {
            query,
            results = new List<object>()
        };
    }

    private async Task<Dictionary<string, object>> GetDashboardMetadataAsync(string dashboardName)
    {
        var snapshot = await _metricsCollector.GetSnapshotAsync();
        
        return new Dictionary<string, object>
        {
            ["total_requests"] = snapshot.Counters.GetValueOrDefault(ApplicationMetrics.Api.RequestCount)?.Value ?? 0,
            ["active_users"] = snapshot.Gauges.GetValueOrDefault(ApplicationMetrics.Auth.ActiveSessions)?.Value ?? 0,
            ["error_rate"] = CalculateErrorRate(snapshot),
            ["cache_hit_rate"] = CalculateCacheHitRate(snapshot),
            ["avg_response_time"] = snapshot.Histograms.GetValueOrDefault(ApplicationMetrics.Api.RequestDuration)?.Mean ?? 0
        };
    }

    private double CalculateErrorRate(MetricsSnapshot snapshot)
    {
        var totalRequests = snapshot.Counters.GetValueOrDefault(ApplicationMetrics.Api.RequestCount)?.Value ?? 0;
        var errors = snapshot.Counters.GetValueOrDefault(ApplicationMetrics.Api.RequestErrors)?.Value ?? 0;
        
        return totalRequests > 0 ? (errors / totalRequests) * 100 : 0;
    }

    private double CalculateCacheHitRate(MetricsSnapshot snapshot)
    {
        var hits = snapshot.Counters.GetValueOrDefault(ApplicationMetrics.Cache.Hits)?.Value ?? 0;
        var misses = snapshot.Counters.GetValueOrDefault(ApplicationMetrics.Cache.Misses)?.Value ?? 0;
        var total = hits + misses;
        
        return total > 0 ? (hits / total) * 100 : 0;
    }
}