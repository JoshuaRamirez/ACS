using ACS.Infrastructure.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for metrics and monitoring endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Operator")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMetricsCollector metricsCollector,
        IDashboardService dashboardService,
        ILogger<MetricsController> logger)
    {
        _metricsCollector = metricsCollector;
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>
    /// Get current metrics snapshot
    /// </summary>
    [HttpGet("snapshot")]
    [ProducesResponseType(typeof(MetricsSnapshot), 200)]
    public async Task<IActionResult> GetSnapshot()
    {
        var snapshot = await _metricsCollector.GetSnapshotAsync();
        return Ok(snapshot);
    }

    /// <summary>
    /// Get metrics for specific metric name and time range
    /// </summary>
    [HttpGet("{metricName}")]
    [ProducesResponseType(typeof(List<MetricDataPoint>), 200)]
    public async Task<IActionResult> GetMetric(
        string metricName,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null)
    {
        var startTime = start ?? DateTime.UtcNow.AddHours(-1);
        var endTime = end ?? DateTime.UtcNow;
        
        var dataPoints = await _metricsCollector.GetMetricsAsync(metricName, startTime, endTime);
        return Ok(dataPoints);
    }

    /// <summary>
    /// Get dashboard data
    /// </summary>
    [HttpGet("dashboard/{dashboardName}")]
    [ProducesResponseType(typeof(DashboardData), 200)]
    public async Task<IActionResult> GetDashboard(
        string dashboardName,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string interval = "1m")
    {
        var timeRange = new TimeRange
        {
            Start = start ?? DateTime.UtcNow.AddHours(-1),
            End = end ?? DateTime.UtcNow,
            Interval = interval
        };
        
        var dashboard = await _dashboardService.GetDashboardAsync(dashboardName, timeRange);
        return Ok(dashboard);
    }

    /// <summary>
    /// Get available dashboards
    /// </summary>
    [HttpGet("dashboards")]
    [ProducesResponseType(typeof(List<DashboardInfo>), 200)]
    public async Task<IActionResult> GetDashboards()
    {
        var dashboards = await _dashboardService.GetAvailableDashboardsAsync();
        return Ok(dashboards);
    }

    /// <summary>
    /// Get dashboard configuration
    /// </summary>
    [HttpGet("dashboard/{dashboardName}/config")]
    [ProducesResponseType(typeof(DashboardConfiguration), 200)]
    public async Task<IActionResult> GetDashboardConfig(string dashboardName)
    {
        var config = await _dashboardService.GetConfigurationAsync(dashboardName);
        return Ok(config);
    }

    /// <summary>
    /// Save dashboard configuration
    /// </summary>
    [HttpPost("dashboard/{dashboardName}/config")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> SaveDashboardConfig(
        string dashboardName,
        [FromBody] DashboardConfiguration configuration)
    {
        await _dashboardService.SaveConfigurationAsync(dashboardName, configuration);
        return NoContent();
    }

    /// <summary>
    /// Get real-time metrics stream (Server-Sent Events)
    /// </summary>
    [HttpGet("stream")]
    [Produces("text/event-stream")]
    public async Task GetMetricsStream(CancellationToken cancellationToken)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        await foreach (var update in _dashboardService.GetRealTimeMetricsAsync(cancellationToken))
        {
            var data = System.Text.Json.JsonSerializer.Serialize(update);
            await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Record custom business metric
    /// </summary>
    [HttpPost("business")]
    [ProducesResponseType(204)]
    public IActionResult RecordBusinessMetric([FromBody] BusinessMetricRequest request)
    {
        _metricsCollector.RecordBusinessMetric(
            request.Category,
            request.Metric,
            request.Value,
            request.Dimensions);
        
        return NoContent();
    }

    /// <summary>
    /// Export metrics in Prometheus format
    /// </summary>
    [HttpGet("prometheus")]
    [Produces("text/plain")]
    [AllowAnonymous] // Prometheus scraper endpoint
    public async Task<IActionResult> GetPrometheusMetrics()
    {
        var snapshot = await _metricsCollector.GetSnapshotAsync();
        var output = new System.Text.StringBuilder();

        // Format counters
        foreach (var (name, value) in snapshot.Counters)
        {
            var prometheusName = name.Replace(".", "_");
            output.AppendLine($"# TYPE {prometheusName} counter");
            output.AppendLine($"{prometheusName} {value.Value}");
        }

        // Format gauges
        foreach (var (name, value) in snapshot.Gauges)
        {
            var prometheusName = name.Replace(".", "_");
            output.AppendLine($"# TYPE {prometheusName} gauge");
            output.AppendLine($"{prometheusName} {value.Value}");
        }

        // Format histograms
        foreach (var (name, value) in snapshot.Histograms)
        {
            var prometheusName = name.Replace(".", "_");
            output.AppendLine($"# TYPE {prometheusName} histogram");
            output.AppendLine($"{prometheusName}_count {value.Count}");
            output.AppendLine($"{prometheusName}_sum {value.Sum}");
            output.AppendLine($"{prometheusName}_bucket{{le=\"{value.P50}\"}} {value.Count * 0.5}");
            output.AppendLine($"{prometheusName}_bucket{{le=\"{value.P95}\"}} {value.Count * 0.95}");
            output.AppendLine($"{prometheusName}_bucket{{le=\"{value.P99}\"}} {value.Count * 0.99}");
            output.AppendLine($"{prometheusName}_bucket{{le=\"+Inf\"}} {value.Count}");
        }

        return Content(output.ToString(), "text/plain");
    }

    /// <summary>
    /// Get top N metrics by value
    /// </summary>
    [HttpGet("top/{category}")]
    [ProducesResponseType(typeof(List<TopMetric>), 200)]
    public async Task<IActionResult> GetTopMetrics(
        string category,
        [FromQuery] int count = 10)
    {
        var snapshot = await _metricsCollector.GetSnapshotAsync();
        var topMetrics = new List<TopMetric>();

        if (category.Equals("endpoints", StringComparison.OrdinalIgnoreCase))
        {
            // Get top endpoints by request count
            var endpointMetrics = snapshot.Counters
                .Where(kvp => kvp.Key.StartsWith(ApplicationMetrics.Api.RequestCount))
                .OrderByDescending(kvp => kvp.Value.Value)
                .Take(count)
                .Select(kvp => new TopMetric
                {
                    Name = kvp.Value.Tags.GetValueOrDefault("endpoint")?.ToString() ?? kvp.Key,
                    Value = kvp.Value.Value,
                    Category = "Endpoints"
                });
            
            topMetrics.AddRange(endpointMetrics);
        }
        else if (category.Equals("errors", StringComparison.OrdinalIgnoreCase))
        {
            // Get top error types
            var errorMetrics = snapshot.Counters
                .Where(kvp => kvp.Key.Contains("error"))
                .OrderByDescending(kvp => kvp.Value.Value)
                .Take(count)
                .Select(kvp => new TopMetric
                {
                    Name = kvp.Key,
                    Value = kvp.Value.Value,
                    Category = "Errors"
                });
            
            topMetrics.AddRange(errorMetrics);
        }

        return Ok(topMetrics);
    }
}

// Request/Response DTOs
public class BusinessMetricRequest
{
    public string Category { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public Dictionary<string, object?>? Dimensions { get; set; }
}

public class TopMetric
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Category { get; set; } = string.Empty;
}