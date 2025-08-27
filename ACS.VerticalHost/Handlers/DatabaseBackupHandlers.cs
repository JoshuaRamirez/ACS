using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServiceBackupResult = ACS.Service.Services.BackupResult;
using ServiceBackupType = ACS.Service.Services.BackupType;
using CommandBackupType = ACS.VerticalHost.Commands.BackupType;
using CommandBackupResult = ACS.VerticalHost.Commands.BackupResult;
using ServiceRestoreResult = ACS.Service.Services.RestoreResult;
using ServiceCleanupResult = ACS.Service.Services.CleanupResult;
using CommandRestoreResult = ACS.VerticalHost.Commands.RestoreResult;
using CommandCleanupResult = ACS.VerticalHost.Commands.CleanupResult;

namespace ACS.VerticalHost.Handlers;

public class CreateBackupCommandHandler : ICommandHandler<CreateBackupCommand, CommandBackupResult>
{
    private readonly IDatabaseBackupService _backupService;
    private readonly ILogger<CreateBackupCommandHandler> _logger;

    public CreateBackupCommandHandler(
        IDatabaseBackupService backupService,
        ILogger<CreateBackupCommandHandler> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    public async Task<CommandBackupResult> HandleAsync(CreateBackupCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing backup request. Type: {BackupType}", command.BackupType);

            var options = new BackupOptions
            {
                BackupType = (ServiceBackupType)command.BackupType,
                BackupPath = command.BackupPath,
                Compress = command.Compress,
                UseNativeCompression = command.UseNativeCompression,
                DeleteUncompressedAfterCompress = command.DeleteUncompressedAfterCompress,
                VerifyAfterBackup = command.VerifyAfterBackup,
                CopyOnly = command.CopyOnly,
                TimeoutSeconds = command.TimeoutSeconds
            };

            var result = await _backupService.CreateBackupAsync(options);

            return new CommandBackupResult
            {
                Success = result.Success,
                Message = result.Message,
                Error = result.Error,
                BackupType = (CommandBackupType)result.BackupType,
                BackupPath = result.BackupPath,
                CompressedPath = result.CompressedPath,
                FileSizeBytes = result.FileSizeBytes,
                CompressedSizeBytes = result.CompressedSizeBytes,
                IsVerified = result.IsVerified,
                Duration = result.Duration,
                StartTime = result.StartTime,
                EndTime = result.EndTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup");
            return new CommandBackupResult
            {
                Success = false,
                Error = ex.Message,
                Message = "Failed to create backup"
            };
        }
    }
}

public class RestoreBackupCommandHandler : ICommandHandler<RestoreBackupCommand, CommandRestoreResult>
{
    private readonly IDatabaseBackupService _backupService;
    private readonly ILogger<RestoreBackupCommandHandler> _logger;

    public RestoreBackupCommandHandler(
        IDatabaseBackupService backupService,
        ILogger<RestoreBackupCommandHandler> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    public async Task<CommandRestoreResult> HandleAsync(RestoreBackupCommand command, CancellationToken cancellationToken)
    {
        try
        {
            if (!command.ConfirmRestore)
            {
                return new CommandRestoreResult
                {
                    Success = false,
                    Error = "Restore operation must be confirmed",
                    Message = "Restore confirmation required"
                };
            }

            _logger.LogWarning("Processing database restore request. Target: {TargetDatabase}", 
                command.TargetDatabaseName ?? "Default");

            var options = new RestoreOptions
            {
                BackupPath = command.BackupPath,
                TargetDatabaseName = command.TargetDatabaseName ?? string.Empty,
                DataFilePath = command.DataFilePath,
                LogFilePath = command.LogFilePath,
                ForceRestore = command.ForceRestore,
                NoRecovery = command.NoRecovery,
                VerifyBeforeRestore = command.VerifyBeforeRestore,
                TimeoutSeconds = command.TimeoutSeconds
            };

            var result = await _backupService.RestoreBackupAsync(options);

            return new CommandRestoreResult
            {
                Success = result.Success,
                Message = result.Message,
                Error = result.Error,
                RestoredDatabaseName = result.RestoredDatabaseName,
                Duration = result.Duration,
                StartTime = result.StartTime,
                EndTime = result.EndTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup");
            return new CommandRestoreResult
            {
                Success = false,
                Error = ex.Message,
                Message = "Failed to restore backup"
            };
        }
    }
}

public class VerifyBackupCommandHandler : ICommandHandler<VerifyBackupCommand, bool>
{
    private readonly IDatabaseBackupService _backupService;
    private readonly ILogger<VerifyBackupCommandHandler> _logger;

    public VerifyBackupCommandHandler(
        IDatabaseBackupService backupService,
        ILogger<VerifyBackupCommandHandler> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(VerifyBackupCommand command, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(command.BackupPath))
            {
                throw new ArgumentException("BackupPath is required");
            }

            _logger.LogInformation("Verifying backup: {BackupPath}", command.BackupPath);

            var isValid = await _backupService.VerifyBackupAsync(command.BackupPath);
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying backup");
            throw;
        }
    }
}

public class CleanupOldBackupsCommandHandler : ICommandHandler<CleanupOldBackupsCommand, CommandCleanupResult>
{
    private readonly IDatabaseBackupService _backupService;
    private readonly ILogger<CleanupOldBackupsCommandHandler> _logger;

    public CleanupOldBackupsCommandHandler(
        IDatabaseBackupService backupService,
        ILogger<CleanupOldBackupsCommandHandler> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    public async Task<CommandCleanupResult> HandleAsync(CleanupOldBackupsCommand command, CancellationToken cancellationToken)
    {
        try
        {
            if (command.RetentionDays < 1)
            {
                throw new ArgumentException("RetentionDays must be at least 1");
            }

            _logger.LogInformation("Processing backup cleanup. Retention: {RetentionDays} days", command.RetentionDays);

            var result = await _backupService.CleanupOldBackupsAsync(command.RetentionDays);

            return new CommandCleanupResult
            {
                Success = result.Success,
                Message = result.Message,
                RetentionDays = result.RetentionDays,
                FilesDeleted = result.FilesDeleted,
                BytesFreed = result.BytesFreed,
                DeletedFiles = result.DeletedFiles,
                FailedFiles = result.FailedFiles,
                StartTime = result.StartTime,
                EndTime = result.EndTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up backups");
            return new CommandCleanupResult
            {
                Success = false,
                Message = $"Failed to cleanup backups: {ex.Message}",
                RetentionDays = command.RetentionDays
            };
        }
    }
}

public class GetBackupHistoryQueryHandler : IQueryHandler<GetBackupHistoryQuery, List<BackupHistoryInfo>>
{
    private readonly IDatabaseBackupService _backupService;
    private readonly ILogger<GetBackupHistoryQueryHandler> _logger;

    public GetBackupHistoryQueryHandler(
        IDatabaseBackupService backupService,
        ILogger<GetBackupHistoryQueryHandler> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    public async Task<List<BackupHistoryInfo>> HandleAsync(GetBackupHistoryQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var history = await _backupService.GetBackupHistoryAsync(query.Days);

            return history.Select(h => new Commands.BackupHistoryInfo
            {
                DatabaseName = h.DatabaseName,
                BackupStartDate = h.BackupStartDate,
                BackupFinishDate = h.BackupFinishDate,
                BackupType = (CommandBackupType)h.BackupType,
                BackupSizeBytes = h.BackupSizeBytes,
                CompressedSizeBytes = h.CompressedSizeBytes,
                BackupPath = h.BackupPath,
                UserName = h.UserName,
                ServerName = h.ServerName
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backup history");
            throw;
        }
    }
}

public class GetBackupConfigurationQueryHandler : IQueryHandler<GetBackupConfigurationQuery, BackupConfiguration>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GetBackupConfigurationQueryHandler> _logger;

    public GetBackupConfigurationQueryHandler(
        IConfiguration configuration,
        ILogger<GetBackupConfigurationQueryHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BackupConfiguration> HandleAsync(GetBackupConfigurationQuery query, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // For async signature

        return new Commands.BackupConfiguration
        {
            DefaultPath = _configuration.GetValue<string>("Database:Backup:DefaultPath") ?? string.Empty,
            Schedule = new Commands.BackupScheduleConfiguration
            {
                Enabled = _configuration.GetValue<bool>("Database:Backup:Schedule:Enabled"),
                FullBackup = _configuration.GetSection("Database:Backup:Schedule:FullBackup").Get<object>(),
                DifferentialBackup = _configuration.GetSection("Database:Backup:Schedule:DifferentialBackup").Get<object>(),
                TransactionLogBackup = _configuration.GetSection("Database:Backup:Schedule:TransactionLogBackup").Get<object>(),
                Cleanup = _configuration.GetSection("Database:Backup:Schedule:CleanupSchedule").Get<object>()
            }
        };
    }
}