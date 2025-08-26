namespace ACS.Service.Domain;

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Compliance event types
/// </summary>
public enum ComplianceEventType
{
    DataAccess,
    DataModification,
    UserLogin,
    PermissionChange,
    PolicyViolation,
    SecurityBreach,
    DataExport,
    SystemChange
}

/// <summary>
/// Alert categories
/// </summary>
public enum AlertCategory
{
    Security,
    Compliance,
    Performance,
    System,
    User,
    Data
}

/// <summary>
/// System alert
/// </summary>
public class Alert
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsResolved { get; set; }
    public string? ResolvedBy { get; set; }
    public string? Resolution { get; set; }
}