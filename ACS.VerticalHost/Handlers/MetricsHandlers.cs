using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Infrastructure.Monitoring;
using Microsoft.Extensions.Logging;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;
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

    public Task HandleAsync(RecordBusinessMetricCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(RecordBusinessMetricCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, 
            new { Category = command.Category, Metric = command.Metric, Value = command.Value }, correlationId);

        try
        {
            _metricsCollector.RecordBusinessMetric(
                command.Category,
                command.Metric,
                command.Value,
                command.Dimensions);

            LogCommandSuccess(_logger, context, 
                new { Category = command.Category, Metric = command.Metric, Value = command.Value }, correlationId);
                
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Re-throw to maintain clean architecture - don't swallow exceptions
            return HandleCommandError<Task>(_logger, ex, context, correlationId);
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

    public async Task HandleAsync(SaveDashboardConfigurationCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(SaveDashboardConfigurationCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { DashboardName = command.DashboardName }, correlationId);

        try
        {
            var config = new InfrastructureMetrics.DashboardConfiguration
            {
                // Map from command type to infrastructure type
                // This mapping would be done properly in a real implementation
            };

            await _dashboardService.SaveConfigurationAsync(command.DashboardName, config);

            LogCommandSuccess(_logger, context, new { DashboardName = command.DashboardName }, correlationId);
        }
        catch (Exception ex)
        {
            // Re-throw to maintain clean architecture - HandleCommandError always throws
#pragma warning disable CS4014 // Fire and forget is intentional - method always throws
            HandleCommandError<Task>(_logger, ex, context, correlationId);
#pragma warning restore CS4014
            throw; // This line never executes but satisfies compiler
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetMetricsSnapshotQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { }, correlationId);

        try
        {
            var snapshot = await _metricsCollector.GetSnapshotAsync();

            var result = new Commands.MetricsSnapshot
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

            LogQuerySuccess(_logger, context, 
                new { CounterCount = snapshot.Counters.Count, GaugeCount = snapshot.Gauges.Count, HistogramCount = snapshot.Histograms.Count }, 
                correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<CommandMetricsSnapshot>(_logger, ex, context, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetMetricDataQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, 
            new { MetricName = query.MetricName, StartTime = query.StartTime, EndTime = query.EndTime }, correlationId);

        try
        {
            var dataPoints = await _metricsCollector.GetMetricsAsync(query.MetricName, query.StartTime, query.EndTime);

            var result = dataPoints.Select(dp => new Commands.MetricDataPoint
            {
                Timestamp = dp.Timestamp,
                Value = dp.Value,
                Tags = dp.Tags
            }).ToList();

            LogQuerySuccess(_logger, context, 
                new { MetricName = query.MetricName, DataPointCount = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<CommandMetricDataPoint>>(_logger, ex, context, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetDashboardQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { DashboardName = query.DashboardName }, correlationId);

        try
        {
            var timeRange = new InfrastructureMetrics.TimeRange
            {
                Start = query.TimeRange.Start,
                End = query.TimeRange.End,
                Interval = query.TimeRange.Interval
            };

            var dashboard = await _dashboardService.GetDashboardAsync(query.DashboardName, timeRange);

            var result = new Commands.DashboardData
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

            LogQuerySuccess(_logger, context, 
                new { DashboardName = query.DashboardName, PanelCount = result.Panels.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<CommandDashboardData>(_logger, ex, context, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetAvailableDashboardsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { }, correlationId);

        try
        {
            var dashboards = await _dashboardService.GetAvailableDashboardsAsync();

            var result = dashboards.Select(d => new Commands.DashboardInfo
            {
                Name = d.Name,
                Title = d.Name,
                Description = d.Description,
                Tags = d.Tags.ToList()
            }).ToList();

            LogQuerySuccess(_logger, context, new { DashboardCount = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<CommandDashboardInfo>>(_logger, ex, context, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetDashboardConfigurationQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { DashboardName = query.DashboardName }, correlationId);

        try
        {
            var config = await _dashboardService.GetConfigurationAsync(query.DashboardName);

            var result = new Commands.DashboardConfiguration
            {
                Name = config.Name,
                Title = config.Name,
                Description = config.Description,
                Panels = new List<Commands.DashboardPanelConfiguration>(),
                Settings = config.Settings?.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value) ?? new Dictionary<string, object?>()
            };

            LogQuerySuccess(_logger, context, 
                new { DashboardName = query.DashboardName, PanelCount = result.Panels.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<CommandDashboardConfiguration>(_logger, ex, context, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetTopMetricsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { Category = query.Category, Count = query.Count }, correlationId);

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

            LogQuerySuccess(_logger, context, 
                new { Category = query.Category, MetricCount = topMetrics.Count }, correlationId);
            var result = topMetrics;
            LogQuerySuccess(_logger, context, 
                new { Category = query.Category, MetricCount = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<TopMetric>>(_logger, ex, context, correlationId);
        }
    }
}