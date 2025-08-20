using ACS.Service.Data.Models;

namespace ACS.Service.Services;

public interface IAuditService
{
    // Core Audit Logging
    Task LogAsync(string action, string entityType, int entityId, string performedBy, string details);
    Task LogSecurityEventAsync(string eventType, string severity, string source, string details, string? userId = null);
    Task LogAccessAttemptAsync(string resource, string action, string userId, bool success, string? reason = null);
    Task LogDataChangeAsync(string tableName, string operation, string recordId, string oldValue, string newValue, string changedBy);
    Task LogSystemEventAsync(string eventType, string component, string details, string? correlationId = null);
    
    // Query and Retrieval
    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<AuditLog>> GetAuditLogsByEntityAsync(string entityType, int entityId);
    Task<IEnumerable<AuditLog>> GetAuditLogsByUserAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<AuditLog>> GetAuditLogsByActionAsync(string action, DateTime? startDate = null, DateTime? endDate = null);
    Task<AuditLog?> GetAuditLogByIdAsync(int auditLogId);
    
    // Security Event Monitoring
    Task<IEnumerable<SecurityEvent>> GetSecurityEventsAsync(string? severity = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<SecurityEvent>> GetFailedLoginAttemptsAsync(string? userId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<SecurityEvent>> GetSuspiciousActivitiesAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<SecurityEvent>> GetAccessViolationsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<bool> HasSuspiciousActivityAsync(string userId, int timeWindowMinutes = 30);
    
    // Compliance Reporting
    Task<ComplianceReport> GenerateGDPRReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
    Task<ComplianceReport> GenerateSOC2ReportAsync(DateTime startDate, DateTime endDate);
    Task<ComplianceReport> GenerateHIPAAReportAsync(DateTime startDate, DateTime endDate);
    Task<ComplianceReport> GeneratePCIDSSReportAsync(DateTime startDate, DateTime endDate);
    Task<ComplianceReport> GenerateCustomComplianceReportAsync(string reportType, Dictionary<string, object> parameters);
    
    // Data Retention and Privacy
    Task<int> PurgeOldAuditLogsAsync(int retentionDays, string? entityType = null);
    Task AnonymizeUserDataAsync(string userId, string anonymizedBy);
    Task<bool> ExportUserDataAsync(string userId, string format = "json");
    Task<bool> DeleteUserDataAsync(string userId, string deletedBy, bool hardDelete = false);
    Task<DataRetentionPolicy> GetDataRetentionPolicyAsync(string dataType);
    
    // Audit Trail Integrity
    Task<bool> VerifyAuditTrailIntegrityAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<string> CalculateAuditHashAsync(int auditLogId);
    Task<bool> ValidateAuditHashAsync(int auditLogId, string expectedHash);
    Task SignAuditLogAsync(int auditLogId, string signature);
    Task<bool> VerifyAuditSignatureAsync(int auditLogId, string signature);
    
    // Analytics and Insights
    Task<Dictionary<string, int>> GetAuditStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<(string Action, int Count)>> GetTopActionsAsync(int topN = 10, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<(string User, int Count)>> GetMostActiveUsersAsync(int topN = 10, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<(string Entity, int Count)>> GetMostModifiedEntitiesAsync(int topN = 10, DateTime? startDate = null, DateTime? endDate = null);
    Task<Dictionary<DateTime, int>> GetAuditTrendAsync(string groupBy = "day", DateTime? startDate = null, DateTime? endDate = null);
    
    // Real-time Monitoring
    Task<bool> EnableRealTimeMonitoringAsync(string userId);
    Task<bool> DisableRealTimeMonitoringAsync(string userId);
    Task<IEnumerable<string>> GetMonitoredUsersAsync();
    Task<bool> IsUserMonitoredAsync(string userId);
    Task NotifySecurityTeamAsync(string alert, string severity, Dictionary<string, object> context);
    
    // Forensic Analysis
    Task<UserActivityTimeline> GetUserActivityTimelineAsync(string userId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<AuditLog>> ReconstructEntityStateAsync(string entityType, int entityId, DateTime pointInTime);
    Task<IEnumerable<SecurityIncident>> DetectAnomaliesAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<AccessPattern> AnalyzeAccessPatternsAsync(string userId, int days = 30);
    Task<RiskScore> CalculateUserRiskScoreAsync(string userId);
    
    // Export and Archival
    Task<string> ExportAuditLogsAsync(string format, DateTime? startDate = null, DateTime? endDate = null);
    Task<bool> ArchiveAuditLogsAsync(DateTime cutoffDate, string archiveLocation);
    Task<bool> RestoreArchivedLogsAsync(string archiveLocation, DateTime? startDate = null, DateTime? endDate = null);
    Task<long> GetAuditLogSizeAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<bool> CompressAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null);
    
    // Alerting and Notifications
    Task<int> CreateAlertRuleAsync(string ruleName, string condition, string action, bool isActive = true);
    Task<bool> UpdateAlertRuleAsync(int ruleId, string? condition = null, string? action = null, bool? isActive = null);
    Task<bool> DeleteAlertRuleAsync(int ruleId);
    Task<IEnumerable<AlertRule>> GetAlertRulesAsync(bool? activeOnly = null);
    Task<bool> TestAlertRuleAsync(int ruleId);
    
    // Session Management
    Task<string> StartAuditSessionAsync(string userId, string sessionType, Dictionary<string, object>? metadata = null);
    Task EndAuditSessionAsync(string sessionId, string? reason = null);
    Task<AuditSession?> GetAuditSessionAsync(string sessionId);
    Task<IEnumerable<AuditSession>> GetActiveSessionsAsync(string? userId = null);
    Task<bool> ExtendSessionAsync(string sessionId, int additionalMinutes);
    
    // Regulatory Compliance
    Task<bool> IsCompliantAsync(string regulation, DateTime? asOfDate = null);
    Task<IEnumerable<ComplianceViolation>> GetComplianceViolationsAsync(string? regulation = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<bool> RemediateViolationAsync(int violationId, string remediationAction, string performedBy);
    Task<ComplianceStatus> GetComplianceStatusAsync(string regulation);
    Task<bool> ScheduleComplianceAuditAsync(string regulation, DateTime scheduledDate);
}

// Supporting types
public class SecurityEvent
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // Critical, High, Medium, Low, Info
    public string Source { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime OccurredAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ComplianceReport
{
    public string ReportType { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCompliant { get; set; }
    public List<ComplianceItem> Items { get; set; } = new();
    public List<ComplianceViolation> Violations { get; set; } = new();
    public Dictionary<string, object> Summary { get; set; } = new();
    public string? SignedBy { get; set; }
}

public class ComplianceItem
{
    public string Category { get; set; } = string.Empty;
    public string Requirement { get; set; } = string.Empty;
    public bool IsMet { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
}

public class ComplianceViolation
{
    public int Id { get; set; }
    public string Regulation { get; set; } = string.Empty;
    public string Requirement { get; set; } = string.Empty;
    public string ViolationType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public bool IsRemediated { get; set; }
    public string? RemediationAction { get; set; }
    public DateTime? RemediatedAt { get; set; }
}

public class DataRetentionPolicy
{
    public string DataType { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public string PurgeStrategy { get; set; } = string.Empty; // Delete, Archive, Anonymize
    public bool RequiresApproval { get; set; }
    public List<string> Exceptions { get; set; } = new();
}

public class UserActivityTimeline
{
    public string UserId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<ActivityEvent> Events { get; set; } = new();
    public Dictionary<string, int> Summary { get; set; } = new();
}

public class ActivityEvent
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
}

public class SecurityIncident
{
    public int Id { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public List<int> RelatedAuditLogIds { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
}

public class AccessPattern
{
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, int> ResourceAccessCounts { get; set; } = new();
    public Dictionary<int, int> HourlyActivity { get; set; } = new();
    public List<string> UnusualActivities { get; set; } = new();
    public double NormalityScore { get; set; }
}

public class RiskScore
{
    public string UserId { get; set; } = string.Empty;
    public double Score { get; set; } // 0-100
    public string RiskLevel { get; set; } = string.Empty; // Low, Medium, High, Critical
    public List<RiskFactor> Factors { get; set; } = new();
    public DateTime CalculatedAt { get; set; }
}

public class RiskFactor
{
    public string FactorName { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double Value { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class AlertRule
{
    public int Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TriggerCount { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
}

public class AuditSession
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ComplianceStatus
{
    public string Regulation { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
    public DateTime LastAuditDate { get; set; }
    public DateTime? NextAuditDate { get; set; }
    public int ViolationCount { get; set; }
    public int RemediatedCount { get; set; }
    public double ComplianceScore { get; set; }
}