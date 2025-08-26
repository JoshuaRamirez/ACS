using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ACS.Service.Infrastructure;
using ACS.Alerting;

namespace ACS.Service.Services;

/// <summary>
/// Background service for scheduled data archiving and purging operations
/// </summary>
public class ScheduledArchivingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ScheduledArchivingService> _logger;
    private readonly ArchivingScheduleOptions _scheduleOptions;
    private readonly List<Timer> _timers = new();

    public ScheduledArchivingService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ScheduledArchivingService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        
        _scheduleOptions = new ArchivingScheduleOptions();
        configuration.GetSection("DataArchiving:Schedule").Bind(_scheduleOptions);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_scheduleOptions.Enabled)
        {
            _logger.LogInformation("Scheduled archiving service is disabled");
            return;
        }

        _logger.LogInformation("Scheduled archiving service started");

        try
        {
            // Schedule archiving tasks
            if (_scheduleOptions.ArchiveSchedule != null && _scheduleOptions.ArchiveSchedule.Enabled)
            {
                ScheduleArchiving(_scheduleOptions.ArchiveSchedule, stoppingToken);
            }

            // Schedule purge tasks
            if (_scheduleOptions.PurgeSchedule != null && _scheduleOptions.PurgeSchedule.Enabled)
            {
                SchedulePurge(_scheduleOptions.PurgeSchedule, stoppingToken);
            }

            // Schedule compliance reports
            if (_scheduleOptions.ComplianceReportSchedule != null && _scheduleOptions.ComplianceReportSchedule.Enabled)
            {
                ScheduleComplianceReport(_scheduleOptions.ComplianceReportSchedule, stoppingToken);
            }

            // Schedule retention checks
            if (_scheduleOptions.RetentionCheckSchedule != null && _scheduleOptions.RetentionCheckSchedule.Enabled)
            {
                ScheduleRetentionCheck(_scheduleOptions.RetentionCheckSchedule, stoppingToken);
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
            _logger.LogError(ex, "Scheduled archiving service encountered an error");
        }
        finally
        {
            // Dispose all timers
            foreach (var timer in _timers)
            {
                timer?.Dispose();
            }
            
            _logger.LogInformation("Scheduled archiving service stopped");
        }
    }

    private void ScheduleArchiving(ArchiveSchedule schedule, CancellationToken cancellationToken)
    {
        var nextRunTime = CalculateNextRunTime(schedule);
        var delay = nextRunTime - DateTime.UtcNow;

        if (delay < TimeSpan.Zero)
        {
            nextRunTime = CalculateNextRunTime(schedule, nextRunTime.AddDays(1));
            delay = nextRunTime - DateTime.UtcNow;
        }

        _logger.LogInformation(
            "Scheduling data archiving. First run at: {NextRunTime} (in {Delay})",
            nextRunTime, delay);

        var timer = new Timer(
            async _ => await ExecuteArchivingAsync(schedule, cancellationToken),
            null,
            delay,
            GetInterval(schedule));

        _timers.Add(timer);
    }

    private void SchedulePurge(PurgeSchedule schedule, CancellationToken cancellationToken)
    {
        var nextRunTime = CalculateNextRunTime(schedule);
        var delay = nextRunTime - DateTime.UtcNow;

        if (delay < TimeSpan.Zero)
        {
            nextRunTime = CalculateNextRunTime(schedule, nextRunTime.AddDays(1));
            delay = nextRunTime - DateTime.UtcNow;
        }

        _logger.LogInformation(
            "Scheduling data purge. First run at: {NextRunTime} (in {Delay})",
            nextRunTime, delay);

        var timer = new Timer(
            async _ => await ExecutePurgeAsync(schedule, cancellationToken),
            null,
            delay,
            GetInterval(schedule));

        _timers.Add(timer);
    }

    private void ScheduleComplianceReport(ComplianceReportSchedule schedule, CancellationToken cancellationToken)
    {
        var nextRunTime = CalculateNextRunTime(schedule);
        var delay = nextRunTime - DateTime.UtcNow;

        if (delay < TimeSpan.Zero)
        {
            nextRunTime = CalculateNextRunTime(schedule, nextRunTime.AddDays(1));
            delay = nextRunTime - DateTime.UtcNow;
        }

        _logger.LogInformation(
            "Scheduling compliance report generation. First run at: {NextRunTime} (in {Delay})",
            nextRunTime, delay);

        var timer = new Timer(
            async _ => await GenerateComplianceReportAsync(schedule, cancellationToken),
            null,
            delay,
            GetInterval(schedule));

        _timers.Add(timer);
    }

    private void ScheduleRetentionCheck(RetentionCheckSchedule schedule, CancellationToken cancellationToken)
    {
        var nextRunTime = CalculateNextRunTime(schedule);
        var delay = nextRunTime - DateTime.UtcNow;

        if (delay < TimeSpan.Zero)
        {
            nextRunTime = CalculateNextRunTime(schedule, nextRunTime.AddDays(1));
            delay = nextRunTime - DateTime.UtcNow;
        }

        _logger.LogInformation(
            "Scheduling retention check. First run at: {NextRunTime} (in {Delay})",
            nextRunTime, delay);

        var timer = new Timer(
            async _ => await CheckRetentionAsync(schedule, cancellationToken),
            null,
            delay,
            GetInterval(schedule));

        _timers.Add(timer);
    }

    private async Task ExecuteArchivingAsync(ArchiveSchedule schedule, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting scheduled data archiving");

            using var scope = _serviceProvider.CreateScope();
            var archivingService = scope.ServiceProvider.GetRequiredService<IDataArchivingService>();
            var alertingService = scope.ServiceProvider.GetService<IAlertingService>();

            var cutoffDate = DateTime.UtcNow.AddDays(-schedule.RetentionDays);
            
            var options = new ArchiveOptions
            {
                ArchiveType = schedule.ArchiveType,
                CutoffDate = cutoffDate,
                CompressArchive = schedule.CompressArchive,
                DeleteAfterArchive = schedule.DeleteAfterArchive,
                VerifyArchive = schedule.VerifyArchive,
                TablesToArchive = schedule.TablesToArchive
            };

            var result = await archivingService.ArchiveDataAsync(options, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Scheduled archiving completed. Records: {Records}, Size: {Size}",
                    result.RecordsArchived, FormatFileSize(result.ArchiveSize));

                // Send success notification if configured
                if (schedule.SendNotifications && alertingService != null)
                {
                    await alertingService.SendAlertAsync(new AlertRequest
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = "Data Archiving Successful",
                        Message = $"Archived {result.RecordsArchived} records. Archive size: {FormatFileSize(result.ArchiveSize)}",
                        Severity = AlertSeverity.Info,
                        Category = AlertCategory.Database,
                        Source = "ScheduledArchivingService",
                        Timestamp = DateTime.UtcNow,
                        Metadata = new Dictionary<string, object>
                        {
                            ["ArchiveId"] = result.ArchiveId,
                            ["RecordsArchived"] = result.RecordsArchived.ToString(),
                            ["ArchiveSize"] = result.ArchiveSize.ToString()
                        }
                    });
                }
            }
            else
            {
                _logger.LogError("Scheduled archiving failed: {Message}", result.Message);

                // Send failure alert
                if (alertingService != null)
                {
                    await alertingService.SendAlertAsync(new AlertRequest
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = "Data Archiving Failed",
                        Message = $"Archiving failed: {result.Message}",
                        Severity = AlertSeverity.Warning,
                        Category = AlertCategory.Database,
                        Source = "ScheduledArchivingService",
                        Timestamp = DateTime.UtcNow,
                        Metadata = new Dictionary<string, object>
                        {
                            ["Error"] = result.Error ?? result.Message
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled archiving");
        }
    }

    private async Task ExecutePurgeAsync(PurgeSchedule schedule, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting scheduled data purge");

            using var scope = _serviceProvider.CreateScope();
            var archivingService = scope.ServiceProvider.GetRequiredService<IDataArchivingService>();
            var alertingService = scope.ServiceProvider.GetService<IAlertingService>();

            var options = new PurgeOptions
            {
                RetentionDays = schedule.RetentionDays,
                TablesToPurge = schedule.TablesToPurge,
                CreateBackupBeforePurge = schedule.CreateBackupBeforePurge,
                ArchiveBeforePurge = schedule.ArchiveBeforePurge,
                UpdateStatisticsAfterPurge = schedule.UpdateStatisticsAfterPurge,
                BatchSize = schedule.BatchSize
            };

            var result = await archivingService.PurgeExpiredDataAsync(options, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Scheduled purge completed. Records purged: {Records}",
                    result.RecordsPurged);

                // Send notification if significant data was purged
                if (result.RecordsPurged > 0 && alertingService != null)
                {
                    await alertingService.SendAlertAsync(new AlertRequest
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = "Data Purge Completed",
                        Message = $"Purged {result.RecordsPurged} expired records from {result.TablesPurged.Count} tables",
                        Severity = AlertSeverity.Info,
                        Category = AlertCategory.Database,
                        Source = "ScheduledArchivingService",
                        Timestamp = DateTime.UtcNow,
                        Metadata = new Dictionary<string, object>
                        {
                            ["RecordsPurged"] = result.RecordsPurged.ToString(),
                            ["Tables"] = string.Join(", ", result.TablesPurged),
                            ["ArchiveId"] = result.ArchiveId ?? "N/A"
                        }
                    });
                }
            }
            else
            {
                _logger.LogError("Scheduled purge failed: {Message}", result.Message);

                // Send failure alert
                if (alertingService != null)
                {
                    await alertingService.SendAlertAsync(new AlertRequest
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = "Data Purge Failed",
                        Message = $"Purge failed: {result.Message}",
                        Severity = AlertSeverity.Warning,
                        Category = AlertCategory.Database,
                        Source = "ScheduledArchivingService",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled purge");
        }
    }

    private async Task GenerateComplianceReportAsync(ComplianceReportSchedule schedule, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting scheduled compliance report generation");

            using var scope = _serviceProvider.CreateScope();
            var archivingService = scope.ServiceProvider.GetRequiredService<IDataArchivingService>();

            var options = new ComplianceReportOptions
            {
                ComplianceType = schedule.ComplianceType,
                ReportPeriod = schedule.ReportPeriod,
                FromDate = GetReportFromDate(schedule.ReportPeriod),
                ToDate = DateTime.UtcNow,
                ExportFormat = schedule.ExportFormat,
                IncludeDetails = schedule.IncludeDetails
            };

            var report = await archivingService.GenerateComplianceReportAsync(options, cancellationToken);

            _logger.LogInformation(
                "Compliance report generated. Score: {Score}%, Violations: {Violations}",
                report.ComplianceScore, report.Violations.Count);

            // Send report via email if configured
            if (schedule.EmailRecipients?.Any() == true)
            {
                await SendComplianceReportEmailAsync(report, schedule.EmailRecipients, cancellationToken);
            }

            // Alert on violations
            if (report.Violations.Any())
            {
                var alertingService = scope.ServiceProvider.GetService<IAlertingService>();
                if (alertingService != null)
                {
                    await alertingService.SendAlertAsync(new AlertRequest
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = "Compliance Violations Detected",
                        Message = $"Found {report.Violations.Count} compliance violations. Score: {report.ComplianceScore}%",
                        Severity = AlertSeverity.Warning,
                        Category = AlertCategory.Compliance,
                        Source = "ScheduledArchivingService",
                        Timestamp = DateTime.UtcNow,
                        Metadata = new Dictionary<string, object>
                        {
                            ["ReportId"] = report.ReportId,
                            ["ComplianceType"] = report.ComplianceType.ToString(),
                            ["Score"] = report.ComplianceScore.ToString("F1"),
                            ["Violations"] = report.Violations.Count.ToString()
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating scheduled compliance report");
        }
    }

    private async Task CheckRetentionAsync(RetentionCheckSchedule schedule, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting scheduled retention check");

            using var scope = _serviceProvider.CreateScope();
            var archivingService = scope.ServiceProvider.GetRequiredService<IDataArchivingService>();
            var alertingService = scope.ServiceProvider.GetService<IAlertingService>();

            var status = await archivingService.GetDataRetentionStatusAsync(cancellationToken);

            _logger.LogInformation(
                "Retention check completed. Compliant: {IsCompliant}, Tables exceeding: {Count}",
                status.IsCompliant, status.TablesExceedingRetention.Count);

            // Alert if non-compliant
            if (!status.IsCompliant && alertingService != null)
            {
                var severity = status.RecordsExceedingRetention > schedule.CriticalThreshold 
                    ? AlertSeverity.Critical 
                    : AlertSeverity.Warning;

                await alertingService.SendAlertAsync(new AlertRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Data Retention Non-Compliance",
                    Message = $"{status.RecordsExceedingRetention} records exceed retention policy in {status.TablesExceedingRetention.Count} tables",
                    Severity = severity,
                    Category = AlertCategory.Compliance,
                    Source = "ScheduledArchivingService",
                    Timestamp = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        ["RecordsExceeding"] = status.RecordsExceedingRetention,
                        ["Tables"] = status.TablesExceedingRetention
                    }
                });

                // Auto-remediate if configured
                if (schedule.AutoRemediate && status.RecordsExceedingRetention > 0)
                {
                    _logger.LogWarning("Auto-remediating retention violations");
                    
                    var purgeOptions = new PurgeOptions
                    {
                        RetentionDays = schedule.DefaultRetentionDays,
                        TablesToPurge = status.TablesExceedingRetention,
                        ArchiveBeforePurge = true,
                        CreateBackupBeforePurge = true
                    };

                    var purgeResult = await archivingService.PurgeExpiredDataAsync(purgeOptions, cancellationToken);
                    
                    _logger.LogInformation(
                        "Auto-remediation completed. Purged: {Records}",
                        purgeResult.RecordsPurged);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled retention check");
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

            case ScheduleType.Monthly:
                if (schedule.MonthlyDay > 0 && !string.IsNullOrEmpty(schedule.MonthlyAt))
                {
                    if (TimeSpan.TryParse(schedule.MonthlyAt, out var timeOfDay))
                    {
                        var nextRun = new DateTime(now.Year, now.Month, Math.Min(schedule.MonthlyDay, DateTime.DaysInMonth(now.Year, now.Month)));
                        nextRun = nextRun.Add(timeOfDay);
                        
                        if (nextRun <= now)
                        {
                            nextRun = nextRun.AddMonths(1);
                            var daysInNextMonth = DateTime.DaysInMonth(nextRun.Year, nextRun.Month);
                            if (schedule.MonthlyDay > daysInNextMonth)
                            {
                                nextRun = new DateTime(nextRun.Year, nextRun.Month, daysInNextMonth).Add(timeOfDay);
                            }
                        }
                        return nextRun;
                    }
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
            ScheduleType.Monthly => TimeSpan.FromDays(30), // Approximate
            _ => TimeSpan.FromDays(1)
        };
    }

    private DateTime GetReportFromDate(string reportPeriod)
    {
        return reportPeriod?.ToLower() switch
        {
            "daily" => DateTime.UtcNow.AddDays(-1),
            "weekly" => DateTime.UtcNow.AddDays(-7),
            "monthly" => DateTime.UtcNow.AddMonths(-1),
            "quarterly" => DateTime.UtcNow.AddMonths(-3),
            "yearly" => DateTime.UtcNow.AddYears(-1),
            _ => DateTime.UtcNow.AddMonths(-1)
        };
    }

    private async Task SendComplianceReportEmailAsync(ComplianceReport report, List<string> recipients, CancellationToken cancellationToken)
    {
        // Implementation would send email with report
        await Task.CompletedTask;
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


public class ArchiveSchedule : IScheduleBase
{
    public bool Enabled { get; set; } = true;
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Weekly;
    public string DailyAt { get; set; } = "02:00:00";
    public string WeeklyOn { get; set; } = "Sunday";
    public string WeeklyAt { get; set; } = "02:00:00";
    public int MonthlyDay { get; set; } = 1;
    public string MonthlyAt { get; set; } = "02:00:00";
    public int IntervalHours { get; set; } = 168;
    public ArchiveType ArchiveType { get; set; } = ArchiveType.All;
    public int RetentionDays { get; set; } = 90;
    public bool CompressArchive { get; set; } = true;
    public bool DeleteAfterArchive { get; set; } = false;
    public bool VerifyArchive { get; set; } = true;
    public List<string> TablesToArchive { get; set; } = new();
    public bool SendNotifications { get; set; } = true;
}

public class PurgeSchedule : IScheduleBase
{
    public bool Enabled { get; set; } = true;
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Monthly;
    public string DailyAt { get; set; } = "03:00:00";
    public string WeeklyOn { get; set; } = "Sunday";
    public string WeeklyAt { get; set; } = "03:00:00";
    public int MonthlyDay { get; set; } = 1;
    public string MonthlyAt { get; set; } = "03:00:00";
    public int IntervalHours { get; set; } = 720;
    public int RetentionDays { get; set; } = 365;
    public List<string> TablesToPurge { get; set; } = new();
    public bool CreateBackupBeforePurge { get; set; } = true;
    public bool ArchiveBeforePurge { get; set; } = true;
    public bool UpdateStatisticsAfterPurge { get; set; } = true;
    public int BatchSize { get; set; } = 1000;
}

public class ComplianceReportSchedule : IScheduleBase
{
    public bool Enabled { get; set; } = true;
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Monthly;
    public string DailyAt { get; set; } = "06:00:00";
    public string WeeklyOn { get; set; } = "Sunday";
    public string WeeklyAt { get; set; } = "06:00:00";
    public int MonthlyDay { get; set; } = 1;
    public string MonthlyAt { get; set; } = "06:00:00";
    public int IntervalHours { get; set; } = 720;
    public ComplianceType ComplianceType { get; set; } = ComplianceType.All;
    public string ReportPeriod { get; set; } = "monthly";
    public ExportFormat ExportFormat { get; set; } = ExportFormat.Pdf;
    public bool IncludeDetails { get; set; } = true;
    public List<string> EmailRecipients { get; set; } = new();
}

public class RetentionCheckSchedule : IScheduleBase
{
    public bool Enabled { get; set; } = true;
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;
    public string DailyAt { get; set; } = "01:00:00";
    public string WeeklyOn { get; set; } = "Sunday";
    public string WeeklyAt { get; set; } = "01:00:00";
    public int MonthlyDay { get; set; } = 1;
    public string MonthlyAt { get; set; } = "01:00:00";
    public int IntervalHours { get; set; } = 24;
    public bool AutoRemediate { get; set; } = false;
    public int DefaultRetentionDays { get; set; } = 90;
    public long CriticalThreshold { get; set; } = 100000;
}

public class ArchivingScheduleOptions
{
    public bool Enabled { get; set; } = true;
    public ArchiveSchedule ArchiveSchedule { get; set; } = new();
    public PurgeSchedule PurgeSchedule { get; set; } = new();
    public ComplianceReportSchedule ComplianceReportSchedule { get; set; } = new();
    public RetentionCheckSchedule RetentionCheckSchedule { get; set; } = new();
}

#endregion