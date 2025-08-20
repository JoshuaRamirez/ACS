using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ACS.Alerting;

/// <summary>
/// Service that monitors health checks and generates alerts for failures
/// </summary>
public class HealthCheckAlertingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthCheckAlertingService> _logger;
    private readonly HealthCheckAlertingOptions _options;
    private readonly ConcurrentDictionary<string, HealthCheckAlertState> _healthCheckStates;
    private readonly Timer _cleanupTimer;

    public HealthCheckAlertingService(
        IServiceProvider serviceProvider,
        ILogger<HealthCheckAlertingService> logger,
        IOptions<HealthCheckAlertingOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _healthCheckStates = new ConcurrentDictionary<string, HealthCheckAlertState>();
        
        // Setup cleanup timer to remove old alert states
        _cleanupTimer = new Timer(CleanupOldStates, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health Check Alerting Service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckHealthAndGenerateAlerts(stoppingToken);
                await Task.Delay(_options.CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check monitoring");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        
        _logger.LogInformation("Health Check Alerting Service stopped");
    }

    private async Task CheckHealthAndGenerateAlerts(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var healthCheckService = scope.ServiceProvider.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
            
            if (healthCheckService == null)
            {
                _logger.LogDebug("HealthCheckService not available, skipping health check monitoring");
                return;
            }

            var healthReport = await healthCheckService.CheckHealthAsync(cancellationToken);
            
            _logger.LogDebug("Health check completed. Overall status: {Status}, Checked {Count} services", 
                healthReport.Status, healthReport.Entries.Count);

            // Process each health check entry
            foreach (var entry in healthReport.Entries)
            {
                await ProcessHealthCheckEntry(entry.Key, entry.Value, cancellationToken);
            }
            
            // Check overall system health
            await ProcessOverallHealth(healthReport, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health status");
        }
    }

    private async Task ProcessHealthCheckEntry(string checkName, HealthReportEntry entry, CancellationToken cancellationToken)
    {
        var currentState = _healthCheckStates.GetOrAdd(checkName, _ => new HealthCheckAlertState
        {
            CheckName = checkName,
            LastStatus = HealthStatus.Healthy,
            LastChecked = DateTime.UtcNow,
            ConsecutiveFailures = 0
        });

        var previousStatus = currentState.LastStatus;
        currentState.LastStatus = entry.Status;
        currentState.LastChecked = DateTime.UtcNow;

        // Update failure count
        if (entry.Status != HealthStatus.Healthy)
        {
            currentState.ConsecutiveFailures++;
            currentState.FirstFailureTime ??= DateTime.UtcNow;
        }
        else
        {
            currentState.ConsecutiveFailures = 0;
            currentState.FirstFailureTime = null;
        }

        // Determine if we should generate an alert
        var shouldAlert = ShouldGenerateAlert(checkName, previousStatus, entry.Status, currentState);
        
        if (shouldAlert)
        {
            await GenerateHealthCheckAlert(checkName, entry, currentState, cancellationToken);
        }
        
        // Check if we should send a recovery alert
        if (previousStatus != HealthStatus.Healthy && entry.Status == HealthStatus.Healthy)
        {
            await GenerateRecoveryAlert(checkName, entry, currentState, cancellationToken);
        }
    }

    private async Task ProcessOverallHealth(HealthReport healthReport, CancellationToken cancellationToken)
    {
        const string overallCheckName = "overall_system_health";
        
        var currentState = _healthCheckStates.GetOrAdd(overallCheckName, _ => new HealthCheckAlertState
        {
            CheckName = overallCheckName,
            LastStatus = HealthStatus.Healthy,
            LastChecked = DateTime.UtcNow,
            ConsecutiveFailures = 0
        });

        var previousStatus = currentState.LastStatus;
        currentState.LastStatus = healthReport.Status;
        currentState.LastChecked = DateTime.UtcNow;

        if (healthReport.Status != HealthStatus.Healthy)
        {
            currentState.ConsecutiveFailures++;
            currentState.FirstFailureTime ??= DateTime.UtcNow;
        }
        else
        {
            currentState.ConsecutiveFailures = 0;
            currentState.FirstFailureTime = null;
        }

        var shouldAlert = ShouldGenerateAlert(overallCheckName, previousStatus, healthReport.Status, currentState);
        
        if (shouldAlert)
        {
            await GenerateOverallHealthAlert(healthReport, currentState, cancellationToken);
        }
    }

    private bool ShouldGenerateAlert(string checkName, HealthStatus previousStatus, HealthStatus currentStatus, HealthCheckAlertState state)
    {
        // Don't alert if disabled for this check
        if (_options.DisabledChecks.Contains(checkName))
            return false;

        // Status changed from healthy to unhealthy/degraded
        if (previousStatus == HealthStatus.Healthy && currentStatus != HealthStatus.Healthy)
            return true;

        // Status degraded to unhealthy from degraded
        if (previousStatus == HealthStatus.Degraded && currentStatus == HealthStatus.Unhealthy)
            return true;

        // Consecutive failures threshold reached
        if (currentStatus != HealthStatus.Healthy && 
            state.ConsecutiveFailures >= _options.ConsecutiveFailureThreshold &&
            state.ConsecutiveFailures % _options.ConsecutiveFailureThreshold == 0)
            return true;

        return false;
    }

    private async Task GenerateHealthCheckAlert(string checkName, HealthReportEntry entry, HealthCheckAlertState state, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var alertingService = scope.ServiceProvider.GetService<IAlertingService>();
            
            if (alertingService == null)
                return;

            var severity = MapHealthStatusToAlertSeverity(entry.Status);
            var category = DetermineAlertCategory(checkName, entry.Tags);
            
            var alertRequest = new AlertRequest
            {
                Title = $"Health Check Failed: {checkName}",
                Message = BuildHealthCheckAlertMessage(checkName, entry, state),
                Severity = severity,
                Category = category,
                Source = "HealthCheck",
                Tags = new List<string> { "health-check", checkName }.Concat(entry.Tags ?? []).ToList(),
                Metadata = new Dictionary<string, object>
                {
                    ["CheckName"] = checkName,
                    ["HealthStatus"] = entry.Status.ToString(),
                    ["Duration"] = entry.Duration.TotalMilliseconds,
                    ["ConsecutiveFailures"] = state.ConsecutiveFailures,
                    ["FirstFailureTime"] = state.FirstFailureTime?.ToString("O") ?? string.Empty,
                    ["Exception"] = entry.Exception?.Message ?? string.Empty,
                    ["Tags"] = string.Join(", ", entry.Tags ?? [])
                }
            };

            await alertingService.SendAlertAsync(alertRequest, cancellationToken);
            
            _logger.LogInformation("Generated alert for health check failure: {CheckName} (Status: {Status})", 
                checkName, entry.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating health check alert for {CheckName}", checkName);
        }
    }

    private async Task GenerateRecoveryAlert(string checkName, HealthReportEntry entry, HealthCheckAlertState state, CancellationToken cancellationToken)
    {
        if (!_options.SendRecoveryAlerts)
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var alertingService = scope.ServiceProvider.GetService<IAlertingService>();
            
            if (alertingService == null)
                return;

            var alertRequest = new AlertRequest
            {
                Title = $"Health Check Recovered: {checkName}",
                Message = $"Health check '{checkName}' has recovered and is now healthy.\n\n" +
                         $"Recovery Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}\n" +
                         $"Duration of Outage: {(state.FirstFailureTime.HasValue ? DateTime.UtcNow - state.FirstFailureTime.Value : TimeSpan.Zero):hh\\:mm\\:ss}",
                Severity = AlertSeverity.Info,
                Category = AlertCategory.SystemHealth,
                Source = "HealthCheck",
                Tags = new List<string> { "health-check", "recovery", checkName }.Concat(entry.Tags ?? []).ToList(),
                Metadata = new Dictionary<string, object>
                {
                    ["CheckName"] = checkName,
                    ["HealthStatus"] = "Healthy",
                    ["RecoveryTime"] = DateTime.UtcNow.ToString("O"),
                    ["OutageDurationMinutes"] = state.FirstFailureTime.HasValue ? 
                        (DateTime.UtcNow - state.FirstFailureTime.Value).TotalMinutes : 0
                }
            };

            await alertingService.SendAlertAsync(alertRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recovery alert for {CheckName}", checkName);
        }
    }

    private async Task GenerateOverallHealthAlert(HealthReport healthReport, HealthCheckAlertState state, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var alertingService = scope.ServiceProvider.GetService<IAlertingService>();
            
            if (alertingService == null)
                return;

            var failedChecks = healthReport.Entries
                .Where(e => e.Value.Status != HealthStatus.Healthy)
                .ToList();

            var alertRequest = new AlertRequest
            {
                Title = $"System Health Alert: {healthReport.Status}",
                Message = $"Overall system health is {healthReport.Status}\n\n" +
                         $"Failed Checks ({failedChecks.Count}):\n" +
                         string.Join("\n", failedChecks.Select(c => $"- {c.Key}: {c.Value.Status}")),
                Severity = MapHealthStatusToAlertSeverity(healthReport.Status),
                Category = AlertCategory.SystemHealth,
                Source = "HealthCheck",
                Tags = new List<string> { "health-check", "system", "overall" },
                Metadata = new Dictionary<string, object>
                {
                    ["OverallStatus"] = healthReport.Status.ToString(),
                    ["TotalChecks"] = healthReport.Entries.Count,
                    ["FailedChecks"] = failedChecks.Count,
                    ["HealthyChecks"] = healthReport.Entries.Count(e => e.Value.Status == HealthStatus.Healthy),
                    ["DegradedChecks"] = healthReport.Entries.Count(e => e.Value.Status == HealthStatus.Degraded),
                    ["UnhealthyChecks"] = healthReport.Entries.Count(e => e.Value.Status == HealthStatus.Unhealthy),
                    ["TotalDuration"] = healthReport.TotalDuration.TotalMilliseconds
                }
            };

            await alertingService.SendAlertAsync(alertRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating overall health alert");
        }
    }

    private static string BuildHealthCheckAlertMessage(string checkName, HealthReportEntry entry, HealthCheckAlertState state)
    {
        var message = $"Health check '{checkName}' failed with status: {entry.Status}\n\n";
        
        if (!string.IsNullOrEmpty(entry.Description))
            message += $"Description: {entry.Description}\n";
        
        message += $"Duration: {entry.Duration.TotalMilliseconds:F2}ms\n";
        message += $"Consecutive Failures: {state.ConsecutiveFailures}\n";
        
        if (state.FirstFailureTime.HasValue)
            message += $"First Failure: {state.FirstFailureTime.Value:yyyy-MM-dd HH:mm:ss UTC}\n";
        
        if (entry.Exception != null)
            message += $"\nException: {entry.Exception.Message}";
        
        if (entry.Tags?.Any() == true)
            message += $"\nTags: {string.Join(", ", entry.Tags)}";

        return message;
    }

    private static AlertSeverity MapHealthStatusToAlertSeverity(HealthStatus status) => status switch
    {
        HealthStatus.Unhealthy => AlertSeverity.Critical,
        HealthStatus.Degraded => AlertSeverity.Warning,
        HealthStatus.Healthy => AlertSeverity.Info,
        _ => AlertSeverity.Warning
    };

    private static AlertCategory DetermineAlertCategory(string checkName, IEnumerable<string>? tags)
    {
        var tagList = tags?.ToList() ?? [];
        
        if (tagList.Contains("database") || tagList.Contains("sql"))
            return AlertCategory.Database;
        
        if (tagList.Contains("cache") || tagList.Contains("redis"))
            return AlertCategory.Infrastructure;
        
        if (tagList.Contains("external"))
            return AlertCategory.Network;
        
        if (tagList.Contains("business") || tagList.Contains("domain"))
            return AlertCategory.Application;
        
        if (tagList.Contains("infrastructure") || tagList.Contains("disk") || tagList.Contains("memory"))
            return AlertCategory.Infrastructure;
        
        return AlertCategory.SystemHealth;
    }

    private void CleanupOldStates(object? state)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24); // Remove states older than 24 hours
            var keysToRemove = _healthCheckStates
                .Where(kvp => kvp.Value.LastChecked < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _healthCheckStates.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} old health check states", keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check state cleanup");
        }
    }

    public override void Dispose()
    {
        _cleanupTimer?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Tracks the state of a health check for alerting purposes
/// </summary>
public class HealthCheckAlertState
{
    public string CheckName { get; set; } = string.Empty;
    public HealthStatus LastStatus { get; set; }
    public DateTime LastChecked { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? FirstFailureTime { get; set; }
}

/// <summary>
/// Configuration options for health check alerting
/// </summary>
public class HealthCheckAlertingOptions
{
    public const string SectionName = "HealthCheckAlerting";
    
    /// <summary>
    /// How often to check health status
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// Number of consecutive failures before generating alert
    /// </summary>
    public int ConsecutiveFailureThreshold { get; set; } = 1;
    
    /// <summary>
    /// Whether to send recovery alerts when services become healthy
    /// </summary>
    public bool SendRecoveryAlerts { get; set; } = true;
    
    /// <summary>
    /// Health checks that should not generate alerts
    /// </summary>
    public List<string> DisabledChecks { get; set; } = new();
}