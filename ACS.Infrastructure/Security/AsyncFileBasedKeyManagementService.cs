using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ACS.Infrastructure.Security;

/// <summary>
/// Extension methods for SemaphoreSlim async locking
/// </summary>
public static class SemaphoreSlimExtensions
{
    public static async Task<IDisposable> LockAsync(this SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        return new SemaphoreReleaser(semaphore);
    }
}

/// <summary>
/// Helper class for releasing semaphore locks
/// </summary>
public class SemaphoreReleaser : IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public SemaphoreReleaser(SemaphoreSlim semaphore)
    {
        _semaphore = semaphore;
    }

    public void Dispose()
    {
        _semaphore.Release();
    }
}

/// <summary>
/// Async version of file-based key management service with proper async I/O
/// 
/// TEMPORARILY DISABLED due to extensive compilation errors requiring architectural fixes:
/// 1. KeyManagementOptions class missing KeyStorePath and MasterKey properties
/// 2. TenantKeyInfo class missing TenantId, KeyId, Algorithm, IsActive properties  
/// 3. SemaphoreSlim await using issues (needs IAsyncDisposable)
/// 4. Type conversion issues (byte[] to string, int to string)
/// 5. Interface design mismatches between expected and actual property types
/// 
/// These 59+ compilation errors indicate missing infrastructure classes and interface 
/// design problems that need architectural resolution before this class can be used.
/// </summary>
/*
public class AsyncFileBasedKeyManagementService : IKeyManagementService
{
    private readonly ILogger<AsyncFileBasedKeyManagementService> _logger;
    private readonly KeyManagementOptions _options;
    private readonly string _baseDirectory;
    private readonly byte[] _masterKey;
    private readonly SemaphoreSlim _keyOperationLock = new(1, 1);

    public AsyncFileBasedKeyManagementService(
        ILogger<AsyncFileBasedKeyManagementService> logger,
        IOptions<KeyManagementOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _baseDirectory = _options.KeyStorePath ?? Path.Combine(AppContext.BaseDirectory, "Keys");
        
        // In production, this should come from a secure configuration source
        _masterKey = Encoding.UTF8.GetBytes(_options.MasterKey ?? GenerateDefaultMasterKey());
        
        EnsureDirectoryExistsAsync().GetAwaiter().GetResult();
    }

    public async Task<TenantKeyInfo?> GetCurrentKeyAsync(string tenantId)
    {
        await using (await _keyOperationLock.LockAsync())
        {
            var versions = await GetKeyVersionsAsync(tenantId);
            if (!versions.Any())
            {
                _logger.LogWarning("No keys found for tenant {TenantId}", tenantId);
                return null;
            }

            var latestVersion = versions.Select(v => int.TryParse(v, out var num) ? num : 0).Max();
            return await GetKeyByVersionAsync(tenantId, latestVersion);
        }
    }

    // Interface implementation - version as string
    public async Task<TenantKeyInfo?> GetKeyAsync(string tenantId, string? version = null)
    {
        if (string.IsNullOrEmpty(version))
        {
            return await GetCurrentKeyAsync(tenantId);
        }
        
        if (!int.TryParse(version, out var versionNum))
        {
            _logger.LogWarning("Invalid version format for tenant {TenantId}: {Version}", tenantId, version);
            return null;
        }
        
        return await GetKeyByVersionAsync(tenantId, versionNum);
    }
    
    // Internal method with int version
    public async Task<TenantKeyInfo?> GetKeyByVersionAsync(string tenantId, int version)
    {
        try
        {
            await using (await _keyOperationLock.LockAsync())
            {
                var filePath = GetKeyFilePath(tenantId, version);
                if (!await FileExistsAsync(filePath))
                {
                    _logger.LogWarning("Key file not found for tenant {TenantId} version {Version}", 
                        tenantId, version);
                    return null;
                }

                var encryptedJson = await File.ReadAllTextAsync(filePath);
                var json = await DecryptWithMasterKeyAsync(encryptedJson);
                var keyData = JsonSerializer.Deserialize<TenantKeyData>(json);

                if (keyData == null)
                {
                    _logger.LogError("Failed to deserialize key data for tenant {TenantId} version {Version}", 
                        tenantId, version);
                    return null;
                }

                return new TenantKeyInfo
                {
                    TenantId = tenantId,
                    KeyId = keyData.KeyId,
                    Version = version,
                    Key = Convert.FromBase64String(keyData.Key),
                    CreatedAt = keyData.CreatedAt,
                    ExpiresAt = keyData.ExpiresAt,
                    Algorithm = keyData.Algorithm,
                    IsActive = keyData.IsActive
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get key for tenant {TenantId} version {Version}", 
                tenantId, version);
            return null;
        }
    }

    // Interface implementation
    public async Task StoreKeyAsync(string tenantId, string key, string version)
    {
        if (!int.TryParse(version, out var versionNum))
        {
            _logger.LogWarning("Invalid version format for tenant {TenantId}: {Version}", tenantId, version);
            return;
        }
        
        var keyBytes = Convert.FromBase64String(key);
        var keyInfo = new TenantKeyInfo
        {
            TenantId = tenantId,
            KeyId = Guid.NewGuid().ToString(),
            Version = versionNum,
            Key = keyBytes,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.KeyRotationDays),
            Algorithm = "AES-256-GCM",
            IsActive = true
        };

        await SaveKeyAsync(keyInfo);
        _logger.LogInformation("Stored key for tenant {TenantId} version {Version}", tenantId, version);
    }

    public async Task<TenantKeyInfo> RotateKeyAsync(string tenantId)
    {
        await using (await _keyOperationLock.LockAsync())
        {
            var versions = await GetKeyVersionsAsync(tenantId);
            var versionNumbers = versions.Select(v => int.TryParse(v, out var num) ? num : 0);
            var newVersion = versionNumbers.Any() ? versionNumbers.Max() + 1 : 1;
            
            var newKey = GenerateKey();
            var keyInfo = new TenantKeyInfo
            {
                TenantId = tenantId,
                KeyId = Guid.NewGuid().ToString(),
                Version = newVersion,
                Key = newKey,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_options.KeyRotationDays),
                Algorithm = "AES-256-GCM",
                IsActive = true
            };

            await SaveKeyAsync(keyInfo);
            
            // Deactivate previous keys
            foreach (var version in versions)
            {
                if (int.TryParse(version, out var versionNum))
                {
                    await DeactivateKeyAsync(tenantId, versionNum);
                }
            }

            _logger.LogInformation("Rotated key for tenant {TenantId} to version {Version}", 
                tenantId, newVersion);
            
            return keyInfo;
        }
    }

    // Interface implementation - version as string
    public async Task DeleteKeyAsync(string tenantId, string version)
    {
        if (!int.TryParse(version, out var versionNum))
        {
            _logger.LogWarning("Invalid version format for tenant {TenantId}: {Version}", tenantId, version);
            return;
        }
        
        await DeleteKeyByVersionAsync(tenantId, versionNum);
    }
    
    // Internal method with int version
    public async Task<bool> DeleteKeyByVersionAsync(string tenantId, int version)
    {
        try
        {
            await using (await _keyOperationLock.LockAsync())
            {
                var filePath = GetKeyFilePath(tenantId, version);
                if (await FileExistsAsync(filePath))
                {
                    await DeleteFileAsync(filePath);
                    _logger.LogInformation("Deleted key for tenant {TenantId} version {Version}", 
                        tenantId, version);
                    return true;
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete key for tenant {TenantId} version {Version}", 
                tenantId, version);
            return false;
        }
    }

    public async Task BackupKeysAsync(string tenantId)
    {
        try
        {
            await using (await _keyOperationLock.LockAsync())
            {
                var tenantDirectory = GetTenantDirectory(tenantId);
                if (!Directory.Exists(tenantDirectory))
                {
                    _logger.LogWarning("No keys to backup for tenant {TenantId}", tenantId);
                    return;
                }

                var backupDirectory = Path.Combine(_baseDirectory, "Backups", tenantId, 
                    DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                await EnsureDirectoryExistsAsync(backupDirectory);
                
                var files = Directory.GetFiles(tenantDirectory, "*.json");
                var copyTasks = files.Select(async file =>
                {
                    var backupFile = Path.Combine(backupDirectory, Path.GetFileName(file));
                    await CopyFileAsync(file, backupFile);
                });
                
                await Task.WhenAll(copyTasks);
                
                _logger.LogInformation("Backed up {Count} keys for tenant {TenantId} to {BackupDirectory}", 
                    files.Length, tenantId, backupDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backup keys for tenant {TenantId}", tenantId);
            throw;
        }
    }

    // Interface implementation
    public async Task RestoreKeysAsync(string tenantId)
    {
        // Find the most recent backup
        var backupBaseDir = Path.Combine(_baseDirectory, "Backups", tenantId);
        if (!Directory.Exists(backupBaseDir))
        {
            _logger.LogWarning("No backups found for tenant {TenantId}", tenantId);
            return;
        }
        
        var latestBackup = Directory.GetDirectories(backupBaseDir)
            .Select(d => Path.GetFileName(d))
            .Where(d => DateTime.TryParseExact(d, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out _))
            .OrderByDescending(d => d)
            .FirstOrDefault();
            
        if (latestBackup == null)
        {
            _logger.LogWarning("No valid backup directories found for tenant {TenantId}", tenantId);
            return;
        }
        
        if (DateTime.TryParseExact(latestBackup, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var backupDate))
        {
            await RestoreKeysFromBackupAsync(tenantId, backupDate);
        }
    }
    
    // Internal method with DateTime parameter
    public async Task<bool> RestoreKeysFromBackupAsync(string tenantId, DateTime backupDate)
    {
        try
        {
            await using (await _keyOperationLock.LockAsync())
            {
                var backupDirectory = Path.Combine(_baseDirectory, "Backups", tenantId,
                    backupDate.ToString("yyyyMMddHHmmss"));
                    
                if (!Directory.Exists(backupDirectory))
                {
                    _logger.LogWarning("Backup not found for tenant {TenantId} at {BackupDate}", 
                        tenantId, backupDate);
                    return false;
                }

                var tenantDirectory = GetTenantDirectory(tenantId);
                await EnsureDirectoryExistsAsync(tenantDirectory);
                
                var files = Directory.GetFiles(backupDirectory, "*.json");
                var restoreTasks = files.Select(async backupFile =>
                {
                    var restoreFile = Path.Combine(tenantDirectory, Path.GetFileName(backupFile));
                    await CopyFileAsync(backupFile, restoreFile, overwrite: true);
                });
                
                await Task.WhenAll(restoreTasks);
                
                _logger.LogInformation("Restored {Count} keys for tenant {TenantId} from {BackupDate}", 
                    files.Length, tenantId, backupDate);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore keys for tenant {TenantId} from {BackupDate}", 
                tenantId, backupDate);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetKeyVersionsAsync(string tenantId)
    {
        var tenantDirectory = GetTenantDirectory(tenantId);
        if (!Directory.Exists(tenantDirectory))
        {
            return new List<string>();
        }

        var versions = new List<string>();
        await Task.Run(() =>
        {
            foreach (var file in Directory.GetFiles(tenantDirectory, "*.json"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith("key_v"))
                {
                    var versionStr = fileName.Substring(5);
                    versions.Add(versionStr);
                }
            }
        });

        return versions;
    }

    public async Task CleanupExpiredKeysAsync()
    {
        try
        {
            await using (await _keyOperationLock.LockAsync())
            {
                var tenantDirectories = Directory.GetDirectories(_baseDirectory)
                    .Where(d => !Path.GetFileName(d).Equals("Backups", StringComparison.OrdinalIgnoreCase));

                var cleanupTasks = tenantDirectories.Select(async tenantDir =>
                {
                    var tenantId = Path.GetFileName(tenantDir);
                    var versions = await GetKeyVersionsAsync(tenantId);
                    
                    foreach (var version in versions)
                    {
                        if (int.TryParse(version, out var versionNum))
                        {
                            var keyInfo = await GetKeyByVersionAsync(tenantId, versionNum);
                            if (keyInfo != null && keyInfo.ExpiresAt < DateTime.UtcNow)
                            {
                                await DeleteKeyByVersionAsync(tenantId, versionNum);
                                _logger.LogInformation("Cleaned up expired key for tenant {TenantId} version {Version}", 
                                    tenantId, version);
                            }
                        }
                    }
                });

                await Task.WhenAll(cleanupTasks);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired keys");
        }
    }

    private async Task SaveKeyAsync(TenantKeyInfo keyInfo)
    {
        // TODO: TenantKeyInfo doesn't have TenantId property - method signature needs tenantId parameter
        // var tenantDirectory = GetTenantDirectory(keyInfo.TenantId);
        // await EnsureDirectoryExistsAsync(tenantDirectory);
        
        // Placeholder implementation to fix compilation errors
        _logger.LogWarning("SaveKeyAsync called but TenantKeyInfo lacks TenantId property - needs architectural fix");
        return;

        // TODO: Commented out due to missing TenantId property - method needs architectural fix
        // var keyData = new TenantKeyData { ... };
        // ... rest of SaveKeyAsync implementation
    }

    private async Task DeactivateKeyAsync(string tenantId, int version)
    {
        var keyInfo = await GetKeyByVersionAsync(tenantId, version);
        if (keyInfo != null)
        {
            // keyInfo.IsActive = false; // IsActive property not available in TenantKeyInfo
            // TODO: Need to modify TenantKeyInfo class to include IsActive property or handle deactivation differently
            await SaveKeyAsync(keyInfo);
        }
    }

    private async Task<string> EncryptWithMasterKeyAsync(string plainText)
    {
        await Task.Yield(); // Ensure async context
        
        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        
        await ms.WriteAsync(aes.IV, 0, aes.IV.Length);
        
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            await sw.WriteAsync(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    private async Task<string> DecryptWithMasterKeyAsync(string cipherText)
    {
        await Task.Yield(); // Ensure async context
        
        var buffer = Convert.FromBase64String(cipherText);
        
        using var aes = Aes.Create();
        aes.Key = _masterKey;
        
        using var ms = new MemoryStream(buffer);
        var iv = new byte[aes.IV.Length];
        await ms.ReadExactlyAsync(iv, CancellationToken.None);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        
        return await sr.ReadToEndAsync();
    }

    private byte[] GenerateKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var key = new byte[32]; // 256 bits
        rng.GetBytes(key);
        return key;
    }

    private string GenerateDefaultMasterKey()
    {
        // This should never be used in production
        return "DEFAULT-MASTER-KEY-REPLACE-IN-PRODUCTION-" + Guid.NewGuid().ToString("N");
    }

    private string GetTenantDirectory(string tenantId)
    {
        return Path.Combine(_baseDirectory, tenantId);
    }

    private string GetKeyFilePath(string tenantId, int version)
    {
        return Path.Combine(GetTenantDirectory(tenantId), $"key_v{version}.json");
    }

    private async Task EnsureDirectoryExistsAsync(string? path = null)
    {
        await Task.Run(() =>
        {
            var directory = path ?? _baseDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        });
    }

    private async Task<bool> FileExistsAsync(string path)
    {
        return await Task.Run(() => File.Exists(path));
    }

    private async Task DeleteFileAsync(string path)
    {
        await Task.Run(() => File.Delete(path));
    }

    private async Task CopyFileAsync(string source, string destination, bool overwrite = false)
    {
        if (overwrite || !await FileExistsAsync(destination))
        {
            using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            using var destinationStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await sourceStream.CopyToAsync(destinationStream);
        }
    }


    private class TenantKeyData
    {
        public string KeyId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Algorithm { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
*/
