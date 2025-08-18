using ACS.Core.Security;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACS.Service.Data.Models;

/// <summary>
/// Example of an entity with encrypted sensitive fields
/// </summary>
[Table("UserProfiles")]
[EncryptedEntity("UserProfile", AuditAccess = true)]
public class EncryptedUserProfile
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// User identifier - not encrypted
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Full name - encrypted for PII protection
    /// </summary>
    [EncryptedField("FullName", IsRequired = true, ReencryptionPriority = 5)]
    public string FullName { get; set; } = string.Empty;
    
    /// <summary>
    /// Email address - encrypted for PII protection
    /// </summary>
    [EncryptedField("EmailAddress", IsRequired = true, ReencryptionPriority = 5)]
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Phone number - encrypted for PII protection
    /// </summary>
    [EncryptedField("PhoneNumber", ReencryptionPriority = 3)]
    public string? PhoneNumber { get; set; }
    
    /// <summary>
    /// Social Security Number - encrypted with highest priority
    /// </summary>
    [EncryptedField("SSN", IsRequired = false, ReencryptionPriority = 10)]
    public string? SocialSecurityNumber { get; set; }
    
    /// <summary>
    /// Address - encrypted for PII protection
    /// </summary>
    [EncryptedField("Address", ReencryptionPriority = 2)]
    public string? Address { get; set; }
    
    /// <summary>
    /// Date of birth - encrypted for PII protection
    /// </summary>
    [EncryptedField("DateOfBirth", ReencryptionPriority = 4)]
    public string? DateOfBirth { get; set; }
    
    /// <summary>
    /// Emergency contact information - encrypted
    /// </summary>
    [EncryptedField("EmergencyContact", ReencryptionPriority = 3)]
    public string? EmergencyContactInfo { get; set; }
    
    /// <summary>
    /// Medical information - encrypted with high priority
    /// </summary>
    [EncryptedField("MedicalInfo", ReencryptionPriority = 8)]
    public string? MedicalInformation { get; set; }
    
    /// <summary>
    /// Financial information - encrypted with highest priority
    /// </summary>
    [EncryptedField("FinancialInfo", ReencryptionPriority = 10)]
    public string? FinancialInformation { get; set; }
    
    // Non-encrypted metadata fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Audit log for encrypted field access
/// </summary>
[Table("EncryptedFieldAccessLog")]
public class EncryptedFieldAccessLog
{
    [Key]
    public int Id { get; set; }
    
    public string TenantId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // Read, Write, Encrypt, Decrypt
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    public string ClientIpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string KeyVersion { get; set; } = string.Empty;
    public bool WasSuccessful { get; set; } = true;
    public string? ErrorMessage { get; set; }
}