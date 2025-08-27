using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// Metrics Commands
public class RecordBusinessMetricCommand : ICommand
{
    public string Category { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public Dictionary<string, object?>? Dimensions { get; set; }
}

public class SaveDashboardConfigurationCommand : ICommand
{
    public string DashboardName { get; set; } = string.Empty;
    public DashboardConfiguration Configuration { get; set; } = new();
}

// Metrics Queries
public class GetMetricsSnapshotQuery : IQuery<MetricsSnapshot>
{
}

public class GetMetricDataQuery : IQuery<List<MetricDataPoint>>
{
    public string MetricName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class GetDashboardQuery : IQuery<DashboardData>
{
    public string DashboardName { get; set; } = string.Empty;
    public TimeRange TimeRange { get; set; } = new();
}

public class GetAvailableDashboardsQuery : IQuery<List<DashboardInfo>>
{
}

public class GetDashboardConfigurationQuery : IQuery<DashboardConfiguration>
{
    public string DashboardName { get; set; } = string.Empty;
}

public class GetTopMetricsQuery : IQuery<List<TopMetric>>
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; } = 10;
}

// Result Types
public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, CounterValue> Counters { get; set; } = new();
    public Dictionary<string, GaugeValue> Gauges { get; set; } = new();
    public Dictionary<string, HistogramValue> Histograms { get; set; } = new();
}

public class CounterValue
{
    public double Value { get; set; }
    public Dictionary<string, object?> Tags { get; set; } = new();
}

public class GaugeValue
{
    public double Value { get; set; }
    public Dictionary<string, object?> Tags { get; set; } = new();
}

public class HistogramValue
{
    public long Count { get; set; }
    public double Sum { get; set; }
    public double P50 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
}

public class MetricDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public Dictionary<string, object?> Tags { get; set; } = new();
}

public class DashboardData
{
    public string Name { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public List<DashboardPanel> Panels { get; set; } = new();
    public TimeRange TimeRange { get; set; } = new();
}

public class DashboardPanel
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<MetricDataPoint> Data { get; set; } = new();
}

public class DashboardInfo
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

public class DashboardConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<DashboardPanelConfiguration> Panels { get; set; } = new();
    public Dictionary<string, object?> Settings { get; set; } = new();
}

public class DashboardPanelConfiguration
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public Dictionary<string, object?> Options { get; set; } = new();
}

public class TimeRange
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Interval { get; set; } = "1m";
}

public class TopMetric
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Category { get; set; } = string.Empty;
}