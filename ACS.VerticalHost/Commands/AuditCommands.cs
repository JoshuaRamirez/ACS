using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// Audit and Compliance Commands
public class RecordAuditEventCommand : ICommand<AuditEventResult>
{
    public string EventType { get; set; } = string.Empty; // "Login", "Permission Grant", "Resource Access", etc.
    public string EventCategory { get; set; } = string.Empty; // "Security", "Business", "System", "Compliance"
    public int? UserId { get; set; }
    public int? EntityId { get; set; }
    public string? EntityType { get; set; }
    public int? ResourceId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; } // JSON or structured data
    public string Severity { get; set; } = "Information"; // "Information", "Warning", "Error", "Critical"
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public DateTime? EventTimestamp { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class PurgeOldAuditDataCommand : ICommand<AuditPurgeResult>
{
    public DateTime OlderThan { get; set; }
    public List<string>? EventCategories { get; set; } // null for all categories
    public List<string>? SeverityLevels { get; set; } // null for all severities
    public bool PreserveCompliance { get; set; } = true; // Keep compliance-related entries longer
    public int BatchSize { get; set; } = 1000;
    public bool DryRun { get; set; } = false;
    public string? RequestedBy { get; set; }
}

// Audit and Compliance Queries
public class GetAuditLogQuery : IQuery<List<AuditLogEntry>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string>? EventTypes { get; set; }
    public List<string>? EventCategories { get; set; }
    public int? UserId { get; set; }
    public int? EntityId { get; set; }
    public string? EntityType { get; set; }
    public int? ResourceId { get; set; }
    public List<string>? SeverityLevels { get; set; }
    public string? SearchText { get; set; }
    public string? IpAddress { get; set; }
    public string SortBy { get; set; } = "EventTimestamp";
    public bool SortDescending { get; set; } = true;
}

public class GetUserAuditTrailQuery : IQuery<List<UserAuditTrailEntry>>
{
    public int UserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string>? EventCategories { get; set; }
    public bool IncludeSystemEvents { get; set; } = false;
    public bool IncludePermissionChanges { get; set; } = true;
    public bool IncludeResourceAccess { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class GetComplianceReportQuery : IQuery<ComplianceReportResult>
{
    public string ReportType { get; set; } = string.Empty; // "GDPR", "SOX", "HIPAA", "Custom"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<int>? UserIds { get; set; } // null for all users
    public List<int>? ResourceIds { get; set; } // null for all resources
    public bool IncludeAnomalies { get; set; } = true;
    public bool IncludeRiskAssessment { get; set; } = true;
    public string ReportFormat { get; set; } = "Summary"; // "Summary", "Detailed", "Raw"
    public string? RequestedBy { get; set; }
}

public class ValidateAuditIntegrityQuery : IQuery<AuditIntegrityResult>
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool CheckHashChain { get; set; } = true;
    public bool CheckCompleteness { get; set; } = true;
    public bool CheckConsistency { get; set; } = true;
    public bool PerformDeepValidation { get; set; } = false;
    public string? RequestedBy { get; set; }
}

// Audit Result Types
public class AuditEventResult
{
    public bool Success { get; set; } = true;
    public long AuditEventId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public string? Message { get; set; }
    public string? CorrelationId { get; set; }
}

public class AuditPurgeResult
{
    public bool Success { get; set; } = true;
    public int RecordsProcessed { get; set; }
    public int RecordsDeleted { get; set; }
    public int RecordsPreserved { get; set; }
    public DateTime PurgeStartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PurgeCompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? Message { get; set; }
    public List<string> PreservedReasons { get; set; } = new(); // Why certain records were preserved
}

public class AuditLogEntry
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventCategory { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public int? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityName { get; set; }
    public int? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public DateTime EventTimestamp { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class UserAuditTrailEntry
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventCategory { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ResourceName { get; set; }
    public string? PermissionName { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? SessionId { get; set; }
    public DateTime EventTimestamp { get; set; }
    public bool IsAnomaly { get; set; }
    public string? AnomalyReason { get; set; }
}

public class ComplianceReportResult
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    public string ReportType { get; set; } = string.Empty;
    public DateTime ReportGeneratedAt { get; set; } = DateTime.UtcNow;
    public string? RequestedBy { get; set; }
    public DateTime CoveredPeriodStart { get; set; }
    public DateTime CoveredPeriodEnd { get; set; }
    public ComplianceReportSummary Summary { get; set; } = new();
    public List<ComplianceViolation> Violations { get; set; } = new();
    public List<ComplianceAnomaly> Anomalies { get; set; } = new();
    public ComplianceRiskAssessment? RiskAssessment { get; set; }
    public Dictionary<string, object> ReportData { get; set; } = new();
}

public class AuditIntegrityResult
{
    public bool IsIntegrityValid { get; set; } = true;
    public DateTime ValidationPerformedAt { get; set; } = DateTime.UtcNow;
    public string? RequestedBy { get; set; }
    public AuditIntegrityChecks ChecksPerformed { get; set; } = new();
    public List<AuditIntegrityIssue> Issues { get; set; } = new();
    public AuditIntegrityStatistics Statistics { get; set; } = new();
    public string? Message { get; set; }
}

// Supporting Types
public class ComplianceReportSummary
{
    public int TotalEvents { get; set; }
    public int SecurityEvents { get; set; }
    public int PermissionChanges { get; set; }
    public int ResourceAccesses { get; set; }
    public int UniqueUsers { get; set; }
    public int UniqueResources { get; set; }
    public int ViolationCount { get; set; }
    public int AnomalyCount { get; set; }
    public string OverallRiskLevel { get; set; } = "Low";
}

public class ComplianceViolation
{
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public int? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? RecommendedAction { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

public class ComplianceAnomaly
{
    public string AnomalyType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime DetectedAt { get; set; }
    public string? Pattern { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
}

public class ComplianceRiskAssessment
{
    public string OverallRiskLevel { get; set; } = "Low"; // "Low", "Medium", "High", "Critical"
    public double RiskScore { get; set; } // 0.0 to 1.0
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class RiskFactor
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Impact { get; set; } // 0.0 to 1.0
    public double Probability { get; set; } // 0.0 to 1.0
    public string Mitigation { get; set; } = string.Empty;
}

public class AuditIntegrityChecks
{
    public bool HashChainValidated { get; set; }
    public bool CompletenessValidated { get; set; }
    public bool ConsistencyValidated { get; set; }
    public bool DeepValidationPerformed { get; set; }
}

public class AuditIntegrityIssue
{
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public long? AffectedAuditId { get; set; }
    public DateTime? AffectedTimestamp { get; set; }
    public string? RecommendedAction { get; set; }
}

public class AuditIntegrityStatistics
{
    public int TotalRecordsChecked { get; set; }
    public int ValidRecords { get; set; }
    public int InvalidRecords { get; set; }
    public TimeSpan ValidationDuration { get; set; }
    public DateTime? EarliestRecord { get; set; }
    public DateTime? LatestRecord { get; set; }
}