using ACS.WebApi.Resources;

namespace ACS.WebApi.Models.Responses;

/// <summary>
/// Response model for audit log entries
/// </summary>
public class AuditLogResponse
{
    /// <summary>
    /// Audit log entry ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Type of event that occurred
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the event
    /// </summary>
    public string EventDescription { get; set; } = string.Empty;

    /// <summary>
    /// ID of the user who triggered the event
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Name of the user who triggered the event
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Type of entity affected by the event
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// ID of the entity affected by the event
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// IP address of the user
    /// </summary>
    public string? IPAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Additional event data as JSON
    /// </summary>
    public string? AdditionalData { get; set; }

    /// <summary>
    /// Risk level of the event
    /// </summary>
    public string RiskLevel { get; set; } = "Low";

    /// <summary>
    /// Session ID if available
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Correlation ID for tracking related events
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Response model for security events
/// </summary>
public class SecurityEventResponse
{
    /// <summary>
    /// Security event ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Type of security event
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Risk level of the event
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Description of the security event
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// User ID involved in the event
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// User name involved in the event
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// IP address of the user
    /// </summary>
    public string? IPAddress { get; set; }

    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Whether the event has been mitigated
    /// </summary>
    public bool Mitigated { get; set; }

    /// <summary>
    /// Notes about mitigation actions taken
    /// </summary>
    public string? MitigationNotes { get; set; }

    /// <summary>
    /// Threat indicators associated with the event
    /// </summary>
    public List<string> ThreatIndicators { get; set; } = new();

    /// <summary>
    /// Resources affected by the event
    /// </summary>
    public List<string> AffectedResources { get; set; } = new();

    /// <summary>
    /// Attack patterns detected
    /// </summary>
    public List<string> AttackPatterns { get; set; } = new();

    /// <summary>
    /// Confidence score of the detection (0-100)
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// Potential impact of the security event
    /// </summary>
    public string PotentialImpact { get; set; } = string.Empty;

    /// <summary>
    /// Recommended actions
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Response model for compliance reports
/// </summary>
public class ComplianceReportResponse
{
    /// <summary>
    /// Compliance standard assessed
    /// </summary>
    public string ComplianceStandard { get; set; } = string.Empty;

    /// <summary>
    /// Period covered by the report
    /// </summary>
    public DateRange ReportPeriod { get; set; } = new();

    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Overall compliance score (0-100)
    /// </summary>
    public decimal OverallScore { get; set; }

    /// <summary>
    /// Overall compliance level
    /// </summary>
    public string ComplianceLevel { get; set; } = string.Empty;

    /// <summary>
    /// Individual compliance requirements
    /// </summary>
    public List<ComplianceRequirementResponse> Requirements { get; set; } = new();

    /// <summary>
    /// Compliance violations found
    /// </summary>
    public List<ComplianceViolationResponse> Violations { get; set; } = new();

    /// <summary>
    /// Recommendations for improving compliance
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Summary statistics
    /// </summary>
    public ComplianceSummaryStats Summary { get; set; } = new();

    /// <summary>
    /// Trend analysis compared to previous periods
    /// </summary>
    public ComplianceTrendAnalysis TrendAnalysis { get; set; } = new();
}

/// <summary>
/// Response model for compliance requirements
/// </summary>
public class ComplianceRequirementResponse
{
    /// <summary>
    /// Requirement identifier
    /// </summary>
    public string RequirementId { get; set; } = string.Empty;

    /// <summary>
    /// Requirement description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Compliance status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Compliance score for this requirement
    /// </summary>
    public decimal Score { get; set; }

    /// <summary>
    /// Evidence supporting compliance
    /// </summary>
    public List<string> Evidence { get; set; } = new();

    /// <summary>
    /// When this requirement was last assessed
    /// </summary>
    public DateTime LastAssessed { get; set; }

    /// <summary>
    /// Next assessment due date
    /// </summary>
    public DateTime? NextAssessmentDue { get; set; }

    /// <summary>
    /// Control category
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Risk level if non-compliant
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;
}

/// <summary>
/// Response model for compliance violations
/// </summary>
public class ComplianceViolationResponse
{
    /// <summary>
    /// Violation identifier
    /// </summary>
    public string ViolationId { get; set; } = string.Empty;

    /// <summary>
    /// Related requirement ID
    /// </summary>
    public string RequirementId { get; set; } = string.Empty;

    /// <summary>
    /// Violation description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Severity of the violation
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// When the violation was detected
    /// </summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>
    /// Current status of the violation
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Recommended remedy action
    /// </summary>
    public string? RemedyAction { get; set; }

    /// <summary>
    /// Entities affected by the violation
    /// </summary>
    public List<string> AffectedEntities { get; set; } = new();

    /// <summary>
    /// Expected resolution date
    /// </summary>
    public DateTime? ExpectedResolution { get; set; }

    /// <summary>
    /// Owner responsible for remediation
    /// </summary>
    public string? AssignedTo { get; set; }
}

/// <summary>
/// Compliance summary statistics
/// </summary>
public class ComplianceSummaryStats
{
    /// <summary>
    /// Total requirements assessed
    /// </summary>
    public int TotalRequirements { get; set; }

    /// <summary>
    /// Number of compliant requirements
    /// </summary>
    public int CompliantRequirements { get; set; }

    /// <summary>
    /// Number of non-compliant requirements
    /// </summary>
    public int NonCompliantRequirements { get; set; }

    /// <summary>
    /// Number of requirements not applicable
    /// </summary>
    public int NotApplicableRequirements { get; set; }

    /// <summary>
    /// Number of critical violations
    /// </summary>
    public int CriticalViolations { get; set; }

    /// <summary>
    /// Number of high severity violations
    /// </summary>
    public int HighSeverityViolations { get; set; }

    /// <summary>
    /// Number of medium severity violations
    /// </summary>
    public int MediumSeverityViolations { get; set; }

    /// <summary>
    /// Number of low severity violations
    /// </summary>
    public int LowSeverityViolations { get; set; }
}

/// <summary>
/// Compliance trend analysis
/// </summary>
public class ComplianceTrendAnalysis
{
    /// <summary>
    /// Score trend compared to previous period
    /// </summary>
    public decimal ScoreTrend { get; set; }

    /// <summary>
    /// Violation trend compared to previous period
    /// </summary>
    public int ViolationTrend { get; set; }

    /// <summary>
    /// Historical compliance scores
    /// </summary>
    public List<ComplianceHistoryPoint> HistoricalScores { get; set; } = new();

    /// <summary>
    /// Improvement areas identified
    /// </summary>
    public List<string> ImprovementAreas { get; set; } = new();

    /// <summary>
    /// Areas showing positive trends
    /// </summary>
    public List<string> PositiveTrends { get; set; } = new();
}

/// <summary>
/// Historical compliance data point
/// </summary>
public class ComplianceHistoryPoint
{
    /// <summary>
    /// Assessment date
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Compliance score for that period
    /// </summary>
    public decimal Score { get; set; }

    /// <summary>
    /// Number of violations for that period
    /// </summary>
    public int ViolationCount { get; set; }
}

/// <summary>
/// Response model for audit statistics
/// </summary>
public class AuditStatisticsResponse
{
    /// <summary>
    /// Period covered by the statistics
    /// </summary>
    public DateRange ReportPeriod { get; set; } = new();

    /// <summary>
    /// Total number of audit events
    /// </summary>
    public int TotalEvents { get; set; }

    /// <summary>
    /// Events grouped by type
    /// </summary>
    public Dictionary<string, int> EventsByType { get; set; } = new();

    /// <summary>
    /// Events grouped by user
    /// </summary>
    public Dictionary<string, int> EventsByUser { get; set; } = new();

    /// <summary>
    /// Number of security events
    /// </summary>
    public int SecurityEvents { get; set; }

    /// <summary>
    /// Number of high-risk events
    /// </summary>
    public int HighRiskEvents { get; set; }

    /// <summary>
    /// Number of compliance violations
    /// </summary>
    public int ComplianceViolations { get; set; }

    /// <summary>
    /// Average events per day
    /// </summary>
    public double AverageEventsPerDay { get; set; }

    /// <summary>
    /// Date with the most events
    /// </summary>
    public DateTime? PeakEventDate { get; set; }

    /// <summary>
    /// Top active users
    /// </summary>
    public List<string> TopUsers { get; set; } = new();

    /// <summary>
    /// Event distribution by hour of day
    /// </summary>
    public Dictionary<int, int> EventsByHour { get; set; } = new();

    /// <summary>
    /// Event distribution by day of week
    /// </summary>
    public Dictionary<string, int> EventsByDayOfWeek { get; set; } = new();

    /// <summary>
    /// Risk level distribution
    /// </summary>
    public Dictionary<string, int> RiskLevelDistribution { get; set; } = new();
}

/// <summary>
/// Response model for security analysis reports
/// </summary>
public class SecurityAnalysisReportResponse
{
    /// <summary>
    /// Type of analysis performed
    /// </summary>
    public string AnalysisType { get; set; } = string.Empty;

    /// <summary>
    /// When the analysis was executed
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Analysis results by category
    /// </summary>
    public Dictionary<string, SecurityAnalysisResultResponse> Results { get; set; } = new();

    /// <summary>
    /// Overall risk level determined
    /// </summary>
    public string OverallRiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Executive summary of findings
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Security recommendations
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Analysis execution metrics
    /// </summary>
    public AnalysisExecutionMetrics ExecutionMetrics { get; set; } = new();

    /// <summary>
    /// Key findings requiring immediate attention
    /// </summary>
    public List<CriticalFinding> CriticalFindings { get; set; } = new();
}

/// <summary>
/// Security analysis result for a specific category
/// </summary>
public class SecurityAnalysisResultResponse
{
    /// <summary>
    /// Entity type analyzed
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Total entities in scope
    /// </summary>
    public int TotalEntities { get; set; }

    /// <summary>
    /// Entities matching the analysis criteria
    /// </summary>
    public int MatchingEntities { get; set; }

    /// <summary>
    /// Risk level of the findings
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Description of what was analyzed
    /// </summary>
    public string QueryDescription { get; set; } = string.Empty;

    /// <summary>
    /// When the analysis was performed
    /// </summary>
    public DateTime AnalysisDate { get; set; }

    /// <summary>
    /// Percentage of entities matching the criteria
    /// </summary>
    public double MatchPercentage { get; set; }

    /// <summary>
    /// Time taken to execute the analysis (milliseconds)
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Sample of entities found (limited for API response)
    /// </summary>
    public List<object> Entities { get; set; } = new();

    /// <summary>
    /// Detailed findings breakdown
    /// </summary>
    public Dictionary<string, int> FindingsBreakdown { get; set; } = new();
}

/// <summary>
/// Analysis execution metrics
/// </summary>
public class AnalysisExecutionMetrics
{
    /// <summary>
    /// Total analysis execution time
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Number of queries executed
    /// </summary>
    public int QueriesExecuted { get; set; }

    /// <summary>
    /// Database performance metrics
    /// </summary>
    public Dictionary<string, object> DatabaseMetrics { get; set; } = new();

    /// <summary>
    /// Memory usage during analysis
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Cache hit ratio during analysis
    /// </summary>
    public double CacheHitRatio { get; set; }
}

/// <summary>
/// Critical security finding
/// </summary>
public class CriticalFinding
{
    /// <summary>
    /// Finding category
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Description of the finding
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Risk level of the finding
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Entities affected
    /// </summary>
    public List<string> AffectedEntities { get; set; } = new();

    /// <summary>
    /// Recommended immediate actions
    /// </summary>
    public List<string> ImmediateActions { get; set; } = new();

    /// <summary>
    /// Business impact assessment
    /// </summary>
    public string BusinessImpact { get; set; } = string.Empty;
}

/// <summary>
/// Response model for user activity audit
/// </summary>
public class UserActivityAuditResponse
{
    /// <summary>
    /// User ID being audited
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// User name
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Period covered by the audit
    /// </summary>
    public DateRange AuditPeriod { get; set; } = new();

    /// <summary>
    /// Total number of actions performed
    /// </summary>
    public int TotalActions { get; set; }

    /// <summary>
    /// Number of security events involving the user
    /// </summary>
    public int SecurityEvents { get; set; }

    /// <summary>
    /// Permission changes affecting the user
    /// </summary>
    public List<PermissionChangeEventResponse> PermissionChanges { get; set; } = new();

    /// <summary>
    /// Access attempts by the user
    /// </summary>
    public List<AccessAttemptEventResponse> AccessAttempts { get; set; } = new();

    /// <summary>
    /// Risk score for the user (0-100)
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Factors contributing to the risk score
    /// </summary>
    public List<string> RiskFactors { get; set; } = new();

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime? LastActivity { get; set; }

    /// <summary>
    /// Activity patterns detected
    /// </summary>
    public UserActivityPatterns ActivityPatterns { get; set; } = new();

    /// <summary>
    /// Behavioral anomalies detected
    /// </summary>
    public List<BehavioralAnomaly> BehavioralAnomalies { get; set; } = new();
}

/// <summary>
/// Permission change event details
/// </summary>
public class PermissionChangeEventResponse
{
    /// <summary>
    /// When the change occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Type of change (Grant, Revoke, Modify)
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// Permission that was changed
    /// </summary>
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// Previous permission value
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// New permission value
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Who made the change
    /// </summary>
    public string ChangedBy { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the change
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Impact assessment of the change
    /// </summary>
    public string Impact { get; set; } = string.Empty;
}

/// <summary>
/// Access attempt event details
/// </summary>
public class AccessAttemptEventResponse
{
    /// <summary>
    /// When the access was attempted
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Resource URI that was accessed
    /// </summary>
    public string ResourceUri { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method used
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// Whether access was granted
    /// </summary>
    public bool Granted { get; set; }

    /// <summary>
    /// Reason for denial if access was not granted
    /// </summary>
    public string? DenialReason { get; set; }

    /// <summary>
    /// IP address of the access attempt
    /// </summary>
    public string? IPAddress { get; set; }

    /// <summary>
    /// User agent used for the request
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Response time for the request
    /// </summary>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    /// HTTP status code returned
    /// </summary>
    public int StatusCode { get; set; }
}

/// <summary>
/// User activity patterns
/// </summary>
public class UserActivityPatterns
{
    /// <summary>
    /// Most active hours of the day
    /// </summary>
    public List<int> MostActiveHours { get; set; } = new();

    /// <summary>
    /// Most commonly accessed resources
    /// </summary>
    public Dictionary<string, int> CommonResources { get; set; } = new();

    /// <summary>
    /// Geographic access patterns
    /// </summary>
    public Dictionary<string, int> AccessLocations { get; set; } = new();

    /// <summary>
    /// Device/browser patterns
    /// </summary>
    public Dictionary<string, int> DevicePatterns { get; set; } = new();

    /// <summary>
    /// Average session duration
    /// </summary>
    public TimeSpan AverageSessionDuration { get; set; }

    /// <summary>
    /// Login frequency pattern
    /// </summary>
    public string LoginPattern { get; set; } = string.Empty;
}

/// <summary>
/// Behavioral anomaly detected for a user
/// </summary>
public class BehavioralAnomaly
{
    /// <summary>
    /// Type of anomaly detected
    /// </summary>
    public string AnomalyType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the anomaly
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// When the anomaly was detected
    /// </summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>
    /// Confidence level of the detection (0-100)
    /// </summary>
    public int ConfidenceLevel { get; set; }

    /// <summary>
    /// Risk level associated with the anomaly
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Evidence supporting the anomaly detection
    /// </summary>
    public List<string> Evidence { get; set; } = new();

    /// <summary>
    /// Recommended actions to investigate
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();
}