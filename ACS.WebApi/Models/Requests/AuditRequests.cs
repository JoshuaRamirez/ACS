using System.ComponentModel.DataAnnotations;

namespace ACS.WebApi.Models.Requests;

/// <summary>
/// Request model for querying audit logs
/// </summary>
public class GetAuditLogsRequest : PagedRequest
{
    /// <summary>
    /// Start date for audit log query
    /// </summary>
    [Required(ErrorMessage = "Start date is required")]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date for audit log query
    /// </summary>
    [Required(ErrorMessage = "End date is required")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Filter by user ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Filter by event type
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Filter by entity type
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Filter by risk level
    /// </summary>
    public string? RiskLevel { get; set; }

    /// <summary>
    /// Filter by IP address
    /// </summary>
    public string? IPAddress { get; set; }

    /// <summary>
    /// Search term for event descriptions
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Sort field (Timestamp, EventType, UserId, RiskLevel)
    /// </summary>
    public string SortBy { get; set; } = "Timestamp";

    /// <summary>
    /// Sort direction (asc, desc)
    /// </summary>
    public string SortDirection { get; set; } = "desc";

    /// <summary>
    /// Whether to include detailed information
    /// </summary>
    public bool IncludeDetails { get; set; } = false;

    /// <summary>
    /// Validates the date range
    /// </summary>
    public bool IsValidDateRange => EndDate >= StartDate && StartDate <= DateTime.UtcNow;
}

/// <summary>
/// Request model for querying security events
/// </summary>
public class GetSecurityEventsRequest : PagedRequest
{
    /// <summary>
    /// Start date for security events query
    /// </summary>
    [Required(ErrorMessage = "Start date is required")]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date for security events query
    /// </summary>
    [Required(ErrorMessage = "End date is required")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Filter by risk level (Low, Medium, High, Critical)
    /// </summary>
    public string? RiskLevel { get; set; }

    /// <summary>
    /// Filter by event category
    /// </summary>
    public string? EventCategory { get; set; }

    /// <summary>
    /// Filter by user ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Filter by IP address
    /// </summary>
    public string? IPAddress { get; set; }

    /// <summary>
    /// Show only unmitigated events
    /// </summary>
    public bool OnlyUnmitigated { get; set; } = false;

    /// <summary>
    /// Sort field (Timestamp, RiskLevel, EventType)
    /// </summary>
    public string SortBy { get; set; } = "Timestamp";

    /// <summary>
    /// Sort direction (asc, desc)
    /// </summary>
    public string SortDirection { get; set; } = "desc";
}

/// <summary>
/// Request model for compliance reports
/// </summary>
public class GetComplianceReportRequest
{
    /// <summary>
    /// Compliance standard (GDPR, SOC2, HIPAA, PCI-DSS, etc.)
    /// </summary>
    [Required(ErrorMessage = "Compliance standard is required")]
    [StringLength(50, ErrorMessage = "Compliance standard cannot exceed 50 characters")]
    public string ComplianceStandard { get; set; } = string.Empty;

    /// <summary>
    /// Start date for compliance assessment
    /// </summary>
    [Required(ErrorMessage = "Start date is required")]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date for compliance assessment
    /// </summary>
    [Required(ErrorMessage = "End date is required")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Whether to include detailed evidence
    /// </summary>
    public bool IncludeDetails { get; set; } = true;

    /// <summary>
    /// Whether to include remediation recommendations
    /// </summary>
    public bool IncludeRecommendations { get; set; } = true;

    /// <summary>
    /// Filter by specific requirement IDs
    /// </summary>
    public List<string> RequirementIds { get; set; } = new();

    /// <summary>
    /// Include only failed requirements
    /// </summary>
    public bool OnlyFailures { get; set; } = false;

    /// <summary>
    /// Assessment scope (Organization, Department, System, etc.)
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Validates the date range
    /// </summary>
    public bool IsValidDateRange => EndDate >= StartDate && StartDate <= DateTime.UtcNow;
}

/// <summary>
/// Request model for exporting audit logs
/// </summary>
public class ExportAuditLogsRequest
{
    /// <summary>
    /// Start date for export
    /// </summary>
    [Required(ErrorMessage = "Start date is required")]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date for export
    /// </summary>
    [Required(ErrorMessage = "End date is required")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Export format (CSV, JSON, XML)
    /// </summary>
    [Required(ErrorMessage = "Export format is required")]
    [RegularExpression("^(CSV|JSON|XML)$", ErrorMessage = "Export format must be CSV, JSON, or XML")]
    public string ExportFormat { get; set; } = "CSV";

    /// <summary>
    /// Filter by user ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Filter by event type
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Filter by entity type
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Filter by risk level
    /// </summary>
    public string? RiskLevel { get; set; }

    /// <summary>
    /// Whether to include detailed information
    /// </summary>
    public bool IncludeDetails { get; set; } = true;

    /// <summary>
    /// Maximum number of records to export
    /// </summary>
    [Range(1, 1000000, ErrorMessage = "Maximum records must be between 1 and 1,000,000")]
    public int MaxRecords { get; set; } = 100000;

    /// <summary>
    /// Export reason for audit trail
    /// </summary>
    [Required(ErrorMessage = "Export reason is required")]
    [StringLength(500, ErrorMessage = "Export reason cannot exceed 500 characters")]
    public string ExportReason { get; set; } = string.Empty;

    /// <summary>
    /// Whether to compress the export file
    /// </summary>
    public bool CompressOutput { get; set; } = true;

    /// <summary>
    /// Fields to include in export
    /// </summary>
    public List<string> IncludeFields { get; set; } = new();

    /// <summary>
    /// Fields to exclude from export
    /// </summary>
    public List<string> ExcludeFields { get; set; } = new();

    /// <summary>
    /// Validates the export request
    /// </summary>
    public bool IsValid => EndDate >= StartDate && 
                          StartDate <= DateTime.UtcNow && 
                          !string.IsNullOrWhiteSpace(ExportFormat) && 
                          !string.IsNullOrWhiteSpace(ExportReason);
}

/// <summary>
/// Request model for security analysis
/// </summary>
public class SecurityAnalysisRequest
{
    /// <summary>
    /// Type of security analysis to perform
    /// </summary>
    [Required(ErrorMessage = "Analysis type is required")]
    [RegularExpression("^(security-audit|compliance-audit|privilege-escalation|access-anomaly|threat-detection|vulnerability-assessment)$", 
        ErrorMessage = "Invalid analysis type")]
    public string AnalysisType { get; set; } = string.Empty;

    /// <summary>
    /// Analysis scope (All, Users, Groups, Roles, Permissions, Resources)
    /// </summary>
    public List<string> Scope { get; set; } = new() { "All" };

    /// <summary>
    /// Start date for analysis data
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for analysis data
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Risk threshold (only include results above this level)
    /// </summary>
    [RegularExpression("^(Low|Medium|High|Critical)$", ErrorMessage = "Invalid risk threshold")]
    public string RiskThreshold { get; set; } = "Medium";

    /// <summary>
    /// Whether to include detailed results
    /// </summary>
    public bool IncludeDetails { get; set; } = true;

    /// <summary>
    /// Maximum number of results per analysis category
    /// </summary>
    [Range(1, 10000, ErrorMessage = "Max results must be between 1 and 10,000")]
    public int MaxResults { get; set; } = 1000;

    /// <summary>
    /// Custom analysis parameters
    /// </summary>
    public Dictionary<string, object> AnalysisParameters { get; set; } = new();

    /// <summary>
    /// Whether to generate recommendations
    /// </summary>
    public bool GenerateRecommendations { get; set; } = true;

    /// <summary>
    /// Whether to save the analysis results
    /// </summary>
    public bool SaveResults { get; set; } = true;

    /// <summary>
    /// Notification recipients for high-risk findings
    /// </summary>
    public List<string> NotificationRecipients { get; set; } = new();
}

/// <summary>
/// Request model for user activity reports
/// </summary>
public class GetUserActivityReportRequest
{
    /// <summary>
    /// User ID to analyze
    /// </summary>
    [Required(ErrorMessage = "User ID is required")]
    public int UserId { get; set; }

    /// <summary>
    /// Start date for activity analysis
    /// </summary>
    [Required(ErrorMessage = "Start date is required")]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date for activity analysis
    /// </summary>
    [Required(ErrorMessage = "End date is required")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Include security events in the report
    /// </summary>
    public bool IncludeSecurityEvents { get; set; } = true;

    /// <summary>
    /// Include permission changes in the report
    /// </summary>
    public bool IncludePermissionChanges { get; set; } = true;

    /// <summary>
    /// Include access attempts in the report
    /// </summary>
    public bool IncludeAccessAttempts { get; set; } = true;

    /// <summary>
    /// Include risk analysis in the report
    /// </summary>
    public bool IncludeRiskAnalysis { get; set; } = true;

    /// <summary>
    /// Activity types to include
    /// </summary>
    public List<string> ActivityTypes { get; set; } = new();

    /// <summary>
    /// Resources to focus on
    /// </summary>
    public List<string> ResourcePatterns { get; set; } = new();

    /// <summary>
    /// Report format (Summary, Detailed, Forensic)
    /// </summary>
    public string ReportFormat { get; set; } = "Detailed";

    /// <summary>
    /// Whether to include behavioral analysis
    /// </summary>
    public bool IncludeBehavioralAnalysis { get; set; } = false;
}

/// <summary>
/// Request model for audit log retention policy
/// </summary>
public class SetRetentionPolicyRequest
{
    /// <summary>
    /// Event types to apply retention policy to
    /// </summary>
    [Required(ErrorMessage = "Event types are required")]
    [MinLength(1, ErrorMessage = "At least one event type is required")]
    public List<string> EventTypes { get; set; } = new();

    /// <summary>
    /// Retention period in days
    /// </summary>
    [Required(ErrorMessage = "Retention period is required")]
    [Range(1, 3650, ErrorMessage = "Retention period must be between 1 and 3,650 days")]
    public int RetentionDays { get; set; }

    /// <summary>
    /// Archive location for expired logs
    /// </summary>
    [StringLength(500, ErrorMessage = "Archive location cannot exceed 500 characters")]
    public string? ArchiveLocation { get; set; }

    /// <summary>
    /// Whether to compress archived logs
    /// </summary>
    public bool CompressArchive { get; set; } = true;

    /// <summary>
    /// Whether to encrypt archived logs
    /// </summary>
    public bool EncryptArchive { get; set; } = true;

    /// <summary>
    /// Auto-purge expired logs after retention period
    /// </summary>
    public bool AutoPurge { get; set; } = false;

    /// <summary>
    /// Notification recipients for retention events
    /// </summary>
    public List<string> NotificationRecipients { get; set; } = new();

    /// <summary>
    /// Policy effective date
    /// </summary>
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Policy description
    /// </summary>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
}

/// <summary>
/// Request model for audit configuration
/// </summary>
public class UpdateAuditConfigurationRequest
{
    /// <summary>
    /// Enable audit logging
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Enable security event detection
    /// </summary>
    public bool EnableSecurityEventDetection { get; set; } = true;

    /// <summary>
    /// Enable compliance monitoring
    /// </summary>
    public bool EnableComplianceMonitoring { get; set; } = true;

    /// <summary>
    /// Audit log level (Information, Warning, Error, Critical)
    /// </summary>
    [RegularExpression("^(Information|Warning|Error|Critical)$", ErrorMessage = "Invalid log level")]
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Events to audit
    /// </summary>
    public List<string> AuditEvents { get; set; } = new();

    /// <summary>
    /// Events to exclude from auditing
    /// </summary>
    public List<string> ExcludeEvents { get; set; } = new();

    /// <summary>
    /// Real-time monitoring enabled
    /// </summary>
    public bool EnableRealTimeMonitoring { get; set; } = true;

    /// <summary>
    /// Alert thresholds for security events
    /// </summary>
    public Dictionary<string, int> AlertThresholds { get; set; } = new();

    /// <summary>
    /// Notification settings
    /// </summary>
    public Dictionary<string, object> NotificationSettings { get; set; } = new();

    /// <summary>
    /// Data retention settings
    /// </summary>
    public Dictionary<string, int> RetentionSettings { get; set; } = new();

    /// <summary>
    /// Performance settings
    /// </summary>
    public Dictionary<string, object> PerformanceSettings { get; set; } = new();
}