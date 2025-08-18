using System.Security.Cryptography;
using System.Text;

namespace ACS.Infrastructure.Security;

/// <summary>
/// Interface for encryption services supporting tenant data isolation
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypt data using tenant-specific encryption key
    /// </summary>
    Task<string> EncryptAsync(string plainText, string tenantId);
    
    /// <summary>
    /// Decrypt data using tenant-specific encryption key
    /// </summary>
    Task<string> DecryptAsync(string encryptedText, string tenantId);
    
    /// <summary>
    /// Encrypt sensitive field data with additional metadata
    /// </summary>
    Task<EncryptedField> EncryptFieldAsync(string plainText, string fieldName, string entityId, string tenantId);
    
    /// <summary>
    /// Decrypt sensitive field data
    /// </summary>
    Task<string> DecryptFieldAsync(EncryptedField encryptedField, string tenantId);
    
    /// <summary>
    /// Generate a new encryption key for a tenant
    /// </summary>
    Task<string> GenerateTenantKeyAsync(string tenantId);
    
    /// <summary>
    /// Rotate encryption keys for a tenant
    /// </summary>
    Task RotateKeysAsync(string tenantId);
    
    /// <summary>
    /// Validate encryption key integrity
    /// </summary>
    Task<bool> ValidateKeyIntegrityAsync(string tenantId);
}

/// <summary>
/// Represents an encrypted field with metadata
/// </summary>
public class EncryptedField
{
    public string EncryptedValue { get; set; } = string.Empty;
    public string KeyVersion { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string InitializationVector { get; set; } = string.Empty;
    public DateTime EncryptedAt { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for tenant encryption settings
/// </summary>
public class TenantEncryptionConfiguration
{
    public string TenantId { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "AES-256-GCM";
    public int KeyRotationIntervalDays { get; set; } = 90;
    public bool EnableFieldLevelEncryption { get; set; } = true;
    public bool EnableDatabaseLevelEncryption { get; set; } = true;
    public string[] EncryptedFields { get; set; } = Array.Empty<string>();
    public DateTime LastKeyRotation { get; set; } = DateTime.UtcNow;
}