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
/// <summary>
/// Service request for recording audit events
/// </summary>
public record RecordAuditEventRequest
{
    public string EventType { get; init; } = string.Empty;
    public string EventCategory { get; init; } = string.Empty;
    public int? UserId { get; init; }
    public int? EntityId { get; init; }
    public string? EntityType { get; init; }
    public int? ResourceId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? Details { get; init; }
    public string Severity { get; init; } = "Information";
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? SessionId { get; init; }
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Service request for purging old audit data
/// </summary>
public record PurgeAuditDataRequest
{
    public DateTime OlderThan { get; init; }
    public List<string>? EventCategories { get; init; }
    public List<string>? SeverityLevels { get; init; }
    public bool PreserveCompliance { get; init; } = true;
    public int BatchSize { get; init; } = 1000;
    public bool DryRun { get; init; } = false;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for enhanced audit log retrieval
/// </summary>
public record GetAuditLogEnhancedRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public List<string>? EventTypes { get; init; }
    public List<string>? EventCategories { get; init; }
    public int? UserId { get; init; }
    public int? EntityId { get; init; }
    public string? EntityType { get; init; }
    public int? ResourceId { get; init; }
    public List<string>? SeverityLevels { get; init; }
    public string? SearchText { get; init; }
    public string? IpAddress { get; init; }
    public string SortBy { get; init; } = "EventTimestamp";
    public bool SortDescending { get; init; } = true;
}

/// <summary>
/// Service request for user audit trail
/// </summary>
public record GetUserAuditTrailRequest
{
    public int UserId { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public List<string>? EventCategories { get; init; }
    public bool IncludeSystemEvents { get; init; } = false;
    public bool IncludePermissionChanges { get; init; } = true;
    public bool IncludeResourceAccess { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// Service request for generating compliance reports
/// </summary>
public record GenerateComplianceReportRequest
{
    public string ReportType { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public List<int>? UserIds { get; init; }
    public List<int>? ResourceIds { get; init; }
    public bool IncludeAnomalies { get; init; } = true;
    public bool IncludeRiskAssessment { get; init; } = true;
    public string ReportFormat { get; init; } = "Summary";
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for validating audit integrity
/// </summary>
public record ValidateAuditIntegrityRequest
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public bool CheckHashChain { get; init; } = true;
    public bool CheckCompleteness { get; init; } = true;
    public bool CheckConsistency { get; init; } = true;
    public bool PerformDeepValidation { get; init; } = false;
    public string RequestedBy { get; init; } = string.Empty;
}
