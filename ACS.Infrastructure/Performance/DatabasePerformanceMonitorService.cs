using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Background service for monitoring database performance
/// </summary>
public class DatabasePerformanceMonitorService : BackgroundService
{
    private readonly IConnectionPoolMonitor _connectionPoolMonitor;
    private readonly ILogger<DatabasePerformanceMonitorService> _logger;
    private readonly TimeSpan _monitoringInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(1);

    public DatabasePerformanceMonitorService(
        IConnectionPoolMonitor connectionPoolMonitor,
        ILogger<DatabasePerformanceMonitorService> logger)
    {
        _connectionPoolMonitor = connectionPoolMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Database performance monitoring service started");

        var monitoringTask = MonitorPerformanceAsync(stoppingToken);
        var healthCheckTask = CheckHealthAsync(stoppingToken);

        await Task.WhenAll(monitoringTask, healthCheckTask);
    }

    private async Task MonitorPerformanceAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var stats = await _connectionPoolMonitor.GetStatisticsAsync();
                
                _logger.LogInformation(
                    "Connection pool stats: Active={Active}, Idle={Idle}, Total={Total}, Waiting={Waiting}",
                    stats.ActiveConnections,
                    stats.IdleConnections,
                    stats.TotalConnections,
                    stats.WaitingRequests);

                // Check for performance issues
                if (stats.TotalConnections >= stats.MaxPoolSize * 0.9)
                {
                    _logger.LogWarning(
                        "Connection pool near capacity: {Total}/{Max} connections in use",
                        stats.TotalConnections,
                        stats.MaxPoolSize);
                }

                if (stats.WaitingRequests > 0)
                {
                    _logger.LogWarning(
                        "Connection pool has {Waiting} waiting requests with average wait time of {WaitTime}ms",
                        stats.WaitingRequests,
                        stats.AverageWaitTime.TotalMilliseconds);
                }

                if (stats.FailedRequests > stats.TotalRequests * 0.01 && stats.TotalRequests > 100)
                {
                    _logger.LogWarning(
                        "High connection failure rate: {Failed}/{Total} ({Percentage:P})",
                        stats.FailedRequests,
                        stats.TotalRequests,
                        (double)stats.FailedRequests / stats.TotalRequests);
                }

                // Clear idle connections if needed
                if (stats.IdleConnections > stats.MaxPoolSize * 0.5)
                {
                    await _connectionPoolMonitor.ClearIdleConnectionsAsync(TimeSpan.FromMinutes(10));
                    _logger.LogDebug("Cleared idle connections from pool");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring database performance");
            }

            await Task.Delay(_monitoringInterval, stoppingToken);
        }
    }

    private async Task CheckHealthAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var health = await _connectionPoolMonitor.GetHealthAsync();
                
                if (!health.IsHealthy)
                {
                    _logger.LogWarning(
                        "Database connection pool health check failed: {Issues}",
                        string.Join(", ", health.Issues));
                    
                    // If utilization is very high, consider resetting the pool
                    if (health.UtilizationPercentage > 95)
                    {
                        _logger.LogWarning("Resetting connection pool due to high utilization");
                        await _connectionPoolMonitor.ResetPoolAsync();
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "Database health check passed. Utilization: {Utilization:F1}%",
                        health.UtilizationPercentage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database health");
            }

            await Task.Delay(_healthCheckInterval, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database performance monitoring service stopping");
        await base.StopAsync(cancellationToken);
    }
}