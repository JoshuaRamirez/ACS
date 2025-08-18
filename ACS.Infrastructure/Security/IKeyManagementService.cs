namespace ACS.Infrastructure.Security;

/// <summary>
/// Interface for managing tenant encryption keys with secure storage
/// </summary>
public interface IKeyManagementService
{
    /// <summary>
    /// Store encryption key for a tenant with version
    /// </summary>
    Task StoreKeyAsync(string tenantId, string key, string version);
    
    /// <summary>
    /// Retrieve encryption key for a tenant
    /// </summary>
    Task<TenantKeyInfo?> GetKeyAsync(string tenantId, string? version = null);
    
    /// <summary>
    /// List all key versions for a tenant
    /// </summary>
    Task<IEnumerable<string>> GetKeyVersionsAsync(string tenantId);
    
    /// <summary>
    /// Delete old key version (use with caution)
    /// </summary>
    Task DeleteKeyAsync(string tenantId, string version);
    
    /// <summary>
    /// Backup encryption keys securely
    /// </summary>
    Task BackupKeysAsync(string tenantId);
    
    /// <summary>
    /// Restore encryption keys from backup
    /// </summary>
    Task RestoreKeysAsync(string tenantId);
}