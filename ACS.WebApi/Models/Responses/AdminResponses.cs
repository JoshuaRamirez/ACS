namespace ACS.WebApi.Models.Responses;

#region System Configuration

/// <summary>
/// Response model for system configuration
/// </summary>
public class SystemConfigurationResponse
{
    /// <summary>
    /// Configuration section
    /// </summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>
    /// Configuration settings
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// When the configuration was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Configuration version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Whether the configuration is in read-only mode
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Configuration schema information
    /// </summary>
    public ConfigurationSchema? Schema { get; set; }

    /// <summary>
    /// Validation rules for the configuration
    /// </summary>
    public List<ConfigurationValidationRule> ValidationRules { get; set; } = new();

    /// <summary>
    /// Configuration change history (last 10 changes)
    /// </summary>
    public List<ConfigurationChange> ChangeHistory { get; set; } = new();
}

/// <summary>
/// Configuration schema information
/// </summary>
public class ConfigurationSchema
{
    /// <summary>
    /// Schema version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Field definitions
    /// </summary>
    public List<ConfigurationField> Fields { get; set; } = new();

    /// <summary>
    /// Required fields
    /// </summary>
    public List<string> RequiredFields { get; set; } = new();
}

/// <summary>
/// Configuration field definition
/// </summary>
public class ConfigurationField
{
    /// <summary>
    /// Field name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Field type
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Field description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Default value
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Whether the field is required
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Allowed values (for enumeration fields)
    /// </summary>
    public List<object> AllowedValues { get; set; } = new();
}

/// <summary>
/// Configuration validation rule
/// </summary>
public class ConfigurationValidationRule
{
    /// <summary>
    /// Field name the rule applies to
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Rule type
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// Rule parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Error message when rule fails
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Configuration change record
/// </summary>
public class ConfigurationChange
{
    /// <summary>
    /// When the change occurred
    /// </summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>
    /// Who made the change
    /// </summary>
    public string ChangedBy { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the change
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Changed fields
    /// </summary>
    public List<string> ChangedFields { get; set; } = new();

    /// <summary>
    /// Configuration version after change
    /// </summary>
    public string Version { get; set; } = string.Empty;
}

#endregion

#region Tenant Management

/// <summary>
/// Response model for tenant information
/// </summary>
public class TenantResponse
{
    /// <summary>
    /// Tenant ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Tenant name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the tenant is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When the tenant was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the tenant was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Tenant configuration settings
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Number of users in the tenant
    /// </summary>
    public int UserCount { get; set; }

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime? LastActivity { get; set; }

    /// <summary>
    /// Tenant domain or subdomain
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Tenant tier or subscription level
    /// </summary>
    public string? Tier { get; set; }

    /// <summary>
    /// User limit for the tenant
    /// </summary>
    public int? UserLimit { get; set; }

    /// <summary>
    /// Storage usage statistics
    /// </summary>
    public TenantStorageUsage StorageUsage { get; set; } = new();

    /// <summary>
    /// Tenant health status
    /// </summary>
    public TenantHealthStatus HealthStatus { get; set; } = new();

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Tenant storage usage information
/// </summary>
public class TenantStorageUsage
{
    /// <summary>
    /// Database size in bytes
    /// </summary>
    public long DatabaseSize { get; set; }

    /// <summary>
    /// File storage size in bytes
    /// </summary>
    public long FileStorageSize { get; set; }

    /// <summary>
    /// Total storage used in bytes
    /// </summary>
    public long TotalStorageUsed { get; set; }

    /// <summary>
    /// Storage limit in bytes
    /// </summary>
    public long? StorageLimit { get; set; }

    /// <summary>
    /// Storage usage percentage
    /// </summary>
    public double UsagePercentage { get; set; }

    /// <summary>
    /// Last calculated timestamp
    /// </summary>
    public DateTime LastCalculated { get; set; }
}

/// <summary>
/// Tenant health status information
/// </summary>
public class TenantHealthStatus
{
    /// <summary>
    /// Overall health status
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// Last health check timestamp
    /// </summary>
    public DateTime LastChecked { get; set; }

    /// <summary>
    /// Database connectivity status
    /// </summary>
    public string DatabaseStatus { get; set; } = "Connected";

    /// <summary>
    /// Any health issues
    /// </summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>
    /// Performance metrics
    /// </summary>
    public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
}

#endregion

#region System Health

/// <summary>
/// Response model for system health information
/// </summary>
public class SystemHealthResponse
{
    /// <summary>
    /// Overall system health status
    /// </summary>
    public string OverallStatus { get; set; } = string.Empty;

    /// <summary>
    /// When the health check was performed
    /// </summary>
    public DateTime CheckedAt { get; set; }

    /// <summary>
    /// Individual health check results
    /// </summary>
    public List<HealthCheckResponse> HealthChecks { get; set; } = new();

    /// <summary>
    /// System information
    /// </summary>
    public SystemInformationResponse SystemInformation { get; set; } = new();

    /// <summary>
    /// Performance metrics
    /// </summary>
    public Dictionary<string, object> PerformanceMetrics { get; set; } = new();

    /// <summary>
    /// System uptime
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Application version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Health trends over time
    /// </summary>
    public List<HealthTrendData> HealthTrends { get; set; } = new();

    /// <summary>
    /// Critical issues requiring attention
    /// </summary>
    public List<string> CriticalIssues { get; set; } = new();
}

/// <summary>
/// Individual health check result
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    /// Health check name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Health check status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Time taken for the check
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Description of the check
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Additional data from the check
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Exception message if check failed
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Tags associated with the check
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Health trend data point
/// </summary>
public class HealthTrendData
{
    /// <summary>
    /// Timestamp of the data point
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Overall health status at this time
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Response time metrics
    /// </summary>
    public double ResponseTime { get; set; }

    /// <summary>
    /// Error rate at this time
    /// </summary>
    public double ErrorRate { get; set; }
}

#endregion

#region Performance Metrics

/// <summary>
/// Response model for performance metrics
/// </summary>
public class PerformanceMetricsResponse
{
    /// <summary>
    /// Time range covered by the metrics
    /// </summary>
    public string TimeRange { get; set; } = string.Empty;

    /// <summary>
    /// When the metrics were collected
    /// </summary>
    public DateTime CollectedAt { get; set; }

    /// <summary>
    /// CPU usage metrics
    /// </summary>
    public CpuUsageMetrics CpuUsage { get; set; } = new();

    /// <summary>
    /// Memory usage metrics
    /// </summary>
    public MemoryUsageMetrics MemoryUsage { get; set; } = new();

    /// <summary>
    /// Disk usage metrics
    /// </summary>
    public DiskUsageMetrics DiskUsage { get; set; } = new();

    /// <summary>
    /// Network metrics
    /// </summary>
    public NetworkMetrics NetworkMetrics { get; set; } = new();

    /// <summary>
    /// Database performance metrics
    /// </summary>
    public DatabaseMetrics DatabaseMetrics { get; set; } = new();

    /// <summary>
    /// Cache performance metrics
    /// </summary>
    public CacheMetrics CacheMetrics { get; set; } = new();

    /// <summary>
    /// Request processing metrics
    /// </summary>
    public RequestMetrics RequestMetrics { get; set; } = new();

    /// <summary>
    /// Error rates by category
    /// </summary>
    public Dictionary<string, double> ErrorRates { get; set; } = new();

    /// <summary>
    /// Response time percentiles
    /// </summary>
    public Dictionary<string, double> ResponseTimes { get; set; } = new();
}

/// <summary>
/// CPU usage metrics
/// </summary>
public class CpuUsageMetrics
{
    /// <summary>
    /// Current CPU usage percentage
    /// </summary>
    public double Current { get; set; }

    /// <summary>
    /// Average CPU usage over the time period
    /// </summary>
    public double Average { get; set; }

    /// <summary>
    /// Maximum CPU usage recorded
    /// </summary>
    public double Maximum { get; set; }

    /// <summary>
    /// CPU usage by core
    /// </summary>
    public List<double> UsageByCore { get; set; } = new();

    /// <summary>
    /// CPU usage history
    /// </summary>
    public List<MetricDataPoint> History { get; set; } = new();
}

/// <summary>
/// Memory usage metrics
/// </summary>
public class MemoryUsageMetrics
{
    /// <summary>
    /// Total available memory in bytes
    /// </summary>
    public long TotalMemory { get; set; }

    /// <summary>
    /// Used memory in bytes
    /// </summary>
    public long UsedMemory { get; set; }

    /// <summary>
    /// Available memory in bytes
    /// </summary>
    public long AvailableMemory { get; set; }

    /// <summary>
    /// Memory usage percentage
    /// </summary>
    public double UsagePercentage { get; set; }

    /// <summary>
    /// Managed heap size
    /// </summary>
    public long ManagedHeapSize { get; set; }

    /// <summary>
    /// Generation 0 collections
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Generation 1 collections
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Generation 2 collections
    /// </summary>
    public int Gen2Collections { get; set; }

    /// <summary>
    /// Memory usage history
    /// </summary>
    public List<MetricDataPoint> History { get; set; } = new();
}

/// <summary>
/// Disk usage metrics
/// </summary>
public class DiskUsageMetrics
{
    /// <summary>
    /// Total disk space in bytes
    /// </summary>
    public long TotalSpace { get; set; }

    /// <summary>
    /// Used disk space in bytes
    /// </summary>
    public long UsedSpace { get; set; }

    /// <summary>
    /// Available disk space in bytes
    /// </summary>
    public long AvailableSpace { get; set; }

    /// <summary>
    /// Disk usage percentage
    /// </summary>
    public double UsagePercentage { get; set; }

    /// <summary>
    /// Disk I/O read operations per second
    /// </summary>
    public double ReadOperationsPerSecond { get; set; }

    /// <summary>
    /// Disk I/O write operations per second
    /// </summary>
    public double WriteOperationsPerSecond { get; set; }

    /// <summary>
    /// Average disk queue length
    /// </summary>
    public double AverageQueueLength { get; set; }
}

/// <summary>
/// Network metrics
/// </summary>
public class NetworkMetrics
{
    /// <summary>
    /// Bytes received per second
    /// </summary>
    public double BytesReceivedPerSecond { get; set; }

    /// <summary>
    /// Bytes sent per second
    /// </summary>
    public double BytesSentPerSecond { get; set; }

    /// <summary>
    /// Packets received per second
    /// </summary>
    public double PacketsReceivedPerSecond { get; set; }

    /// <summary>
    /// Packets sent per second
    /// </summary>
    public double PacketsSentPerSecond { get; set; }

    /// <summary>
    /// Network errors per second
    /// </summary>
    public double ErrorsPerSecond { get; set; }

    /// <summary>
    /// Active connections count
    /// </summary>
    public int ActiveConnections { get; set; }
}

/// <summary>
/// Database performance metrics
/// </summary>
public class DatabaseMetrics
{
    /// <summary>
    /// Active connections
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Average query execution time
    /// </summary>
    public double AverageQueryTime { get; set; }

    /// <summary>
    /// Queries per second
    /// </summary>
    public double QueriesPerSecond { get; set; }

    /// <summary>
    /// Database size in bytes
    /// </summary>
    public long DatabaseSize { get; set; }

    /// <summary>
    /// Lock waits per second
    /// </summary>
    public double LockWaitsPerSecond { get; set; }

    /// <summary>
    /// Deadlocks per second
    /// </summary>
    public double DeadlocksPerSecond { get; set; }

    /// <summary>
    /// Buffer cache hit ratio
    /// </summary>
    public double BufferCacheHitRatio { get; set; }
}

/// <summary>
/// Cache performance metrics
/// </summary>
public class CacheMetrics
{
    /// <summary>
    /// Cache hit ratio
    /// </summary>
    public double HitRatio { get; set; }

    /// <summary>
    /// Cache miss ratio
    /// </summary>
    public double MissRatio { get; set; }

    /// <summary>
    /// Total cache entries
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Cache memory usage in bytes
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Average get operation time
    /// </summary>
    public double AverageGetTime { get; set; }

    /// <summary>
    /// Average set operation time
    /// </summary>
    public double AverageSetTime { get; set; }

    /// <summary>
    /// Evictions per second
    /// </summary>
    public double EvictionsPerSecond { get; set; }
}

/// <summary>
/// Request processing metrics
/// </summary>
public class RequestMetrics
{
    /// <summary>
    /// Requests per second
    /// </summary>
    public double RequestsPerSecond { get; set; }

    /// <summary>
    /// Average response time
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// 95th percentile response time
    /// </summary>
    public double P95ResponseTime { get; set; }

    /// <summary>
    /// 99th percentile response time
    /// </summary>
    public double P99ResponseTime { get; set; }

    /// <summary>
    /// Active requests
    /// </summary>
    public int ActiveRequests { get; set; }

    /// <summary>
    /// Failed requests per second
    /// </summary>
    public double FailedRequestsPerSecond { get; set; }

    /// <summary>
    /// Request queue length
    /// </summary>
    public int QueueLength { get; set; }
}

/// <summary>
/// Generic metric data point
/// </summary>
public class MetricDataPoint
{
    /// <summary>
    /// Timestamp of the data point
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Metric value
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

#endregion

#region Cache Management

/// <summary>
/// Response model for cache statistics
/// </summary>
public class CacheStatisticsResponse
{
    /// <summary>
    /// Total number of cache keys
    /// </summary>
    public int TotalKeys { get; set; }

    /// <summary>
    /// Total memory usage in bytes
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Cache hit rate percentage
    /// </summary>
    public double HitRate { get; set; }

    /// <summary>
    /// Cache miss rate percentage
    /// </summary>
    public double MissRate { get; set; }

    /// <summary>
    /// Number of evicted entries
    /// </summary>
    public int EvictionCount { get; set; }

    /// <summary>
    /// Number of expired keys
    /// </summary>
    public int ExpiredKeys { get; set; }

    /// <summary>
    /// Statistics by cache region
    /// </summary>
    public List<CacheRegionResponse> CacheRegions { get; set; } = new();

    /// <summary>
    /// When the statistics were last refreshed
    /// </summary>
    public DateTime LastRefreshed { get; set; }

    /// <summary>
    /// Cache performance over time
    /// </summary>
    public List<CachePerformanceData> PerformanceHistory { get; set; } = new();
}

/// <summary>
/// Cache region statistics
/// </summary>
public class CacheRegionResponse
{
    /// <summary>
    /// Region name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Number of keys in the region
    /// </summary>
    public int KeyCount { get; set; }

    /// <summary>
    /// Memory usage for the region
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Hit rate for the region
    /// </summary>
    public double HitRate { get; set; }

    /// <summary>
    /// When the region was last accessed
    /// </summary>
    public DateTime? LastAccessed { get; set; }

    /// <summary>
    /// Average item size in the region
    /// </summary>
    public long AverageItemSize { get; set; }

    /// <summary>
    /// TTL settings for the region
    /// </summary>
    public TimeSpan? DefaultTtl { get; set; }
}

/// <summary>
/// Cache performance data over time
/// </summary>
public class CachePerformanceData
{
    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Hit rate at this time
    /// </summary>
    public double HitRate { get; set; }

    /// <summary>
    /// Operations per second
    /// </summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>
    /// Memory usage at this time
    /// </summary>
    public long MemoryUsage { get; set; }
}

/// <summary>
/// Response model for cache clear operation
/// </summary>
public class CacheClearResponse
{
    /// <summary>
    /// Pattern used for clearing
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Region that was cleared
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Number of entries cleared
    /// </summary>
    public int ClearedCount { get; set; }

    /// <summary>
    /// When the cache was cleared
    /// </summary>
    public DateTime ClearedAt { get; set; }

    /// <summary>
    /// Whether force clear was used
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// Memory freed by the clear operation
    /// </summary>
    public long MemoryFreed { get; set; }

    /// <summary>
    /// Time taken to clear the cache
    /// </summary>
    public TimeSpan ClearDuration { get; set; }
}

/// <summary>
/// Response model for cache preload operation
/// </summary>
public class CachePreloadResponse
{
    /// <summary>
    /// Regions that were preloaded
    /// </summary>
    public List<string> Regions { get; set; } = new();

    /// <summary>
    /// Total number of items preloaded
    /// </summary>
    public int TotalPreloaded { get; set; }

    /// <summary>
    /// Time taken for preloading
    /// </summary>
    public TimeSpan PreloadDuration { get; set; }

    /// <summary>
    /// When preloading was completed
    /// </summary>
    public DateTime PreloadedAt { get; set; }

    /// <summary>
    /// Results by region
    /// </summary>
    public List<CachePreloadResult> Results { get; set; } = new();

    /// <summary>
    /// Memory used by preloaded data
    /// </summary>
    public long MemoryUsed { get; set; }

    /// <summary>
    /// Any warnings during preload
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Cache preload result for a specific region
/// </summary>
public class CachePreloadResult
{
    /// <summary>
    /// Region name
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Number of items preloaded
    /// </summary>
    public int PreloadedCount { get; set; }

    /// <summary>
    /// Time taken to preload this region
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether preloading was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if preloading failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Memory used by this region
    /// </summary>
    public long MemoryUsed { get; set; }
}

#endregion

#region Maintenance Operations

/// <summary>
/// Response model for maintenance operations
/// </summary>
public class MaintenanceResponse
{
    /// <summary>
    /// Maintenance tasks that were performed
    /// </summary>
    public List<string> Tasks { get; set; } = new();

    /// <summary>
    /// When maintenance started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When maintenance completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total maintenance duration
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether all tasks completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Results for each maintenance task
    /// </summary>
    public List<MaintenanceTaskResult> Results { get; set; } = new();

    /// <summary>
    /// Warnings generated during maintenance
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// When the next scheduled maintenance is due
    /// </summary>
    public DateTime? NextScheduledMaintenance { get; set; }

    /// <summary>
    /// System status after maintenance
    /// </summary>
    public string SystemStatus { get; set; } = "Operational";

    /// <summary>
    /// Services that were restarted during maintenance
    /// </summary>
    public List<string> RestartedServices { get; set; } = new();
}

/// <summary>
/// Result of a single maintenance task
/// </summary>
public class MaintenanceTaskResult
{
    /// <summary>
    /// Task name
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the task was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Time taken to complete the task
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Task completion message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional task details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();

    /// <summary>
    /// Task output or log information
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Error information if task failed
    /// </summary>
    public string? ErrorDetails { get; set; }
}

/// <summary>
/// Response model for maintenance schedule
/// </summary>
public class MaintenanceScheduleResponse
{
    /// <summary>
    /// Scheduled maintenance tasks
    /// </summary>
    public List<ScheduledMaintenanceTask> ScheduledTasks { get; set; } = new();

    /// <summary>
    /// Next scheduled maintenance time
    /// </summary>
    public DateTime? NextMaintenance { get; set; }

    /// <summary>
    /// Maintenance window information
    /// </summary>
    public MaintenanceWindow MaintenanceWindow { get; set; } = new();

    /// <summary>
    /// Last maintenance information
    /// </summary>
    public DateTime? LastMaintenance { get; set; }

    /// <summary>
    /// Recurring maintenance tasks
    /// </summary>
    public List<RecurringMaintenanceTask> RecurringTasks { get; set; } = new();

    /// <summary>
    /// Maintenance history (last 10 maintenances)
    /// </summary>
    public List<MaintenanceHistoryItem> History { get; set; } = new();
}

/// <summary>
/// Scheduled maintenance task
/// </summary>
public class ScheduledMaintenanceTask
{
    /// <summary>
    /// Task name
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// Scheduled time
    /// </summary>
    public DateTime ScheduledFor { get; set; }

    /// <summary>
    /// Estimated duration
    /// </summary>
    public TimeSpan EstimatedDuration { get; set; }

    /// <summary>
    /// Task priority
    /// </summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// Task description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether system downtime is expected
    /// </summary>
    public bool RequiresDowntime { get; set; }

    /// <summary>
    /// Affected services
    /// </summary>
    public List<string> AffectedServices { get; set; } = new();
}

/// <summary>
/// Recurring maintenance task
/// </summary>
public class RecurringMaintenanceTask
{
    /// <summary>
    /// Task name
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// Schedule expression (cron format)
    /// </summary>
    public string Schedule { get; set; } = string.Empty;

    /// <summary>
    /// When the task was last run
    /// </summary>
    public DateTime? LastRun { get; set; }

    /// <summary>
    /// When the task will next run
    /// </summary>
    public DateTime? NextRun { get; set; }

    /// <summary>
    /// Whether the task is enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Task description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Average task duration
    /// </summary>
    public TimeSpan AverageDuration { get; set; }
}

/// <summary>
/// Maintenance window configuration
/// </summary>
public class MaintenanceWindow
{
    /// <summary>
    /// Start time for maintenance window
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// End time for maintenance window
    /// </summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// Days of week for maintenance
    /// </summary>
    public List<DayOfWeek> AllowedDays { get; set; } = new();

    /// <summary>
    /// Time zone for maintenance window
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// Whether maintenance window is currently active
    /// </summary>
    public bool IsCurrentlyInWindow { get; set; }

    /// <summary>
    /// Next maintenance window start time
    /// </summary>
    public DateTime? NextWindowStart { get; set; }
}

/// <summary>
/// Historical maintenance information
/// </summary>
public class MaintenanceHistoryItem
{
    /// <summary>
    /// Maintenance date
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Tasks performed
    /// </summary>
    public List<string> TasksPerformed { get; set; } = new();

    /// <summary>
    /// Duration of maintenance
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether maintenance was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of issues resolved
    /// </summary>
    public int IssuesResolved { get; set; }
}

#endregion

#region System Information

/// <summary>
/// Response model for system information
/// </summary>
public class SystemInformationResponse
{
    /// <summary>
    /// Application name
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Application version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Build date
    /// </summary>
    public DateTime BuildDate { get; set; }

    /// <summary>
    /// Environment (Development, Staging, Production)
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Machine name
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Number of processors
    /// </summary>
    public int ProcessorCount { get; set; }

    /// <summary>
    /// Operating system version
    /// </summary>
    public string OSVersion { get; set; } = string.Empty;

    /// <summary>
    /// Working set memory in bytes
    /// </summary>
    public long WorkingSet { get; set; }

    /// <summary>
    /// Managed memory in bytes
    /// </summary>
    public long ManagedMemory { get; set; }

    /// <summary>
    /// System uptime
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Application start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Number of threads
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Number of handles
    /// </summary>
    public int HandleCount { get; set; }

    /// <summary>
    /// Framework information
    /// </summary>
    public FrameworkInformation Framework { get; set; } = new();

    /// <summary>
    /// Hardware information
    /// </summary>
    public HardwareInformation Hardware { get; set; } = new();

    /// <summary>
    /// Software dependencies
    /// </summary>
    public List<DependencyInformation> Dependencies { get; set; } = new();
}

/// <summary>
/// Framework information
/// </summary>
public class FrameworkInformation
{
    /// <summary>
    /// .NET version
    /// </summary>
    public string DotNetVersion { get; set; } = string.Empty;

    /// <summary>
    /// ASP.NET Core version
    /// </summary>
    public string AspNetCoreVersion { get; set; } = string.Empty;

    /// <summary>
    /// Entity Framework version
    /// </summary>
    public string EntityFrameworkVersion { get; set; } = string.Empty;

    /// <summary>
    /// Runtime identifier
    /// </summary>
    public string RuntimeIdentifier { get; set; } = string.Empty;
}

/// <summary>
/// Hardware information
/// </summary>
public class HardwareInformation
{
    /// <summary>
    /// Total physical memory in bytes
    /// </summary>
    public long TotalPhysicalMemory { get; set; }

    /// <summary>
    /// Available physical memory in bytes
    /// </summary>
    public long AvailablePhysicalMemory { get; set; }

    /// <summary>
    /// Total disk space in bytes
    /// </summary>
    public long TotalDiskSpace { get; set; }

    /// <summary>
    /// Available disk space in bytes
    /// </summary>
    public long AvailableDiskSpace { get; set; }

    /// <summary>
    /// Processor information
    /// </summary>
    public string ProcessorInfo { get; set; } = string.Empty;
}

/// <summary>
/// Software dependency information
/// </summary>
public class DependencyInformation
{
    /// <summary>
    /// Dependency name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Dependency version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Dependency type (NuGet package, system library, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether the dependency is critical
    /// </summary>
    public bool IsCritical { get; set; }
}

#endregion

#region Backup Operations

/// <summary>
/// Response model for backup creation
/// </summary>
public class BackupResponse
{
    /// <summary>
    /// Backup identifier
    /// </summary>
    public string BackupId { get; set; } = string.Empty;

    /// <summary>
    /// Type of backup
    /// </summary>
    public string BackupType { get; set; } = string.Empty;

    /// <summary>
    /// When the backup was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Backup size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Backup location
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Whether the backup is compressed
    /// </summary>
    public bool Compressed { get; set; }

    /// <summary>
    /// Whether the backup is encrypted
    /// </summary>
    public bool Encrypted { get; set; }

    /// <summary>
    /// Backup checksum for integrity verification
    /// </summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// When the backup expires
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Backup status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Time taken to create the backup
    /// </summary>
    public TimeSpan CreationDuration { get; set; }

    /// <summary>
    /// Backup metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Response model for backup summary
/// </summary>
public class BackupSummaryResponse
{
    /// <summary>
    /// Backup identifier
    /// </summary>
    public string BackupId { get; set; } = string.Empty;

    /// <summary>
    /// Type of backup
    /// </summary>
    public string BackupType { get; set; } = string.Empty;

    /// <summary>
    /// When the backup was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Backup size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Backup status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// When the backup expires
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Backup description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Backup tags
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

#endregion