using ACS.Infrastructure.Telemetry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ACS.Infrastructure.Services;

/// <summary>
/// Service for collecting and reporting tenant-specific metrics and usage data
/// </summary>
public class TenantMetricsService : BackgroundService // TEMPORARILY DISABLED metric methods due to OpenTelemetry API issues
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TenantMetricsService> _logger;
    
    // Metrics tracking
    private readonly ConcurrentDictionary<string, TenantUsageData> _tenantUsage = new();
    private readonly ObservableGauge<long> _activeTenants;
    private readonly ObservableGauge<long> _activeUsers;
    private readonly ObservableGauge<long> _totalPermissionChecks;
    private readonly ObservableGauge<double> _averageResponseTime;
    
    // Collection intervals
    private readonly TimeSpan _collectionInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _reportingInterval = TimeSpan.FromMinutes(5);
    
    public TenantMetricsService(IServiceProvider serviceProvider, ILogger<TenantMetricsService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Create observable gauges
        _activeTenants = OpenTelemetryConfiguration.ServiceMeter.CreateObservableGauge<long>(
            "acs_active_tenants_gauge", 
            () => new Measurement<long>(_tenantUsage.Count));
            
        _activeUsers = OpenTelemetryConfiguration.ServiceMeter.CreateObservableGauge<long>(
            "acs_active_users_gauge", 
            () => new Measurement<long>(_tenantUsage.Values.Sum(t => t.ActiveUsers)));
            
        _totalPermissionChecks = OpenTelemetryConfiguration.ServiceMeter.CreateObservableGauge<long>(
            "acs_permission_checks_gauge", 
            () => new Measurement<long>(_tenantUsage.Values.Sum(t => t.PermissionChecks)));
            
        _averageResponseTime = OpenTelemetryConfiguration.ServiceMeter.CreateObservableGauge<double>(
            "acs_average_response_time_gauge", 
            () => 
            {
                var allResponseTimes = _tenantUsage.Values.Where(t => t.RequestCount > 0).ToList();
                if (!allResponseTimes.Any()) return new Measurement<double>(0);
                
                var weightedSum = allResponseTimes.Sum(t => t.TotalResponseTime * t.RequestCount);
                var totalRequests = allResponseTimes.Sum(t => t.RequestCount);
                var average = totalRequests > 0 ? weightedSum / totalRequests : 0;
                
                return new Measurement<double>(average);
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting tenant metrics collection service");

        var lastReportTime = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Collect current metrics
                await CollectTenantMetricsAsync();

                // Report detailed metrics every reporting interval
                if (DateTime.UtcNow - lastReportTime >= _reportingInterval)
                {
                    await ReportDetailedMetricsAsync();
                    lastReportTime = DateTime.UtcNow;
                }

                await Task.Delay(_collectionInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Tenant metrics collection cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tenant metrics collection");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task CollectTenantMetricsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            // In a real implementation, you would inject your data context here
            // For now, we'll simulate data collection
            
            await SimulateMetricsCollectionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting tenant metrics");
        }
    }

    private async Task SimulateMetricsCollectionAsync()
    {
        // This would typically query your database for actual metrics
        // For demonstration purposes, we'll simulate some data
        
        var random = new Random();
        var tenantIds = new[] { "tenant1", "tenant2", "tenant3", "enterprise-client" };
        
        foreach (var tenantId in tenantIds)
        {
            var usage = _tenantUsage.GetOrAdd(tenantId, _ => new TenantUsageData
            {
                TenantId = tenantId,
                LastUpdated = DateTime.UtcNow
            });

            // Simulate realistic usage patterns
            usage.ActiveUsers = random.Next(5, 100);
            usage.PermissionChecks += random.Next(10, 500);
            usage.RequestCount += random.Next(20, 200);
            usage.TotalResponseTime += random.NextDouble() * 10; // Average response time
            usage.StorageUsedBytes += random.Next(1000, 10000);
            usage.LastUpdated = DateTime.UtcNow;

            // Record tenant-specific metrics
            RecordTenantSpecificMetrics(usage);
        }
        
        await Task.CompletedTask;
    }

    private void RecordTenantSpecificMetrics(TenantUsageData usage)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("tenant.id", usage.TenantId)
        };

        // Record per-tenant gauges
        OpenTelemetryConfiguration.ServiceMeter.CreateObservableGauge<long>(
            "acs_tenant_active_users",
            () => new Measurement<long>(usage.ActiveUsers, tags));

        OpenTelemetryConfiguration.ServiceMeter.CreateObservableGauge<long>(
            "acs_tenant_permission_checks",
            () => new Measurement<long>(usage.PermissionChecks, tags));

        OpenTelemetryConfiguration.ServiceMeter.CreateObservableGauge<long>(
            "acs_tenant_storage_bytes",
            () => new Measurement<long>(usage.StorageUsedBytes, tags));

        OpenTelemetryConfiguration.ServiceMeter.CreateObservableGauge<double>(
            "acs_tenant_avg_response_time",
            () => new Measurement<double>(
                usage.RequestCount > 0 ? usage.TotalResponseTime / usage.RequestCount : 0, tags));
    }

    private async Task ReportDetailedMetricsAsync()
    {
        _logger.LogInformation("Reporting detailed tenant metrics for {TenantCount} tenants", _tenantUsage.Count);

        foreach (var (tenantId, usage) in _tenantUsage)
        {
            var avgResponseTime = usage.RequestCount > 0 ? usage.TotalResponseTime / usage.RequestCount : 0;
            
            _logger.LogInformation(
                "Tenant {TenantId}: Users={ActiveUsers}, Requests={RequestCount}, " +
                "PermissionChecks={PermissionChecks}, AvgResponseTime={AvgResponseTime:F2}ms, " +
                "Storage={StorageUsedMB:F2}MB",
                tenantId,
                usage.ActiveUsers,
                usage.RequestCount,
                usage.PermissionChecks,
                avgResponseTime,
                usage.StorageUsedBytes / 1024.0 / 1024.0);

            // Record business KPIs
            await RecordBusinessKPIsAsync(usage);
        }

        // Clean up old data
        CleanupOldMetrics();
    }

    private async Task RecordBusinessKPIsAsync(TenantUsageData usage)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("tenant.id", usage.TenantId),
            new KeyValuePair<string, object?>("collection_time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
        };

        // Calculate and record KPIs
        var avgResponseTime = usage.RequestCount > 0 ? usage.TotalResponseTime / usage.RequestCount : 0;
        
        // SLA compliance (assuming SLA is < 500ms average response time)
        var slaCompliance = avgResponseTime < 500;
        OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_sla_compliance_total", "SLA compliance events")
            .Add(1, tags.Concat(new[] { new KeyValuePair<string, object?>("compliant", slaCompliance) }).ToArray());

        // Resource utilization alerts
        if (usage.StorageUsedBytes > 1_000_000_000) // > 1GB
        {
            _logger.LogWarning("High storage usage for tenant {TenantId}: {StorageGB:F2}GB", 
                usage.TenantId, usage.StorageUsedBytes / 1024.0 / 1024.0 / 1024.0);
                
            OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_resource_alerts_total", "Resource usage alerts")
                .Add(1, tags.Concat(new[] { new KeyValuePair<string, object?>("alert_type", "high_storage") }).ToArray());
        }

        // High permission check volume
        if (usage.PermissionChecks > 10000)
        {
            _logger.LogInformation("High permission check volume for tenant {TenantId}: {PermissionChecks}", 
                usage.TenantId, usage.PermissionChecks);
                
            OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_resource_alerts_total", "Resource usage alerts")
                .Add(1, tags.Concat(new[] { new KeyValuePair<string, object?>("alert_type", "high_permission_volume") }).ToArray());
        }

        await Task.CompletedTask;
    }

    private void CleanupOldMetrics()
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-24);
        var staleEntries = _tenantUsage.Where(kvp => kvp.Value.LastUpdated < cutoffTime).ToList();
        
        foreach (var (tenantId, _) in staleEntries)
        {
            _tenantUsage.TryRemove(tenantId, out _);
            _logger.LogDebug("Cleaned up stale metrics for tenant {TenantId}", tenantId);
        }
    }

    public void RecordTenantActivity(string tenantId, string activityType, double duration = 0)
    {
        if (string.IsNullOrEmpty(tenantId)) return;

        var usage = _tenantUsage.GetOrAdd(tenantId, _ => new TenantUsageData
        {
            TenantId = tenantId,
            LastUpdated = DateTime.UtcNow
        });

        usage.LastUpdated = DateTime.UtcNow;

        switch (activityType.ToLowerInvariant())
        {
            case "request":
                usage.RequestCount++;
                if (duration > 0) usage.TotalResponseTime += duration;
                break;
            case "permission_check":
                usage.PermissionChecks++;
                break;
            case "user_activity":
                // Could track unique users here
                break;
        }
    }

    public TenantUsageData? GetTenantUsage(string tenantId)
    {
        return _tenantUsage.TryGetValue(tenantId, out var usage) ? usage : null;
    }

    public IReadOnlyDictionary<string, TenantUsageData> GetAllTenantUsage()
    {
        return new Dictionary<string, TenantUsageData>(_tenantUsage);
    }
}

public class TenantUsageData
{
    public string TenantId { get; set; } = string.Empty;
    public long ActiveUsers { get; set; }
    public long PermissionChecks { get; set; }
    public long RequestCount { get; set; }
    public double TotalResponseTime { get; set; }
    public long StorageUsedBytes { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Extension methods for registering tenant metrics service
/// </summary>
public static class TenantMetricsServiceExtensions
{
    public static IServiceCollection AddTenantMetrics(this IServiceCollection services)
    {
        services.AddSingleton<TenantMetricsService>();
        services.AddHostedService<TenantMetricsService>(provider => provider.GetRequiredService<TenantMetricsService>());
        return services;
    }
}