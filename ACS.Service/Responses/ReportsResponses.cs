using ACS.Service.Domain;

namespace ACS.Service.Responses;

/// <summary>
/// Service response for user analytics reports
/// </summary>
public record UserAnalyticsReportResponse
{
    public int TotalUsers { get; init; }
    public int ActiveUsers { get; init; }
    public int InactiveUsers { get; init; }
    public ICollection<UserActivityMetrics> UserMetrics { get; init; } = new List<UserActivityMetrics>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for access patterns reports
/// </summary>
public record AccessPatternsReportResponse
{
    public ICollection<AccessPatternData> Patterns { get; init; } = new List<AccessPatternData>();
    public TimeSpan ReportPeriod { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission usage reports
/// </summary>
public record PermissionUsageReportResponse
{
    public ICollection<PermissionUsageData> UsageData { get; init; } = new List<PermissionUsageData>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for role analysis reports
/// </summary>
public record RoleAnalysisReportResponse
{
    public ICollection<RoleAnalysisData> RoleData { get; init; } = new List<RoleAnalysisData>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for security dashboard
/// </summary>
public record SecurityDashboardResponse
{
    public SecurityMetrics SecurityMetrics { get; init; } = new();
    public ICollection<SecurityAlertInfo> Alerts { get; init; } = new List<SecurityAlertInfo>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for risk assessment reports
/// </summary>
public record RiskAssessmentReportResponse
{
    public RiskLevel OverallRiskLevel { get; init; }
    public ICollection<RiskFactor> RiskFactors { get; init; } = new List<RiskFactor>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for compliance assessment reports
/// </summary>
public record ComplianceAssessmentReportResponse
{
    public ReportsComplianceStatus Status { get; init; }
    public ICollection<ComplianceCheck> Checks { get; init; } = new List<ComplianceCheck>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for usage statistics reports
/// </summary>
public record UsageStatisticsReportResponse
{
    public UsageStatistics Statistics { get; init; } = new();
    public ICollection<UsageMetric> Metrics { get; init; } = new List<UsageMetric>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for scheduled reports
/// </summary>
public record ScheduledReportResponse
{
    public ScheduledReport Report { get; init; } = new();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for scheduled report summaries
/// </summary>
public record ScheduledReportSummaryResponse
{
    public ICollection<ScheduledReportSummary> Summaries { get; init; } = new List<ScheduledReportSummary>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for custom analytics
/// </summary>
public record CustomAnalyticsResponse
{
    public AnalyticsResult Result { get; init; } = new();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

// Supporting data structures
public record UserActivityMetrics
{
    public int UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public int LoginCount { get; init; }
    public DateTime LastLogin { get; init; }
    public int PermissionChanges { get; init; }
}

public record AccessPatternData
{
    public string Resource { get; init; } = string.Empty;
    public int AccessCount { get; init; }
    public DateTime FirstAccess { get; init; }
    public DateTime LastAccess { get; init; }
    public ICollection<string> Users { get; init; } = new List<string>();
}

public record PermissionUsageData
{
    public string Permission { get; init; } = string.Empty;
    public int UsageCount { get; init; }
    public ICollection<string> Users { get; init; } = new List<string>();
}

public record RoleAnalysisData
{
    public string RoleName { get; init; } = string.Empty;
    public int UserCount { get; init; }
    public int PermissionCount { get; init; }
    public DateTime LastModified { get; init; }
}

public record SecurityMetrics
{
    public int TotalUsers { get; init; }
    public int TotalRoles { get; init; }
    public int TotalPermissions { get; init; }
    public int FailedLogins { get; init; }
    public int SecurityAlerts { get; init; }
}

public record SecurityAlertInfo
{
    public string AlertType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string Severity { get; init; } = string.Empty;
}

public record RiskFactor
{
    public string FactorType { get; init; } = string.Empty;
    public RiskLevel Level { get; init; }
    public string Description { get; init; } = string.Empty;
    public double Impact { get; init; }
}

public record ComplianceCheck
{
    public string CheckName { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string? Details { get; init; }
    public DateTime LastChecked { get; init; }
}

public record UsageStatistics
{
    public int TotalRequests { get; init; }
    public int UniqueUsers { get; init; }
    public double AverageResponseTime { get; init; }
    public DateTime ReportDate { get; init; }
}

public record UsageMetric
{
    public string MetricName { get; init; } = string.Empty;
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
}

public record ScheduledReport
{
    public int ReportId { get; init; }
    public string ReportName { get; init; } = string.Empty;
    public string Schedule { get; init; } = string.Empty;
    public DateTime NextRun { get; init; }
    public bool IsActive { get; init; }
}

public record ScheduledReportSummary
{
    public int ReportId { get; init; }
    public string ReportName { get; init; } = string.Empty;
    public DateTime LastRun { get; init; }
    public string Status { get; init; } = string.Empty;
}

public record AnalyticsResult
{
    public ICollection<ChartData> Charts { get; init; } = new List<ChartData>();
    public ICollection<MetricData> Metrics { get; init; } = new List<MetricData>();
}

public record ChartData
{
    public string ChartType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public ICollection<DataPoint> Data { get; init; } = new List<DataPoint>();
}

public record MetricData
{
    public string Name { get; init; } = string.Empty;
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
}

public record DataPoint
{
    public string Label { get; init; } = string.Empty;
    public double Value { get; init; }
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Compliance status for reports (renamed to avoid conflicts)
/// </summary>
public enum ReportsComplianceStatus
{
    Compliant,
    NonCompliant,
    PartiallyCompliant,
    Unknown
}

/// <summary>
/// Usage and performance metrics for system analysis
/// </summary>
public record UsagePerformanceMetrics
{
    public int TotalRequests { get; init; }
    public double AverageResponseTime { get; init; }
    public double ThroughputPerSecond { get; init; }
    public int ErrorCount { get; init; }
    public double ErrorRate { get; init; }
    public int ConcurrentUsers { get; init; }
    public DateTime MeasurementPeriodStart { get; init; }
    public DateTime MeasurementPeriodEnd { get; init; }
    public ICollection<PerformanceDataPoint> DataPoints { get; init; } = new List<PerformanceDataPoint>();
}

/// <summary>
/// Individual performance data point for time-series analysis
/// </summary>
public record PerformanceDataPoint
{
    public DateTime Timestamp { get; init; }
    public double ResponseTime { get; init; }
    public int RequestCount { get; init; }
    public int ErrorCount { get; init; }
    public int ActiveUsers { get; init; }
}

/// <summary>
/// Chart visualization data structure
/// </summary>
public record Chart
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public ChartType Type { get; init; }
    public ICollection<DataSeries> Series { get; init; } = new List<DataSeries>();
    public ChartAxisConfig XAxis { get; init; } = new();
    public ChartAxisConfig YAxis { get; init; } = new();
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Data series for chart visualization
/// </summary>
public record DataSeries
{
    public string Name { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public ICollection<ChartDataPoint> Data { get; init; } = new List<ChartDataPoint>();
}

/// <summary>
/// Individual data point in a chart series
/// </summary>
public record ChartDataPoint
{
    public object X { get; init; } = new();
    public double Y { get; init; }
    public string? Label { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Chart axis configuration
/// </summary>
public record ChartAxisConfig
{
    public string Title { get; init; } = string.Empty;
    public AxisType Type { get; init; }
    public object? Min { get; init; }
    public object? Max { get; init; }
    public string Format { get; init; } = string.Empty;
}

public enum ChartType
{
    Line,
    Bar,
    Pie,
    Area,
    Scatter,
    Histogram,
    Heatmap
}

public enum AxisType
{
    Linear,
    Logarithmic,
    DateTime,
    Category
}

// Additional missing types referenced in controllers

/// <summary>
/// Trend analysis data for users
/// </summary>
public record UserTrendAnalysis
{
    public ICollection<TrendData> TrendData { get; init; } = new List<TrendData>();
    public TimeSpan AnalysisPeriod { get; init; }
    public string TrendType { get; init; } = string.Empty;
}

/// <summary>
/// Trend data points for analysis
/// </summary>
public record TrendData
{
    public DateTime Timestamp { get; init; }
    public double Value { get; init; }
    public string Category { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Access performance metrics
/// </summary>
public record AccessPerformanceMetrics
{
    public ICollection<PerformanceData> PerformanceData { get; init; } = new List<PerformanceData>();
    public double AverageAccessTime { get; init; }
    public int TotalAccesses { get; init; }
    public DateTime MeasurementPeriod { get; init; }
}

/// <summary>
/// Performance data measurements
/// </summary>
public record PerformanceData
{
    public string ResourceName { get; init; } = string.Empty;
    public double ResponseTime { get; init; }
    public int AccessCount { get; init; }
    public DateTime Timestamp { get; init; }
    public Dictionary<string, double> Metrics { get; init; } = new();
}

/// <summary>
/// Permission efficiency metrics
/// </summary>
public record PermissionEfficiencyMetrics
{
    public ICollection<EfficiencyData> EfficiencyData { get; init; } = new List<EfficiencyData>();
    public double OverallEfficiencyScore { get; init; }
    public DateTime CalculatedAt { get; init; }
}

/// <summary>
/// Efficiency measurement data
/// </summary>
public record EfficiencyData
{
    public string PermissionName { get; init; } = string.Empty;
    public double UtilizationRate { get; init; }
    public int GrantCount { get; init; }
    public int UsageCount { get; init; }
    public double EfficiencyScore { get; init; }
}

/// <summary>
/// Role hierarchy response
/// </summary>
public record RoleHierarchyResponse
{
    public RoleHierarchy Hierarchy { get; init; } = new();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Role hierarchy structure
/// </summary>
public record RoleHierarchy
{
    public ICollection<Role> Roles { get; init; } = new List<Role>();
    public ICollection<RoleRelationship> Relationships { get; init; } = new List<RoleRelationship>();
    public int MaxDepth { get; init; }
}

/// <summary>
/// Relationship between roles
/// </summary>
public record RoleRelationship
{
    public int ParentRoleId { get; init; }
    public int ChildRoleId { get; init; }
    public string RelationshipType { get; init; } = string.Empty;
}

/// <summary>
/// Vulnerability assessment data
/// </summary>
public record VulnerabilityAssessment
{
    public ICollection<VulnerabilityData> Vulnerabilities { get; init; } = new List<VulnerabilityData>();
    public VulnerabilityRiskLevel OverallRisk { get; init; }
    public DateTime AssessmentDate { get; init; }
}

/// <summary>
/// Individual vulnerability data
/// </summary>
public record VulnerabilityData
{
    public string VulnerabilityId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public VulnerabilityRiskLevel RiskLevel { get; init; }
    public string Category { get; init; } = string.Empty;
    public DateTime DiscoveredAt { get; init; }
}

/// <summary>
/// Standard compliance assessment
/// </summary>
public record StandardCompliance
{
    public ICollection<ComplianceStandardResult> Results { get; init; } = new List<ComplianceStandardResult>();
    public string ComplianceFramework { get; init; } = string.Empty;
    public DateTime AssessmentDate { get; init; }
}

/// <summary>
/// Compliance standard result
/// </summary>
public record ComplianceStandardResult
{
    public string StandardId { get; init; } = string.Empty;
    public string StandardName { get; init; } = string.Empty;
    public bool IsCompliant { get; init; }
    public double ComplianceScore { get; init; }
    public ICollection<string> Findings { get; init; } = new List<string>();
}

/// <summary>
/// Compliance requirement response
/// </summary>
public record ComplianceRequirementResponse
{
    public ICollection<ComplianceRequirement> Requirements { get; init; } = new List<ComplianceRequirement>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}


/// <summary>
/// Compliance violation response
/// </summary>
public record ComplianceViolationResponse
{
    public ICollection<ReportsComplianceViolation> Violations { get; init; } = new List<ReportsComplianceViolation>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Compliance violation for reports (renamed to avoid conflicts)
/// </summary>
public record ReportsComplianceViolation
{
    public string ViolationId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public DateTime DetectedAt { get; init; }
    public string Source { get; init; } = string.Empty;
    public bool IsResolved { get; init; }
}

public enum VulnerabilityRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

// Additional missing types found in controller methods

/// <summary>
/// User engagement metrics for analysis
/// </summary>
public record UserEngagementMetrics
{
    public ICollection<EngagementData> EngagementData { get; init; } = new List<EngagementData>();
    public double OverallEngagementScore { get; init; }
    public TimeSpan MeasurementPeriod { get; init; }
    public DateTime CalculatedAt { get; init; }
}

/// <summary>
/// Engagement data for individual metrics
/// </summary>
public record EngagementData
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public int SessionCount { get; init; }
    public TimeSpan TotalActiveTime { get; init; }
    public int ActionsPerformed { get; init; }
    public double EngagementScore { get; init; }
    public DateTime LastActivity { get; init; }
}

/// <summary>
/// Endpoint metric for API monitoring
/// </summary>
public record EndpointMetric
{
    public string Endpoint { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public int RequestCount { get; init; }
    public double AverageResponseTime { get; init; }
    public int ErrorCount { get; init; }
    public double ErrorRate { get; init; }
}

/// <summary>
/// Database metrics summary
/// </summary>
public record DatabaseMetricsSummary
{
    public int TotalConnections { get; init; }
    public int ActiveConnections { get; init; }
    public long TotalQueries { get; init; }
    public double AverageQueryTime { get; init; }
    public long DatabaseSize { get; init; }
    public double CpuUsage { get; init; }
    public double MemoryUsage { get; init; }
}

/// <summary>
/// Cache metrics for monitoring
/// </summary>
public record CacheMetrics
{
    public int TotalKeys { get; init; }
    public double HitRate { get; init; }
    public double MissRate { get; init; }
    public long MemoryUsage { get; init; }
    public int EvictionCount { get; init; }
    public TimeSpan AverageResponseTime { get; init; }
}

/// <summary>
/// Database capacity information
/// </summary>
public record DatabaseCapacity
{
    public long TotalSize { get; init; }
    public long UsedSize { get; init; }
    public long FreeSize { get; init; }
    public double UsagePercentage { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Memory capacity information
/// </summary>
public record MemoryCapacity
{
    public long TotalMemory { get; init; }
    public long UsedMemory { get; init; }
    public long FreeMemory { get; init; }
    public double UsagePercentage { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Error analysis data
/// </summary>
public record ErrorAnalysis
{
    public int TotalErrors { get; init; }
    public Dictionary<string, int> ErrorsByType { get; init; } = new();
    public Dictionary<string, int> ErrorsByEndpoint { get; init; } = new();
    public double ErrorRate { get; init; }
    public string TopError { get; init; } = string.Empty;
}

/// <summary>
/// Tenant metric for multi-tenancy monitoring
/// </summary>
public record TenantMetric
{
    public string TenantId { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public int UserCount { get; init; }
    public int RequestCount { get; init; }
    public long StorageUsage { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Alert trend data for monitoring
/// </summary>
public record AlertTrend
{
    public string AlertType { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public int Count { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Trend { get; init; } = string.Empty;
}