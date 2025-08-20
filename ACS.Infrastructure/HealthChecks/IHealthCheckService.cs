using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ACS.Infrastructure.HealthChecks;

/// <summary>
/// Service for managing health checks
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Performs a comprehensive health check
    /// </summary>
    Task<HealthReport> CheckHealthAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks specific component health
    /// </summary>
    Task<HealthCheckResult> CheckComponentAsync(string componentName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets health check history
    /// </summary>
    Task<IEnumerable<HealthCheckHistory>> GetHistoryAsync(string? componentName = null, TimeSpan? period = null);
    
    /// <summary>
    /// Registers a custom health check
    /// </summary>
    void RegisterHealthCheck(string name, IHealthCheck healthCheck, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null);
    
    /// <summary>
    /// Gets current system metrics
    /// </summary>
    Task<SystemMetrics> GetSystemMetricsAsync();
}

/// <summary>
/// Health check history entry
/// </summary>
public class HealthCheckHistory
{
    public string ComponentName { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// System metrics
/// </summary>
public class SystemMetrics
{
    public double CpuUsagePercentage { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long MemoryAvailableBytes { get; set; }
    public double MemoryUsagePercentage { get; set; }
    public long DiskUsedBytes { get; set; }
    public long DiskAvailableBytes { get; set; }
    public double DiskUsagePercentage { get; set; }
    public int ActiveConnections { get; set; }
    public int ThreadCount { get; set; }
    public TimeSpan Uptime { get; set; }
    public DateTime Timestamp { get; set; }
}