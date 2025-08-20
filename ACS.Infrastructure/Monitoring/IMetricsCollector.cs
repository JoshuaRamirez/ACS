namespace ACS.Infrastructure.Monitoring;

/// <summary>
/// Interface for collecting custom metrics
/// </summary>
public interface IMetricsCollector
{
    // Counter metrics
    void IncrementCounter(string name, double value = 1, params KeyValuePair<string, object?>[] tags);
    
    // Gauge metrics
    void RecordGauge(string name, double value, params KeyValuePair<string, object?>[] tags);
    
    // Histogram metrics
    void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags);
    
    // Timer metrics
    IDisposable StartTimer(string name, params KeyValuePair<string, object?>[] tags);
    void RecordDuration(string name, TimeSpan duration, params KeyValuePair<string, object?>[] tags);
    
    // Business metrics
    void RecordBusinessMetric(string category, string metric, double value, Dictionary<string, object?>? dimensions = null);
    
    // Get current metrics snapshot
    Task<MetricsSnapshot> GetSnapshotAsync();
    
    // Get metrics for specific time range
    Task<IEnumerable<MetricDataPoint>> GetMetricsAsync(string name, DateTime start, DateTime end);
}

/// <summary>
/// Represents a snapshot of current metrics
/// </summary>
public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, MetricValue> Counters { get; set; } = new();
    public Dictionary<string, MetricValue> Gauges { get; set; } = new();
    public Dictionary<string, HistogramValue> Histograms { get; set; } = new();
    public Dictionary<string, BusinessMetricValue> BusinessMetrics { get; set; } = new();
}

/// <summary>
/// Represents a metric value
/// </summary>
public class MetricValue
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object?> Tags { get; set; } = new();
}

/// <summary>
/// Represents a histogram value with statistics
/// </summary>
public class HistogramValue : MetricValue
{
    public double Min { get; set; }
    public double Max { get; set; }
    public double Mean { get; set; }
    public double StdDev { get; set; }
    public double P50 { get; set; }
    public double P75 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public long Count { get; set; }
    public double Sum { get; set; }
}

/// <summary>
/// Represents a business metric value
/// </summary>
public class BusinessMetricValue : MetricValue
{
    public string Category { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public Dictionary<string, object?> Dimensions { get; set; } = new();
}

/// <summary>
/// Represents a metric data point in time series
/// </summary>
public class MetricDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public Dictionary<string, object?> Tags { get; set; } = new();
}