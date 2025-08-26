using ACS.Service.Domain;

namespace ACS.Service.Requests;

/// <summary>
/// Service request for compliance reports
/// </summary>
public record ComplianceReportRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? ComplianceFramework { get; init; }
    public bool IncludeDetails { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for usage statistics reports
/// </summary>
public record UsageStatisticsReportRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? ResourceType { get; init; }
    public string? GroupBy { get; init; }
    public bool IncludeMetrics { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for exporting reports
/// </summary>
public record ExportReportRequest
{
    public string ReportType { get; init; } = string.Empty;
    public string Format { get; init; } = "PDF";
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for scheduling reports
/// </summary>
public record ScheduleReportRequest
{
    public string ReportName { get; init; } = string.Empty;
    public string ReportType { get; init; } = string.Empty;
    public string Schedule { get; init; } = string.Empty;
    public string Format { get; init; } = "PDF";
    public ICollection<string> Recipients { get; init; } = new List<string>();
    public Dictionary<string, object> Parameters { get; init; } = new();
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for custom analytics
/// </summary>
public record CustomAnalyticsRequest
{
    public string AnalyticsType { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public Dictionary<string, object> Filters { get; init; } = new();
    public ICollection<string> Metrics { get; init; } = new List<string>();
    public string GroupBy { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for audit log searches
/// </summary>
public record AuditLogSearchRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? EntityType { get; init; }
    public string? Action { get; init; }
    public string? UserId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for user activity reports
/// </summary>
public record UserActivityReportRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int? UserId { get; init; }
    public string? ActivityType { get; init; }
    public bool IncludeDetails { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for access pattern analysis
/// </summary>
public record AccessPatternAnalysisRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? ResourcePattern { get; init; }
    public string? UserPattern { get; init; }
    public string AnalysisType { get; init; } = "Summary";
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for permission usage analysis
/// </summary>
public record PermissionUsageAnalysisRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int? PermissionId { get; init; }
    public string? UsageType { get; init; }
    public bool IncludeUnused { get; init; } = false;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for role analysis
/// </summary>
public record RoleAnalysisRequest
{
    public int? RoleId { get; init; }
    public bool IncludePermissions { get; init; } = true;
    public bool IncludeUsers { get; init; } = true;
    public bool IncludeHierarchy { get; init; } = false;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for security dashboard data
/// </summary>
public record SecurityDashboardRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public ICollection<string> MetricTypes { get; init; } = new List<string>();
    public bool IncludeAlerts { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for risk assessment
/// </summary>
public record RiskAssessmentRequest
{
    public string AssessmentType { get; init; } = "Comprehensive";
    public int? EntityId { get; init; }
    public bool IncludeRecommendations { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for user analytics reports
/// </summary>
public record UserAnalyticsReportRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public bool IncludeActivityMetrics { get; init; } = true;
    public bool IncludeUsagePatterns { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for access patterns reports
/// </summary>
public record AccessPatternsReportRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? UserId { get; init; }
    public string? ResourceType { get; init; }
    public bool IncludeAnomalies { get; init; } = false;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for permission usage reports
/// </summary>
public record PermissionUsageReportRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int? PermissionId { get; init; }
    public bool IncludeUnused { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for role analysis reports
/// </summary>
public record RoleAnalysisReportRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int? RoleId { get; init; }
    public bool IncludePermissions { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for security reports
/// </summary>
public record SecurityReportRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? SecurityEventType { get; init; }
    public string? Severity { get; init; }
    public bool IncludeThreats { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for risk assessment reports
/// </summary>
public record RiskAssessmentReportRequest
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string AssessmentType { get; init; } = "Comprehensive";
    public bool IncludeRecommendations { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
}