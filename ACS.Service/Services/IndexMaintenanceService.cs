using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ACS.Service.Data;
using ACS.Service.Domain;

namespace ACS.Service.Services;

/// <summary>
/// Background service for automated database index maintenance
/// </summary>
public class IndexMaintenanceService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IndexMaintenanceService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _analysisInterval;
    private readonly double _fragmentationThreshold;
    private readonly int _unusedIndexDaysThreshold;

    public IndexMaintenanceService(
        IServiceProvider serviceProvider,
        ILogger<IndexMaintenanceService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        // Load configuration with defaults
        _analysisInterval = TimeSpan.FromHours(
            configuration.GetValue<double>("IndexMaintenance:AnalysisIntervalHours", 24));
        _fragmentationThreshold = configuration.GetValue<double>(
            "IndexMaintenance:FragmentationThreshold", 30.0);
        _unusedIndexDaysThreshold = configuration.GetValue<int>(
            "IndexMaintenance:UnusedIndexDaysThreshold", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Index maintenance service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformIndexMaintenance(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during index maintenance");
            }

            // Wait for the next analysis interval
            await Task.Delay(_analysisInterval, stoppingToken);
        }

        _logger.LogInformation("Index maintenance service stopped");
    }

    private async Task PerformIndexMaintenance(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var indexAnalyzer = scope.ServiceProvider.GetRequiredService<IIndexAnalyzer>();

        _logger.LogInformation("Starting index maintenance analysis");

        // Perform comprehensive index analysis
        var report = await indexAnalyzer.AnalyzeIndexesAsync(cancellationToken);

        _logger.LogInformation(
            "Index analysis completed. Health Score: {HealthScore}%, Total Indexes: {TotalIndexes}",
            report.HealthScore, report.TotalIndexes);

        // Process fragmented indexes
        await ProcessFragmentedIndexes(indexAnalyzer, report, cancellationToken);

        // Log missing index recommendations
        LogMissingIndexRecommendations(report);

        // Log unused indexes for review
        LogUnusedIndexes(report);

        // Log duplicate indexes
        LogDuplicateIndexes(report);

        // Send alerts if health score is low
        await SendHealthScoreAlerts(report);
    }

    private async Task ProcessFragmentedIndexes(
        IIndexAnalyzer indexAnalyzer,
        IndexAnalysisReport report,
        CancellationToken cancellationToken)
    {
        if (!report.FragmentedIndexes.Any())
        {
            _logger.LogDebug("No fragmented indexes found");
            return;
        }

        _logger.LogInformation(
            "Found {Count} fragmented indexes above {Threshold}% threshold",
            report.FragmentedIndexes.Count, _fragmentationThreshold);

        // Auto-maintenance only during maintenance window
        if (!IsInMaintenanceWindow())
        {
            _logger.LogInformation("Outside maintenance window. Skipping automatic index maintenance");
            return;
        }

        foreach (var fragmentedIndex in report.FragmentedIndexes)
        {
            try
            {
                if (fragmentedIndex.RecommendedAction == "REBUILD")
                {
                    _logger.LogInformation(
                        "Rebuilding heavily fragmented index {IndexName} on {TableName} ({Fragmentation:F2}%)",
                        fragmentedIndex.IndexName, fragmentedIndex.TableName, fragmentedIndex.FragmentationPercent);

                    var success = await indexAnalyzer.RebuildIndexAsync(
                        fragmentedIndex.TableName,
                        fragmentedIndex.IndexName,
                        cancellationToken);

                    if (success)
                    {
                        _logger.LogInformation(
                            "Successfully rebuilt index {IndexName} on {TableName}",
                            fragmentedIndex.IndexName, fragmentedIndex.TableName);
                    }
                }
                else if (fragmentedIndex.RecommendedAction == "REORGANIZE")
                {
                    _logger.LogInformation(
                        "Reorganizing moderately fragmented index {IndexName} on {TableName} ({Fragmentation:F2}%)",
                        fragmentedIndex.IndexName, fragmentedIndex.TableName, fragmentedIndex.FragmentationPercent);

                    var success = await indexAnalyzer.ReorganizeIndexAsync(
                        fragmentedIndex.TableName,
                        fragmentedIndex.IndexName,
                        cancellationToken);

                    if (success)
                    {
                        _logger.LogInformation(
                            "Successfully reorganized index {IndexName} on {TableName}",
                            fragmentedIndex.IndexName, fragmentedIndex.TableName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error maintaining index {IndexName} on {TableName}",
                    fragmentedIndex.IndexName, fragmentedIndex.TableName);
            }
        }
    }

    private void LogMissingIndexRecommendations(IndexAnalysisReport report)
    {
        if (!report.MissingIndexes.Any())
        {
            _logger.LogDebug("No missing index recommendations");
            return;
        }

        _logger.LogWarning(
            "Found {Count} missing index recommendations",
            report.MissingIndexes.Count);

        // Log top 5 recommendations
        foreach (var recommendation in report.MissingIndexes.Take(5))
        {
            _logger.LogWarning(
                "Missing index recommendation for {TableName}: " +
                "ImprovementMeasure={ImprovementMeasure:F0}, " +
                "AvgImpact={AvgImpact:F2}%, " +
                "Columns=[{EqualityColumns}], " +
                "Included=[{IncludedColumns}]",
                recommendation.TableName,
                recommendation.ImprovementMeasure,
                recommendation.AverageImpact,
                recommendation.EqualityColumns ?? "none",
                recommendation.IncludedColumns ?? "none");
        }
    }

    private void LogUnusedIndexes(IndexAnalysisReport report)
    {
        if (!report.UnusedIndexes.Any())
        {
            _logger.LogDebug("No unused indexes found");
            return;
        }

        var totalSizeMB = report.UnusedIndexes.Sum(i => i.SizeMB);

        _logger.LogWarning(
            "Found {Count} unused indexes (not used in {Days} days) consuming {Size:F2} MB",
            report.UnusedIndexes.Count, _unusedIndexDaysThreshold, totalSizeMB);

        // Log largest unused indexes
        foreach (var unusedIndex in report.UnusedIndexes.OrderByDescending(i => i.SizeMB).Take(5))
        {
            _logger.LogWarning(
                "Unused index: {SchemaName}.{TableName}.{IndexName} - " +
                "Size={Size:F2}MB, DaysSinceLastUse={Days}, Updates={Updates}",
                unusedIndex.SchemaName,
                unusedIndex.TableName,
                unusedIndex.IndexName,
                unusedIndex.SizeMB,
                unusedIndex.DaysSinceLastUse,
                unusedIndex.UserUpdates);
        }
    }

    private void LogDuplicateIndexes(IndexAnalysisReport report)
    {
        if (!report.DuplicateIndexes.Any())
        {
            _logger.LogDebug("No duplicate indexes found");
            return;
        }

        _logger.LogWarning(
            "Found {Count} duplicate indexes",
            report.DuplicateIndexes.Count);

        foreach (var duplicate in report.DuplicateIndexes)
        {
            _logger.LogWarning(
                "Duplicate indexes on {SchemaName}.{TableName}: [{Index1}] and [{Index2}] " +
                "have same columns: {KeyColumns}",
                duplicate.SchemaName,
                duplicate.TableName,
                duplicate.Index1,
                duplicate.Index2,
                duplicate.KeyColumns);
        }
    }

    private Task SendHealthScoreAlerts(IndexAnalysisReport report)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            // Alerting service not implemented - using logging instead

            if (report.HealthScore < 30)
            {
                _logger.LogCritical("Critical: Database index health is very poor ({HealthScore:F1}%). Immediate maintenance required.", report.HealthScore);
            }
            else if (report.HealthScore < 50)
            {
                _logger.LogWarning("Warning: Database index health is poor ({HealthScore:F1}%). Maintenance recommended.", report.HealthScore);
            }
            else if (report.HealthScore < 75)
            {
                _logger.LogInformation("Info: Database index health needs attention ({HealthScore:F1}%).", report.HealthScore);
            }
            else
            {
                // Health is good, no alert needed
                return Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending index health alert");
        }
        
        return Task.CompletedTask;
    }

    private bool IsInMaintenanceWindow()
    {
        // Check if we're in the configured maintenance window
        var maintenanceStart = _configuration.GetValue<TimeSpan?>("IndexMaintenance:MaintenanceWindowStart");
        var maintenanceEnd = _configuration.GetValue<TimeSpan?>("IndexMaintenance:MaintenanceWindowEnd");

        if (!maintenanceStart.HasValue || !maintenanceEnd.HasValue)
        {
            // No maintenance window configured, allow maintenance anytime
            _logger.LogDebug("No maintenance window configured, allowing index maintenance");
            return true;
        }

        var now = DateTime.UtcNow.TimeOfDay;

        if (maintenanceStart.Value < maintenanceEnd.Value)
        {
            // Window doesn't cross midnight
            return now >= maintenanceStart.Value && now <= maintenanceEnd.Value;
        }
        else
        {
            // Window crosses midnight
            return now >= maintenanceStart.Value || now <= maintenanceEnd.Value;
        }
    }
}

/// <summary>
/// Options for index maintenance service
/// </summary>
public class IndexMaintenanceOptions
{
    /// <summary>
    /// Interval between index analyses in hours
    /// </summary>
    public double AnalysisIntervalHours { get; set; } = 24;

    /// <summary>
    /// Fragmentation percentage threshold for triggering maintenance
    /// </summary>
    public double FragmentationThreshold { get; set; } = 30.0;

    /// <summary>
    /// Number of days since last use to consider an index unused
    /// </summary>
    public int UnusedIndexDaysThreshold { get; set; } = 30;

    /// <summary>
    /// Start time of maintenance window (UTC)
    /// </summary>
    public TimeSpan? MaintenanceWindowStart { get; set; }

    /// <summary>
    /// End time of maintenance window (UTC)
    /// </summary>
    public TimeSpan? MaintenanceWindowEnd { get; set; }

    /// <summary>
    /// Whether to automatically rebuild heavily fragmented indexes
    /// </summary>
    public bool AutoRebuildEnabled { get; set; } = false;

    /// <summary>
    /// Whether to automatically reorganize moderately fragmented indexes
    /// </summary>
    public bool AutoReorganizeEnabled { get; set; } = true;
}