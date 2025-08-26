using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;
using System.IO.Compression;
using ACS.Service.Data;

namespace ACS.Service.Services;

/// <summary>
/// Service for database backup and recovery operations
/// </summary>
public interface IDatabaseBackupService
{
    Task<BackupResult> CreateBackupAsync(BackupOptions options, CancellationToken cancellationToken = default);
    Task<RestoreResult> RestoreBackupAsync(RestoreOptions options, CancellationToken cancellationToken = default);
    Task<List<BackupInfo>> GetBackupHistoryAsync(int days = 30, CancellationToken cancellationToken = default);
    Task<bool> VerifyBackupAsync(string backupPath, CancellationToken cancellationToken = default);
    Task<CleanupResult> CleanupOldBackupsAsync(int retentionDays, CancellationToken cancellationToken = default);
}

public class DatabaseBackupService : IDatabaseBackupService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly string _defaultBackupPath;
    private readonly string _connectionString;

    public DatabaseBackupService(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<DatabaseBackupService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _defaultBackupPath = configuration.GetValue<string>("Database:Backup:DefaultPath") 
            ?? Path.Combine(Path.GetTempPath(), "ACS_Backups");
        _connectionString = dbContext.Database.GetConnectionString() 
            ?? throw new InvalidOperationException("Connection string not found");
        
        // Ensure backup directory exists
        Directory.CreateDirectory(_defaultBackupPath);
    }

    public async Task<BackupResult> CreateBackupAsync(BackupOptions options, CancellationToken cancellationToken = default)
    {
        var result = new BackupResult
        {
            StartTime = DateTime.UtcNow,
            BackupType = options.BackupType
        };

        try
        {
            // Generate backup file name
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var databaseName = GetDatabaseName();
            var backupFileName = $"{databaseName}_{options.BackupType}_{timestamp}.bak";
            var backupPath = Path.Combine(options.BackupPath ?? _defaultBackupPath, backupFileName);
            
            result.BackupPath = backupPath;
            
            _logger.LogInformation("Starting {BackupType} backup of database {DatabaseName} to {BackupPath}",
                options.BackupType, databaseName, backupPath);

            // Create backup using SQL command
            var backupCommand = GenerateBackupCommand(databaseName, backupPath, options);
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var command = new SqlCommand(backupCommand, connection);
            command.CommandTimeout = options.TimeoutSeconds ?? 3600; // Default 1 hour timeout
            
            await command.ExecuteNonQueryAsync(cancellationToken);
            
            // Get backup file info
            var fileInfo = new FileInfo(backupPath);
            result.FileSizeBytes = fileInfo.Length;
            
            // Compress if requested
            if (options.Compress)
            {
                var compressedPath = await CompressBackupAsync(backupPath, cancellationToken);
                result.CompressedPath = compressedPath;
                result.CompressedSizeBytes = new FileInfo(compressedPath).Length;
                
                // Delete uncompressed file if requested
                if (options.DeleteUncompressedAfterCompress)
                {
                    File.Delete(backupPath);
                    result.BackupPath = compressedPath;
                }
            }
            
            // Verify backup if requested
            if (options.VerifyAfterBackup)
            {
                result.IsVerified = await VerifyBackupAsync(result.BackupPath, cancellationToken);
            }
            
            // Log backup to history
            await LogBackupHistoryAsync(result, cancellationToken);
            
            result.EndTime = DateTime.UtcNow;
            result.Success = true;
            result.Message = $"Backup completed successfully. Size: {FormatFileSize(result.FileSizeBytes)}";
            
            _logger.LogInformation("Backup completed successfully. Duration: {Duration}ms, Size: {Size}",
                result.Duration.TotalMilliseconds, FormatFileSize(result.FileSizeBytes));
            
            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.Message = $"Backup failed: {ex.Message}";
            result.Error = ex.ToString();
            
            _logger.LogError(ex, "Database backup failed");
            
            return result;
        }
    }

    public async Task<RestoreResult> RestoreBackupAsync(RestoreOptions options, CancellationToken cancellationToken = default)
    {
        var result = new RestoreResult
        {
            StartTime = DateTime.UtcNow,
            BackupPath = options.BackupPath
        };

        try
        {
            // Verify backup file exists
            if (!File.Exists(options.BackupPath))
            {
                throw new FileNotFoundException($"Backup file not found: {options.BackupPath}");
            }
            
            var databaseName = options.TargetDatabaseName ?? GetDatabaseName();
            
            _logger.LogWarning("Starting database restore of {DatabaseName} from {BackupPath}",
                databaseName, options.BackupPath);
            
            // Decompress if needed
            var backupPath = options.BackupPath;
            if (Path.GetExtension(backupPath).Equals(".gz", StringComparison.OrdinalIgnoreCase))
            {
                backupPath = await DecompressBackupAsync(backupPath, cancellationToken);
            }
            
            // Verify backup before restore
            if (options.VerifyBeforeRestore)
            {
                var isValid = await VerifyBackupAsync(backupPath, cancellationToken);
                if (!isValid)
                {
                    throw new InvalidOperationException("Backup verification failed");
                }
            }
            
            // Set database to single user mode if requested
            if (options.ForceRestore)
            {
                await SetDatabaseSingleUserModeAsync(databaseName, true, cancellationToken);
            }
            
            try
            {
                // Generate restore command
                var restoreCommand = GenerateRestoreCommand(databaseName, backupPath, options);
                
                using var connection = new SqlConnection(GetMasterConnectionString());
                await connection.OpenAsync(cancellationToken);
                
                using var command = new SqlCommand(restoreCommand, connection);
                command.CommandTimeout = options.TimeoutSeconds ?? 3600; // Default 1 hour timeout
                
                await command.ExecuteNonQueryAsync(cancellationToken);
                
                result.RestoredDatabaseName = databaseName;
                result.Success = true;
                result.Message = $"Database restored successfully to {databaseName}";
            }
            finally
            {
                // Restore multi-user mode if we set single user mode
                if (options.ForceRestore)
                {
                    await SetDatabaseSingleUserModeAsync(databaseName, false, cancellationToken);
                }
            }
            
            result.EndTime = DateTime.UtcNow;
            
            _logger.LogInformation("Database restore completed successfully. Duration: {Duration}ms",
                result.Duration.TotalMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.Message = $"Restore failed: {ex.Message}";
            result.Error = ex.ToString();
            
            _logger.LogError(ex, "Database restore failed");
            
            return result;
        }
    }

    public async Task<List<BackupInfo>> GetBackupHistoryAsync(int days = 30, CancellationToken cancellationToken = default)
    {
        var history = new List<BackupInfo>();
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        
        try
        {
            // Get backup history from SQL Server
            const string sql = @"
                SELECT 
                    bs.database_name,
                    bs.backup_start_date,
                    bs.backup_finish_date,
                    bs.type,
                    bs.backup_size,
                    bs.compressed_backup_size,
                    bmf.physical_device_name,
                    bs.user_name,
                    bs.server_name,
                    bs.machine_name
                FROM msdb.dbo.backupset bs
                INNER JOIN msdb.dbo.backupmediafamily bmf 
                    ON bs.media_set_id = bmf.media_set_id
                WHERE bs.database_name = @DatabaseName
                    AND bs.backup_start_date >= @CutoffDate
                ORDER BY bs.backup_start_date DESC";
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@DatabaseName", GetDatabaseName());
            command.Parameters.AddWithValue("@CutoffDate", cutoffDate);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            while (await reader.ReadAsync(cancellationToken))
            {
                history.Add(new BackupInfo
                {
                    DatabaseName = reader.GetString(0),
                    BackupStartDate = reader.GetDateTime(1),
                    BackupFinishDate = reader.GetDateTime(2),
                    BackupType = GetBackupTypeFromChar(reader.GetString(3)),
                    BackupSizeBytes = reader.GetInt64(4),
                    CompressedSizeBytes = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                    BackupPath = reader.GetString(6),
                    UserName = reader.GetString(7),
                    ServerName = reader.GetString(8),
                    MachineName = reader.GetString(9)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backup history");
        }
        
        // Also check local backup directory
        if (Directory.Exists(_defaultBackupPath))
        {
            var files = Directory.GetFiles(_defaultBackupPath, "*.bak", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(_defaultBackupPath, "*.gz", SearchOption.AllDirectories));
            
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTimeUtc >= cutoffDate)
                {
                    history.Add(new BackupInfo
                    {
                        DatabaseName = GetDatabaseName(),
                        BackupStartDate = fileInfo.CreationTimeUtc,
                        BackupFinishDate = fileInfo.CreationTimeUtc,
                        BackupType = DetermineBackupTypeFromFileName(file),
                        BackupSizeBytes = fileInfo.Length,
                        BackupPath = file,
                        UserName = Environment.UserName,
                        ServerName = Environment.MachineName,
                        MachineName = Environment.MachineName
                    });
                }
            }
        }
        
        return history.OrderByDescending(h => h.BackupStartDate).ToList();
    }

    public async Task<bool> VerifyBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Verifying backup file: {BackupPath}", backupPath);
            
            // Decompress if needed
            if (Path.GetExtension(backupPath).Equals(".gz", StringComparison.OrdinalIgnoreCase))
            {
                backupPath = await DecompressBackupAsync(backupPath, cancellationToken);
            }
            
            const string sql = "RESTORE VERIFYONLY FROM DISK = @BackupPath";
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@BackupPath", backupPath);
            command.CommandTimeout = 600; // 10 minutes timeout
            
            await command.ExecuteNonQueryAsync(cancellationToken);
            
            _logger.LogInformation("Backup verification successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup verification failed for {BackupPath}", backupPath);
            return false;
        }
    }

    public Task<CleanupResult> CleanupOldBackupsAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        var result = new CleanupResult
        {
            StartTime = DateTime.UtcNow,
            RetentionDays = retentionDays
        };
        
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            
            _logger.LogInformation("Starting backup cleanup. Retention: {RetentionDays} days, Cutoff: {CutoffDate}",
                retentionDays, cutoffDate);
            
            if (!Directory.Exists(_defaultBackupPath))
            {
                result.Message = "Backup directory does not exist";
                result.Success = true;
                return Task.FromResult(result);
            }
            
            var files = Directory.GetFiles(_defaultBackupPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || 
                           f.EndsWith(".gz", StringComparison.OrdinalIgnoreCase));
            
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTimeUtc < cutoffDate)
                {
                    try
                    {
                        File.Delete(file);
                        result.FilesDeleted++;
                        result.BytesFreed += fileInfo.Length;
                        result.DeletedFiles.Add(file);
                        
                        _logger.LogInformation("Deleted old backup: {File}, Age: {Age} days",
                            file, (DateTime.UtcNow - fileInfo.CreationTimeUtc).Days);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete backup file: {File}", file);
                        result.FailedFiles.Add(file);
                    }
                }
            }
            
            result.EndTime = DateTime.UtcNow;
            result.Success = true;
            result.Message = $"Cleanup completed. Deleted {result.FilesDeleted} files, freed {FormatFileSize(result.BytesFreed)}";
            
            _logger.LogInformation("Backup cleanup completed. {Message}", result.Message);
            
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.Message = $"Cleanup failed: {ex.Message}";
            
            _logger.LogError(ex, "Backup cleanup failed");
            
            return Task.FromResult(result);
        }
    }

    private string GenerateBackupCommand(string databaseName, string backupPath, BackupOptions options)
    {
        var commandBuilder = new System.Text.StringBuilder();
        
        commandBuilder.AppendLine($"BACKUP DATABASE [{databaseName}]");
        commandBuilder.AppendLine($"TO DISK = '{backupPath}'");
        commandBuilder.AppendLine("WITH");
        
        var withOptions = new List<string>();
        
        // Add backup type specific options
        switch (options.BackupType)
        {
            case BackupType.Full:
                withOptions.Add("FORMAT");
                withOptions.Add("INIT");
                break;
            case BackupType.Differential:
                withOptions.Add("DIFFERENTIAL");
                withOptions.Add("FORMAT");
                break;
            case BackupType.TransactionLog:
                commandBuilder.Clear();
                commandBuilder.AppendLine($"BACKUP LOG [{databaseName}]");
                commandBuilder.AppendLine($"TO DISK = '{backupPath}'");
                commandBuilder.AppendLine("WITH");
                break;
        }
        
        // Add compression if supported
        if (options.UseNativeCompression)
        {
            withOptions.Add("COMPRESSION");
        }
        
        // Add checksum for verification
        withOptions.Add("CHECKSUM");
        
        // Add statistics for progress reporting
        withOptions.Add("STATS = 10");
        
        // Add description
        withOptions.Add($"DESCRIPTION = 'ACS Backup - {options.BackupType} - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}'");
        
        // Add copy only if requested
        if (options.CopyOnly)
        {
            withOptions.Add("COPY_ONLY");
        }
        
        commandBuilder.AppendLine(string.Join(",\n", withOptions));
        
        return commandBuilder.ToString();
    }

    private string GenerateRestoreCommand(string databaseName, string backupPath, RestoreOptions options)
    {
        var commandBuilder = new System.Text.StringBuilder();
        
        commandBuilder.AppendLine($"RESTORE DATABASE [{databaseName}]");
        commandBuilder.AppendLine($"FROM DISK = '{backupPath}'");
        commandBuilder.AppendLine("WITH");
        
        var withOptions = new List<string>();
        
        // Add replace option if forcing restore
        if (options.ForceRestore)
        {
            withOptions.Add("REPLACE");
        }
        
        // Add recovery option
        withOptions.Add(options.NoRecovery ? "NORECOVERY" : "RECOVERY");
        
        // Add statistics for progress reporting
        withOptions.Add("STATS = 10");
        
        // Add file relocation if specified
        if (!string.IsNullOrEmpty(options.DataFilePath) || !string.IsNullOrEmpty(options.LogFilePath))
        {
            // Get logical file names from backup
            var logicalNames = GetLogicalFileNamesFromBackup(backupPath).Result;
            
            if (!string.IsNullOrEmpty(options.DataFilePath) && logicalNames.DataFileName != null)
            {
                withOptions.Add($"MOVE '{logicalNames.DataFileName}' TO '{options.DataFilePath}'");
            }
            
            if (!string.IsNullOrEmpty(options.LogFilePath) && logicalNames.LogFileName != null)
            {
                withOptions.Add($"MOVE '{logicalNames.LogFileName}' TO '{options.LogFilePath}'");
            }
        }
        
        commandBuilder.AppendLine(string.Join(",\n", withOptions));
        
        return commandBuilder.ToString();
    }

    private async Task<string> CompressBackupAsync(string backupPath, CancellationToken cancellationToken)
    {
        var compressedPath = $"{backupPath}.gz";
        
        using var originalStream = File.OpenRead(backupPath);
        using var compressedStream = File.Create(compressedPath);
        using var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal);
        
        await originalStream.CopyToAsync(gzipStream, cancellationToken);
        
        _logger.LogInformation("Compressed backup from {OriginalSize} to {CompressedSize}",
            FormatFileSize(originalStream.Length),
            FormatFileSize(compressedStream.Length));
        
        return compressedPath;
    }

    private async Task<string> DecompressBackupAsync(string compressedPath, CancellationToken cancellationToken)
    {
        var decompressedPath = compressedPath.Replace(".gz", "");
        
        using var compressedStream = File.OpenRead(compressedPath);
        using var decompressedStream = File.Create(decompressedPath);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        
        await gzipStream.CopyToAsync(decompressedStream, cancellationToken);
        
        return decompressedPath;
    }

    private async Task SetDatabaseSingleUserModeAsync(string databaseName, bool singleUser, CancellationToken cancellationToken)
    {
        var mode = singleUser ? "SINGLE_USER" : "MULTI_USER";
        var sql = $"ALTER DATABASE [{databaseName}] SET {mode} WITH ROLLBACK IMMEDIATE";
        
        using var connection = new SqlConnection(GetMasterConnectionString());
        await connection.OpenAsync(cancellationToken);
        
        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<(string DataFileName, string LogFileName)> GetLogicalFileNamesFromBackup(string backupPath)
    {
        const string sql = "RESTORE FILELISTONLY FROM DISK = @BackupPath";
        
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BackupPath", backupPath);
        
        using var reader = await command.ExecuteReaderAsync();
        
        string? dataFile = null;
        string? logFile = null;
        
        while (await reader.ReadAsync())
        {
            var logicalName = reader.GetString(0);
            var type = reader.GetString(2);
            
            if (type == "D" && dataFile == null)
                dataFile = logicalName;
            else if (type == "L" && logFile == null)
                logFile = logicalName;
        }
        
        return (dataFile ?? "Data", logFile ?? "Log");
    }

    private async Task LogBackupHistoryAsync(BackupResult result, CancellationToken cancellationToken)
    {
        try
        {
            // Log to a custom backup history table if it exists
            // This is a placeholder for custom logging implementation
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log backup history");
        }
    }

    private string GetDatabaseName()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        return builder.InitialCatalog;
    }

    private string GetMasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }

    private static BackupType GetBackupTypeFromChar(string type)
    {
        return type switch
        {
            "D" => BackupType.Full,
            "I" => BackupType.Differential,
            "L" => BackupType.TransactionLog,
            _ => BackupType.Full
        };
    }

    private static BackupType DetermineBackupTypeFromFileName(string fileName)
    {
        if (fileName.Contains("_Differential_", StringComparison.OrdinalIgnoreCase))
            return BackupType.Differential;
        if (fileName.Contains("_TransactionLog_", StringComparison.OrdinalIgnoreCase))
            return BackupType.TransactionLog;
        return BackupType.Full;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

#region Models

public enum BackupType
{
    Full,
    Differential,
    TransactionLog
}

public class BackupOptions
{
    public BackupType BackupType { get; set; } = BackupType.Full;
    public string BackupPath { get; set; } = string.Empty;
    public bool Compress { get; set; } = true;
    public bool UseNativeCompression { get; set; } = true;
    public bool DeleteUncompressedAfterCompress { get; set; } = true;
    public bool VerifyAfterBackup { get; set; } = true;
    public bool CopyOnly { get; set; } = false;
    public int? TimeoutSeconds { get; set; }
}

public class RestoreOptions
{
    public string BackupPath { get; set; } = string.Empty;
    public string TargetDatabaseName { get; set; } = string.Empty;
    public string DataFilePath { get; set; } = string.Empty;
    public string LogFilePath { get; set; } = string.Empty;
    public bool ForceRestore { get; set; } = false;
    public bool NoRecovery { get; set; } = false;
    public bool VerifyBeforeRestore { get; set; } = true;
    public int? TimeoutSeconds { get; set; }
}

public class BackupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public BackupType BackupType { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string CompressedPath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public long? CompressedSizeBytes { get; set; }
    public bool IsVerified { get; set; }
}

public class RestoreResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public string BackupPath { get; set; } = string.Empty;
    public string RestoredDatabaseName { get; set; } = string.Empty;
}

public class BackupInfo
{
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime BackupStartDate { get; set; }
    public DateTime BackupFinishDate { get; set; }
    public BackupType BackupType { get; set; }
    public long BackupSizeBytes { get; set; }
    public long? CompressedSizeBytes { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
}

public class CleanupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int RetentionDays { get; set; }
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public List<string> DeletedFiles { get; set; } = new();
    public List<string> FailedFiles { get; set; } = new();
}

#endregion