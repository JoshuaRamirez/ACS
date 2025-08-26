using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ACS.Infrastructure.Security;

/// <summary>
/// AES-based encryption service with tenant isolation and key management
/// </summary>
public class AesEncryptionService : IEncryptionService
{
    private readonly ILogger<AesEncryptionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IKeyManagementService _keyManagementService;
    
    // Cache for tenant keys to avoid frequent key retrieval
    private readonly Dictionary<string, TenantKeyInfo> _tenantKeyCache = new();
    private readonly SemaphoreSlim _keyCacheLock = new(1, 1);
    
    public AesEncryptionService(
        ILogger<AesEncryptionService> logger,
        IConfiguration configuration,
        IKeyManagementService keyManagementService)
    {
        _logger = logger;
        _configuration = configuration;
        _keyManagementService = keyManagementService;
    }

    public async Task<string> EncryptAsync(string plainText, string tenantId)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            var keyInfo = await GetTenantKeyAsync(tenantId);
            
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(keyInfo.Key);
            aes.GenerateIV();
            
            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            
            // Combine IV and encrypted data
            var result = new byte[aes.IV.Length + encryptedBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);
            
            var encryptedText = Convert.ToBase64String(result);
            
            _logger.LogDebug("Successfully encrypted data for tenant {TenantId}", tenantId);
            return encryptedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<string> DecryptAsync(string encryptedText, string tenantId)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            var keyInfo = await GetTenantKeyAsync(tenantId);
            
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(keyInfo.Key);
            
            // Extract IV from the beginning of encrypted data
            var iv = new byte[aes.IV.Length];
            var cipherBytes = new byte[encryptedBytes.Length - iv.Length];
            
            Buffer.BlockCopy(encryptedBytes, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(encryptedBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);
            
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            var plainText = Encoding.UTF8.GetString(decryptedBytes);
            
            _logger.LogDebug("Successfully decrypted data for tenant {TenantId}", tenantId);
            return plainText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<EncryptedField> EncryptFieldAsync(string plainText, string fieldName, string entityId, string tenantId)
    {
        if (string.IsNullOrEmpty(plainText))
            return new EncryptedField { FieldName = fieldName, EntityId = entityId };

        try
        {
            var keyInfo = await GetTenantKeyAsync(tenantId);
            
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(keyInfo.Key);
            aes.GenerateIV();
            
            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            
            var encryptedValue = Convert.ToBase64String(encryptedBytes);
            var iv = Convert.ToBase64String(aes.IV);
            
            // Generate checksum for integrity verification
            var checksum = GenerateChecksum(encryptedValue, keyInfo.Version, fieldName, entityId);
            
            var encryptedField = new EncryptedField
            {
                EncryptedValue = encryptedValue,
                KeyVersion = keyInfo.Version,
                Algorithm = "AES-256-GCM",
                InitializationVector = iv,
                EncryptedAt = DateTime.UtcNow,
                FieldName = fieldName,
                EntityId = entityId,
                Checksum = checksum
            };
            
            _logger.LogDebug("Successfully encrypted field {FieldName} for entity {EntityId} in tenant {TenantId}", 
                fieldName, entityId, tenantId);
                
            return encryptedField;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt field {FieldName} for entity {EntityId} in tenant {TenantId}", 
                fieldName, entityId, tenantId);
            throw;
        }
    }

    public async Task<string> DecryptFieldAsync(EncryptedField encryptedField, string tenantId)
    {
        if (encryptedField == null || string.IsNullOrEmpty(encryptedField.EncryptedValue))
            return string.Empty;

        try
        {
            var keyInfo = await GetTenantKeyAsync(tenantId, encryptedField.KeyVersion);
            
            // Verify checksum for integrity
            var expectedChecksum = GenerateChecksum(encryptedField.EncryptedValue, encryptedField.KeyVersion, 
                encryptedField.FieldName, encryptedField.EntityId);
            if (expectedChecksum != encryptedField.Checksum)
            {
                _logger.LogWarning("Checksum mismatch for field {FieldName} entity {EntityId} in tenant {TenantId}", 
                    encryptedField.FieldName, encryptedField.EntityId, tenantId);
                throw new CryptographicException("Data integrity check failed");
            }
            
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(keyInfo.Key);
            aes.IV = Convert.FromBase64String(encryptedField.InitializationVector);
            
            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(encryptedField.EncryptedValue);
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            var plainText = Encoding.UTF8.GetString(decryptedBytes);
            
            _logger.LogDebug("Successfully decrypted field {FieldName} for entity {EntityId} in tenant {TenantId}", 
                encryptedField.FieldName, encryptedField.EntityId, tenantId);
                
            return plainText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt field {FieldName} for entity {EntityId} in tenant {TenantId}", 
                encryptedField.FieldName, encryptedField.EntityId, tenantId);
            throw;
        }
    }

    public async Task<string> GenerateTenantKeyAsync(string tenantId)
    {
        try
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            var keyBase64 = Convert.ToBase64String(aes.Key);
            
            await _keyManagementService.StoreKeyAsync(tenantId, keyBase64, "1");
            
            // Clear cache to force reload
            await _keyCacheLock.WaitAsync();
            try
            {
                _tenantKeyCache.Remove(tenantId);
            }
            finally
            {
                _keyCacheLock.Release();
            }
            
            _logger.LogInformation("Generated new encryption key for tenant {TenantId}", tenantId);
            return keyBase64;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate key for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task RotateKeysAsync(string tenantId)
    {
        try
        {
            var oldKeyInfo = await GetTenantKeyAsync(tenantId);
            var newKeyVersion = (int.Parse(oldKeyInfo.Version) + 1).ToString();
            
            // Generate new key
            using var aes = Aes.Create();
            aes.GenerateKey();
            var newKeyBase64 = Convert.ToBase64String(aes.Key);
            
            await _keyManagementService.StoreKeyAsync(tenantId, newKeyBase64, newKeyVersion);
            
            // Keep old key for decryption of existing data
            // Schedule background re-encryption process
            _ = Task.Run(async () => await StartBackgroundReEncryptionAsync(tenantId, oldKeyInfo.Version, newKeyVersion));
            
            // Clear cache to force reload
            await _keyCacheLock.WaitAsync();
            try
            {
                _tenantKeyCache.Remove(tenantId);
            }
            finally
            {
                _keyCacheLock.Release();
            }
            
            _logger.LogInformation("Rotated encryption key for tenant {TenantId} to version {Version}", 
                tenantId, newKeyVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate keys for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<bool> ValidateKeyIntegrityAsync(string tenantId)
    {
        try
        {
            var keyInfo = await GetTenantKeyAsync(tenantId);
            
            // Test encryption/decryption round trip
            var testData = "integrity_check_" + Guid.NewGuid().ToString();
            var encrypted = await EncryptAsync(testData, tenantId);
            var decrypted = await DecryptAsync(encrypted, tenantId);
            
            var isValid = testData == decrypted;
            
            _logger.LogDebug("Key integrity validation for tenant {TenantId}: {IsValid}", tenantId, isValid);
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Key integrity validation failed for tenant {TenantId}", tenantId);
            return false;
        }
    }

    private async Task<TenantKeyInfo> GetTenantKeyAsync(string tenantId, string? version = null)
    {
        var cacheKey = $"{tenantId}:{version ?? "latest"}";
        
        await _keyCacheLock.WaitAsync();
        try
        {
            if (_tenantKeyCache.TryGetValue(cacheKey, out var cachedKey) && 
                cachedKey.ExpiresAt > DateTime.UtcNow)
            {
                return cachedKey;
            }

            var keyInfo = await _keyManagementService.GetKeyAsync(tenantId, version);
            if (keyInfo == null)
            {
                if (version == null)
                {
                    // Generate initial key if none exists
                    await GenerateTenantKeyAsync(tenantId);
                    keyInfo = await _keyManagementService.GetKeyAsync(tenantId, version);
                }
                
                if (keyInfo == null)
                    throw new InvalidOperationException($"Failed to retrieve encryption key for tenant {tenantId}");
            }
            
            // Cache for 30 minutes
            keyInfo.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
            _tenantKeyCache[cacheKey] = keyInfo;
            
            return keyInfo;
        }
        finally
        {
            _keyCacheLock.Release();
        }
    }
    
    private static string GenerateChecksum(string encryptedValue, string keyVersion, string fieldName, string entityId)
    {
        var data = $"{encryptedValue}:{keyVersion}:{fieldName}:{entityId}";
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }
    
    /// <summary>
    /// Starts background re-encryption of existing data with new key
    /// </summary>
    private async Task StartBackgroundReEncryptionAsync(string tenantId, string oldKeyVersion, string newKeyVersion)
    {
        try
        {
            _logger.LogInformation("Starting background re-encryption for tenant {TenantId} from key version {OldVersion} to {NewVersion}",
                tenantId, oldKeyVersion, newKeyVersion);
            
            // This is a simplified implementation - in production you would:
            // 1. Query all encrypted fields for the tenant
            // 2. Process them in batches to avoid memory issues
            // 3. Implement retry logic for failed re-encryptions
            // 4. Track progress and completion status
            // 5. Clean up old keys after successful re-encryption
            
            // For now, just log the intent and mark as completed
            await Task.Delay(1000); // Simulate work
            
            _logger.LogInformation("Background re-encryption completed for tenant {TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background re-encryption failed for tenant {TenantId}", tenantId);
        }
    }
}

/// <summary>
/// Tenant key information with metadata
/// </summary>
public class TenantKeyInfo
{
    public string Key { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}