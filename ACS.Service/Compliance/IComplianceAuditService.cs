using ACS.Service.Domain;

namespace ACS.Service.Compliance;

/// <summary>
/// Service for compliance audit logging across various regulatory frameworks
/// </summary>
public interface IComplianceAuditService
{
    /// <summary>
    /// Log a GDPR-related event
    /// </summary>
    Task LogGdprEventAsync(GdprAuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log a SOC2-related event
    /// </summary>
    Task LogSoc2EventAsync(Soc2AuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log a HIPAA-related event
    /// </summary>
    Task LogHipaaEventAsync(HipaaAuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log a PCI-DSS related event
    /// </summary>
    Task LogPciDssEventAsync(PciDssAuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate compliance report for a specific framework
    /// </summary>
    Task<ComplianceReport> GenerateComplianceReportAsync(
        ComplianceFramework framework,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit trail for a specific user
    /// </summary>
    Task<IEnumerable<ComplianceAuditEntry>> GetUserAuditTrailAsync(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit trail for a specific resource
    /// </summary>
    Task<IEnumerable<ComplianceAuditEntry>> GetResourceAuditTrailAsync(
        string resourceId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive old audit logs
    /// </summary>
    Task<int> ArchiveAuditLogsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export audit logs for external analysis
    /// </summary>
    Task<byte[]> ExportAuditLogsAsync(
        ComplianceFramework? framework,
        DateTime startDate,
        DateTime endDate,
        ExportFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate compliance status
    /// </summary>
    Task<ComplianceValidationResult> ValidateComplianceAsync(
        ComplianceFramework framework,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compliance frameworks
/// </summary>
public enum ComplianceFramework
{
    GDPR,
    SOC2,
    HIPAA,
    PCI_DSS,
    ISO27001,
    CCPA
}

/// <summary>
/// Export formats for audit logs
/// </summary>
public enum ExportFormat
{
    JSON,
    CSV,
    XML,
    PDF
}

/// <summary>
/// Base class for all compliance audit events
/// </summary>
public abstract class ComplianceAuditEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public ComplianceFramework Framework { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    public ComplianceSeverity Severity { get; set; } = ComplianceSeverity.Info;
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// GDPR-specific audit event
/// </summary>
public class GdprAuditEvent : ComplianceAuditEvent
{
    public GdprAuditEvent()
    {
        Framework = ComplianceFramework.GDPR;
    }

    public GdprEventType GdprEventType { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;
    public string LawfulBasis { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public List<string> DataCategories { get; set; } = new();
    public bool ConsentGiven { get; set; }
    public DateTime? ConsentTimestamp { get; set; }
    public string ConsentVersion { get; set; } = string.Empty;
    public bool IsDataPortability { get; set; }
    public bool IsRightToErasure { get; set; }
    public string ProcessingActivity { get; set; } = string.Empty;
    public string DataController { get; set; } = string.Empty;
    public string DataProcessor { get; set; } = string.Empty;
    public int? RetentionPeriodDays { get; set; }
}

/// <summary>
/// GDPR event types
/// </summary>
public enum GdprEventType
{
    ConsentGiven,
    ConsentWithdrawn,
    DataAccess,
    DataModification,
    DataDeletion,
    DataExport,
    DataBreach,
    RightToAccess,
    RightToRectification,
    RightToErasure,
    RightToPortability,
    RightToRestriction,
    RightToObject,
    PrivacyPolicyAccepted,
    DataRetention,
    CrossBorderTransfer
}

/// <summary>
/// SOC2-specific audit event
/// </summary>
public class Soc2AuditEvent : ComplianceAuditEvent
{
    public Soc2AuditEvent()
    {
        Framework = ComplianceFramework.SOC2;
    }

    public Soc2TrustPrinciple TrustPrinciple { get; set; }
    public Soc2EventType Soc2EventType { get; set; }
    public string ControlId { get; set; } = string.Empty;
    public string ControlDescription { get; set; } = string.Empty;
    public bool ControlEffective { get; set; }
    public string SystemComponent { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string RemediationAction { get; set; } = string.Empty;
    public DateTime? RemediationDeadline { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

/// <summary>
/// SOC2 Trust Service Principles
/// </summary>
public enum Soc2TrustPrinciple
{
    Security,
    Availability,
    ProcessingIntegrity,
    Confidentiality,
    Privacy
}

/// <summary>
/// SOC2 event types
/// </summary>
public enum Soc2EventType
{
    AccessControl,
    ChangeManagement,
    SystemOperation,
    RiskMitigation,
    IncidentResponse,
    LogicalAccess,
    PhysicalAccess,
    SystemMonitoring,
    BackupAndRecovery,
    VendorManagement,
    PolicyViolation,
    SecurityIncident
}

/// <summary>
/// HIPAA-specific audit event
/// </summary>
public class HipaaAuditEvent : ComplianceAuditEvent
{
    public HipaaAuditEvent()
    {
        Framework = ComplianceFramework.HIPAA;
    }

    public HipaaEventType HipaaEventType { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public bool ContainsPhi { get; set; }
    public string PhiCategories { get; set; } = string.Empty;
    public string CoveredEntity { get; set; } = string.Empty;
    public string BusinessAssociate { get; set; } = string.Empty;
    public string SafeguardType { get; set; } = string.Empty; // Administrative, Physical, Technical
    public string DisclosurePurpose { get; set; } = string.Empty;
    public string AuthorizationId { get; set; } = string.Empty;
    public bool IsEmergencyAccess { get; set; }
    public bool IsMinimumNecessary { get; set; }
    public string EncryptionStatus { get; set; } = string.Empty;
}

/// <summary>
/// HIPAA event types
/// </summary>
public enum HipaaEventType
{
    PhiAccess,
    PhiDisclosure,
    PhiModification,
    PhiDeletion,
    AuthorizationGranted,
    AuthorizationRevoked,
    SecurityIncident,
    BreachNotification,
    AuditLogAccess,
    UserAuthentication,
    AccessDenied,
    EncryptionApplied,
    DataTransmission,
    BackupCreated,
    SystemAccess
}

/// <summary>
/// PCI-DSS specific audit event
/// </summary>
public class PciDssAuditEvent : ComplianceAuditEvent
{
    public PciDssAuditEvent()
    {
        Framework = ComplianceFramework.PCI_DSS;
    }

    public PciDssEventType PciEventType { get; set; }
    public string CardholderDataElement { get; set; } = string.Empty;
    public bool IsMasked { get; set; }
    public bool IsEncrypted { get; set; }
    public string PaymentCardBrand { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public int PciDssRequirement { get; set; }
    public string NetworkSegment { get; set; } = string.Empty;
    public bool IsCardPresent { get; set; }
}

/// <summary>
/// PCI-DSS event types
/// </summary>
public enum PciDssEventType
{
    CardDataAccess,
    CardDataStorage,
    CardDataTransmission,
    CardDataDeletion,
    AuthenticationSuccess,
    AuthenticationFailure,
    PrivilegeEscalation,
    ConfigurationChange,
    SecurityScan,
    PenetrationTest,
    IncidentResponse,
    KeyManagement,
    NetworkAccess,
    VulnerabilityFound
}

/// <summary>
/// Compliance severity levels
/// </summary>
public enum ComplianceSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Compliance audit entry in the database
/// </summary>
public class ComplianceAuditEntry
{
    public long Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public ComplianceFramework Framework { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public ComplianceSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty; // JSON serialized additional data
    public string CorrelationId { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTime? ArchivedDate { get; set; }
}

/// <summary>
/// Compliance report
/// </summary>
public class ComplianceReport
{
    public ComplianceFramework Framework { get; set; }
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    public ComplianceStatus OverallStatus { get; set; }
    public int TotalEvents { get; set; }
    public int CriticalEvents { get; set; }
    public int HighSeverityEvents { get; set; }
    public int MediumSeverityEvents { get; set; }
    public int LowSeverityEvents { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public List<ComplianceViolation> Violations { get; set; } = new();
    public List<ComplianceRecommendation> Recommendations { get; set; } = new();
    public Dictionary<string, object> Statistics { get; set; } = new();
    public string ExecutiveSummary { get; set; } = string.Empty;
}

/// <summary>
/// Compliance status
/// </summary>
public enum ComplianceStatus
{
    Compliant,
    PartiallyCompliant,
    NonCompliant,
    UnderReview
}

/// <summary>
/// Compliance violation
/// </summary>
public class ComplianceViolation
{
    public string ViolationId { get; set; } = string.Empty;
    public DateTime DetectedDate { get; set; }
    public string Requirement { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplianceSeverity Severity { get; set; }
    public string Impact { get; set; } = string.Empty;
    public string RemediationRequired { get; set; } = string.Empty;
    public DateTime? RemediationDeadline { get; set; }
    public string ResponsibleParty { get; set; } = string.Empty;
}

/// <summary>
/// Compliance recommendation
/// </summary>
public class ComplianceRecommendation
{
    public string RecommendationId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Benefit { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string ImplementationGuidance { get; set; } = string.Empty;
    public string EstimatedEffort { get; set; } = string.Empty;
}

/// <summary>
/// Compliance validation result
/// </summary>
public class ComplianceValidationResult
{
    public ComplianceFramework Framework { get; set; }
    public DateTime ValidationDate { get; set; } = DateTime.UtcNow;
    public bool IsCompliant { get; set; }
    public List<ComplianceCheckResult> CheckResults { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// Individual compliance check result
/// </summary>
public class ComplianceCheckResult
{
    public string CheckId { get; set; } = string.Empty;
    public string CheckName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Result { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public ComplianceSeverity Severity { get; set; }
}