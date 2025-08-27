using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Infrastructure.Monitoring;
using Microsoft.Extensions.Logging;
using InfrastructureMetrics = ACS.Infrastructure.Monitoring;
using Commands = ACS.VerticalHost.Commands;
using CommandMetricDataPoint = ACS.VerticalHost.Commands.MetricDataPoint;
using CommandDashboardInfo = ACS.VerticalHost.Commands.DashboardInfo;
using CommandDashboardData = ACS.VerticalHost.Commands.DashboardData;
using CommandMetricsSnapshot = ACS.VerticalHost.Commands.MetricsSnapshot;
using CommandDashboardConfiguration = ACS.VerticalHost.Commands.DashboardConfiguration;

namespace ACS.VerticalHost.Handlers;

public class RecordBusinessMetricCommandHandler : ICommandHandler<RecordBusinessMetricCommand>
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger<RecordBusinessMetricCommandHandler> _logger;

    public RecordBusinessMetricCommandHandler(
        IMetricsCollector metricsCollector,
        ILogger<RecordBusinessMetricCommandHandler> logger)
    {
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    public async Task<object?> HandleAsync(RecordBusinessMetricCommand command, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // For async signature

        try
        {
            _metricsCollector.RecordBusinessMetric(
                command.Category,
                command.Metric,
                command.Value,
                command.Dimensions);

            _logger.LogInformation("Business metric recorded: {Category}.{Metric} = {Value}", 
                command.Category, command.Metric, command.Value);

            return null; // ICommand returns object?
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording business metric: {Category}.{Metric}", 
                command.Category, command.Metric);
            throw;
        }
    }
}

public class SaveDashboardConfigurationCommandHandler : ICommandHandler<SaveDashboardConfigurationCommand>
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<SaveDashboardConfigurationCommandHandler> _logger;

    public SaveDashboardConfigurationCommandHandler(
        IDashboardService dashboardService,
        ILogger<SaveDashboardConfigurationCommandHandler> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    public async Task<object?> HandleAsync(SaveDashboardConfigurationCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var config = new InfrastructureMetrics.DashboardConfiguration
            {
                // Map from command type to infrastructure type
                // This mapping would be done properly in a real implementation
            };

            await _dashboardService.SaveConfigurationAsync(command.DashboardName, config);

            _logger.LogInformation("Dashboard configuration saved: {DashboardName}", command.DashboardName);

            return null; // ICommand returns object?
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving dashboard configuration: {DashboardName}", command.DashboardName);
            throw;
        }
    }
}

public class GetMetricsSnapshotQueryHandler : IQueryHandler<GetMetricsSnapshotQuery, CommandMetricsSnapshot>
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger<GetMetricsSnapshotQueryHandler> _logger;

    public GetMetricsSnapshotQueryHandler(
        IMetricsCollector metricsCollector,
        ILogger<GetMetricsSnapshotQueryHandler> logger)
    {
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    public async Task<CommandMetricsSnapshot> HandleAsync(GetMetricsSnapshotQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _metricsCollector.GetSnapshotAsync();

            return new Commands.MetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                Counters = snapshot.Counters.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new Commands.CounterValue
                    {
                        Value = kvp.Value.Value,
                        Tags = kvp.Value.Tags
                    }),
                Gauges = snapshot.Gauges.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new Commands.GaugeValue
                    {
                        Value = kvp.Value.Value,
                        Tags = kvp.Value.Tags
                    }),
                Histograms = snapshot.Histograms.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new Commands.HistogramValue
                    {
                        Count = kvp.Value.Count,
                        Sum = kvp.Value.Sum,
                        P50 = kvp.Value.P50,
                        P95 = kvp.Value.P95,
                        P99 = kvp.Value.P99
                    })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metrics snapshot");
            throw;
        }
    }
}

public class GetMetricDataQueryHandler : IQueryHandler<GetMetricDataQuery, List<CommandMetricDataPoint>>
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger<GetMetricDataQueryHandler> _logger;

    public GetMetricDataQueryHandler(
        IMetricsCollector metricsCollector,
        ILogger<GetMetricDataQueryHandler> logger)
    {
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    public async Task<List<CommandMetricDataPoint>> HandleAsync(GetMetricDataQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var dataPoints = await _metricsCollector.GetMetricsAsync(query.MetricName, query.StartTime, query.EndTime);

            return dataPoints.Select(dp => new Commands.MetricDataPoint
            {
                Timestamp = dp.Timestamp,
                Value = dp.Value,
                Tags = dp.Tags
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metric data: {MetricName}", query.MetricName);
            throw;
        }
    }
}

public class GetDashboardQueryHandler : IQueryHandler<GetDashboardQuery, CommandDashboardData>
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<GetDashboardQueryHandler> _logger;

    public GetDashboardQueryHandler(
        IDashboardService dashboardService,
        ILogger<GetDashboardQueryHandler> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    public async Task<CommandDashboardData> HandleAsync(GetDashboardQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var timeRange = new InfrastructureMetrics.TimeRange
            {
                Start = query.TimeRange.Start,
                End = query.TimeRange.End,
                Interval = query.TimeRange.Interval
            };

            var dashboard = await _dashboardService.GetDashboardAsync(query.DashboardName, timeRange);

            return new Commands.DashboardData
            {
                Name = dashboard.Name,
                GeneratedAt = DateTime.UtcNow,
                Panels = dashboard.Widgets.Select(p => new Commands.DashboardPanel
                {
                    Title = p.Title,
                    Type = p.Type.ToString(),
                    Data = new List<Commands.MetricDataPoint>()
                }).ToList(),
                TimeRange = new Commands.TimeRange
                {
                    Start = timeRange.Start,
                    End = timeRange.End,
                    Interval = timeRange.Interval
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard: {DashboardName}", query.DashboardName);
            throw;
        }
    }
}

public class GetAvailableDashboardsQueryHandler : IQueryHandler<GetAvailableDashboardsQuery, List<CommandDashboardInfo>>
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<GetAvailableDashboardsQueryHandler> _logger;

    public GetAvailableDashboardsQueryHandler(
        IDashboardService dashboardService,
        ILogger<GetAvailableDashboardsQueryHandler> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    public async Task<List<CommandDashboardInfo>> HandleAsync(GetAvailableDashboardsQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var dashboards = await _dashboardService.GetAvailableDashboardsAsync();

            return dashboards.Select(d => new Commands.DashboardInfo
            {
                Name = d.Name,
                Title = d.Name,
                Description = d.Description,
                Tags = d.Tags.ToList()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available dashboards");
            throw;
        }
    }
}

public class GetDashboardConfigurationQueryHandler : IQueryHandler<GetDashboardConfigurationQuery, CommandDashboardConfiguration>
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<GetDashboardConfigurationQueryHandler> _logger;

    public GetDashboardConfigurationQueryHandler(
        IDashboardService dashboardService,
        ILogger<GetDashboardConfigurationQueryHandler> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    public async Task<CommandDashboardConfiguration> HandleAsync(GetDashboardConfigurationQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var config = await _dashboardService.GetConfigurationAsync(query.DashboardName);

            return new Commands.DashboardConfiguration
            {
                Name = config.Name,
                Title = config.Name,
                Description = config.Description,
                Panels = new List<Commands.DashboardPanelConfiguration>(),
                Settings = config.Settings?.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value) ?? new Dictionary<string, object?>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard configuration: {DashboardName}", query.DashboardName);
            throw;
        }
    }
}

public class GetTopMetricsQueryHandler : IQueryHandler<GetTopMetricsQuery, List<TopMetric>>
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger<GetTopMetricsQueryHandler> _logger;

    public GetTopMetricsQueryHandler(
        IMetricsCollector metricsCollector,
        ILogger<GetTopMetricsQueryHandler> logger)
    {
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    public async Task<List<TopMetric>> HandleAsync(GetTopMetricsQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _metricsCollector.GetSnapshotAsync();
            var topMetrics = new List<TopMetric>();

            if (query.Category.Equals("endpoints", StringComparison.OrdinalIgnoreCase))
            {
                // Get top endpoints by request count
                var endpointMetrics = snapshot.Counters
                    .Where(kvp => kvp.Key.StartsWith("api_request_count")) // Simplified metric name
                    .OrderByDescending(kvp => kvp.Value.Value)
                    .Take(query.Count)
                    .Select(kvp => new Commands.TopMetric
                    {
                        Name = kvp.Value.Tags.GetValueOrDefault("endpoint")?.ToString() ?? kvp.Key,
                        Value = kvp.Value.Value,
                        Category = "Endpoints"
                    });
                
                topMetrics.AddRange(endpointMetrics);
            }
            else if (query.Category.Equals("errors", StringComparison.OrdinalIgnoreCase))
            {
                // Get top error types
                var errorMetrics = snapshot.Counters
                    .Where(kvp => kvp.Key.Contains("error"))
                    .OrderByDescending(kvp => kvp.Value.Value)
                    .Take(query.Count)
                    .Select(kvp => new Commands.TopMetric
                    {
                        Name = kvp.Key,
                        Value = kvp.Value.Value,
                        Category = "Errors"
                    });
                
                topMetrics.AddRange(errorMetrics);
            }

            return topMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top metrics for category: {Category}", query.Category);
            throw;
        }
    }
}