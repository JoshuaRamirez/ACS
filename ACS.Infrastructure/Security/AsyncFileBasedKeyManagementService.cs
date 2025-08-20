using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ACS.Infrastructure.Security;

/// <summary>
/// Async version of file-based key management service with proper async I/O
/// </summary>
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

            var latestVersion = versions.Max();
            return await GetKeyAsync(tenantId, latestVersion);
        }
    }

    public async Task<TenantKeyInfo?> GetKeyAsync(string tenantId, int version)
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

    public async Task<TenantKeyInfo> RotateKeyAsync(string tenantId)
    {
        await using (await _keyOperationLock.LockAsync())
        {
            var versions = await GetKeyVersionsAsync(tenantId);
            var newVersion = versions.Any() ? versions.Max() + 1 : 1;
            
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
                await DeactivateKeyAsync(tenantId, version);
            }

            _logger.LogInformation("Rotated key for tenant {TenantId} to version {Version}", 
                tenantId, newVersion);
            
            return keyInfo;
        }
    }

    public async Task<bool> DeleteKeyAsync(string tenantId, int version)
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

    public async Task<bool> RestoreKeysAsync(string tenantId, DateTime backupDate)
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

    public async Task<List<int>> GetKeyVersionsAsync(string tenantId)
    {
        var tenantDirectory = GetTenantDirectory(tenantId);
        if (!Directory.Exists(tenantDirectory))
        {
            return new List<int>();
        }

        var versions = new List<int>();
        await Task.Run(() =>
        {
            foreach (var file in Directory.GetFiles(tenantDirectory, "*.json"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith("key_v") && int.TryParse(fileName.Substring(5), out var version))
                {
                    versions.Add(version);
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
                        var keyInfo = await GetKeyAsync(tenantId, version);
                        if (keyInfo != null && keyInfo.ExpiresAt < DateTime.UtcNow)
                        {
                            await DeleteKeyAsync(tenantId, version);
                            _logger.LogInformation("Cleaned up expired key for tenant {TenantId} version {Version}", 
                                tenantId, version);
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
        var tenantDirectory = GetTenantDirectory(keyInfo.TenantId);
        await EnsureDirectoryExistsAsync(tenantDirectory);

        var keyData = new TenantKeyData
        {
            KeyId = keyInfo.KeyId,
            Key = Convert.ToBase64String(keyInfo.Key),
            CreatedAt = keyInfo.CreatedAt,
            ExpiresAt = keyInfo.ExpiresAt,
            Algorithm = keyInfo.Algorithm,
            IsActive = keyInfo.IsActive
        };

        var json = JsonSerializer.Serialize(keyData);
        var encryptedJson = await EncryptWithMasterKeyAsync(json);
        
        var filePath = GetKeyFilePath(keyInfo.TenantId, keyInfo.Version);
        await File.WriteAllTextAsync(filePath, encryptedJson);
    }

    private async Task DeactivateKeyAsync(string tenantId, int version)
    {
        var keyInfo = await GetKeyAsync(tenantId, version);
        if (keyInfo != null)
        {
            keyInfo.IsActive = false;
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
        await ms.ReadAsync(iv, 0, iv.Length);
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

    private async Task<IDisposable> LockAsync(this SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        return new SemaphoreReleaser(semaphore);
    }

    private class SemaphoreReleaser : IDisposable
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