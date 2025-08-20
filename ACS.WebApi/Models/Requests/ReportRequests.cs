using System.ComponentModel.DataAnnotations;

namespace ACS.WebApi.Models.Requests;

#region Base Report Requests

/// <summary>
/// Base class for report requests with common parameters
/// </summary>
public abstract class BaseReportRequest
{
    /// <summary>
    /// Start date for the report period
    /// </summary>
    [Required(ErrorMessage = "Start date is required")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-30);

    /// <summary>
    /// End date for the report period
    /// </summary>
    [Required(ErrorMessage = "End date is required")]
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time zone for date calculations
    /// </summary>
    [StringLength(50, ErrorMessage = "Time zone cannot exceed 50 characters")]
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// Whether to include detailed data in the report
    /// </summary>
    public bool IncludeDetails { get; set; } = true;

    /// <summary>
    /// Additional filters to apply to the report
    /// </summary>
    public Dictionary<string, object> Filters { get; set; } = new();

    /// <summary>
    /// Validates the date range
    /// </summary>
    public bool IsValidDateRange => EndDate >= StartDate && StartDate <= DateTime.UtcNow;
}

#endregion

#region User Analytics Reports

/// <summary>
/// Request model for user analytics report
/// </summary>
public class UserAnalyticsReportRequest : BaseReportRequest
{
    /// <summary>
    /// Whether to include inactive users in the analysis
    /// </summary>
    public bool IncludeInactive { get; set; } = false;

    /// <summary>
    /// Grouping criteria for the analysis (Department, Role, Location, etc.)
    /// </summary>
    public List<string> GroupBy { get; set; } = new();

    /// <summary>
    /// Specific user IDs to include in the analysis
    /// </summary>
    public List<int> UserIds { get; set; } = new();

    /// <summary>
    /// Departments to include in the analysis
    /// </summary>
    public List<string> Departments { get; set; } = new();

    /// <summary>
    /// Roles to include in the analysis
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Whether to include engagement metrics
    /// </summary>
    public bool IncludeEngagementMetrics { get; set; } = true;

    /// <summary>
    /// Whether to include trend analysis
    /// </summary>
    public bool IncludeTrendAnalysis { get; set; } = true;

    /// <summary>
    /// Minimum activity threshold for including users
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Activity threshold must be non-negative")]
    public int MinimumActivityThreshold { get; set; } = 0;
}

/// <summary>
/// Request model for access patterns report
/// </summary>
public class AccessPatternsReportRequest : BaseReportRequest
{
    /// <summary>
    /// Resource filters to apply
    /// </summary>
    public List<string> ResourceFilters { get; set; } = new();

    /// <summary>
    /// User filters to apply
    /// </summary>
    public List<string> UserFilters { get; set; } = new();

    /// <summary>
    /// Access result filters (Success, Failure, All)
    /// </summary>
    [RegularExpression("^(Success|Failure|All)$", ErrorMessage = "Access result filter must be Success, Failure, or All")]
    public string AccessResult { get; set; } = "All";

    /// <summary>
    /// Whether to include security alerts
    /// </summary>
    public bool IncludeSecurityAlerts { get; set; } = true;

    /// <summary>
    /// Whether to include performance metrics
    /// </summary>
    public bool IncludePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Whether to analyze unusual patterns
    /// </summary>
    public bool AnalyzeUnusualPatterns { get; set; } = true;

    /// <summary>
    /// Anomaly detection sensitivity (Low, Medium, High)
    /// </summary>
    [RegularExpression("^(Low|Medium|High)$", ErrorMessage = "Sensitivity must be Low, Medium, or High")]
    public string AnomalySensitivity { get; set; } = "Medium";

    /// <summary>
    /// Geographic regions to include
    /// </summary>
    public List<string> GeographicRegions { get; set; } = new();

    /// <summary>
    /// IP address ranges to analyze
    /// </summary>
    public List<string> IpAddressRanges { get; set; } = new();
}

#endregion

#region Permission and Role Reports

/// <summary>
/// Request model for permission usage report
/// </summary>
public class PermissionUsageReportRequest : BaseReportRequest
{
    /// <summary>
    /// Whether to include unused permissions in the analysis
    /// </summary>
    public bool IncludeUnused { get; set; } = true;

    /// <summary>
    /// Whether to group results by entity
    /// </summary>
    public bool GroupByEntity { get; set; } = false;

    /// <summary>
    /// Specific resources to analyze
    /// </summary>
    public List<string> ResourceFilters { get; set; } = new();

    /// <summary>
    /// Specific entities to analyze
    /// </summary>
    public List<int> EntityFilters { get; set; } = new();

    /// <summary>
    /// HTTP verbs to include in the analysis
    /// </summary>
    public List<string> HttpVerbs { get; set; } = new();

    /// <summary>
    /// Permission schemes to analyze
    /// </summary>
    public List<string> PermissionSchemes { get; set; } = new();

    /// <summary>
    /// Whether to include expired permissions
    /// </summary>
    public bool IncludeExpired { get; set; } = false;

    /// <summary>
    /// Whether to analyze permission effectiveness
    /// </summary>
    public bool AnalyzeEffectiveness { get; set; } = true;

    /// <summary>
    /// Minimum usage threshold for including permissions
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Usage threshold must be non-negative")]
    public int MinimumUsageThreshold { get; set; } = 0;
}

/// <summary>
/// Request model for role analysis report
/// </summary>
public class RoleAnalysisReportRequest
{
    /// <summary>
    /// Whether to include inactive roles in the analysis
    /// </summary>
    public bool IncludeInactive { get; set; } = false;

    /// <summary>
    /// Whether to analyze role usage patterns
    /// </summary>
    public bool AnalyzeUsage { get; set; } = true;

    /// <summary>
    /// Whether to include recommendations for role optimization
    /// </summary>
    public bool IncludeRecommendations { get; set; } = true;

    /// <summary>
    /// Specific roles to analyze
    /// </summary>
    public List<string> RoleFilters { get; set; } = new();

    /// <summary>
    /// Role categories to include
    /// </summary>
    public List<string> RoleCategories { get; set; } = new();

    /// <summary>
    /// Whether to analyze role hierarchy
    /// </summary>
    public bool AnalyzeHierarchy { get; set; } = true;

    /// <summary>
    /// Whether to identify permission overlaps
    /// </summary>
    public bool IdentifyOverlaps { get; set; } = true;

    /// <summary>
    /// Whether to analyze security risks
    /// </summary>
    public bool AnalyzeSecurityRisks { get; set; } = true;

    /// <summary>
    /// Minimum user count for including roles
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "User count threshold must be non-negative")]
    public int MinimumUserCount { get; set; } = 0;
}

#endregion

#region Security Reports

/// <summary>
/// Request model for security dashboard report
/// </summary>
public class SecurityReportRequest
{
    /// <summary>
    /// Time range for the security analysis (1h, 24h, 7d, 30d)
    /// </summary>
    [RegularExpression("^(1h|24h|7d|30d)$", ErrorMessage = "Time range must be 1h, 24h, 7d, or 30d")]
    public string TimeRange { get; set; } = "24h";

    /// <summary>
    /// Whether to include threat analysis
    /// </summary>
    public bool IncludeThreats { get; set; } = true;

    /// <summary>
    /// Whether to include vulnerability assessment
    /// </summary>
    public bool IncludeVulnerabilities { get; set; } = true;

    /// <summary>
    /// Security event types to include
    /// </summary>
    public List<string> EventTypes { get; set; } = new();

    /// <summary>
    /// Risk levels to include (Low, Medium, High, Critical)
    /// </summary>
    public List<string> RiskLevels { get; set; } = new() { "High", "Critical" };

    /// <summary>
    /// Whether to include compliance status
    /// </summary>
    public bool IncludeComplianceStatus { get; set; } = true;

    /// <summary>
    /// Whether to include incident analysis
    /// </summary>
    public bool IncludeIncidentAnalysis { get; set; } = true;

    /// <summary>
    /// Whether to include access anomalies
    /// </summary>
    public bool IncludeAccessAnomalies { get; set; } = true;

    /// <summary>
    /// Security domains to analyze
    /// </summary>
    public List<string> SecurityDomains { get; set; } = new();
}

/// <summary>
/// Request model for risk assessment report
/// </summary>
public class RiskAssessmentReportRequest
{
    /// <summary>
    /// Scope of the risk assessment (System, Users, Permissions, All)
    /// </summary>
    [RegularExpression("^(System|Users|Permissions|Resources|All)$", ErrorMessage = "Invalid assessment scope")]
    public string AssessmentScope { get; set; } = "All";

    /// <summary>
    /// Whether to include historical risk data
    /// </summary>
    public bool IncludeHistorical { get; set; } = true;

    /// <summary>
    /// Minimum risk threshold to include in the report
    /// </summary>
    [RegularExpression("^(Low|Medium|High|Critical)$", ErrorMessage = "Risk threshold must be Low, Medium, High, or Critical")]
    public string RiskThreshold { get; set; } = "Medium";

    /// <summary>
    /// Risk categories to analyze
    /// </summary>
    public List<string> RiskCategories { get; set; } = new();

    /// <summary>
    /// Whether to include mitigation strategies
    /// </summary>
    public bool IncludeMitigationStrategies { get; set; } = true;

    /// <summary>
    /// Whether to generate risk heatmap
    /// </summary>
    public bool GenerateHeatmap { get; set; } = true;

    /// <summary>
    /// Whether to analyze compliance impact
    /// </summary>
    public bool AnalyzeComplianceImpact { get; set; } = true;

    /// <summary>
    /// Time horizon for risk projection (days)
    /// </summary>
    [Range(1, 365, ErrorMessage = "Time horizon must be between 1 and 365 days")]
    public int TimeHorizonDays { get; set; } = 30;
}

#endregion

#region Compliance Reports

/// <summary>
/// Request model for compliance assessment report
/// </summary>
public class ComplianceReportRequest : BaseReportRequest
{
    /// <summary>
    /// Compliance standards to assess
    /// </summary>
    [Required(ErrorMessage = "At least one compliance standard is required")]
    [MinLength(1, ErrorMessage = "At least one compliance standard is required")]
    public List<string> ComplianceStandards { get; set; } = new();

    /// <summary>
    /// Whether to include remediation recommendations
    /// </summary>
    public bool IncludeRemediation { get; set; } = true;

    /// <summary>
    /// Compliance domains to assess (Access, Data, Security, Privacy, etc.)
    /// </summary>
    public List<string> ComplianceDomains { get; set; } = new();

    /// <summary>
    /// Organizational units to include in the assessment
    /// </summary>
    public List<string> OrganizationalUnits { get; set; } = new();

    /// <summary>
    /// Whether to include control effectiveness analysis
    /// </summary>
    public bool IncludeControlEffectiveness { get; set; } = true;

    /// <summary>
    /// Whether to assess audit readiness
    /// </summary>
    public bool AssessAuditReadiness { get; set; } = true;

    /// <summary>
    /// Whether to include certification status
    /// </summary>
    public bool IncludeCertificationStatus { get; set; } = true;

    /// <summary>
    /// Severity levels to include in findings
    /// </summary>
    public List<string> SeverityLevels { get; set; } = new() { "High", "Critical" };

    /// <summary>
    /// Whether to include trend analysis
    /// </summary>
    public bool IncludeTrendAnalysis { get; set; } = true;
}

#endregion

#region Usage and Performance Reports

/// <summary>
/// Request model for usage statistics report
/// </summary>
public class UsageStatisticsReportRequest : BaseReportRequest
{
    /// <summary>
    /// Grouping criteria for usage analysis (Hour, Day, Week, Month)
    /// </summary>
    [RegularExpression("^(Hour|Day|Week|Month)$", ErrorMessage = "Group by must be Hour, Day, Week, or Month")]
    public string GroupBy { get; set; } = "Day";

    /// <summary>
    /// Whether to include performance metrics
    /// </summary>
    public bool IncludePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Whether to include capacity analysis
    /// </summary>
    public bool IncludeCapacityAnalysis { get; set; } = true;

    /// <summary>
    /// API endpoints to include in the analysis
    /// </summary>
    public List<string> EndpointFilters { get; set; } = new();

    /// <summary>
    /// User agents to analyze
    /// </summary>
    public List<string> UserAgentFilters { get; set; } = new();

    /// <summary>
    /// Geographic regions to include
    /// </summary>
    public List<string> GeographicFilters { get; set; } = new();

    /// <summary>
    /// Device types to analyze
    /// </summary>
    public List<string> DeviceTypeFilters { get; set; } = new();

    /// <summary>
    /// Whether to include session analysis
    /// </summary>
    public bool IncludeSessionAnalysis { get; set; } = true;

    /// <summary>
    /// Whether to analyze peak usage patterns
    /// </summary>
    public bool AnalyzePeakPatterns { get; set; } = true;

    /// <summary>
    /// Minimum request count threshold
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Request threshold must be non-negative")]
    public int MinimumRequestThreshold { get; set; } = 0;
}

#endregion

#region Report Export and Scheduling

/// <summary>
/// Request model for exporting reports
/// </summary>
public class ExportReportRequest
{
    /// <summary>
    /// Type of report to export
    /// </summary>
    [Required(ErrorMessage = "Report type is required")]
    [RegularExpression("^(UserAnalytics|AccessPatterns|PermissionUsage|RoleAnalysis|SecurityDashboard|RiskAssessment|Compliance|UsageStatistics|Custom)$", 
        ErrorMessage = "Invalid report type")]
    public string ReportType { get; set; } = string.Empty;

    /// <summary>
    /// Export format (CSV, JSON, XML, Excel, PDF)
    /// </summary>
    [Required(ErrorMessage = "Export format is required")]
    [RegularExpression("^(CSV|JSON|XML|Excel|PDF)$", ErrorMessage = "Export format must be CSV, JSON, XML, Excel, or PDF")]
    public string ExportFormat { get; set; } = "JSON";

    /// <summary>
    /// Parameters for generating the report
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Whether to compress the exported file
    /// </summary>
    public bool CompressOutput { get; set; } = false;

    /// <summary>
    /// Whether to include metadata in the export
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Custom filename for the export (optional)
    /// </summary>
    [StringLength(255, ErrorMessage = "Filename cannot exceed 255 characters")]
    public string? CustomFilename { get; set; }

    /// <summary>
    /// Fields to include in the export (empty = all fields)
    /// </summary>
    public List<string> IncludeFields { get; set; } = new();

    /// <summary>
    /// Fields to exclude from the export
    /// </summary>
    public List<string> ExcludeFields { get; set; } = new();

    /// <summary>
    /// Maximum number of records to export
    /// </summary>
    [Range(1, 1000000, ErrorMessage = "Max records must be between 1 and 1,000,000")]
    public int MaxRecords { get; set; } = 100000;
}

/// <summary>
/// Request model for scheduling recurring reports
/// </summary>
public class ScheduleReportRequest
{
    /// <summary>
    /// Type of report to schedule
    /// </summary>
    [Required(ErrorMessage = "Report type is required")]
    [RegularExpression("^(UserAnalytics|AccessPatterns|PermissionUsage|RoleAnalysis|SecurityDashboard|RiskAssessment|Compliance|UsageStatistics)$", 
        ErrorMessage = "Invalid report type")]
    public string ReportType { get; set; } = string.Empty;

    /// <summary>
    /// Schedule expression (cron format)
    /// </summary>
    [Required(ErrorMessage = "Schedule is required")]
    [StringLength(100, ErrorMessage = "Schedule expression cannot exceed 100 characters")]
    public string Schedule { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for generating the report
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Export format for scheduled reports
    /// </summary>
    [RegularExpression("^(CSV|JSON|XML|Excel|PDF)$", ErrorMessage = "Export format must be CSV, JSON, XML, Excel, or PDF")]
    public string ExportFormat { get; set; } = "PDF";

    /// <summary>
    /// Email recipients for the scheduled report
    /// </summary>
    [Required(ErrorMessage = "At least one recipient is required")]
    [MinLength(1, ErrorMessage = "At least one recipient is required")]
    public List<string> Recipients { get; set; } = new();

    /// <summary>
    /// Email subject template
    /// </summary>
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
    public string? EmailSubject { get; set; }

    /// <summary>
    /// Email body template
    /// </summary>
    [StringLength(2000, ErrorMessage = "Email body cannot exceed 2000 characters")]
    public string? EmailBody { get; set; }

    /// <summary>
    /// Whether the scheduled report is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Start date for the scheduled report
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for the scheduled report
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Time zone for scheduling
    /// </summary>
    [StringLength(50, ErrorMessage = "Time zone cannot exceed 50 characters")]
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// Priority of the scheduled report (Low, Normal, High)
    /// </summary>
    [RegularExpression("^(Low|Normal|High)$", ErrorMessage = "Priority must be Low, Normal, or High")]
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// Whether to send the report only if there are findings
    /// </summary>
    public bool SendOnlyIfFindings { get; set; } = false;
}

#endregion

#region Custom Analytics

/// <summary>
/// Request model for custom analytics queries
/// </summary>
public class CustomAnalyticsRequest
{
    /// <summary>
    /// Name of the custom query to execute
    /// </summary>
    [Required(ErrorMessage = "Query name is required")]
    [StringLength(100, ErrorMessage = "Query name cannot exceed 100 characters")]
    public string QueryName { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for the custom query
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Date range for the query
    /// </summary>
    public DateRange? DateRange { get; set; }

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    [Range(1, 100000, ErrorMessage = "Max results must be between 1 and 100,000")]
    public int MaxResults { get; set; } = 10000;

    /// <summary>
    /// Whether to include aggregated results
    /// </summary>
    public bool IncludeAggregations { get; set; } = true;

    /// <summary>
    /// Whether to include chart data
    /// </summary>
    public bool IncludeCharts { get; set; } = true;

    /// <summary>
    /// Output format for the results (Table, Chart, Summary, All)
    /// </summary>
    [RegularExpression("^(Table|Chart|Summary|All)$", ErrorMessage = "Output format must be Table, Chart, Summary, or All")]
    public string OutputFormat { get; set; } = "All";

    /// <summary>
    /// Grouping fields for aggregation
    /// </summary>
    public List<string> GroupBy { get; set; } = new();

    /// <summary>
    /// Sorting criteria
    /// </summary>
    public List<SortCriteria> SortBy { get; set; } = new();

    /// <summary>
    /// Additional filters to apply
    /// </summary>
    public List<QueryFilter> Filters { get; set; } = new();

    /// <summary>
    /// Timeout for query execution (in seconds)
    /// </summary>
    [Range(1, 300, ErrorMessage = "Timeout must be between 1 and 300 seconds")]
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Sort criteria for custom analytics
/// </summary>
public class SortCriteria
{
    /// <summary>
    /// Field to sort by
    /// </summary>
    [Required(ErrorMessage = "Sort field is required")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Sort direction (Asc, Desc)
    /// </summary>
    [RegularExpression("^(Asc|Desc)$", ErrorMessage = "Sort direction must be Asc or Desc")]
    public string Direction { get; set; } = "Asc";
}

/// <summary>
/// Query filter for custom analytics
/// </summary>
public class QueryFilter
{
    /// <summary>
    /// Field to filter on
    /// </summary>
    [Required(ErrorMessage = "Filter field is required")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Filter operator (Equals, NotEquals, Contains, GreaterThan, LessThan, etc.)
    /// </summary>
    [Required(ErrorMessage = "Filter operator is required")]
    public string Operator { get; set; } = string.Empty;

    /// <summary>
    /// Filter value
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Whether the filter is case sensitive (for string operations)
    /// </summary>
    public bool CaseSensitive { get; set; } = false;
}

#endregion