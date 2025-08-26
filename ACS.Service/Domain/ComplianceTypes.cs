using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Domain;

/// <summary>
/// GDPR consent record for tracking user consent
/// </summary>
public class ConsentRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ConsentType { get; set; } = string.Empty;
    public bool IsConsented { get; set; }
    public DateTime ConsentDate { get; set; }
    public DateTime? WithdrawnDate { get; set; }
    public string ConsentVersion { get; set; } = string.Empty;
    public string? LegalBasis { get; set; }
    public string? Purpose { get; set; }
    public bool IsActive => IsConsented && !WithdrawnDate.HasValue;
}

/// <summary>
/// Data breach record for GDPR compliance
/// </summary>
public class DataBreachRecord
{
    public int Id { get; set; }
    public string BreachType { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public DateTime? ReportedAt { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int AffectedUsersCount { get; set; }
    public string DataTypesAffected { get; set; } = string.Empty;
    public bool IsContained { get; set; }
    public DateTime? ContainedAt { get; set; }
    public string? RemediationActions { get; set; }
    public bool IsReportableToAuthorities { get; set; }
    public DateTime? AuthorityReportedAt { get; set; }
    
    // Additional properties needed by GdprComplianceService
    public DateTime IncidentDate { get; set; }
    public DateTime DiscoveryDate { get; set; }
    public DateTime? ReportedDate { get; set; }
    public List<string> DataTypes { get; set; } = new();
    public List<string> AffectedUsers { get; set; } = new();
    public List<string> MitigationSteps { get; set; } = new();
    public bool NotificationSent { get; set; }
    public DateTime? NotificationDeadline { get; set; }
}

/// <summary>
/// Compliance report for GDPR and regulatory requirements
/// </summary>
public class ComplianceReport
{
    public int Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
    public List<ComplianceViolation> Violations { get; set; } = new();
    public string? Summary { get; set; }
    public string? Recommendations { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
}

/// <summary>
/// Compliance violation record
/// </summary>
public class ComplianceViolation
{
    public int Id { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public string? AffectedEntities { get; set; }
    public bool IsRemediated { get; set; }
    public DateTime? RemediatedAt { get; set; }
    public string? RemediationNotes { get; set; }
    public string? ResponsibleParty { get; set; }
}

// Alert and AlertCategory moved to dedicated Alert.cs file to avoid duplicates

/// <summary>
/// Encryption service interface for GDPR data protection
/// </summary>
public interface IEncryptionService
{
    Task<string> EncryptAsync(string plainText);
    Task<string> DecryptAsync(string cipherText);
    Task<byte[]> EncryptAsync(byte[] data);
    Task<byte[]> DecryptAsync(byte[] encryptedData);
    string GenerateDataKey();
    bool ValidateDataIntegrity(string data, string hash);
}