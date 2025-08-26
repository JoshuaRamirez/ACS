using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ACS.Infrastructure.Security;

/// <summary>
/// File-based key management service with encryption at rest
/// For production use, consider Azure Key Vault or HashiCorp Vault
/// </summary>
public class FileBasedKeyManagementService : IKeyManagementService
{
    private readonly ILogger<FileBasedKeyManagementService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _keyStoragePath;
    private readonly string _masterKey;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public FileBasedKeyManagementService(
        ILogger<FileBasedKeyManagementService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _keyStoragePath = configuration.GetValue<string>("Encryption:KeyStoragePath") 
                         ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ACS", "Keys");
        _masterKey = configuration.GetValue<string>("Encryption:MasterKey") 
                    ?? throw new InvalidOperationException("Master key not configured");
        
        // Ensure key storage directory exists
        Directory.CreateDirectory(_keyStoragePath);
        
        // Set restrictive permissions on key directory
        SetKeyDirectoryPermissions();
    }

    public async Task StoreKeyAsync(string tenantId, string key, string version)
    {
        await _fileLock.WaitAsync();
        try
        {
            var keyData = new TenantKeyData
            {
                TenantId = tenantId,
                Key = key,
                Version = version,
                CreatedAt = DateTime.UtcNow,
                Algorithm = "AES-256"
            };

            var json = JsonSerializer.Serialize(keyData, new JsonSerializerOptions { WriteIndented = true });
            var encryptedJson = await EncryptWithMasterKeyAsync(json);
            
            var filePath = GetKeyFilePath(tenantId, version);
            await File.WriteAllTextAsync(filePath, encryptedJson);
            
            _logger.LogInformation("Stored encryption key for tenant {TenantId} version {Version}", tenantId, version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store key for tenant {TenantId} version {Version}", tenantId, version);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<TenantKeyInfo?> GetKeyAsync(string tenantId, string? version = null)
    {
        await _fileLock.WaitAsync();
        try
        {
            if (version == null)
            {
                // Get latest version
                var versions = await GetKeyVersionsAsync(tenantId);
                version = versions.OrderByDescending(v => int.Parse(v)).FirstOrDefault();
                
                if (version == null)
                    return null;
            }

            var filePath = GetKeyFilePath(tenantId, version);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Key file not found for tenant {TenantId} version {Version}", tenantId, version);
                return null;
            }

            var encryptedJson = await File.ReadAllTextAsync(filePath);
            var json = await DecryptWithMasterKeyAsync(encryptedJson);
            var keyData = JsonSerializer.Deserialize<TenantKeyData>(json);

            if (keyData == null)
            {
                _logger.LogError("Failed to deserialize key data for tenant {TenantId} version {Version}", tenantId, version);
                return null;
            }

            return new TenantKeyInfo
            {
                Key = keyData.Key,
                Version = keyData.Version,
                CreatedAt = keyData.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve key for tenant {TenantId} version {Version}", tenantId, version);
            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<IEnumerable<string>> GetKeyVersionsAsync(string tenantId)
    {
        await _fileLock.WaitAsync();
        try
        {
            var tenantDirectory = Path.Combine(_keyStoragePath, tenantId);
            if (!Directory.Exists(tenantDirectory))
                return Enumerable.Empty<string>();

            var files = Directory.GetFiles(tenantDirectory, "key_v*.json");
            var versions = new List<string>();

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith("key_v") && fileName.Length > 5)
                {
                    var versionStr = fileName[5..]; // Remove "key_v" prefix
                    if (int.TryParse(versionStr, out _))
                    {
                        versions.Add(versionStr);
                    }
                }
            }

            return versions.OrderByDescending(v => int.Parse(v));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get key versions for tenant {TenantId}", tenantId);
            return Enumerable.Empty<string>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteKeyAsync(string tenantId, string version)
    {
        await _fileLock.WaitAsync();
        try
        {
            var filePath = GetKeyFilePath(tenantId, version);
            if (File.Exists(filePath))
            {
                // Secure deletion - overwrite before deleting
                await SecureDeleteFileAsync(filePath);
                _logger.LogWarning("Deleted encryption key for tenant {TenantId} version {Version}", tenantId, version);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete key for tenant {TenantId} version {Version}", tenantId, version);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task BackupKeysAsync(string tenantId)
    {
        await _fileLock.WaitAsync();
        try
        {
            var tenantDirectory = Path.Combine(_keyStoragePath, tenantId);
            var backupDirectory = Path.Combine(_keyStoragePath, "backups", tenantId, DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss"));
            
            if (Directory.Exists(tenantDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
                
                foreach (var file in Directory.GetFiles(tenantDirectory, "*.json"))
                {
                    var backupFile = Path.Combine(backupDirectory, Path.GetFileName(file));
                    File.Copy(file, backupFile);
                }
                
                _logger.LogInformation("Backed up encryption keys for tenant {TenantId} to {BackupDirectory}", 
                    tenantId, backupDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backup keys for tenant {TenantId}", tenantId);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task RestoreKeysAsync(string tenantId)
    {
        await _fileLock.WaitAsync();
        try
        {
            var backupBaseDirectory = Path.Combine(_keyStoragePath, "backups", tenantId);
            if (!Directory.Exists(backupBaseDirectory))
            {
                _logger.LogWarning("No backups found for tenant {TenantId}", tenantId);
                return;
            }

            // Find the most recent backup
            var latestBackup = Directory.GetDirectories(backupBaseDirectory)
                .OrderByDescending(d => Path.GetFileName(d))
                .FirstOrDefault();

            if (latestBackup == null)
            {
                _logger.LogWarning("No backup directories found for tenant {TenantId}", tenantId);
                return;
            }

            var tenantDirectory = Path.Combine(_keyStoragePath, tenantId);
            Directory.CreateDirectory(tenantDirectory);

            foreach (var backupFile in Directory.GetFiles(latestBackup, "*.json"))
            {
                var restoreFile = Path.Combine(tenantDirectory, Path.GetFileName(backupFile));
                File.Copy(backupFile, restoreFile, overwrite: true);
            }

            _logger.LogInformation("Restored encryption keys for tenant {TenantId} from backup {BackupDirectory}", 
                tenantId, latestBackup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore keys for tenant {TenantId}", tenantId);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private string GetKeyFilePath(string tenantId, string version)
    {
        var tenantDirectory = Path.Combine(_keyStoragePath, tenantId);
        Directory.CreateDirectory(tenantDirectory);
        return Path.Combine(tenantDirectory, $"key_v{version}.json");
    }

    private Task<string> EncryptWithMasterKeyAsync(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(_masterKey);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Combine IV and encrypted data
        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return Task.FromResult(Convert.ToBase64String(result));
    }

    private Task<string> DecryptWithMasterKeyAsync(string encryptedText)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedText);

        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(_masterKey);

        // Extract IV from the beginning of encrypted data
        var iv = new byte[aes.IV.Length];
        var cipherBytes = new byte[encryptedBytes.Length - iv.Length];

        Buffer.BlockCopy(encryptedBytes, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(encryptedBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Task.FromResult(Encoding.UTF8.GetString(decryptedBytes));
    }

    private async Task SecureDeleteFileAsync(string filePath)
    {
        // Overwrite file content before deletion
        var fileInfo = new FileInfo(filePath);
        var random = new Random();
        var buffer = new byte[fileInfo.Length];

        // Multiple overwrite passes
        for (int pass = 0; pass < 3; pass++)
        {
            random.NextBytes(buffer);
            await File.WriteAllBytesAsync(filePath, buffer);
            await File.WriteAllBytesAsync(filePath, new byte[fileInfo.Length]); // Zero fill
        }

        File.Delete(filePath);
    }

    private void SetKeyDirectoryPermissions()
    {
        try
        {
            // On Windows, this would set NTFS permissions
            // On Linux/Mac, this would set file permissions
            // For simplicity, we'll just ensure the directory exists
            // In production, implement proper OS-specific permission setting
            if (!Directory.Exists(_keyStoragePath))
            {
                Directory.CreateDirectory(_keyStoragePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set restrictive permissions on key directory");
        }
    }

    /// <summary>
    /// Internal data structure for storing tenant key information
    /// </summary>
    private class TenantKeyData
    {
        public string TenantId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Algorithm { get; set; } = string.Empty;
    }
}