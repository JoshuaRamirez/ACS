namespace ACS.WebApi.Models.Responses;

#region Base Report Responses

/// <summary>
/// Base class for report responses with common fields
/// </summary>
public abstract class BaseReportResponse
{
    /// <summary>
    /// Date range covered by the report
    /// </summary>
    public DateRange ReportPeriod { get; set; } = new();

    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Time taken to generate the report
    /// </summary>
    public TimeSpan GenerationTime { get; set; }

    /// <summary>
    /// Report version or identifier
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User who generated the report
    /// </summary>
    public string? GeneratedBy { get; set; }

    /// <summary>
    /// Additional metadata about the report
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

#endregion

#region User Analytics Reports

/// <summary>
/// Response model for user analytics report
/// </summary>
public class UserAnalyticsReportResponse : BaseReportResponse
{
    /// <summary>
    /// Total number of users in the system
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Number of active users during the report period
    /// </summary>
    public int ActiveUsers { get; set; }

    /// <summary>
    /// Number of inactive users during the report period
    /// </summary>
    public int InactiveUsers { get; set; }

    /// <summary>
    /// Number of new users created during the report period
    /// </summary>
    public int NewUsers { get; set; }

    /// <summary>
    /// User growth rate as a percentage
    /// </summary>
    public double UserGrowthRate { get; set; }

    /// <summary>
    /// User distribution by role
    /// </summary>
    public Dictionary<string, int> UsersByRole { get; set; } = new();

    /// <summary>
    /// User distribution by group
    /// </summary>
    public Dictionary<string, int> UsersByGroup { get; set; } = new();

    /// <summary>
    /// User distribution by department
    /// </summary>
    public Dictionary<string, int> UsersByDepartment { get; set; } = new();

    /// <summary>
    /// User login frequency distribution
    /// </summary>
    public Dictionary<string, int> LoginFrequency { get; set; } = new();

    /// <summary>
    /// Most active users during the report period
    /// </summary>
    public List<UserActivitySummary> MostActiveUsers { get; set; } = new();

    /// <summary>
    /// Least active users during the report period
    /// </summary>
    public List<UserActivitySummary> LeastActiveUsers { get; set; } = new();

    /// <summary>
    /// User engagement metrics
    /// </summary>
    public UserEngagementMetrics UserEngagementMetrics { get; set; } = new();

    /// <summary>
    /// Trend analysis data
    /// </summary>
    public UserTrendAnalysis TrendAnalysis { get; set; } = new();

    /// <summary>
    /// User demographics breakdown
    /// </summary>
    public UserDemographics Demographics { get; set; } = new();
}

/// <summary>
/// User activity summary
/// </summary>
public class UserActivitySummary
{
    /// <summary>
    /// User ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// User's email
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's department
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Number of login sessions
    /// </summary>
    public int LoginSessions { get; set; }

    /// <summary>
    /// Total time spent in the system
    /// </summary>
    public TimeSpan TotalTimeSpent { get; set; }

    /// <summary>
    /// Number of actions performed
    /// </summary>
    public int ActionsPerformed { get; set; }

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime? LastActivity { get; set; }

    /// <summary>
    /// Activity score (0-100)
    /// </summary>
    public int ActivityScore { get; set; }
}

/// <summary>
/// User engagement metrics
/// </summary>
public class UserEngagementMetrics
{
    /// <summary>
    /// Daily active users average
    /// </summary>
    public double DailyActiveUsers { get; set; }

    /// <summary>
    /// Weekly active users average
    /// </summary>
    public double WeeklyActiveUsers { get; set; }

    /// <summary>
    /// Monthly active users average
    /// </summary>
    public double MonthlyActiveUsers { get; set; }

    /// <summary>
    /// Average sessions per user
    /// </summary>
    public double AverageSessionsPerUser { get; set; }

    /// <summary>
    /// Average time per session
    /// </summary>
    public TimeSpan AverageTimePerSession { get; set; }

    /// <summary>
    /// User retention rate percentage
    /// </summary>
    public double RetentionRate { get; set; }

    /// <summary>
    /// User churn rate percentage
    /// </summary>
    public double ChurnRate { get; set; }

    /// <summary>
    /// Engagement score distribution
    /// </summary>
    public Dictionary<string, int> EngagementScoreDistribution { get; set; } = new();

    /// <summary>
    /// Feature adoption rates
    /// </summary>
    public Dictionary<string, double> FeatureAdoptionRates { get; set; } = new();
}

/// <summary>
/// User trend analysis
/// </summary>
public class UserTrendAnalysis
{
    /// <summary>
    /// Growth trend (Increasing, Stable, Decreasing)
    /// </summary>
    public string GrowthTrend { get; set; } = string.Empty;

    /// <summary>
    /// Activity trend (Increasing, Stable, Decreasing)
    /// </summary>
    public string ActivityTrend { get; set; } = string.Empty;

    /// <summary>
    /// Engagement trend (Improving, Stable, Declining)
    /// </summary>
    public string EngagementTrend { get; set; } = string.Empty;

    /// <summary>
    /// Predicted growth for next period
    /// </summary>
    public double PredictedGrowth { get; set; }

    /// <summary>
    /// Seasonal patterns identified
    /// </summary>
    public List<string> SeasonalPatterns { get; set; } = new();

    /// <summary>
    /// Historical data points
    /// </summary>
    public List<TrendDataPoint> HistoricalData { get; set; } = new();

    /// <summary>
    /// Key insights from trend analysis
    /// </summary>
    public List<string> KeyInsights { get; set; } = new();
}

/// <summary>
/// Trend data point
/// </summary>
public class TrendDataPoint
{
    /// <summary>
    /// Date of the data point
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Number of users
    /// </summary>
    public int UserCount { get; set; }

    /// <summary>
    /// Number of active users
    /// </summary>
    public int ActiveUserCount { get; set; }

    /// <summary>
    /// Activity level
    /// </summary>
    public double ActivityLevel { get; set; }

    /// <summary>
    /// Engagement score
    /// </summary>
    public double EngagementScore { get; set; }
}

/// <summary>
/// User demographics information
/// </summary>
public class UserDemographics
{
    /// <summary>
    /// Age distribution
    /// </summary>
    public Dictionary<string, int> AgeDistribution { get; set; } = new();

    /// <summary>
    /// Geographic distribution
    /// </summary>
    public Dictionary<string, int> GeographicDistribution { get; set; } = new();

    /// <summary>
    /// Tenure distribution
    /// </summary>
    public Dictionary<string, int> TenureDistribution { get; set; } = new();

    /// <summary>
    /// Job function distribution
    /// </summary>
    public Dictionary<string, int> JobFunctionDistribution { get; set; } = new();

    /// <summary>
    /// Access level distribution
    /// </summary>
    public Dictionary<string, int> AccessLevelDistribution { get; set; } = new();
}

#endregion

#region Access Patterns Reports

/// <summary>
/// Response model for access patterns report
/// </summary>
public class AccessPatternsReportResponse : BaseReportResponse
{
    /// <summary>
    /// Total number of access attempts
    /// </summary>
    public int TotalAccessAttempts { get; set; }

    /// <summary>
    /// Number of successful access attempts
    /// </summary>
    public int SuccessfulAccesses { get; set; }

    /// <summary>
    /// Number of failed access attempts
    /// </summary>
    public int FailedAccesses { get; set; }

    /// <summary>
    /// Overall success rate percentage
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Most frequently accessed resources
    /// </summary>
    public List<ResourceAccessSummary> MostAccessedResources { get; set; } = new();

    /// <summary>
    /// Access patterns by time of day
    /// </summary>
    public Dictionary<int, int> AccessByTimeOfDay { get; set; } = new();

    /// <summary>
    /// Access patterns by day of week
    /// </summary>
    public Dictionary<string, int> AccessByDayOfWeek { get; set; } = new();

    /// <summary>
    /// Access patterns by geographic location
    /// </summary>
    public Dictionary<string, int> AccessByLocation { get; set; } = new();

    /// <summary>
    /// Unusual access patterns detected
    /// </summary>
    public List<UnusualAccessPattern> UnusualAccessPatterns { get; set; } = new();

    /// <summary>
    /// Security alerts generated
    /// </summary>
    public List<SecurityAlert> SecurityAlerts { get; set; } = new();

    /// <summary>
    /// Access performance metrics
    /// </summary>
    public AccessPerformanceMetrics PerformanceMetrics { get; set; } = new();

    /// <summary>
    /// Device and browser analysis
    /// </summary>
    public DeviceBrowserAnalysis DeviceAnalysis { get; set; } = new();

    /// <summary>
    /// Peak usage analysis
    /// </summary>
    public PeakUsageAnalysis PeakUsage { get; set; } = new();
}

/// <summary>
/// Resource access summary
/// </summary>
public class ResourceAccessSummary
{
    /// <summary>
    /// Resource ID
    /// </summary>
    public int ResourceId { get; set; }

    /// <summary>
    /// Resource URI
    /// </summary>
    public string ResourceUri { get; set; } = string.Empty;

    /// <summary>
    /// Resource type
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Total access count
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Unique users who accessed
    /// </summary>
    public int UniqueUsers { get; set; }

    /// <summary>
    /// Success rate for this resource
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Average response time
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Most common access methods
    /// </summary>
    public Dictionary<string, int> AccessMethods { get; set; } = new();

    /// <summary>
    /// Peak access times
    /// </summary>
    public List<PeakAccessTime> PeakTimes { get; set; } = new();
}

/// <summary>
/// Unusual access pattern
/// </summary>
public class UnusualAccessPattern
{
    /// <summary>
    /// Pattern ID
    /// </summary>
    public string PatternId { get; set; } = string.Empty;

    /// <summary>
    /// Pattern type
    /// </summary>
    public string PatternType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the unusual pattern
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Risk level
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// When the pattern was detected
    /// </summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>
    /// Affected users or resources
    /// </summary>
    public List<string> AffectedEntities { get; set; } = new();

    /// <summary>
    /// Confidence score of the detection
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Recommended actions
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Security alert information
/// </summary>
public class SecurityAlert
{
    /// <summary>
    /// Alert ID
    /// </summary>
    public string AlertId { get; set; } = string.Empty;

    /// <summary>
    /// Alert type
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Alert severity
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Alert message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When the alert was triggered
    /// </summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>
    /// Source of the alert
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Alert status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Additional alert details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Access performance metrics
/// </summary>
public class AccessPerformanceMetrics
{
    /// <summary>
    /// Average response time across all access attempts
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// 95th percentile response time
    /// </summary>
    public TimeSpan P95ResponseTime { get; set; }

    /// <summary>
    /// 99th percentile response time
    /// </summary>
    public TimeSpan P99ResponseTime { get; set; }

    /// <summary>
    /// Requests processed per second
    /// </summary>
    public double ThroughputPerSecond { get; set; }

    /// <summary>
    /// Error rate percentage
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// System availability percentage
    /// </summary>
    public double AvailabilityPercentage { get; set; }

    /// <summary>
    /// Performance by time periods
    /// </summary>
    public Dictionary<DateTime, PerformanceSnapshot> PerformanceByTime { get; set; } = new();

    /// <summary>
    /// Slowest endpoints
    /// </summary>
    public List<EndpointPerformance> SlowestEndpoints { get; set; } = new();
}

/// <summary>
/// Performance snapshot at a point in time
/// </summary>
public class PerformanceSnapshot
{
    /// <summary>
    /// Response time at this point
    /// </summary>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    /// Throughput at this point
    /// </summary>
    public double Throughput { get; set; }

    /// <summary>
    /// Error rate at this point
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Active connections at this point
    /// </summary>
    public int ActiveConnections { get; set; }
}

/// <summary>
/// Endpoint performance information
/// </summary>
public class EndpointPerformance
{
    /// <summary>
    /// Endpoint URI
    /// </summary>
    public string EndpointUri { get; set; } = string.Empty;

    /// <summary>
    /// Average response time
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Request count
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// Error rate for this endpoint
    /// </summary>
    public double ErrorRate { get; set; }
}

/// <summary>
/// Device and browser analysis
/// </summary>
public class DeviceBrowserAnalysis
{
    /// <summary>
    /// Device types used for access
    /// </summary>
    public Dictionary<string, int> DeviceTypes { get; set; } = new();

    /// <summary>
    /// Operating systems used
    /// </summary>
    public Dictionary<string, int> OperatingSystems { get; set; } = new();

    /// <summary>
    /// Browser types used
    /// </summary>
    public Dictionary<string, int> BrowserTypes { get; set; } = new();

    /// <summary>
    /// Mobile vs desktop usage
    /// </summary>
    public Dictionary<string, int> MobileVsDesktop { get; set; } = new();

    /// <summary>
    /// Screen resolutions
    /// </summary>
    public Dictionary<string, int> ScreenResolutions { get; set; } = new();
}

/// <summary>
/// Peak usage analysis
/// </summary>
public class PeakUsageAnalysis
{
    /// <summary>
    /// Peak hour of the day
    /// </summary>
    public int PeakHour { get; set; }

    /// <summary>
    /// Peak day of the week
    /// </summary>
    public string PeakDayOfWeek { get; set; } = string.Empty;

    /// <summary>
    /// Peak access count
    /// </summary>
    public int PeakAccessCount { get; set; }

    /// <summary>
    /// Peak concurrent users
    /// </summary>
    public int PeakConcurrentUsers { get; set; }

    /// <summary>
    /// Peak times throughout the report period
    /// </summary>
    public List<PeakAccessTime> PeakTimes { get; set; } = new();
}

/// <summary>
/// Peak access time information
/// </summary>
public class PeakAccessTime
{
    /// <summary>
    /// Date and time of peak access
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Number of accesses during peak
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Duration of the peak period
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Reason for the peak (if identifiable)
    /// </summary>
    public string? Reason { get; set; }
}

#endregion

#region Permission Usage Reports

/// <summary>
/// Response model for permission usage report
/// </summary>
public class PermissionUsageReportResponse : BaseReportResponse
{
    /// <summary>
    /// Total number of permissions in the system
    /// </summary>
    public int TotalPermissions { get; set; }

    /// <summary>
    /// Number of actively used permissions
    /// </summary>
    public int ActivePermissions { get; set; }

    /// <summary>
    /// Number of unused permissions
    /// </summary>
    public int UnusedPermissions { get; set; }

    /// <summary>
    /// Number of expired permissions
    /// </summary>
    public int ExpiredPermissions { get; set; }

    /// <summary>
    /// Most frequently used permissions
    /// </summary>
    public List<PermissionUsageSummary> MostUsedPermissions { get; set; } = new();

    /// <summary>
    /// List of unused permissions (candidates for cleanup)
    /// </summary>
    public List<PermissionUsageSummary> UnusedPermissionsList { get; set; } = new();

    /// <summary>
    /// Permission usage by resource
    /// </summary>
    public Dictionary<string, int> PermissionsByResource { get; set; } = new();

    /// <summary>
    /// Permission usage by entity (user/role/group)
    /// </summary>
    public Dictionary<string, int> PermissionsByEntity { get; set; } = new();

    /// <summary>
    /// Permission usage by HTTP verb
    /// </summary>
    public Dictionary<string, int> PermissionsByHttpVerb { get; set; } = new();

    /// <summary>
    /// Permission efficiency metrics
    /// </summary>
    public PermissionEfficiencyMetrics EfficiencyMetrics { get; set; } = new();

    /// <summary>
    /// Recommended permissions for cleanup
    /// </summary>
    public List<CleanupRecommendation> RecommendedCleanup { get; set; } = new();

    /// <summary>
    /// Permission overlap analysis
    /// </summary>
    public PermissionOverlapAnalysis OverlapAnalysis { get; set; } = new();
}

/// <summary>
/// Permission usage summary
/// </summary>
public class PermissionUsageSummary
{
    /// <summary>
    /// Permission ID
    /// </summary>
    public int PermissionId { get; set; }

    /// <summary>
    /// Entity ID that has the permission
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// Entity name
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// Entity type (User, Role, Group)
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Resource URI
    /// </summary>
    public string ResourceUri { get; set; } = string.Empty;

    /// <summary>
    /// HTTP verb
    /// </summary>
    public string HttpVerb { get; set; } = string.Empty;

    /// <summary>
    /// Usage count during the report period
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Last time the permission was used
    /// </summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>
    /// Permission created date
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Permission expiration date
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Whether the permission is effective
    /// </summary>
    public bool IsEffective { get; set; }

    /// <summary>
    /// Risk level associated with this permission
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;
}

/// <summary>
/// Permission efficiency metrics
/// </summary>
public class PermissionEfficiencyMetrics
{
    /// <summary>
    /// Overall permission utilization rate (0-100)
    /// </summary>
    public double UtilizationRate { get; set; }

    /// <summary>
    /// Permission redundancy rate (0-100)
    /// </summary>
    public double RedundancyRate { get; set; }

    /// <summary>
    /// Number of optimization opportunities identified
    /// </summary>
    public int OptimizationOpportunities { get; set; }

    /// <summary>
    /// Number of permissions requiring maintenance
    /// </summary>
    public int MaintenanceRequired { get; set; }

    /// <summary>
    /// Overall efficiency score (0-100)
    /// </summary>
    public double EfficiencyScore { get; set; }

    /// <summary>
    /// Efficiency trends over time
    /// </summary>
    public List<EfficiencyTrendPoint> EfficiencyTrends { get; set; } = new();

    /// <summary>
    /// Cost savings potential from optimization
    /// </summary>
    public CostSavingsAnalysis CostSavings { get; set; } = new();
}

/// <summary>
/// Efficiency trend data point
/// </summary>
public class EfficiencyTrendPoint
{
    /// <summary>
    /// Date of the measurement
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Efficiency score at this date
    /// </summary>
    public double EfficiencyScore { get; set; }

    /// <summary>
    /// Utilization rate at this date
    /// </summary>
    public double UtilizationRate { get; set; }

    /// <summary>
    /// Number of optimizations performed
    /// </summary>
    public int OptimizationsPerformed { get; set; }
}

/// <summary>
/// Cost savings analysis
/// </summary>
public class CostSavingsAnalysis
{
    /// <summary>
    /// Estimated administrative time savings (hours)
    /// </summary>
    public double TimeSavingsHours { get; set; }

    /// <summary>
    /// Estimated cost savings (in currency)
    /// </summary>
    public decimal EstimatedCostSavings { get; set; }

    /// <summary>
    /// Security risk reduction percentage
    /// </summary>
    public double RiskReductionPercentage { get; set; }

    /// <summary>
    /// Performance improvement percentage
    /// </summary>
    public double PerformanceImprovementPercentage { get; set; }
}

/// <summary>
/// Cleanup recommendation
/// </summary>
public class CleanupRecommendation
{
    /// <summary>
    /// Recommendation type
    /// </summary>
    public string RecommendationType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the recommendation
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Priority level (Low, Medium, High, Critical)
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Affected permissions count
    /// </summary>
    public int AffectedPermissions { get; set; }

    /// <summary>
    /// Estimated impact of implementing the recommendation
    /// </summary>
    public string EstimatedImpact { get; set; } = string.Empty;

    /// <summary>
    /// Steps to implement the recommendation
    /// </summary>
    public List<string> ImplementationSteps { get; set; } = new();

    /// <summary>
    /// Risk assessment of implementing the recommendation
    /// </summary>
    public string RiskAssessment { get; set; } = string.Empty;
}

/// <summary>
/// Permission overlap analysis
/// </summary>
public class PermissionOverlapAnalysis
{
    /// <summary>
    /// Total number of permission overlaps found
    /// </summary>
    public int TotalOverlaps { get; set; }

    /// <summary>
    /// Overlap details by entity pairs
    /// </summary>
    public List<PermissionOverlap> OverlapDetails { get; set; } = new();

    /// <summary>
    /// Consolidation opportunities
    /// </summary>
    public List<ConsolidationOpportunity> ConsolidationOpportunities { get; set; } = new();

    /// <summary>
    /// Overlap statistics
    /// </summary>
    public OverlapStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Permission overlap between entities
/// </summary>
public class PermissionOverlap
{
    /// <summary>
    /// First entity in the overlap
    /// </summary>
    public string Entity1 { get; set; } = string.Empty;

    /// <summary>
    /// Second entity in the overlap
    /// </summary>
    public string Entity2 { get; set; } = string.Empty;

    /// <summary>
    /// Number of overlapping permissions
    /// </summary>
    public int OverlapCount { get; set; }

    /// <summary>
    /// Percentage of overlap
    /// </summary>
    public double OverlapPercentage { get; set; }

    /// <summary>
    /// List of overlapping permission details
    /// </summary>
    public List<string> OverlappingPermissions { get; set; } = new();

    /// <summary>
    /// Significance of the overlap (Low, Medium, High)
    /// </summary>
    public string Significance { get; set; } = string.Empty;
}

/// <summary>
/// Consolidation opportunity
/// </summary>
public class ConsolidationOpportunity
{
    /// <summary>
    /// Entities that can be consolidated
    /// </summary>
    public List<string> Entities { get; set; } = new();

    /// <summary>
    /// Potential new role name
    /// </summary>
    public string SuggestedRoleName { get; set; } = string.Empty;

    /// <summary>
    /// Number of permissions in consolidated role
    /// </summary>
    public int ConsolidatedPermissionCount { get; set; }

    /// <summary>
    /// Estimated reduction in total permissions
    /// </summary>
    public int PermissionReduction { get; set; }

    /// <summary>
    /// Confidence level of the recommendation
    /// </summary>
    public double ConfidenceLevel { get; set; }
}

/// <summary>
/// Overlap statistics
/// </summary>
public class OverlapStatistics
{
    /// <summary>
    /// Average overlap percentage
    /// </summary>
    public double AverageOverlapPercentage { get; set; }

    /// <summary>
    /// Maximum overlap found
    /// </summary>
    public double MaxOverlapPercentage { get; set; }

    /// <summary>
    /// Entities with highest overlap counts
    /// </summary>
    public Dictionary<string, int> HighestOverlapEntities { get; set; } = new();

    /// <summary>
    /// Most commonly overlapping permissions
    /// </summary>
    public Dictionary<string, int> CommonOverlappingPermissions { get; set; } = new();
}

#endregion

#region Role Analysis Reports

/// <summary>
/// Response model for role analysis report
/// </summary>
public class RoleAnalysisReportResponse : BaseReportResponse
{
    /// <summary>
    /// Total number of roles in the system
    /// </summary>
    public int TotalRoles { get; set; }

    /// <summary>
    /// Number of active roles
    /// </summary>
    public int ActiveRoles { get; set; }

    /// <summary>
    /// Number of unused roles
    /// </summary>
    public int UnusedRoles { get; set; }

    /// <summary>
    /// Number of roles with overlapping permissions
    /// </summary>
    public int OverlappingRoles { get; set; }

    /// <summary>
    /// Role distribution across users
    /// </summary>
    public Dictionary<string, int> RoleDistribution { get; set; } = new();

    /// <summary>
    /// Role hierarchy structure
    /// </summary>
    public RoleHierarchyResponse RoleHierarchy { get; set; } = new();

    /// <summary>
    /// Permission overlap analysis between roles
    /// </summary>
    public Dictionary<string, double> PermissionOverlap { get; set; } = new();

    /// <summary>
    /// Role effectiveness metrics
    /// </summary>
    public Dictionary<string, RoleEffectiveness> RoleEffectiveness { get; set; } = new();

    /// <summary>
    /// Consolidation opportunities
    /// </summary>
    public List<RoleConsolidationOpportunity> ConsolidationOpportunities { get; set; } = new();

    /// <summary>
    /// Security risks identified
    /// </summary>
    public List<RoleSecurityRisk> SecurityRisks { get; set; } = new();

    /// <summary>
    /// Recommendations for role optimization
    /// </summary>
    public List<RoleRecommendation> Recommendations { get; set; } = new();

    /// <summary>
    /// Role usage analytics
    /// </summary>
    public RoleUsageAnalytics UsageAnalytics { get; set; } = new();
}

/// <summary>
/// Role hierarchy response structure
/// </summary>
public class RoleHierarchyResponse
{
    /// <summary>
    /// Role ID
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// Role name
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Parent roles
    /// </summary>
    public List<RoleHierarchyResponse> ParentRoles { get; set; } = new();

    /// <summary>
    /// Child roles
    /// </summary>
    public List<RoleHierarchyResponse> ChildRoles { get; set; } = new();

    /// <summary>
    /// Number of permissions for this role
    /// </summary>
    public int PermissionCount { get; set; }

    /// <summary>
    /// Number of users assigned to this role
    /// </summary>
    public int UserCount { get; set; }

    /// <summary>
    /// Depth in the hierarchy
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Whether this role is a leaf node
    /// </summary>
    public bool IsLeafNode => !ChildRoles.Any();

    /// <summary>
    /// Whether this role is a root node
    /// </summary>
    public bool IsRootNode => !ParentRoles.Any();
}

/// <summary>
/// Role effectiveness metrics
/// </summary>
public class RoleEffectiveness
{
    /// <summary>
    /// Role utilization rate (0-100)
    /// </summary>
    public double UtilizationRate { get; set; }

    /// <summary>
    /// Permission efficiency score (0-100)
    /// </summary>
    public double EfficiencyScore { get; set; }

    /// <summary>
    /// Security score (0-100)
    /// </summary>
    public double SecurityScore { get; set; }

    /// <summary>
    /// Overall effectiveness score (0-100)
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// Usage frequency
    /// </summary>
    public string UsageFrequency { get; set; } = string.Empty;

    /// <summary>
    /// Last usage date
    /// </summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>
    /// Performance impact rating
    /// </summary>
    public string PerformanceImpact { get; set; } = string.Empty;
}

/// <summary>
/// Role consolidation opportunity
/// </summary>
public class RoleConsolidationOpportunity
{
    /// <summary>
    /// Roles that can be consolidated
    /// </summary>
    public List<string> RolesToConsolidate { get; set; } = new();

    /// <summary>
    /// Suggested name for the consolidated role
    /// </summary>
    public string SuggestedConsolidatedRoleName { get; set; } = string.Empty;

    /// <summary>
    /// Potential reduction in role count
    /// </summary>
    public int RoleReduction { get; set; }

    /// <summary>
    /// Permission overlap percentage
    /// </summary>
    public double OverlapPercentage { get; set; }

    /// <summary>
    /// Users affected by consolidation
    /// </summary>
    public int AffectedUsers { get; set; }

    /// <summary>
    /// Estimated complexity of consolidation
    /// </summary>
    public string ConsolidationComplexity { get; set; } = string.Empty;

    /// <summary>
    /// Benefits of consolidation
    /// </summary>
    public List<string> Benefits { get; set; } = new();

    /// <summary>
    /// Risks of consolidation
    /// </summary>
    public List<string> Risks { get; set; } = new();
}

/// <summary>
/// Role security risk
/// </summary>
public class RoleSecurityRisk
{
    /// <summary>
    /// Role name
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Risk type
    /// </summary>
    public string RiskType { get; set; } = string.Empty;

    /// <summary>
    /// Risk level (Low, Medium, High, Critical)
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Risk description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Potential impact
    /// </summary>
    public string PotentialImpact { get; set; } = string.Empty;

    /// <summary>
    /// Mitigation recommendations
    /// </summary>
    public List<string> MitigationRecommendations { get; set; } = new();

    /// <summary>
    /// Affected users count
    /// </summary>
    public int AffectedUsersCount { get; set; }

    /// <summary>
    /// Risk score (0-100)
    /// </summary>
    public int RiskScore { get; set; }
}

/// <summary>
/// Role recommendation
/// </summary>
public class RoleRecommendation
{
    /// <summary>
    /// Recommendation type
    /// </summary>
    public string RecommendationType { get; set; } = string.Empty;

    /// <summary>
    /// Role name the recommendation applies to
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Recommendation description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Priority level
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Expected benefits
    /// </summary>
    public List<string> ExpectedBenefits { get; set; } = new();

    /// <summary>
    /// Implementation steps
    /// </summary>
    public List<string> ImplementationSteps { get; set; } = new();

    /// <summary>
    /// Estimated effort
    /// </summary>
    public string EstimatedEffort { get; set; } = string.Empty;

    /// <summary>
    /// Risk level of implementation
    /// </summary>
    public string ImplementationRisk { get; set; } = string.Empty;
}

/// <summary>
/// Role usage analytics
/// </summary>
public class RoleUsageAnalytics
{
    /// <summary>
    /// Most frequently assigned roles
    /// </summary>
    public List<RoleUsageSummary> MostAssignedRoles { get; set; } = new();

    /// <summary>
    /// Least used roles
    /// </summary>
    public List<RoleUsageSummary> LeastUsedRoles { get; set; } = new();

    /// <summary>
    /// Role assignment trends
    /// </summary>
    public Dictionary<string, List<TrendDataPoint>> AssignmentTrends { get; set; } = new();

    /// <summary>
    /// Average permissions per role
    /// </summary>
    public double AveragePermissionsPerRole { get; set; }

    /// <summary>
    /// Average users per role
    /// </summary>
    public double AverageUsersPerRole { get; set; }

    /// <summary>
    /// Role churn rate
    /// </summary>
    public double RoleChurnRate { get; set; }
}

/// <summary>
/// Role usage summary
/// </summary>
public class RoleUsageSummary
{
    /// <summary>
    /// Role ID
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// Role name
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Number of users assigned
    /// </summary>
    public int AssignedUsers { get; set; }

    /// <summary>
    /// Number of permissions
    /// </summary>
    public int PermissionCount { get; set; }

    /// <summary>
    /// Usage frequency score
    /// </summary>
    public int UsageFrequencyScore { get; set; }

    /// <summary>
    /// Last assignment date
    /// </summary>
    public DateTime? LastAssigned { get; set; }

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

#endregion

#region Security Reports

/// <summary>
/// Response model for security dashboard
/// </summary>
public class SecurityDashboardResponse : BaseReportResponse
{
    /// <summary>
    /// Overall security score (0-100)
    /// </summary>
    public int OverallSecurityScore { get; set; }

    /// <summary>
    /// Current security level (Low, Medium, High, Critical)
    /// </summary>
    public string SecurityLevel { get; set; } = string.Empty;

    /// <summary>
    /// Current threat level (Low, Medium, High, Critical)
    /// </summary>
    public string ThreatLevel { get; set; } = string.Empty;

    /// <summary>
    /// Time range for the dashboard data
    /// </summary>
    public string TimeRange { get; set; } = string.Empty;

    /// <summary>
    /// Number of security events in the time range
    /// </summary>
    public int SecurityEvents { get; set; }

    /// <summary>
    /// Number of critical alerts
    /// </summary>
    public int CriticalAlerts { get; set; }

    /// <summary>
    /// Recent security incidents
    /// </summary>
    public List<SecurityIncident> RecentIncidents { get; set; } = new();

    /// <summary>
    /// Vulnerability assessment summary
    /// </summary>
    public VulnerabilityAssessment VulnerabilityAssessment { get; set; } = new();

    /// <summary>
    /// Access anomalies detected
    /// </summary>
    public List<AccessAnomaly> AccessAnomalies { get; set; } = new();

    /// <summary>
    /// Permission-related security risks
    /// </summary>
    public List<PermissionRisk> PermissionRisks { get; set; } = new();

    /// <summary>
    /// Compliance status overview
    /// </summary>
    public List<ComplianceStatus> ComplianceStatus { get; set; } = new();

    /// <summary>
    /// Security trends over time
    /// </summary>
    public SecurityTrends SecurityTrends { get; set; } = new();

    /// <summary>
    /// Recommended security actions
    /// </summary>
    public List<SecurityAction> RecommendedActions { get; set; } = new();

    /// <summary>
    /// Security metrics summary
    /// </summary>
    public SecurityMetricsSummary MetricsSummary { get; set; } = new();
}

/// <summary>
/// Security incident information
/// </summary>
public class SecurityIncident
{
    /// <summary>
    /// Incident ID
    /// </summary>
    public string IncidentId { get; set; } = string.Empty;

    /// <summary>
    /// Incident type
    /// </summary>
    public string IncidentType { get; set; } = string.Empty;

    /// <summary>
    /// Severity level
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Incident description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// When the incident occurred
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Incident status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Affected systems or users
    /// </summary>
    public List<string> AffectedEntities { get; set; } = new();

    /// <summary>
    /// Actions taken
    /// </summary>
    public List<string> ActionsTaken { get; set; } = new();

    /// <summary>
    /// Resolution time
    /// </summary>
    public TimeSpan? ResolutionTime { get; set; }
}

/// <summary>
/// Vulnerability assessment
/// </summary>
public class VulnerabilityAssessment
{
    /// <summary>
    /// Number of critical vulnerabilities
    /// </summary>
    public int CriticalVulnerabilities { get; set; }

    /// <summary>
    /// Number of high severity vulnerabilities
    /// </summary>
    public int HighVulnerabilities { get; set; }

    /// <summary>
    /// Number of medium severity vulnerabilities
    /// </summary>
    public int MediumVulnerabilities { get; set; }

    /// <summary>
    /// Number of low severity vulnerabilities
    /// </summary>
    public int LowVulnerabilities { get; set; }

    /// <summary>
    /// Total vulnerabilities
    /// </summary>
    public int TotalVulnerabilities { get; set; }

    /// <summary>
    /// Remediation timeline
    /// </summary>
    public Dictionary<string, DateTime> RemediationTimeline { get; set; } = new();

    /// <summary>
    /// Vulnerability trends
    /// </summary>
    public List<VulnerabilityTrend> VulnerabilityTrends { get; set; } = new();

    /// <summary>
    /// Top vulnerability categories
    /// </summary>
    public Dictionary<string, int> TopVulnerabilityCategories { get; set; } = new();
}

/// <summary>
/// Vulnerability trend data
/// </summary>
public class VulnerabilityTrend
{
    /// <summary>
    /// Date of the trend point
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Number of vulnerabilities at this date
    /// </summary>
    public int VulnerabilityCount { get; set; }

    /// <summary>
    /// Number resolved at this date
    /// </summary>
    public int ResolvedCount { get; set; }

    /// <summary>
    /// Number newly discovered at this date
    /// </summary>
    public int NewlyDiscovered { get; set; }
}

/// <summary>
/// Access anomaly information
/// </summary>
public class AccessAnomaly
{
    /// <summary>
    /// Anomaly ID
    /// </summary>
    public string AnomalyId { get; set; } = string.Empty;

    /// <summary>
    /// Anomaly type
    /// </summary>
    public string AnomalyType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the anomaly
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Risk level of the anomaly
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// When the anomaly was detected
    /// </summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>
    /// User or entity involved
    /// </summary>
    public string InvolvedEntity { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level of the detection
    /// </summary>
    public double ConfidenceLevel { get; set; }

    /// <summary>
    /// Recommended actions
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Permission-related security risk
/// </summary>
public class PermissionRisk
{
    /// <summary>
    /// Risk ID
    /// </summary>
    public string RiskId { get; set; } = string.Empty;

    /// <summary>
    /// Risk type
    /// </summary>
    public string RiskType { get; set; } = string.Empty;

    /// <summary>
    /// Risk level
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Risk description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Affected permissions count
    /// </summary>
    public int AffectedPermissions { get; set; }

    /// <summary>
    /// Potential impact
    /// </summary>
    public string PotentialImpact { get; set; } = string.Empty;

    /// <summary>
    /// Mitigation steps
    /// </summary>
    public List<string> MitigationSteps { get; set; } = new();
}

/// <summary>
/// Compliance status information
/// </summary>
public class ComplianceStatus
{
    /// <summary>
    /// Compliance standard name
    /// </summary>
    public string Standard { get; set; } = string.Empty;

    /// <summary>
    /// Compliance status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Compliance score (0-100)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Last assessment date
    /// </summary>
    public DateTime LastAssessed { get; set; }

    /// <summary>
    /// Number of compliance issues
    /// </summary>
    public int Issues { get; set; }

    /// <summary>
    /// Next assessment due date
    /// </summary>
    public DateTime? NextAssessment { get; set; }
}

/// <summary>
/// Security trends information
/// </summary>
public class SecurityTrends
{
    /// <summary>
    /// Security score trend (Improving, Stable, Declining)
    /// </summary>
    public string SecurityScoreTrend { get; set; } = string.Empty;

    /// <summary>
    /// Incident frequency trend
    /// </summary>
    public string IncidentTrend { get; set; } = string.Empty;

    /// <summary>
    /// Vulnerability discovery trend
    /// </summary>
    public string VulnerabilityTrend { get; set; } = string.Empty;

    /// <summary>
    /// Historical security scores
    /// </summary>
    public List<SecurityScoreHistoryPoint> SecurityScoreHistory { get; set; } = new();

    /// <summary>
    /// Threat level history
    /// </summary>
    public List<ThreatLevelHistoryPoint> ThreatLevelHistory { get; set; } = new();

    /// <summary>
    /// Key trend insights
    /// </summary>
    public List<string> TrendInsights { get; set; } = new();
}

/// <summary>
/// Security score history point
/// </summary>
public class SecurityScoreHistoryPoint
{
    /// <summary>
    /// Date of the score
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Security score at this date
    /// </summary>
    public int SecurityScore { get; set; }

    /// <summary>
    /// Number of incidents at this date
    /// </summary>
    public int IncidentCount { get; set; }
}

/// <summary>
/// Threat level history point
/// </summary>
public class ThreatLevelHistoryPoint
{
    /// <summary>
    /// Date of the threat level assessment
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Threat level at this date
    /// </summary>
    public string ThreatLevel { get; set; } = string.Empty;

    /// <summary>
    /// Threat score at this date
    /// </summary>
    public int ThreatScore { get; set; }
}

/// <summary>
/// Security action recommendation
/// </summary>
public class SecurityAction
{
    /// <summary>
    /// Action ID
    /// </summary>
    public string ActionId { get; set; } = string.Empty;

    /// <summary>
    /// Action type
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Priority level
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Action description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Expected impact
    /// </summary>
    public string ExpectedImpact { get; set; } = string.Empty;

    /// <summary>
    /// Estimated effort
    /// </summary>
    public string EstimatedEffort { get; set; } = string.Empty;

    /// <summary>
    /// Deadline for action
    /// </summary>
    public DateTime? Deadline { get; set; }

    /// <summary>
    /// Responsible party
    /// </summary>
    public string? ResponsibleParty { get; set; }
}

/// <summary>
/// Security metrics summary
/// </summary>
public class SecurityMetricsSummary
{
    /// <summary>
    /// Mean time to detect (MTTD) incidents
    /// </summary>
    public TimeSpan MeanTimeToDetect { get; set; }

    /// <summary>
    /// Mean time to respond (MTTR) to incidents
    /// </summary>
    public TimeSpan MeanTimeToRespond { get; set; }

    /// <summary>
    /// Security incident resolution rate
    /// </summary>
    public double IncidentResolutionRate { get; set; }

    /// <summary>
    /// False positive rate for security alerts
    /// </summary>
    public double FalsePositiveRate { get; set; }

    /// <summary>
    /// Security awareness training completion rate
    /// </summary>
    public double TrainingCompletionRate { get; set; }

    /// <summary>
    /// Patch deployment rate
    /// </summary>
    public double PatchDeploymentRate { get; set; }
}

#endregion

#region Risk Assessment Reports

/// <summary>
/// Response model for risk assessment report
/// </summary>
public class RiskAssessmentReportResponse : BaseReportResponse
{
    /// <summary>
    /// Scope of the risk assessment
    /// </summary>
    public string AssessmentScope { get; set; } = string.Empty;

    /// <summary>
    /// Overall risk score (0-100)
    /// </summary>
    public int OverallRiskScore { get; set; }

    /// <summary>
    /// Overall risk level
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Critical risk items
    /// </summary>
    public List<RiskItem> CriticalRisks { get; set; } = new();

    /// <summary>
    /// High risk items
    /// </summary>
    public List<RiskItem> HighRisks { get; set; } = new();

    /// <summary>
    /// Medium risk items
    /// </summary>
    public List<RiskItem> MediumRisks { get; set; } = new();

    /// <summary>
    /// Low risk items
    /// </summary>
    public List<RiskItem> LowRisks { get; set; } = new();

    /// <summary>
    /// Risk distribution by category
    /// </summary>
    public Dictionary<string, int> RiskCategories { get; set; } = new();

    /// <summary>
    /// Risk distribution by entity type
    /// </summary>
    public Dictionary<string, int> RiskByEntity { get; set; } = new();

    /// <summary>
    /// Risk trends over time
    /// </summary>
    public List<RiskTrendPoint> RiskTrends { get; set; } = new();

    /// <summary>
    /// Mitigation strategies
    /// </summary>
    public List<MitigationStrategy> MitigationStrategies { get; set; } = new();

    /// <summary>
    /// Risk heatmap data
    /// </summary>
    public RiskHeatmapData RiskHeatmap { get; set; } = new();

    /// <summary>
    /// Compliance impact assessment
    /// </summary>
    public ComplianceImpactAssessment ComplianceImpact { get; set; } = new();

    /// <summary>
    /// Recommended actions
    /// </summary>
    public List<RiskMitigationAction> RecommendedActions { get; set; } = new();

    /// <summary>
    /// Executive summary of the assessment
    /// </summary>
    public string ExecutiveSummary { get; set; } = string.Empty;
}

/// <summary>
/// Risk item information
/// </summary>
public class RiskItem
{
    /// <summary>
    /// Risk ID
    /// </summary>
    public string RiskId { get; set; } = string.Empty;

    /// <summary>
    /// Risk title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Risk description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Risk category
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Risk level
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Risk score (0-100)
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Probability of occurrence (0-100)
    /// </summary>
    public int Probability { get; set; }

    /// <summary>
    /// Impact severity (0-100)
    /// </summary>
    public int Impact { get; set; }

    /// <summary>
    /// Affected entities
    /// </summary>
    public List<string> AffectedEntities { get; set; } = new();

    /// <summary>
    /// Current mitigation measures
    /// </summary>
    public List<string> CurrentMitigations { get; set; } = new();

    /// <summary>
    /// Residual risk level after mitigation
    /// </summary>
    public string ResidualRisk { get; set; } = string.Empty;

    /// <summary>
    /// Risk owner
    /// </summary>
    public string? RiskOwner { get; set; }

    /// <summary>
    /// Target resolution date
    /// </summary>
    public DateTime? TargetResolution { get; set; }
}

/// <summary>
/// Risk trend data point
/// </summary>
public class RiskTrendPoint
{
    /// <summary>
    /// Date of the assessment
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Overall risk score at this date
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Number of critical risks
    /// </summary>
    public int CriticalRiskCount { get; set; }

    /// <summary>
    /// Number of high risks
    /// </summary>
    public int HighRiskCount { get; set; }

    /// <summary>
    /// Risk velocity (rate of change)
    /// </summary>
    public double RiskVelocity { get; set; }
}

/// <summary>
/// Mitigation strategy
/// </summary>
public class MitigationStrategy
{
    /// <summary>
    /// Strategy ID
    /// </summary>
    public string StrategyId { get; set; } = string.Empty;

    /// <summary>
    /// Strategy name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Strategy description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Risk categories this strategy addresses
    /// </summary>
    public List<string> AddressedRiskCategories { get; set; } = new();

    /// <summary>
    /// Expected risk reduction percentage
    /// </summary>
    public double ExpectedRiskReduction { get; set; }

    /// <summary>
    /// Implementation cost estimate
    /// </summary>
    public string ImplementationCost { get; set; } = string.Empty;

    /// <summary>
    /// Implementation timeline
    /// </summary>
    public string ImplementationTimeline { get; set; } = string.Empty;

    /// <summary>
    /// Resource requirements
    /// </summary>
    public List<string> ResourceRequirements { get; set; } = new();

    /// <summary>
    /// Success metrics
    /// </summary>
    public List<string> SuccessMetrics { get; set; } = new();
}

/// <summary>
/// Risk heatmap data
/// </summary>
public class RiskHeatmapData
{
    /// <summary>
    /// Heatmap matrix data (probability vs impact)
    /// </summary>
    public Dictionary<string, Dictionary<string, int>> Matrix { get; set; } = new();

    /// <summary>
    /// Color coding scheme
    /// </summary>
    public Dictionary<string, string> ColorScheme { get; set; } = new();

    /// <summary>
    /// Risk distribution zones
    /// </summary>
    public Dictionary<string, List<string>> RiskZones { get; set; } = new();

    /// <summary>
    /// Axis labels
    /// </summary>
    public HeatmapAxes Axes { get; set; } = new();
}

/// <summary>
/// Heatmap axes information
/// </summary>
public class HeatmapAxes
{
    /// <summary>
    /// X-axis labels (typically probability)
    /// </summary>
    public List<string> XAxisLabels { get; set; } = new();

    /// <summary>
    /// Y-axis labels (typically impact)
    /// </summary>
    public List<string> YAxisLabels { get; set; } = new();

    /// <summary>
    /// X-axis title
    /// </summary>
    public string XAxisTitle { get; set; } = string.Empty;

    /// <summary>
    /// Y-axis title
    /// </summary>
    public string YAxisTitle { get; set; } = string.Empty;
}

/// <summary>
/// Compliance impact assessment
/// </summary>
public class ComplianceImpactAssessment
{
    /// <summary>
    /// Compliance standards potentially affected
    /// </summary>
    public List<string> AffectedStandards { get; set; } = new();

    /// <summary>
    /// Impact level on each standard
    /// </summary>
    public Dictionary<string, string> ImpactLevels { get; set; } = new();

    /// <summary>
    /// Regulatory implications
    /// </summary>
    public List<string> RegulatoryImplications { get; set; } = new();

    /// <summary>
    /// Potential fines or penalties
    /// </summary>
    public Dictionary<string, string> PotentialPenalties { get; set; } = new();

    /// <summary>
    /// Remediation requirements
    /// </summary>
    public List<string> RemediationRequirements { get; set; } = new();
}

/// <summary>
/// Risk mitigation action
/// </summary>
public class RiskMitigationAction
{
    /// <summary>
    /// Action ID
    /// </summary>
    public string ActionId { get; set; } = string.Empty;

    /// <summary>
    /// Action description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Priority level
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Risk IDs this action addresses
    /// </summary>
    public List<string> AddressedRisks { get; set; } = new();

    /// <summary>
    /// Expected risk reduction
    /// </summary>
    public double ExpectedRiskReduction { get; set; }

    /// <summary>
    /// Implementation effort
    /// </summary>
    public string ImplementationEffort { get; set; } = string.Empty;

    /// <summary>
    /// Target completion date
    /// </summary>
    public DateTime? TargetCompletion { get; set; }

    /// <summary>
    /// Assigned to
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Dependencies
    /// </summary>
    public List<string> Dependencies { get; set; } = new();
}

#endregion

#region Compliance Reports

/// <summary>
/// Response model for compliance assessment report
/// </summary>
public class ComplianceAssessmentReportResponse : BaseReportResponse
{
    /// <summary>
    /// Compliance standards assessed
    /// </summary>
    public List<string> ComplianceStandards { get; set; } = new();

    /// <summary>
    /// Overall compliance score (0-100)
    /// </summary>
    public decimal OverallComplianceScore { get; set; }

    /// <summary>
    /// Overall compliance level
    /// </summary>
    public string ComplianceLevel { get; set; } = string.Empty;

    /// <summary>
    /// Compliance status for each standard
    /// </summary>
    public List<StandardCompliance> StandardsCompliance { get; set; } = new();

    /// <summary>
    /// Critical findings requiring immediate attention
    /// </summary>
    public List<CriticalFinding> CriticalFindings { get; set; } = new();

    /// <summary>
    /// Compliance violations identified
    /// </summary>
    public List<ComplianceViolationResponse> Violations { get; set; } = new();

    /// <summary>
    /// Remediation plan for addressing violations
    /// </summary>
    public RemediationPlan RemediationPlan { get; set; } = new();

    /// <summary>
    /// Control effectiveness assessment
    /// </summary>
    public ControlEffectivenessAssessment ControlEffectiveness { get; set; } = new();

    /// <summary>
    /// Compliance trends over time
    /// </summary>
    public ComplianceTrendAnalysis ComplianceTrends { get; set; } = new();

    /// <summary>
    /// Audit readiness assessment
    /// </summary>
    public AuditReadinessAssessment AuditReadiness { get; set; } = new();

    /// <summary>
    /// Certification status for relevant standards
    /// </summary>
    public List<CertificationStatus> CertificationStatus { get; set; } = new();

    /// <summary>
    /// Next scheduled assessment date
    /// </summary>
    public DateTime? NextAssessmentDate { get; set; }

    /// <summary>
    /// Executive summary
    /// </summary>
    public string ExecutiveSummary { get; set; } = string.Empty;
}

/// <summary>
/// Standard compliance information
/// </summary>
public class StandardCompliance
{
    /// <summary>
    /// Standard name
    /// </summary>
    public string StandardName { get; set; } = string.Empty;

    /// <summary>
    /// Compliance score for this standard
    /// </summary>
    public decimal ComplianceScore { get; set; }

    /// <summary>
    /// Compliance status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Individual requirements compliance
    /// </summary>
    public List<ComplianceRequirementResponse> Requirements { get; set; } = new();

    /// <summary>
    /// Last assessment date for this standard
    /// </summary>
    public DateTime LastAssessment { get; set; }

    /// <summary>
    /// Next assessment due date
    /// </summary>
    public DateTime? NextAssessment { get; set; }

    /// <summary>
    /// Number of violations for this standard
    /// </summary>
    public int ViolationCount { get; set; }

    /// <summary>
    /// Critical gaps in compliance
    /// </summary>
    public List<string> CriticalGaps { get; set; } = new();
}

/// <summary>
/// Remediation plan
/// </summary>
public class RemediationPlan
{
    /// <summary>
    /// Total remediation items
    /// </summary>
    public int TotalRemediationItems { get; set; }

    /// <summary>
    /// High priority items
    /// </summary>
    public int HighPriorityItems { get; set; }

    /// <summary>
    /// Estimated completion timeline
    /// </summary>
    public TimeSpan EstimatedCompletion { get; set; }

    /// <summary>
    /// Remediation activities
    /// </summary>
    public List<RemediationActivity> Activities { get; set; } = new();

    /// <summary>
    /// Resource requirements
    /// </summary>
    public ResourceRequirements ResourceRequirements { get; set; } = new();

    /// <summary>
    /// Milestones and deadlines
    /// </summary>
    public List<RemediationMilestone> Milestones { get; set; } = new();
}

/// <summary>
/// Remediation activity
/// </summary>
public class RemediationActivity
{
    /// <summary>
    /// Activity ID
    /// </summary>
    public string ActivityId { get; set; } = string.Empty;

    /// <summary>
    /// Activity description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Priority level
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Estimated effort
    /// </summary>
    public string EstimatedEffort { get; set; } = string.Empty;

    /// <summary>
    /// Target completion date
    /// </summary>
    public DateTime TargetCompletion { get; set; }

    /// <summary>
    /// Assigned to
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Status of the activity
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Related compliance requirements
    /// </summary>
    public List<string> RelatedRequirements { get; set; } = new();
}

/// <summary>
/// Resource requirements for remediation
/// </summary>
public class ResourceRequirements
{
    /// <summary>
    /// Human resources needed
    /// </summary>
    public Dictionary<string, int> HumanResources { get; set; } = new();

    /// <summary>
    /// Technology resources needed
    /// </summary>
    public List<string> TechnologyResources { get; set; } = new();

    /// <summary>
    /// Estimated budget requirement
    /// </summary>
    public decimal EstimatedBudget { get; set; }

    /// <summary>
    /// External consultant needs
    /// </summary>
    public List<string> ExternalConsultants { get; set; } = new();

    /// <summary>
    /// Training requirements
    /// </summary>
    public List<string> TrainingNeeds { get; set; } = new();
}

/// <summary>
/// Remediation milestone
/// </summary>
public class RemediationMilestone
{
    /// <summary>
    /// Milestone name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Target date
    /// </summary>
    public DateTime TargetDate { get; set; }

    /// <summary>
    /// Deliverables
    /// </summary>
    public List<string> Deliverables { get; set; } = new();

    /// <summary>
    /// Success criteria
    /// </summary>
    public List<string> SuccessCriteria { get; set; } = new();

    /// <summary>
    /// Dependencies
    /// </summary>
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Control effectiveness assessment
/// </summary>
public class ControlEffectivenessAssessment
{
    /// <summary>
    /// Overall control effectiveness score
    /// </summary>
    public decimal OverallEffectiveness { get; set; }

    /// <summary>
    /// Control effectiveness by category
    /// </summary>
    public Dictionary<string, decimal> EffectivenessByCategory { get; set; } = new();

    /// <summary>
    /// Ineffective controls
    /// </summary>
    public List<IneffectiveControl> IneffectiveControls { get; set; } = new();

    /// <summary>
    /// Control gaps identified
    /// </summary>
    public List<ControlGap> ControlGaps { get; set; } = new();

    /// <summary>
    /// Recommendations for improvement
    /// </summary>
    public List<string> ImprovementRecommendations { get; set; } = new();
}

/// <summary>
/// Ineffective control information
/// </summary>
public class IneffectiveControl
{
    /// <summary>
    /// Control ID
    /// </summary>
    public string ControlId { get; set; } = string.Empty;

    /// <summary>
    /// Control name
    /// </summary>
    public string ControlName { get; set; } = string.Empty;

    /// <summary>
    /// Reason for ineffectiveness
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Impact of ineffectiveness
    /// </summary>
    public string Impact { get; set; } = string.Empty;

    /// <summary>
    /// Recommended actions
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Control gap information
/// </summary>
public class ControlGap
{
    /// <summary>
    /// Gap ID
    /// </summary>
    public string GapId { get; set; } = string.Empty;

    /// <summary>
    /// Gap description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Risk level of the gap
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Affected compliance requirements
    /// </summary>
    public List<string> AffectedRequirements { get; set; } = new();

    /// <summary>
    /// Recommended control implementation
    /// </summary>
    public string RecommendedControl { get; set; } = string.Empty;
}

/// <summary>
/// Audit readiness assessment
/// </summary>
public class AuditReadinessAssessment
{
    /// <summary>
    /// Overall readiness score (0-100)
    /// </summary>
    public int ReadinessScore { get; set; }

    /// <summary>
    /// Readiness level
    /// </summary>
    public string ReadinessLevel { get; set; } = string.Empty;

    /// <summary>
    /// Areas requiring attention before audit
    /// </summary>
    public List<string> AttentionAreas { get; set; } = new();

    /// <summary>
    /// Documentation completeness
    /// </summary>
    public decimal DocumentationCompleteness { get; set; }

    /// <summary>
    /// Evidence availability
    /// </summary>
    public decimal EvidenceAvailability { get; set; }

    /// <summary>
    /// Process maturity assessment
    /// </summary>
    public decimal ProcessMaturity { get; set; }

    /// <summary>
    /// Recommended pre-audit activities
    /// </summary>
    public List<string> PreAuditActivities { get; set; } = new();
}

/// <summary>
/// Certification status
/// </summary>
public class CertificationStatus
{
    /// <summary>
    /// Certification name
    /// </summary>
    public string CertificationName { get; set; } = string.Empty;

    /// <summary>
    /// Current status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Certification date
    /// </summary>
    public DateTime? CertificationDate { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Certifying body
    /// </summary>
    public string CertifyingBody { get; set; } = string.Empty;

    /// <summary>
    /// Next renewal date
    /// </summary>
    public DateTime? NextRenewal { get; set; }

    /// <summary>
    /// Renewal requirements
    /// </summary>
    public List<string> RenewalRequirements { get; set; } = new();
}

#endregion

#region Usage Statistics Reports

/// <summary>
/// Response model for usage statistics report
/// </summary>
public class UsageStatisticsReportResponse : BaseReportResponse
{
    /// <summary>
    /// Total number of requests during the period
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Total number of users during the period
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Number of active sessions
    /// </summary>
    public int ActiveSessions { get; set; }

    /// <summary>
    /// Peak concurrent users
    /// </summary>
    public int PeakConcurrentUsers { get; set; }

    /// <summary>
    /// Average session duration
    /// </summary>
    public TimeSpan AverageSessionDuration { get; set; }

    /// <summary>
    /// Requests per hour distribution
    /// </summary>
    public Dictionary<DateTime, int> RequestsPerHour { get; set; } = new();

    /// <summary>
    /// Usage by API endpoint
    /// </summary>
    public Dictionary<string, int> UsageByEndpoint { get; set; } = new();

    /// <summary>
    /// Usage by user (top users)
    /// </summary>
    public List<UserUsageSummary> UsageByUser { get; set; } = new();

    /// <summary>
    /// Usage patterns by time of day
    /// </summary>
    public Dictionary<int, int> UsageByTimeOfDay { get; set; } = new();

    /// <summary>
    /// Usage patterns by day of week
    /// </summary>
    public Dictionary<string, int> UsageByDayOfWeek { get; set; } = new();

    /// <summary>
    /// Geographic distribution of usage
    /// </summary>
    public Dictionary<string, int> GeographicDistribution { get; set; } = new();

    /// <summary>
    /// Device types used for access
    /// </summary>
    public Dictionary<string, int> DeviceTypes { get; set; } = new();

    /// <summary>
    /// Browser types used for access
    /// </summary>
    public Dictionary<string, int> BrowserTypes { get; set; } = new();

    /// <summary>
    /// Performance metrics
    /// </summary>
    public UsagePerformanceMetrics PerformanceMetrics { get; set; } = new();

    /// <summary>
    /// Capacity analysis
    /// </summary>
    public CapacityAnalysis CapacityAnalysis { get; set; } = new();

    /// <summary>
    /// Error analysis
    /// </summary>
    public ErrorAnalysis ErrorAnalysis { get; set; } = new();
}

/// <summary>
/// User usage summary
/// </summary>
public class UserUsageSummary
{
    /// <summary>
    /// User ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Number of requests made
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// Number of sessions
    /// </summary>
    public int SessionCount { get; set; }

    /// <summary>
    /// Total time spent
    /// </summary>
    public TimeSpan TotalTimeSpent { get; set; }

    /// <summary>
    /// Last activity
    /// </summary>
    public DateTime LastActivity { get; set; }

    /// <summary>
    /// Most used endpoints
    /// </summary>
    public Dictionary<string, int> MostUsedEndpoints { get; set; } = new();
}

/// <summary>
/// Usage performance metrics
/// </summary>
public class UsagePerformanceMetrics
{
    /// <summary>
    /// Average response time
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Requests per second
    /// </summary>
    public double ThroughputPerSecond { get; set; }

    /// <summary>
    /// Error rate percentage
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Memory utilization percentage
    /// </summary>
    public double MemoryUtilization { get; set; }

    /// <summary>
    /// CPU utilization percentage
    /// </summary>
    public double CpuUtilization { get; set; }

    /// <summary>
    /// Database performance metrics
    /// </summary>
    public Dictionary<string, double> DatabasePerformance { get; set; } = new();
}

/// <summary>
/// Capacity analysis
/// </summary>
public class CapacityAnalysis
{
    /// <summary>
    /// Current capacity utilization percentage
    /// </summary>
    public double CurrentUtilization { get; set; }

    /// <summary>
    /// Projected utilization for next period
    /// </summary>
    public double ProjectedUtilization { get; set; }

    /// <summary>
    /// Capacity threshold warnings
    /// </summary>
    public List<string> CapacityWarnings { get; set; } = new();

    /// <summary>
    /// Recommended capacity adjustments
    /// </summary>
    public List<string> RecommendedAdjustments { get; set; } = new();

    /// <summary>
    /// Peak usage forecasting
    /// </summary>
    public PeakUsageForecast PeakForecast { get; set; } = new();
}

/// <summary>
/// Peak usage forecast
/// </summary>
public class PeakUsageForecast
{
    /// <summary>
    /// Predicted peak time
    /// </summary>
    public DateTime PredictedPeakTime { get; set; }

    /// <summary>
    /// Predicted peak load
    /// </summary>
    public int PredictedPeakLoad { get; set; }

    /// <summary>
    /// Confidence level of the prediction
    /// </summary>
    public double ConfidenceLevel { get; set; }

    /// <summary>
    /// Factors contributing to the forecast
    /// </summary>
    public List<string> ForecastFactors { get; set; } = new();
}

/// <summary>
/// Error analysis
/// </summary>
public class ErrorAnalysis
{
    /// <summary>
    /// Total errors during the period
    /// </summary>
    public int TotalErrors { get; set; }

    /// <summary>
    /// Error rate percentage
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Errors by type
    /// </summary>
    public Dictionary<string, int> ErrorsByType { get; set; } = new();

    /// <summary>
    /// Errors by endpoint
    /// </summary>
    public Dictionary<string, int> ErrorsByEndpoint { get; set; } = new();

    /// <summary>
    /// Most common error messages
    /// </summary>
    public Dictionary<string, int> CommonErrorMessages { get; set; } = new();

    /// <summary>
    /// Error trends over time
    /// </summary>
    public List<ErrorTrendPoint> ErrorTrends { get; set; } = new();
}

/// <summary>
/// Error trend data point
/// </summary>
public class ErrorTrendPoint
{
    /// <summary>
    /// Time of the measurement
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// Error count at this time
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Error rate at this time
    /// </summary>
    public double ErrorRate { get; set; }
}

#endregion

#region Report Export and Scheduling

/// <summary>
/// Response model for scheduled reports
/// </summary>
public class ScheduledReportResponse
{
    /// <summary>
    /// Unique schedule ID
    /// </summary>
    public string ScheduleId { get; set; } = string.Empty;

    /// <summary>
    /// Type of report being scheduled
    /// </summary>
    public string ReportType { get; set; } = string.Empty;

    /// <summary>
    /// Schedule expression (cron format)
    /// </summary>
    public string Schedule { get; set; } = string.Empty;

    /// <summary>
    /// Next scheduled run time
    /// </summary>
    public DateTime NextRunTime { get; set; }

    /// <summary>
    /// Email recipients for the report
    /// </summary>
    public List<string> Recipients { get; set; } = new();

    /// <summary>
    /// Whether the scheduled report is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When the schedule was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Who created the schedule
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Export format for the scheduled report
    /// </summary>
    public string ExportFormat { get; set; } = string.Empty;

    /// <summary>
    /// Last execution time
    /// </summary>
    public DateTime? LastExecuted { get; set; }

    /// <summary>
    /// Execution history (last 10 runs)
    /// </summary>
    public List<ScheduledReportExecution> ExecutionHistory { get; set; } = new();
}

/// <summary>
/// Summary response model for scheduled reports
/// </summary>
public class ScheduledReportSummaryResponse
{
    /// <summary>
    /// Schedule ID
    /// </summary>
    public string ScheduleId { get; set; } = string.Empty;

    /// <summary>
    /// Report type
    /// </summary>
    public string ReportType { get; set; } = string.Empty;

    /// <summary>
    /// Schedule expression
    /// </summary>
    public string Schedule { get; set; } = string.Empty;

    /// <summary>
    /// Next run time
    /// </summary>
    public DateTime NextRunTime { get; set; }

    /// <summary>
    /// Last run time
    /// </summary>
    public DateTime? LastRunTime { get; set; }

    /// <summary>
    /// Whether the schedule is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Number of recipients
    /// </summary>
    public int RecipientCount { get; set; }

    /// <summary>
    /// Last execution status
    /// </summary>
    public string? LastExecutionStatus { get; set; }
}

/// <summary>
/// Scheduled report execution record
/// </summary>
public class ScheduledReportExecution
{
    /// <summary>
    /// Execution timestamp
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Execution status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Duration of execution
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Recipients who received the report
    /// </summary>
    public int RecipientsCount { get; set; }

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Report size in bytes
    /// </summary>
    public long ReportSize { get; set; }
}

#endregion

#region Custom Analytics

/// <summary>
/// Response model for custom analytics queries
/// </summary>
public class CustomAnalyticsResponse
{
    /// <summary>
    /// Query name that was executed
    /// </summary>
    public string QueryName { get; set; } = string.Empty;

    /// <summary>
    /// When the query was executed
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Query execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Total number of records returned
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Query result data
    /// </summary>
    public List<Dictionary<string, object>> Data { get; set; } = new();

    /// <summary>
    /// Query metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Chart data for visualization
    /// </summary>
    public List<ChartData> Charts { get; set; } = new();

    /// <summary>
    /// Query summary and insights
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Aggregated results
    /// </summary>
    public Dictionary<string, object> Aggregations { get; set; } = new();

    /// <summary>
    /// Query performance metrics
    /// </summary>
    public QueryPerformanceMetrics Performance { get; set; } = new();
}

/// <summary>
/// Chart data for visualization
/// </summary>
public class ChartData
{
    /// <summary>
    /// Chart type (bar, line, pie, etc.)
    /// </summary>
    public string ChartType { get; set; } = string.Empty;

    /// <summary>
    /// Chart title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Chart data points
    /// </summary>
    public List<Dictionary<string, object>> Data { get; set; } = new();

    /// <summary>
    /// Chart labels
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// Chart configuration options
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();

    /// <summary>
    /// Chart color scheme
    /// </summary>
    public List<string> Colors { get; set; } = new();
}

/// <summary>
/// Query performance metrics
/// </summary>
public class QueryPerformanceMetrics
{
    /// <summary>
    /// Database query time
    /// </summary>
    public TimeSpan DatabaseQueryTime { get; set; }

    /// <summary>
    /// Data processing time
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Memory usage during query
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Number of database hits
    /// </summary>
    public int DatabaseHits { get; set; }

    /// <summary>
    /// Cache hit ratio
    /// </summary>
    public double CacheHitRatio { get; set; }

    /// <summary>
    /// Query optimization suggestions
    /// </summary>
    public List<string> OptimizationSuggestions { get; set; } = new();
}

#endregion