namespace ACS.Alerting;

/// <summary>
/// Interface for persistent storage of alerts and their history
/// </summary>
public interface IAlertStorage
{
    /// <summary>
    /// Store a new alert
    /// </summary>
    Task StoreAlertAsync(AlertHistory alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing alert
    /// </summary>
    Task UpdateAlertAsync(AlertHistory alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific alert by ID
    /// </summary>
    Task<AlertHistory?> GetAlertAsync(string alertId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alert history within a time range
    /// </summary>
    Task<IEnumerable<AlertHistory>> GetAlertHistoryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all currently active alerts
    /// </summary>
    Task<IEnumerable<AlertHistory>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alerts by tenant ID
    /// </summary>
    Task<IEnumerable<AlertHistory>> GetAlertsByTenantAsync(string tenantId, DateTime? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alerts by severity
    /// </summary>
    Task<IEnumerable<AlertHistory>> GetAlertsBySeverityAsync(AlertSeverity severity, DateTime? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alert statistics for reporting
    /// </summary>
    Task<AlertStatistics> GetAlertStatisticsAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up old alerts based on retention policy
    /// </summary>
    Task CleanupOldAlertsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search alerts by criteria
    /// </summary>
    Task<IEnumerable<AlertHistory>> SearchAlertsAsync(AlertSearchCriteria criteria, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alert statistics for reporting and analysis
/// </summary>
public class AlertStatistics
{
    public int TotalAlerts { get; set; }
    public Dictionary<AlertSeverity, int> AlertsBySeverity { get; set; } = new();
    public Dictionary<AlertCategory, int> AlertsByCategory { get; set; } = new();
    public Dictionary<string, int> AlertsBySource { get; set; } = new();
    public Dictionary<string, int> AlertsByTenant { get; set; } = new();
    public int ActiveAlerts { get; set; }
    public int AcknowledgedAlerts { get; set; }
    public int ResolvedAlerts { get; set; }
    public double AverageResolutionTimeMinutes { get; set; }
    public double AverageAcknowledgmentTimeMinutes { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Search criteria for finding alerts
/// </summary>
public class AlertSearchCriteria
{
    public string? TextSearch { get; set; }
    public List<AlertSeverity>? Severities { get; set; }
    public List<AlertCategory>? Categories { get; set; }
    public List<AlertStatus>? Statuses { get; set; }
    public List<string>? Sources { get; set; }
    public List<string>? TenantIds { get; set; }
    public List<string>? UserIds { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? Skip { get; set; }
    public int? Take { get; set; }
    public string? OrderBy { get; set; }
    public bool OrderDescending { get; set; } = true;
}