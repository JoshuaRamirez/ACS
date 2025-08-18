using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Net.Sockets;
using System.Net.Http;
using ACS.Service.Infrastructure;

namespace ACS.Service.Services;

/// <summary>
/// Health monitoring service that tracks system health metrics,
/// monitors error rates, and provides health check endpoints
/// </summary>
public class HealthMonitoringService : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("ACS.HealthMonitoring");
    
    private readonly ILogger<HealthMonitoringService> _logger;
    private readonly ErrorRecoveryService _errorRecoveryService;
    private readonly string _tenantId;
    
    // Health metrics tracking
    private readonly ConcurrentDictionary<string, HealthMetrics> _healthMetrics = new();
    private readonly Timer _metricsCollectionTimer;
    
    // Thresholds for health status
    private const double ErrorRateWarningThreshold = 0.10; // 10% error rate
    private const double ErrorRateCriticalThreshold = 0.25; // 25% error rate
    private const int MinSampleSize = 10; // Minimum operations before calculating rates
    
    public enum HealthStatus { Healthy, Warning, Critical, Unknown }
    
    public HealthMonitoringService(
        ILogger<HealthMonitoringService> logger,
        ErrorRecoveryService errorRecoveryService,
        TenantConfiguration tenantConfig)
    {
        _logger = logger;
        _errorRecoveryService = errorRecoveryService;
        _tenantId = tenantConfig.TenantId;
        
        // Initialize metrics collection timer (every 30 seconds)
        _metricsCollectionTimer = new Timer(CollectMetrics, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        _logger.LogInformation("Health Monitoring Service initialized for tenant {TenantId}", _tenantId);
    }
    
    /// <summary>
    /// Records a successful operation for health tracking
    /// </summary>
    public void RecordSuccess(string operationType, TimeSpan duration)
    {
        var metrics = GetOrCreateMetrics(operationType);
        metrics.RecordSuccess(duration);
        
        using var activity = ActivitySource.StartActivity("health.record_success");
        activity?.SetTag("tenant.id", _tenantId);
        activity?.SetTag("operation.type", operationType);
        activity?.SetTag("operation.duration_ms", duration.TotalMilliseconds);
    }
    
    /// <summary>
    /// Records a failed operation for health tracking
    /// </summary>
    public void RecordFailure(string operationType, Exception exception, TimeSpan duration)
    {
        var metrics = GetOrCreateMetrics(operationType);
        metrics.RecordFailure(exception, duration);
        
        using var activity = ActivitySource.StartActivity("health.record_failure");
        activity?.SetTag("tenant.id", _tenantId);
        activity?.SetTag("operation.type", operationType);
        activity?.SetTag("operation.duration_ms", duration.TotalMilliseconds);
        activity?.SetTag("error.type", exception.GetType().Name);
        activity?.SetTag("error.message", exception.Message);
    }
    
    /// <summary>
    /// Record a batch operation completion
    /// </summary>
    public void RecordBatchOperation(string operationType, int successCount, int failureCount, TimeSpan duration)
    {
        using var activity = ActivitySource.StartActivity("health.batch_operation");
        activity?.SetTag("operation.type", operationType);
        activity?.SetTag("batch.success_count", successCount);
        activity?.SetTag("batch.failure_count", failureCount);
        activity?.SetTag("batch.duration_ms", duration.TotalMilliseconds);
        activity?.SetTag("tenant.id", _tenantId);
        
        var totalCount = successCount + failureCount;
        var successRate = totalCount > 0 ? (double)successCount / totalCount : 0;
        
        _logger.LogInformation("Batch operation {OperationType} completed: {Success}/{Total} successful ({Rate:P}) in {Duration}ms",
            operationType, successCount, totalCount, successRate, duration.TotalMilliseconds);
        
        // Record as a normal operation for health tracking
        if (successRate >= 0.95)
        {
            RecordSuccess(operationType, duration);
        }
        else if (successRate >= 0.5)
        {
            // Partial success - record as warning
            RecordSuccess(operationType, duration);
            _logger.LogWarning("Batch operation {OperationType} had partial success: {Rate:P}", operationType, successRate);
        }
        else
        {
            // Mostly failed - record as failure
            RecordFailure(operationType, new Exception($"Batch operation failed with {successRate:P} success rate"), duration);
        }
    }
    
    /// <summary>
    /// Gets the current health status for a specific operation type
    /// </summary>
    public HealthStatus GetHealthStatus(string operationType)
    {
        if (!_healthMetrics.TryGetValue(operationType, out var metrics))
        {
            return HealthStatus.Unknown;
        }
        
        return metrics.GetHealthStatus();
    }
    
    /// <summary>
    /// Gets the overall system health status
    /// </summary>
    public HealthStatus GetOverallHealthStatus()
    {
        using var activity = ActivitySource.StartActivity("health.get_overall_status");
        activity?.SetTag("tenant.id", _tenantId);
        
        if (_healthMetrics.IsEmpty)
        {
            return HealthStatus.Unknown;
        }
        
        var statuses = _healthMetrics.Values.Select(m => m.GetHealthStatus()).ToList();
        
        if (statuses.Any(s => s == HealthStatus.Critical))
        {
            activity?.SetTag("health.status", "critical");
            return HealthStatus.Critical;
        }
        
        if (statuses.Any(s => s == HealthStatus.Warning))
        {
            activity?.SetTag("health.status", "warning");
            return HealthStatus.Warning;
        }
        
        if (statuses.All(s => s == HealthStatus.Healthy))
        {
            activity?.SetTag("health.status", "healthy");
            return HealthStatus.Healthy;
        }
        
        activity?.SetTag("health.status", "unknown");
        return HealthStatus.Unknown;
    }
    
    /// <summary>
    /// Gets detailed health report for all operation types
    /// </summary>
    public HealthReport GetDetailedHealthReport()
    {
        using var activity = ActivitySource.StartActivity("health.get_detailed_report");
        activity?.SetTag("tenant.id", _tenantId);
        
        var operationReports = _healthMetrics.ToDictionary(
            kvp => kvp.Key,
            kvp => new OperationHealthReport
            {
                OperationType = kvp.Key,
                Status = kvp.Value.GetHealthStatus(),
                TotalOperations = kvp.Value.TotalOperations,
                SuccessfulOperations = kvp.Value.SuccessfulOperations,
                FailedOperations = kvp.Value.FailedOperations,
                ErrorRate = kvp.Value.GetErrorRate(),
                AverageResponseTime = kvp.Value.GetAverageResponseTime(),
                LastOperationTime = kvp.Value.LastOperationTime,
                CircuitBreakerState = _errorRecoveryService.GetCircuitBreakerState(kvp.Key).State.ToString(),
                RecentErrors = kvp.Value.GetRecentErrors(5)
            });
        
        var report = new HealthReport
        {
            TenantId = _tenantId,
            OverallStatus = GetOverallHealthStatus(),
            Timestamp = DateTime.UtcNow,
            OperationReports = operationReports
        };
        
        activity?.SetTag("health.operations_count", operationReports.Count);
        
        return report;
    }
    
    /// <summary>
    /// Background service execution for continuous health monitoring
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health monitoring started for tenant {TenantId}", _tenantId);
        
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await MonitorSystemHealth(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Health monitoring cancelled for tenant {TenantId}", _tenantId);
        }
    }
    
    private Task MonitorSystemHealth(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("health.monitor_system");
        activity?.SetTag("tenant.id", _tenantId);
        
        try
        {
            var overallStatus = GetOverallHealthStatus();
            activity?.SetTag("health.overall_status", overallStatus.ToString().ToLower());
            
            // Log health status changes
            var previousStatus = GetPreviousHealthStatus();
            if (previousStatus != overallStatus)
            {
                _logger.LogInformation("Health status changed from {PreviousStatus} to {CurrentStatus} for tenant {TenantId}",
                    previousStatus, overallStatus, _tenantId);
                
                SetPreviousHealthStatus(overallStatus);
                
                // Log detailed report on status changes
                if (overallStatus == HealthStatus.Warning || overallStatus == HealthStatus.Critical)
                {
                    var report = GetDetailedHealthReport();
                    var problemAreas = report.OperationReports
                        .Where(kvp => kvp.Value.Status != HealthStatus.Healthy)
                        .Select(kvp => $"{kvp.Key}: {kvp.Value.Status} (Error Rate: {kvp.Value.ErrorRate:P2})")
                        .ToArray();
                    
                    _logger.LogWarning("Health degradation detected for tenant {TenantId}. Problem areas: {ProblemAreas}",
                        _tenantId, string.Join(", ", problemAreas));
                }
            }
            
            // Check for circuit breaker state changes
            foreach (var operationType in _healthMetrics.Keys)
            {
                var circuitBreakerState = _errorRecoveryService.GetCircuitBreakerState(operationType);
                
                // Log circuit breaker state changes
                if (circuitBreakerState.State != CircuitBreakerState.CircuitState.Closed)
                {
                    _logger.LogWarning("Circuit breaker for {OperationType} is {State} (Failures: {FailureCount})",
                        operationType, circuitBreakerState.State, circuitBreakerState.FailureCount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health monitoring for tenant {TenantId}", _tenantId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
        
        return Task.CompletedTask;
    }
    
    private void CollectMetrics(object? state)
    {
        using var activity = ActivitySource.StartActivity("health.collect_metrics");
        activity?.SetTag("tenant.id", _tenantId);
        
        try
        {
            // Clean up old metrics (older than 1 hour)
            var cutoffTime = DateTime.UtcNow - TimeSpan.FromHours(1);
            
            foreach (var metrics in _healthMetrics.Values)
            {
                metrics.CleanupOldData(cutoffTime);
            }
            
            activity?.SetTag("metrics.operation_types", _healthMetrics.Count);
            
            _logger.LogDebug("Metrics collection completed for {OperationTypes} operation types",
                _healthMetrics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during metrics collection for tenant {TenantId}", _tenantId);
        }
    }
    
    private HealthMetrics GetOrCreateMetrics(string operationType)
    {
        return _healthMetrics.GetOrAdd(operationType, _ => new HealthMetrics(operationType));
    }
    
    private HealthStatus _previousHealthStatus = HealthStatus.Unknown;
    private HealthStatus GetPreviousHealthStatus() => _previousHealthStatus;
    private void SetPreviousHealthStatus(HealthStatus status) => _previousHealthStatus = status;
    
    public override void Dispose()
    {
        _metricsCollectionTimer?.Dispose();
        ActivitySource.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Tracks health metrics for a specific operation type
/// </summary>
public class HealthMetrics
{
    private readonly string _operationType;
    private readonly object _lock = new();
    private readonly List<OperationResult> _operations = new();
    
    public string OperationType => _operationType;
    public int TotalOperations { get; private set; }
    public int SuccessfulOperations { get; private set; }
    public int FailedOperations { get; private set; }
    public DateTime LastOperationTime { get; private set; } = DateTime.MinValue;
    
    public HealthMetrics(string operationType)
    {
        _operationType = operationType;
    }
    
    public void RecordSuccess(TimeSpan duration)
    {
        lock (_lock)
        {
            var operation = new OperationResult
            {
                IsSuccess = true,
                Duration = duration,
                Timestamp = DateTime.UtcNow
            };
            
            _operations.Add(operation);
            TotalOperations++;
            SuccessfulOperations++;
            LastOperationTime = operation.Timestamp;
        }
    }
    
    public void RecordFailure(Exception exception, TimeSpan duration)
    {
        lock (_lock)
        {
            var operation = new OperationResult
            {
                IsSuccess = false,
                Duration = duration,
                Exception = exception,
                Timestamp = DateTime.UtcNow
            };
            
            _operations.Add(operation);
            TotalOperations++;
            FailedOperations++;
            LastOperationTime = operation.Timestamp;
        }
    }
    
    public double GetErrorRate()
    {
        lock (_lock)
        {
            if (TotalOperations == 0) return 0.0;
            return (double)FailedOperations / TotalOperations;
        }
    }
    
    public TimeSpan GetAverageResponseTime()
    {
        lock (_lock)
        {
            if (!_operations.Any()) return TimeSpan.Zero;
            
            var totalMs = _operations.Sum(o => o.Duration.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(totalMs / _operations.Count);
        }
    }
    
    public HealthMonitoringService.HealthStatus GetHealthStatus()
    {
        lock (_lock)
        {
            if (TotalOperations < HealthThresholds.MinSampleSize)
            {
                return HealthMonitoringService.HealthStatus.Unknown;
            }
            
            var errorRate = GetErrorRate();
            
            if (errorRate >= HealthThresholds.ErrorRateCriticalThreshold)
            {
                return HealthMonitoringService.HealthStatus.Critical;
            }
            
            if (errorRate >= HealthThresholds.ErrorRateWarningThreshold)
            {
                return HealthMonitoringService.HealthStatus.Warning;
            }
            
            return HealthMonitoringService.HealthStatus.Healthy;
        }
    }
    
    public List<string> GetRecentErrors(int count)
    {
        lock (_lock)
        {
            return _operations
                .Where(o => !o.IsSuccess && o.Exception != null)
                .OrderByDescending(o => o.Timestamp)
                .Take(count)
                .Select(o => $"{o.Exception!.GetType().Name}: {o.Exception.Message}")
                .ToList();
        }
    }
    
    public void CleanupOldData(DateTime cutoffTime)
    {
        lock (_lock)
        {
            var removedCount = _operations.RemoveAll(o => o.Timestamp < cutoffTime);
            
            // Recalculate totals
            TotalOperations = _operations.Count;
            SuccessfulOperations = _operations.Count(o => o.IsSuccess);
            FailedOperations = _operations.Count(o => !o.IsSuccess);
        }
    }
    
    private class OperationResult
    {
        public bool IsSuccess { get; set; }
        public TimeSpan Duration { get; set; }
        public Exception? Exception { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

/// <summary>
/// Comprehensive health report
/// </summary>
public class HealthReport
{
    public string TenantId { get; set; } = null!;
    public HealthMonitoringService.HealthStatus OverallStatus { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, OperationHealthReport> OperationReports { get; set; } = new();
}

/// <summary>
/// Health report for a specific operation type
/// </summary>
public class OperationHealthReport
{
    public string OperationType { get; set; } = null!;
    public HealthMonitoringService.HealthStatus Status { get; set; }
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public double ErrorRate { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public DateTime LastOperationTime { get; set; }
    public string CircuitBreakerState { get; set; } = null!;
    public List<string> RecentErrors { get; set; } = new();
}

// Constants for thresholds
file static class HealthThresholds
{
    public const double ErrorRateWarningThreshold = 0.10; // 10%
    public const double ErrorRateCriticalThreshold = 0.25; // 25%
    public const int MinSampleSize = 10;
}