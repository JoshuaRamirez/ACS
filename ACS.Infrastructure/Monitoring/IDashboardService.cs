namespace ACS.Infrastructure.Monitoring;

/// <summary>
/// Service for managing monitoring dashboards
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Get dashboard data for specific view
    /// </summary>
    Task<DashboardData> GetDashboardAsync(string dashboardName, TimeRange? timeRange = null);
    
    /// <summary>
    /// Get real-time metrics stream
    /// </summary>
    IAsyncEnumerable<MetricUpdate> GetRealTimeMetricsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get custom dashboard configuration
    /// </summary>
    Task<DashboardConfiguration> GetConfigurationAsync(string dashboardName);
    
    /// <summary>
    /// Save custom dashboard configuration
    /// </summary>
    Task SaveConfigurationAsync(string dashboardName, DashboardConfiguration configuration);
    
    /// <summary>
    /// Get available dashboards
    /// </summary>
    Task<IEnumerable<DashboardInfo>> GetAvailableDashboardsAsync();
}

/// <summary>
/// Dashboard data container
/// </summary>
public class DashboardData
{
    public string Name { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public TimeRange TimeRange { get; set; } = new();
    public List<Widget> Widgets { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Dashboard widget
/// </summary>
public class Widget
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public WidgetType Type { get; set; }
    public object Data { get; set; } = new { };
    public WidgetOptions Options { get; set; } = new();
}

/// <summary>
/// Widget types
/// </summary>
public enum WidgetType
{
    LineChart,
    BarChart,
    PieChart,
    Gauge,
    Counter,
    Table,
    Heatmap,
    Timeline,
    Map,
    Text
}

/// <summary>
/// Widget display options
/// </summary>
public class WidgetOptions
{
    public int Width { get; set; } = 6; // Grid columns (1-12)
    public int Height { get; set; } = 4; // Grid rows
    public bool Refreshable { get; set; } = true;
    public int RefreshIntervalSeconds { get; set; } = 30;
    public Dictionary<string, object> CustomOptions { get; set; } = new();
}

/// <summary>
/// Time range for dashboard data
/// </summary>
public class TimeRange
{
    public DateTime Start { get; set; } = DateTime.UtcNow.AddHours(-1);
    public DateTime End { get; set; } = DateTime.UtcNow;
    public string Interval { get; set; } = "1m"; // 1m, 5m, 15m, 1h, 1d
}

/// <summary>
/// Real-time metric update
/// </summary>
public class MetricUpdate
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object?> Tags { get; set; } = new();
}

/// <summary>
/// Dashboard configuration
/// </summary>
public class DashboardConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WidgetConfiguration> Widgets { get; set; } = new();
    public TimeRange DefaultTimeRange { get; set; } = new();
    public int RefreshIntervalSeconds { get; set; } = 30;
    public Dictionary<string, object> Settings { get; set; } = new();
}

/// <summary>
/// Widget configuration
/// </summary>
public class WidgetConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public WidgetType Type { get; set; }
    public string MetricQuery { get; set; } = string.Empty;
    public List<string> Metrics { get; set; } = new();
    public WidgetOptions Options { get; set; } = new();
}

/// <summary>
/// Dashboard information
/// </summary>
public class DashboardInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsDefault { get; set; }
    public List<string> Tags { get; set; } = new();
}