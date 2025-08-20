using System.ComponentModel;

namespace ACS.Alerting;

/// <summary>
/// Service interface for managing alerts and notifications
/// </summary>
public interface IAlertingService
{
    /// <summary>
    /// Send an alert notification
    /// </summary>
    Task SendAlertAsync(AlertRequest alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send multiple alerts in batch
    /// </summary>
    Task SendBatchAlertsAsync(IEnumerable<AlertRequest> alerts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if alert should be throttled based on frequency rules
    /// </summary>
    Task<bool> ShouldThrottleAlertAsync(string alertKey, AlertSeverity severity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alert history for analysis
    /// </summary>
    Task<IEnumerable<AlertHistory>> GetAlertHistoryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, string? notes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve an alert
    /// </summary>
    Task ResolveAlertAsync(string alertId, string resolvedBy, string? resolution = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active alerts
    /// </summary>
    Task<IEnumerable<AlertHistory>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Alert request information
/// </summary>
public class AlertRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public AlertCategory Category { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new();
    public TimeSpan? ExpiresAfter { get; set; }
    public bool RequiresAcknowledgment { get; set; }
    public List<NotificationChannel> NotificationChannels { get; set; } = new();
}

/// <summary>
/// Alert history record
/// </summary>
public class AlertHistory
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public AlertCategory Category { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public string? AcknowledgmentNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? Resolution { get; set; }
    public AlertStatus Status { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<NotificationDelivery> NotificationDeliveries { get; set; } = new();
}

/// <summary>
/// Notification delivery record
/// </summary>
public class NotificationDelivery
{
    public NotificationChannel Channel { get; set; }
    public string Target { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TimeSpan DeliveryTime { get; set; }
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    [Description("Information")]
    Info = 1,
    
    [Description("Warning")]
    Warning = 2,
    
    [Description("Critical")]
    Critical = 3,
    
    [Description("Emergency")]
    Emergency = 4
}

/// <summary>
/// Alert categories for classification
/// </summary>
public enum AlertCategory
{
    [Description("System Health")]
    SystemHealth,
    
    [Description("Performance")]
    Performance,
    
    [Description("Security")]
    Security,
    
    [Description("Business Logic")]
    BusinessLogic,
    
    [Description("Infrastructure")]
    Infrastructure,
    
    [Description("Database")]
    Database,
    
    [Description("Network")]
    Network,
    
    [Description("Application")]
    Application,
    
    [Description("Compliance")]
    Compliance,
    
    [Description("User Activity")]
    UserActivity
}

/// <summary>
/// Alert status
/// </summary>
public enum AlertStatus
{
    [Description("Active")]
    Active,
    
    [Description("Acknowledged")]
    Acknowledged,
    
    [Description("Resolved")]
    Resolved,
    
    [Description("Expired")]
    Expired,
    
    [Description("Suppressed")]
    Suppressed
}

/// <summary>
/// Available notification channels
/// </summary>
public enum NotificationChannel
{
    [Description("Email")]
    Email,
    
    [Description("SMS")]
    SMS,
    
    [Description("Slack")]
    Slack,
    
    [Description("Microsoft Teams")]
    Teams,
    
    [Description("Webhook")]
    Webhook,
    
    [Description("Push Notification")]
    Push,
    
    [Description("Discord")]
    Discord,
    
    [Description("PagerDuty")]
    PagerDuty,
    
    [Description("Log File")]
    LogFile
}