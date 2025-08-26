using ACS.Service.Domain;

namespace ACS.Service.Responses;

/// <summary>
/// Audit log entry for system events
/// </summary>
public record AuditLogEntry
{
    public int Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public string? Description { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Permission change event for audit tracking
/// </summary>
public record PermissionChangeEvent
{
    public int Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string UserId { get; init; } = string.Empty;
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public int PermissionId { get; init; }
    public string PermissionName { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty; // Grant, Revoke, Modify
    public string? Reason { get; init; }
    public string? ApprovedBy { get; init; }
    public Dictionary<string, object> Details { get; init; } = new();
}

/// <summary>
/// Access attempt event for security monitoring
/// </summary>
public record AccessAttemptEvent
{
    public int Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? FailureReason { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public Dictionary<string, object> Context { get; init; } = new();
}

/// <summary>
/// Service response for audit log entries
/// </summary>
public record AuditLogResponse
{
    public ICollection<AuditLogEntry> Entries { get; init; } = new List<AuditLogEntry>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission change events
/// </summary>
public record PermissionChangeEventResponse
{
    public ICollection<PermissionChangeEvent> Events { get; init; } = new List<PermissionChangeEvent>();
    public int TotalCount { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for access attempt events
/// </summary>
public record AccessAttemptEventResponse
{
    public ICollection<AccessAttemptEvent> Events { get; init; } = new List<AccessAttemptEvent>();
    public int TotalCount { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for audit trail export
/// </summary>
public record AuditTrailExportResponse
{
    public byte[] FileContent { get; init; } = Array.Empty<byte>();
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public int RecordCount { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for compliance requirements
/// </summary>
public record ComplianceRequirementsResponse
{
    public ICollection<ComplianceRequirement> Requirements { get; init; } = new List<ComplianceRequirement>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Compliance requirement definition
/// </summary>
public record ComplianceRequirement
{
    public string RequirementId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Framework { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsMandatory { get; init; }
    public DateTime EffectiveDate { get; init; }
    public string? ComplianceLevel { get; init; }
    public ICollection<string> ControlObjectives { get; init; } = new List<string>();
}

/// <summary>
/// Service response for audit statistics
/// </summary>
public record AuditStatisticsResponse
{
    public int TotalEvents { get; init; }
    public int UserEvents { get; init; }
    public int SystemEvents { get; init; }
    public int SecurityEvents { get; init; }
    public int ComplianceViolations { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public Dictionary<string, int> EventsByType { get; init; } = new();
    public Dictionary<string, int> EventsByUser { get; init; } = new();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for security events
/// </summary>
public record SecurityEventResponse
{
    public ICollection<SecurityEvent> Events { get; init; } = new List<SecurityEvent>();
    public int TotalCount { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Security event for monitoring
/// </summary>
public record SecurityEvent
{
    public int Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? IpAddress { get; init; }
    public Dictionary<string, object> Details { get; init; } = new();
}

/// <summary>
/// Service response for security analysis reports
/// </summary>
public record SecurityAnalysisReportResponse
{
    public SecurityAnalysisResult AnalysisResult { get; init; } = new();
    public DateTime AnalysisDate { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Security analysis result data
/// </summary>
public record SecurityAnalysisResult
{
    public string AnalysisType { get; init; } = string.Empty;
    public string OverallRiskLevel { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
    public double RiskScore { get; init; }
    public int TotalEntities { get; init; }
    public int MatchingEntities { get; init; }
    public ICollection<SecurityRiskFactor> RiskFactors { get; init; } = new List<SecurityRiskFactor>();
    public ICollection<SecurityRecommendation> Recommendations { get; init; } = new List<SecurityRecommendation>();
    public Dictionary<string, object> Metrics { get; init; } = new();
}

/// <summary>
/// Security risk factor
/// </summary>
public record SecurityRiskFactor
{
    public string FactorType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public double Impact { get; init; }
    public string? Mitigation { get; init; }
}

/// <summary>
/// Security recommendation
/// </summary>
public record SecurityRecommendation
{
    public string RecommendationType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string? Implementation { get; init; }
    public DateTime? TargetDate { get; init; }
}

/// <summary>
/// Service response for user activity audits
/// </summary>
public record UserActivityAuditResponse
{
    public ICollection<UserActivitySummary> Activities { get; init; } = new List<UserActivitySummary>();
    public int TotalActivities { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// User activity summary for auditing
/// </summary>
public record UserActivitySummary
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public int LoginCount { get; init; }
    public int ActionCount { get; init; }
    public DateTime LastActivity { get; init; }
    public ICollection<string> TopActions { get; init; } = new List<string>();
    public Dictionary<string, int> ActivityBreakdown { get; init; } = new();
}

/// <summary>
/// Service response for security analysis results
/// </summary>
public record SecurityAnalysisResultResponse
{
    public SecurityAnalysisResult Result { get; init; } = new();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for compliance reports
/// </summary>
public record ComplianceReportResponse
{
    public ComplianceReport Report { get; init; } = new();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Compliance report data
/// </summary>
public record ComplianceReport
{
    public string ReportId { get; init; } = string.Empty;
    public string Framework { get; init; } = string.Empty;
    public DateTime AssessmentDate { get; init; }
    public string OverallStatus { get; init; } = string.Empty;
    public double ComplianceScore { get; init; }
    public ICollection<ComplianceControl> Controls { get; init; } = new List<ComplianceControl>();
    public ICollection<ComplianceGap> Gaps { get; init; } = new List<ComplianceGap>();
}

/// <summary>
/// Compliance control assessment
/// </summary>
public record ComplianceControl
{
    public string ControlId { get; init; } = string.Empty;
    public string ControlName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Evidence { get; init; }
    public DateTime LastAssessed { get; init; }
}

/// <summary>
/// Compliance gap identification
/// </summary>
public record ComplianceGap
{
    public string GapType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string? Recommendation { get; init; }
    public DateTime IdentifiedDate { get; init; }
}