using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace ACS.Service.Services;

/// <summary>
/// Background service for scheduled database backups
/// </summary>
public class ScheduledBackupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ScheduledBackupService> _logger;
    private readonly BackupScheduleOptions _scheduleOptions;
    private readonly List<Timer> _backupTimers = new();

    public ScheduledBackupService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ScheduledBackupService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        
        _scheduleOptions = new BackupScheduleOptions();
        configuration.GetSection("Database:Backup:Schedule").Bind(_scheduleOptions);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_scheduleOptions.Enabled)
        {
            _logger.LogInformation("Scheduled backup service is disabled");
            return;
        }

        _logger.LogInformation("Scheduled backup service started");

        try
        {
            // Schedule full backups
            if (_scheduleOptions.FullBackup != null && _scheduleOptions.FullBackup.Enabled)
            {
                ScheduleBackup(_scheduleOptions.FullBackup, BackupType.Full, stoppingToken);
            }

            // Schedule differential backups
            if (_scheduleOptions.DifferentialBackup != null && _scheduleOptions.DifferentialBackup.Enabled)
            {
                ScheduleBackup(_scheduleOptions.DifferentialBackup, BackupType.Differential, stoppingToken);
            }

            // Schedule transaction log backups
            if (_scheduleOptions.TransactionLogBackup != null && _scheduleOptions.TransactionLogBackup.Enabled)
            {
                ScheduleBackup(_scheduleOptions.TransactionLogBackup, BackupType.TransactionLog, stoppingToken);
            }

            // Schedule cleanup
            if (_scheduleOptions.CleanupSchedule != null && _scheduleOptions.CleanupSchedule.Enabled)
            {
                ScheduleCleanup(_scheduleOptions.CleanupSchedule, stoppingToken);
            }

            // Keep the service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled backup service encountered an error");
        }
        finally
        {
            // Dispose all timers
            foreach (var timer in _backupTimers)
            {
                timer?.Dispose();
            }
            
            _logger.LogInformation("Scheduled backup service stopped");
        }
    }

    private void ScheduleBackup(BackupSchedule schedule, BackupType backupType, CancellationToken cancellationToken)
    {
        try
        {
            var nextRunTime = CalculateNextRunTime(schedule);
            var delay = nextRunTime - DateTime.UtcNow;

            if (delay < TimeSpan.Zero)
            {
                // If the calculated time is in the past, schedule for the next occurrence
                nextRunTime = CalculateNextRunTime(schedule, nextRunTime.AddDays(1));
                delay = nextRunTime - DateTime.UtcNow;
            }

            _logger.LogInformation(
                "Scheduling {BackupType} backup. First run at: {NextRunTime} (in {Delay})",
                backupType, nextRunTime, delay);

            var timer = new Timer(
                async _ => await ExecuteBackupAsync(backupType, schedule, cancellationToken),
                null,
                delay,
                GetInterval(schedule));

            _backupTimers.Add(timer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling {BackupType} backup", backupType);
        }
    }

    private void ScheduleCleanup(CleanupSchedule schedule, CancellationToken cancellationToken)
    {
        try
        {
            var nextRunTime = CalculateNextRunTime(schedule);
            var delay = nextRunTime - DateTime.UtcNow;

            if (delay < TimeSpan.Zero)
            {
                nextRunTime = CalculateNextRunTime(schedule, nextRunTime.AddDays(1));
                delay = nextRunTime - DateTime.UtcNow;
            }

            _logger.LogInformation(
                "Scheduling backup cleanup. First run at: {NextRunTime} (in {Delay})",
                nextRunTime, delay);

            var timer = new Timer(
                async _ => await ExecuteCleanupAsync(schedule, cancellationToken),
                null,
                delay,
                GetInterval(schedule));

            _backupTimers.Add(timer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling backup cleanup");
        }
    }

    private async Task ExecuteBackupAsync(BackupType backupType, BackupSchedule schedule, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting scheduled {BackupType} backup", backupType);

            using var scope = _serviceProvider.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
            var alertingService = scope.ServiceProvider.GetService<IAlertingService>();

            var options = new BackupOptions
            {
                BackupType = backupType,
                BackupPath = schedule.BackupPath,
                Compress = schedule.Compress,
                UseNativeCompression = schedule.UseNativeCompression,
                VerifyAfterBackup = schedule.VerifyAfterBackup,
                TimeoutSeconds = schedule.TimeoutSeconds
            };

            var result = await backupService.CreateBackupAsync(options, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Scheduled {BackupType} backup completed successfully. Size: {Size}",
                    backupType, FormatFileSize(result.FileSizeBytes));

                // Send success alert if configured
                if (schedule.SendSuccessAlerts && alertingService != null)
                {
                    await alertingService.RaiseAlertAsync(new Alert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = $"Database Backup Successful",
                        Message = $"{backupType} backup completed successfully. Size: {FormatFileSize(result.FileSizeBytes)}",
                        Severity = AlertSeverity.Info,
                        Category = AlertCategory.Database,
                        Source = "ScheduledBackupService",
                        CreatedAt = DateTime.UtcNow,
                        Metadata = new Dictionary<string, string>
                        {
                            ["BackupType"] = backupType.ToString(),
                            ["BackupPath"] = result.BackupPath,
                            ["Size"] = result.FileSizeBytes.ToString(),
                            ["Duration"] = result.Duration.TotalSeconds.ToString("F2")
                        }
                    });
                }
            }
            else
            {
                _logger.LogError("Scheduled {BackupType} backup failed: {Message}", backupType, result.Message);

                // Send failure alert
                if (alertingService != null)
                {
                    await alertingService.RaiseAlertAsync(new Alert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = $"Database Backup Failed",
                        Message = $"{backupType} backup failed: {result.Message}",
                        Severity = AlertSeverity.Critical,
                        Category = AlertCategory.Database,
                        Source = "ScheduledBackupService",
                        CreatedAt = DateTime.UtcNow,
                        Metadata = new Dictionary<string, string>
                        {
                            ["BackupType"] = backupType.ToString(),
                            ["Error"] = result.Error ?? result.Message
                        }
                    });
                }

                // Retry if configured
                if (schedule.RetryOnFailure && schedule.RetryCount > 0)
                {
                    await RetryBackupAsync(backupType, schedule, 0, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled {BackupType} backup", backupType);
        }
    }

    private async Task RetryBackupAsync(BackupType backupType, BackupSchedule schedule, int attemptNumber, CancellationToken cancellationToken)
    {
        if (attemptNumber >= schedule.RetryCount)
        {
            _logger.LogError("Maximum retry attempts reached for {BackupType} backup", backupType);
            return;
        }

        var delay = TimeSpan.FromMinutes(schedule.RetryDelayMinutes * Math.Pow(2, attemptNumber)); // Exponential backoff
        _logger.LogInformation("Retrying {BackupType} backup in {Delay} (attempt {Attempt}/{Max})",
            backupType, delay, attemptNumber + 1, schedule.RetryCount);

        await Task.Delay(delay, cancellationToken);

        using var scope = _serviceProvider.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();

        var options = new BackupOptions
        {
            BackupType = backupType,
            BackupPath = schedule.BackupPath,
            Compress = schedule.Compress,
            UseNativeCompression = schedule.UseNativeCompression,
            VerifyAfterBackup = schedule.VerifyAfterBackup,
            TimeoutSeconds = schedule.TimeoutSeconds
        };

        var result = await backupService.CreateBackupAsync(options, cancellationToken);

        if (!result.Success)
        {
            await RetryBackupAsync(backupType, schedule, attemptNumber + 1, cancellationToken);
        }
    }

    private async Task ExecuteCleanupAsync(CleanupSchedule schedule, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting scheduled backup cleanup. Retention: {RetentionDays} days",
                schedule.RetentionDays);

            using var scope = _serviceProvider.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();

            var result = await backupService.CleanupOldBackupsAsync(schedule.RetentionDays, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Scheduled cleanup completed. Deleted {FilesDeleted} files, freed {BytesFreed}",
                    result.FilesDeleted, FormatFileSize(result.BytesFreed));
            }
            else
            {
                _logger.LogError("Scheduled cleanup failed: {Message}", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled cleanup");
        }
    }

    private DateTime CalculateNextRunTime(IScheduleBase schedule, DateTime? baseTime = null)
    {
        var now = baseTime ?? DateTime.UtcNow;

        switch (schedule.ScheduleType)
        {
            case ScheduleType.Daily:
                if (!string.IsNullOrEmpty(schedule.DailyAt))
                {
                    if (TimeSpan.TryParse(schedule.DailyAt, out var timeOfDay))
                    {
                        var nextRun = now.Date.Add(timeOfDay);
                        if (nextRun <= now)
                        {
                            nextRun = nextRun.AddDays(1);
                        }
                        return nextRun;
                    }
                }
                break;

            case ScheduleType.Weekly:
                if (!string.IsNullOrEmpty(schedule.WeeklyOn) && !string.IsNullOrEmpty(schedule.WeeklyAt))
                {
                    if (Enum.TryParse<DayOfWeek>(schedule.WeeklyOn, out var dayOfWeek) &&
                        TimeSpan.TryParse(schedule.WeeklyAt, out var timeOfDay))
                    {
                        var daysUntilTarget = ((int)dayOfWeek - (int)now.DayOfWeek + 7) % 7;
                        if (daysUntilTarget == 0 && now.TimeOfDay > timeOfDay)
                        {
                            daysUntilTarget = 7;
                        }
                        return now.Date.AddDays(daysUntilTarget).Add(timeOfDay);
                    }
                }
                break;

            case ScheduleType.Interval:
                if (schedule.IntervalHours > 0)
                {
                    return now.AddHours(schedule.IntervalHours);
                }
                break;
        }

        // Default to daily at 2 AM if parsing fails
        return now.Date.AddDays(1).AddHours(2);
    }

    private TimeSpan GetInterval(IScheduleBase schedule)
    {
        return schedule.ScheduleType switch
        {
            ScheduleType.Daily => TimeSpan.FromDays(1),
            ScheduleType.Weekly => TimeSpan.FromDays(7),
            ScheduleType.Interval when schedule.IntervalHours > 0 => TimeSpan.FromHours(schedule.IntervalHours),
            _ => TimeSpan.FromDays(1)
        };
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

#region Configuration Models

public enum ScheduleType
{
    Daily,
    Weekly,
    Interval
}

public interface IScheduleBase
{
    bool Enabled { get; set; }
    ScheduleType ScheduleType { get; set; }
    string DailyAt { get; set; }
    string WeeklyOn { get; set; }
    string WeeklyAt { get; set; }
    int IntervalHours { get; set; }
}

public class BackupSchedule : IScheduleBase
{
    public bool Enabled { get; set; } = true;
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;
    public string DailyAt { get; set; } = "02:00:00";
    public string WeeklyOn { get; set; } = "Sunday";
    public string WeeklyAt { get; set; } = "02:00:00";
    public int IntervalHours { get; set; } = 24;
    public string BackupPath { get; set; }
    public bool Compress { get; set; } = true;
    public bool UseNativeCompression { get; set; } = true;
    public bool VerifyAfterBackup { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 3600;
    public bool RetryOnFailure { get; set; } = true;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMinutes { get; set; } = 5;
    public bool SendSuccessAlerts { get; set; } = false;
}

public class CleanupSchedule : IScheduleBase
{
    public bool Enabled { get; set; } = true;
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;
    public string DailyAt { get; set; } = "03:00:00";
    public string WeeklyOn { get; set; } = "Sunday";
    public string WeeklyAt { get; set; } = "03:00:00";
    public int IntervalHours { get; set; } = 24;
    public int RetentionDays { get; set; } = 30;
}

public class BackupScheduleOptions
{
    public bool Enabled { get; set; } = true;
    public BackupSchedule FullBackup { get; set; }
    public BackupSchedule DifferentialBackup { get; set; }
    public BackupSchedule TransactionLogBackup { get; set; }
    public CleanupSchedule CleanupSchedule { get; set; }
}

#endregion