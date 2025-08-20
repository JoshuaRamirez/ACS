using ACS.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ACS.Infrastructure.HealthChecks;

/// <summary>
/// Health check for monitoring individual tenant processes and their health status
/// </summary>
public class TenantHealthCheck : IHealthCheck
{
    private readonly TenantProcessDiscoveryService _tenantDiscoveryService;
    private readonly TenantMetricsService _tenantMetricsService;
    private readonly ILogger<TenantHealthCheck> _logger;
    private readonly TimeSpan _timeout;

    public TenantHealthCheck(
        TenantProcessDiscoveryService tenantDiscoveryService,
        TenantMetricsService tenantMetricsService,
        ILogger<TenantHealthCheck> logger)
    {
        _tenantDiscoveryService = tenantDiscoveryService;
        _tenantMetricsService = tenantMetricsService;
        _logger = logger;
        _timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var healthData = new Dictionary<string, object>();
        var issues = new List<string>();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            // Get all tenant usage data
            var allTenantUsage = _tenantMetricsService.GetAllTenantUsage();
            var tenantHealthStatuses = new ConcurrentDictionary<string, object>();

            // Check each tenant's health
            var tenantCheckTasks = allTenantUsage.Select(async kvp =>
            {
                var tenantId = kvp.Key;
                var usage = kvp.Value;
                
                try
                {
                    var tenantHealth = await CheckIndividualTenantHealthAsync(tenantId, usage, cts.Token);
                    tenantHealthStatuses[tenantId] = tenantHealth;
                    
                    if (tenantHealth is Dictionary<string, object> healthDict && 
                        healthDict.TryGetValue("Issues", out var tenantIssues) && 
                        tenantIssues is List<string> issueList)
                    {
                        lock (issues)
                        {
                            issues.AddRange(issueList.Select(i => $"[{tenantId}] {i}"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to check health for tenant {TenantId}", tenantId);
                    tenantHealthStatuses[tenantId] = new { Error = ex.Message, Status = "Unknown" };
                    lock (issues)
                    {
                        issues.Add($"[{tenantId}] CRITICAL: Health check failed - {ex.Message}");
                    }
                }
            });

            await Task.WhenAll(tenantCheckTasks);

            stopwatch.Stop();

            // Aggregate results
            var totalTenants = tenantHealthStatuses.Count;
            var healthyTenants = tenantHealthStatuses.Count(kvp => 
                kvp.Value is Dictionary<string, object> healthDict && 
                healthDict.TryGetValue("Status", out var status) && 
                status?.ToString() == "Healthy");
            var degradedTenants = tenantHealthStatuses.Count(kvp => 
                kvp.Value is Dictionary<string, object> healthDict && 
                healthDict.TryGetValue("Status", out var status) && 
                status?.ToString() == "Degraded");
            var unhealthyTenants = totalTenants - healthyTenants - degradedTenants;

            healthData["TotalTenants"] = totalTenants;
            healthData["HealthyTenants"] = healthyTenants;
            healthData["DegradedTenants"] = degradedTenants;
            healthData["UnhealthyTenants"] = unhealthyTenants;
            healthData["CheckDuration"] = stopwatch.ElapsedMilliseconds;
            healthData["TenantDetails"] = tenantHealthStatuses.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Determine overall health status
            if (unhealthyTenants > 0)
            {
                var criticalIssues = issues.Count(i => i.Contains("CRITICAL"));
                healthData["CriticalIssues"] = criticalIssues;
                healthData["Issues"] = issues;

                return HealthCheckResult.Unhealthy(
                    $"Tenant health: {unhealthyTenants}/{totalTenants} tenants unhealthy, {criticalIssues} critical issues",
                    null,
                    healthData);
            }

            if (degradedTenants > 0 || issues.Any())
            {
                healthData["Issues"] = issues;
                return HealthCheckResult.Degraded(
                    $"Tenant health: {degradedTenants}/{totalTenants} tenants degraded, {issues.Count} issues",
                    null,
                    healthData);
            }

            return HealthCheckResult.Healthy(
                $"All {totalTenants} tenants are healthy ({stopwatch.ElapsedMilliseconds}ms)",
                healthData);
        }
        catch (OperationCanceledException)
        {
            healthData["TimedOut"] = true;
            _logger.LogWarning("Tenant health check timed out after {Timeout}ms", _timeout.TotalMilliseconds);
            
            return HealthCheckResult.Unhealthy(
                $"Tenant health check timed out after {_timeout.TotalSeconds} seconds",
                null,
                healthData);
        }
        catch (Exception ex)
        {
            healthData["Exception"] = ex.Message;
            _logger.LogError(ex, "Tenant health check failed");
            
            return HealthCheckResult.Unhealthy(
                $"Tenant health check failed: {ex.Message}",
                ex,
                healthData);
        }
    }

    private async Task<Dictionary<string, object>> CheckIndividualTenantHealthAsync(
        string tenantId, 
        TenantUsageData usage, 
        CancellationToken cancellationToken)
    {
        var tenantHealth = new Dictionary<string, object>();
        var tenantIssues = new List<string>();

        try
        {
            // Check tenant process health
            var processInfo = await _tenantDiscoveryService.GetOrStartTenantProcessAsync(tenantId);
            
            tenantHealth["ProcessId"] = processInfo.ProcessId;
            tenantHealth["GrpcEndpoint"] = processInfo.GrpcEndpoint;
            tenantHealth["StartTime"] = processInfo.StartTime;
            tenantHealth["LastHealthCheck"] = processInfo.LastHealthCheck;
            tenantHealth["IsHealthy"] = processInfo.IsHealthy;

            // Check process health
            if (!processInfo.IsHealthy)
            {
                tenantIssues.Add("CRITICAL: Tenant process is unhealthy");
            }

            // Check if process is responsive
            var lastHealthCheck = processInfo.LastHealthCheck;
            var healthCheckAge = DateTime.UtcNow - lastHealthCheck;
            if (healthCheckAge.TotalMinutes > 5)
            {
                tenantIssues.Add($"WARNING: Last health check was {healthCheckAge.TotalMinutes:F1} minutes ago");
            }

            // Check tenant usage metrics
            await CheckTenantUsageMetricsAsync(tenantId, usage, tenantHealth, tenantIssues, cancellationToken);

            // Check tenant resource utilization
            CheckTenantResourceUtilization(usage, tenantHealth, tenantIssues);

            // Check tenant performance metrics
            CheckTenantPerformanceMetrics(usage, tenantHealth, tenantIssues);

            // Determine overall tenant status
            var criticalIssues = tenantIssues.Count(i => i.Contains("CRITICAL"));
            var warningIssues = tenantIssues.Count - criticalIssues;

            if (criticalIssues > 0)
            {
                tenantHealth["Status"] = "Unhealthy";
            }
            else if (warningIssues > 0)
            {
                tenantHealth["Status"] = "Degraded";
            }
            else
            {
                tenantHealth["Status"] = "Healthy";
            }

            tenantHealth["CriticalIssues"] = criticalIssues;
            tenantHealth["WarningIssues"] = warningIssues;
            tenantHealth["Issues"] = tenantIssues;
            tenantHealth["LastUpdated"] = usage.LastUpdated;

            return tenantHealth;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check individual tenant health for {TenantId}", tenantId);
            tenantIssues.Add($"CRITICAL: Tenant health check failed - {ex.Message}");
            
            tenantHealth["Status"] = "Unknown";
            tenantHealth["Error"] = ex.Message;
            tenantHealth["Issues"] = tenantIssues;
            
            return tenantHealth;
        }
    }

    private async Task CheckTenantUsageMetricsAsync(
        string tenantId,
        TenantUsageData usage,
        Dictionary<string, object> tenantHealth,
        List<string> issues,
        CancellationToken cancellationToken)
    {
        var usageMetrics = new Dictionary<string, object>
        {
            ["ActiveUsers"] = usage.ActiveUsers,
            ["PermissionChecks"] = usage.PermissionChecks,
            ["RequestCount"] = usage.RequestCount,
            ["StorageUsedMB"] = Math.Round(usage.StorageUsedBytes / 1024.0 / 1024.0, 2),
            ["AvgResponseTimeMs"] = usage.RequestCount > 0 ? 
                Math.Round(usage.TotalResponseTime / usage.RequestCount, 2) : 0
        };

        // Check for inactive tenants
        var timeSinceLastUpdate = DateTime.UtcNow - usage.LastUpdated;
        if (timeSinceLastUpdate.TotalHours > 24)
        {
            issues.Add($"WARNING: Tenant has been inactive for {timeSinceLastUpdate.TotalHours:F1} hours");
            usageMetrics["InactiveHours"] = Math.Round(timeSinceLastUpdate.TotalHours, 1);
        }

        // Check for no recent activity
        if (usage.RequestCount == 0 && timeSinceLastUpdate.TotalHours > 1)
        {
            issues.Add("WARNING: No requests processed in the last hour");
        }

        tenantHealth["UsageMetrics"] = usageMetrics;

        await Task.CompletedTask;
    }

    private void CheckTenantResourceUtilization(
        TenantUsageData usage,
        Dictionary<string, object> tenantHealth,
        List<string> issues)
    {
        var resourceMetrics = new Dictionary<string, object>();

        // Check storage usage (assuming limits)
        var storageUsedGB = usage.StorageUsedBytes / 1024.0 / 1024.0 / 1024.0;
        var storageWarningThresholdGB = 5.0; // 5GB warning
        var storageCriticalThresholdGB = 10.0; // 10GB critical

        resourceMetrics["StorageUsedGB"] = Math.Round(storageUsedGB, 3);

        if (storageUsedGB > storageCriticalThresholdGB)
        {
            issues.Add($"CRITICAL: Storage usage is very high: {storageUsedGB:F2}GB");
        }
        else if (storageUsedGB > storageWarningThresholdGB)
        {
            issues.Add($"WARNING: Storage usage is high: {storageUsedGB:F2}GB");
        }

        // Check permission check volume
        if (usage.PermissionChecks > 100000) // Very high volume
        {
            issues.Add($"WARNING: Very high permission check volume: {usage.PermissionChecks:N0}");
        }

        resourceMetrics["PermissionCheckVolume"] = usage.PermissionChecks;
        tenantHealth["ResourceUtilization"] = resourceMetrics;
    }

    private void CheckTenantPerformanceMetrics(
        TenantUsageData usage,
        Dictionary<string, object> tenantHealth,
        List<string> issues)
    {
        var performanceMetrics = new Dictionary<string, object>();

        // Calculate average response time
        var avgResponseTime = usage.RequestCount > 0 ? usage.TotalResponseTime / usage.RequestCount : 0;
        
        performanceMetrics["AvgResponseTimeMs"] = Math.Round(avgResponseTime, 2);
        performanceMetrics["RequestCount"] = usage.RequestCount;

        // Check performance thresholds
        if (avgResponseTime > 2000) // > 2 seconds
        {
            issues.Add($"CRITICAL: Average response time is very slow: {avgResponseTime:F0}ms");
        }
        else if (avgResponseTime > 1000) // > 1 second
        {
            issues.Add($"WARNING: Average response time is slow: {avgResponseTime:F0}ms");
        }

        // Check request volume patterns
        var requestRate = usage.RequestCount / Math.Max(1, (DateTime.UtcNow - usage.LastUpdated).TotalHours);
        performanceMetrics["RequestsPerHour"] = Math.Round(requestRate, 1);

        // Unusually high request rate could indicate issues
        if (requestRate > 10000) // > 10K requests/hour
        {
            issues.Add($"WARNING: Very high request rate: {requestRate:F0} requests/hour");
        }

        tenantHealth["PerformanceMetrics"] = performanceMetrics;
    }
}

/// <summary>
/// Extension methods for registering tenant health check
/// </summary>
public static class TenantHealthCheckExtensions
{
    public static IHealthChecksBuilder AddTenantHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "tenants",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.AddTypeActivatedCheck<TenantHealthCheck>(
            name,
            failureStatus ?? HealthStatus.Degraded,
            tags ?? new[] { "tenants", "processes", "multi-tenant" },
            timeout ?? TimeSpan.FromSeconds(10));
    }
}