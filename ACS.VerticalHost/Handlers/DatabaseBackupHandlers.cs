using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(CreateBackupCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { BackupType = command.BackupType, BackupPath = command.BackupPath }, correlationId);

        try
        {
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

            var commandResult = new CommandBackupResult
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

            LogCommandSuccess(_logger, context, 
                new { Success = result.Success, BackupPath = result.BackupPath, FileSizeBytes = result.FileSizeBytes }, 
                correlationId);
                
            return commandResult;
        }
        catch (Exception ex)
        {
            // For backup operations, return error result instead of throwing
            // to provide operational details to the caller
            _logger.LogError(ex, "Error creating backup. CorrelationId: {CorrelationId}", correlationId);
            LogCommandSuccess(_logger, context, new { Success = false, Error = "Backup creation failed" }, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(RestoreBackupCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { BackupPath = command.BackupPath, TargetDatabase = command.TargetDatabaseName }, correlationId);

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

            LogCommandSuccess(_logger, context, 
                new { Success = result.Success, Database = result.RestoredDatabaseName }, correlationId);

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
            // For restore operations, return error result instead of throwing
            // to provide operational details to the caller
            _logger.LogError(ex, "Error restoring backup. CorrelationId: {CorrelationId}", correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(VerifyBackupCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { BackupPath = command.BackupPath }, correlationId);

        try
        {
            if (string.IsNullOrEmpty(command.BackupPath))
            {
                throw new ArgumentException("BackupPath is required");
            }

            var isValid = await _backupService.VerifyBackupAsync(command.BackupPath);
            
            LogCommandSuccess(_logger, context, new { BackupPath = command.BackupPath, IsValid = isValid }, correlationId);
            return isValid;
        }
        catch (Exception ex)
        {
            return HandleCommandError<bool>(_logger, ex, context, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(CleanupOldBackupsCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { RetentionDays = command.RetentionDays }, correlationId);

        try
        {
            if (command.RetentionDays < 1)
            {
                throw new ArgumentException("RetentionDays must be at least 1");
            }

            _logger.LogInformation("Processing backup cleanup. Retention: {RetentionDays} days", command.RetentionDays);

            var result = await _backupService.CleanupOldBackupsAsync(command.RetentionDays);

            LogCommandSuccess(_logger, context, 
                new { Success = result.Success, FilesDeleted = result.FilesDeleted, BytesFreed = result.BytesFreed }, correlationId);

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
            // For cleanup operations, return error result instead of throwing
            // to provide operational details to the caller
            _logger.LogError(ex, "Error cleaning up backups. CorrelationId: {CorrelationId}", correlationId);
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
            var correlationId = GetCorrelationId();
            var context = GetContext(nameof(GetBackupHistoryQueryHandler), nameof(HandleAsync));
            return HandleQueryError<List<BackupHistoryInfo>>(_logger, ex, context, correlationId);
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