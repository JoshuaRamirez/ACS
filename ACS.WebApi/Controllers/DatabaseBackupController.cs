using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACS.Service.Services;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for database backup and recovery operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator,DatabaseAdmin")]
public class DatabaseBackupController : ControllerBase
{
    private readonly IDatabaseBackupService _backupService;
    private readonly ILogger<DatabaseBackupController> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseBackupController(
        IDatabaseBackupService backupService,
        ILogger<DatabaseBackupController> logger,
        IConfiguration configuration)
    {
        _backupService = backupService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Create a database backup
    /// </summary>
    /// <param name="request">Backup request options</param>
    /// <returns>Backup result</returns>
    [HttpPost("backup")]
    public async Task<IActionResult> CreateBackup([FromBody] CreateBackupRequest request)
    {
        try
        {
            _logger.LogInformation("Manual backup requested. Type: {BackupType}", request.BackupType);

            var options = new BackupOptions
            {
                BackupType = request.BackupType,
                BackupPath = request.BackupPath,
                Compress = request.Compress,
                UseNativeCompression = request.UseNativeCompression,
                DeleteUncompressedAfterCompress = request.DeleteUncompressedAfterCompress,
                VerifyAfterBackup = request.VerifyAfterBackup,
                CopyOnly = request.CopyOnly,
                TimeoutSeconds = request.TimeoutSeconds
            };

            var result = await _backupService.CreateBackupAsync(options);

            if (result.Success)
            {
                return Ok(new
                {
                    result.Success,
                    result.Message,
                    result.BackupType,
                    result.BackupPath,
                    result.CompressedPath,
                    FileSizeBytes = result.FileSizeBytes,
                    FileSizeFormatted = FormatFileSize(result.FileSizeBytes),
                    CompressedSizeBytes = result.CompressedSizeBytes,
                    CompressedSizeFormatted = result.CompressedSizeBytes.HasValue 
                        ? FormatFileSize(result.CompressedSizeBytes.Value) 
                        : null,
                    CompressionRatio = result.CompressedSizeBytes.HasValue 
                        ? $"{(100.0 - (result.CompressedSizeBytes.Value * 100.0 / result.FileSizeBytes)):F1}%" 
                        : null,
                    result.IsVerified,
                    Duration = result.Duration.TotalSeconds,
                    result.StartTime,
                    result.EndTime
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    result.Success,
                    result.Message,
                    result.Error
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup");
            return StatusCode(500, new { error = "Failed to create backup", message = ex.Message });
        }
    }

    /// <summary>
    /// Restore a database from backup
    /// </summary>
    /// <param name="request">Restore request options</param>
    /// <returns>Restore result</returns>
    [HttpPost("restore")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> RestoreBackup([FromBody] RestoreBackupRequest request)
    {
        try
        {
            // Additional safety check for restore operations
            if (!request.ConfirmRestore)
            {
                return BadRequest(new { error = "Restore operation must be confirmed" });
            }

            _logger.LogWarning("Manual database restore requested. Target: {TargetDatabase}", 
                request.TargetDatabaseName ?? "Default");

            var options = new RestoreOptions
            {
                BackupPath = request.BackupPath,
                TargetDatabaseName = request.TargetDatabaseName ?? string.Empty,
                DataFilePath = request.DataFilePath,
                LogFilePath = request.LogFilePath,
                ForceRestore = request.ForceRestore,
                NoRecovery = request.NoRecovery,
                VerifyBeforeRestore = request.VerifyBeforeRestore,
                TimeoutSeconds = request.TimeoutSeconds
            };

            var result = await _backupService.RestoreBackupAsync(options);

            if (result.Success)
            {
                return Ok(new
                {
                    result.Success,
                    result.Message,
                    result.RestoredDatabaseName,
                    Duration = result.Duration.TotalSeconds,
                    result.StartTime,
                    result.EndTime
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    result.Success,
                    result.Message,
                    result.Error
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup");
            return StatusCode(500, new { error = "Failed to restore backup", message = ex.Message });
        }
    }

    /// <summary>
    /// Get backup history
    /// </summary>
    /// <param name="days">Number of days of history to retrieve (default: 30)</param>
    /// <returns>List of backup information</returns>
    [HttpGet("history")]
    public async Task<IActionResult> GetBackupHistory([FromQuery] int days = 30)
    {
        try
        {
            var history = await _backupService.GetBackupHistoryAsync(days);

            return Ok(new
            {
                Days = days,
                Count = history.Count,
                TotalSizeBytes = history.Sum(h => h.BackupSizeBytes),
                TotalSizeFormatted = FormatFileSize(history.Sum(h => h.BackupSizeBytes)),
                Backups = history.Select(h => new
                {
                    h.DatabaseName,
                    h.BackupStartDate,
                    h.BackupFinishDate,
                    Duration = (h.BackupFinishDate - h.BackupStartDate).TotalSeconds,
                    h.BackupType,
                    BackupSizeBytes = h.BackupSizeBytes,
                    BackupSizeFormatted = FormatFileSize(h.BackupSizeBytes),
                    CompressedSizeBytes = h.CompressedSizeBytes,
                    CompressedSizeFormatted = h.CompressedSizeBytes.HasValue 
                        ? FormatFileSize(h.CompressedSizeBytes.Value) 
                        : null,
                    h.BackupPath,
                    h.UserName,
                    h.ServerName
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backup history");
            return StatusCode(500, new { error = "Failed to retrieve backup history", message = ex.Message });
        }
    }

    /// <summary>
    /// Verify a backup file
    /// </summary>
    /// <param name="request">Verify request with backup path</param>
    /// <returns>Verification result</returns>
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyBackup([FromBody] VerifyBackupRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.BackupPath))
            {
                return BadRequest(new { error = "BackupPath is required" });
            }

            _logger.LogInformation("Backup verification requested for: {BackupPath}", request.BackupPath);

            var isValid = await _backupService.VerifyBackupAsync(request.BackupPath);

            return Ok(new
            {
                BackupPath = request.BackupPath,
                IsValid = isValid,
                Message = isValid ? "Backup file is valid" : "Backup file verification failed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying backup");
            return StatusCode(500, new { error = "Failed to verify backup", message = ex.Message });
        }
    }

    /// <summary>
    /// Clean up old backup files
    /// </summary>
    /// <param name="retentionDays">Number of days to retain backups</param>
    /// <returns>Cleanup result</returns>
    [HttpPost("cleanup")]
    public async Task<IActionResult> CleanupOldBackups([FromQuery] int retentionDays = 30)
    {
        try
        {
            if (retentionDays < 1)
            {
                return BadRequest(new { error = "RetentionDays must be at least 1" });
            }

            _logger.LogInformation("Manual backup cleanup requested. Retention: {RetentionDays} days", retentionDays);

            var result = await _backupService.CleanupOldBackupsAsync(retentionDays);

            if (result.Success)
            {
                return Ok(new
                {
                    result.Success,
                    result.Message,
                    result.RetentionDays,
                    result.FilesDeleted,
                    BytesFreed = result.BytesFreed,
                    BytesFreedFormatted = FormatFileSize(result.BytesFreed),
                    result.DeletedFiles,
                    result.FailedFiles,
                    Duration = (result.EndTime - result.StartTime).TotalSeconds
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    result.Success,
                    result.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up backups");
            return StatusCode(500, new { error = "Failed to cleanup backups", message = ex.Message });
        }
    }

    /// <summary>
    /// Get backup configuration
    /// </summary>
    /// <returns>Current backup configuration</returns>
    [HttpGet("configuration")]
    public IActionResult GetBackupConfiguration()
    {
        var config = new
        {
            DefaultPath = _configuration.GetValue<string>("Database:Backup:DefaultPath"),
            Schedule = new
            {
                Enabled = _configuration.GetValue<bool>("Database:Backup:Schedule:Enabled"),
                FullBackup = _configuration.GetSection("Database:Backup:Schedule:FullBackup").Get<object>(),
                DifferentialBackup = _configuration.GetSection("Database:Backup:Schedule:DifferentialBackup").Get<object>(),
                TransactionLogBackup = _configuration.GetSection("Database:Backup:Schedule:TransactionLogBackup").Get<object>(),
                Cleanup = _configuration.GetSection("Database:Backup:Schedule:CleanupSchedule").Get<object>()
            }
        };

        return Ok(config);
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

#region Request Models

public class CreateBackupRequest
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

public class RestoreBackupRequest
{
    public string BackupPath { get; set; } = string.Empty;
    public string TargetDatabaseName { get; set; } = string.Empty;
    public string DataFilePath { get; set; } = string.Empty;
    public string LogFilePath { get; set; } = string.Empty;
    public bool ForceRestore { get; set; } = false;
    public bool NoRecovery { get; set; } = false;
    public bool VerifyBeforeRestore { get; set; } = true;
    public int? TimeoutSeconds { get; set; }
    public bool ConfirmRestore { get; set; } = false; // Safety confirmation
}

public class VerifyBackupRequest
{
    public string BackupPath { get; set; } = string.Empty;
}

#endregion