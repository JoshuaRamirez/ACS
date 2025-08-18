using System.ComponentModel.DataAnnotations;

namespace ACS.Infrastructure.Security;

/// <summary>
/// Attribute to mark fields for automatic encryption
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class EncryptedFieldAttribute : Attribute
{
    /// <summary>
    /// Field name for encryption metadata
    /// </summary>
    public string FieldName { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this field is required for the entity
    /// </summary>
    public bool IsRequired { get; set; } = false;
    
    /// <summary>
    /// Whether to include this field in search operations (encrypted fields typically cannot be searched)
    /// </summary>
    public bool IsSearchable { get; set; } = false;
    
    /// <summary>
    /// Custom encryption algorithm if different from tenant default
    /// </summary>
    public string? Algorithm { get; set; }
    
    /// <summary>
    /// Priority level for re-encryption during key rotation (higher = more important)
    /// </summary>
    public int ReencryptionPriority { get; set; } = 1;

    public EncryptedFieldAttribute(string fieldName = "")
    {
        FieldName = fieldName;
    }
}

/// <summary>
/// Attribute to mark classes that contain encrypted fields
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class EncryptedEntityAttribute : Attribute
{
    /// <summary>
    /// Whether encryption is required for this entity type
    /// </summary>
    public bool RequiresEncryption { get; set; } = true;
    
    /// <summary>
    /// Entity type name for encryption metadata
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether to audit access to encrypted fields
    /// </summary>
    public bool AuditAccess { get; set; } = true;

    public EncryptedEntityAttribute(string entityType = "")
    {
        EntityType = entityType;
    }
}