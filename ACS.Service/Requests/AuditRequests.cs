using ACS.Service.Domain;

namespace ACS.Service.Requests;

/// <summary>
/// Service request for audit log entries
/// </summary>
public record GetAuditLogRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? EntityType { get; init; }
    public string? EventType { get; init; }
    public string? Action { get; init; }
    public string? UserId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for compliance requirements
/// </summary>
public record GetComplianceRequirementsRequest
{
    public string? Framework { get; init; }
    public string? Category { get; init; }
    public bool? IsMandatory { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for compliance violations
/// </summary>
public record GetComplianceViolationsRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? ViolationType { get; init; }
    public string? Severity { get; init; }
    public bool? IsResolved { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for permission change events
/// </summary>
public record GetPermissionChangesRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int? EntityId { get; init; }
    public int? PermissionId { get; init; }
    public string? ChangeType { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for access attempt events
/// </summary>
public record GetAccessAttemptsRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? UserId { get; init; }
    public string? Resource { get; init; }
    public bool? Success { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for audit trail export
/// </summary>
public record ExportAuditTrailRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string Format { get; init; } = "CSV";
    public ICollection<string> Columns { get; init; } = new List<string>();
    public Dictionary<string, object> Filters { get; init; } = new();
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for generating compliance reports
/// </summary>
public record GetComplianceReportRequest
{
    public string Framework { get; init; } = string.Empty;
    public string ComplianceStandard { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public bool IncludeDetails { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for exporting audit logs
/// </summary>
public record ExportAuditLogsRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string ExportFormat { get; init; } = "CSV";
    public string? UserId { get; init; }
    public string? EventType { get; init; }
    public string? EntityType { get; init; }
    public bool IncludeDetails { get; init; } = true;
    public ICollection<string> Columns { get; init; } = new List<string>();
    public Dictionary<string, object> Filters { get; init; } = new();
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for security analysis
/// </summary>
public record SecurityAnalysisRequest
{
    public string AnalysisType { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public ICollection<string> IncludeMetrics { get; init; } = new List<string>();
    public bool IncludeRecommendations { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for security events
/// </summary>
public record GetSecurityEventsRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? EventType { get; init; }
    public string? Severity { get; init; }
    public string? RiskLevel { get; init; }
    public string? EventCategory { get; init; }
    public string? Source { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string RequestedBy { get; init; } = string.Empty;
}