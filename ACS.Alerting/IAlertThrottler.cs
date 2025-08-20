namespace ACS.Alerting;

/// <summary>
/// Interface for alert throttling to prevent spam and alert fatigue
/// </summary>
public interface IAlertThrottler
{
    /// <summary>
    /// Check if an alert should be throttled
    /// </summary>
    Task<bool> ShouldThrottleAsync(string alertKey, AlertSeverity severity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record an alert occurrence for throttling purposes
    /// </summary>
    Task RecordAlertOccurrenceAsync(string alertKey, AlertSeverity severity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset throttling for a specific alert key
    /// </summary>
    Task ResetThrottleAsync(string alertKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get throttling statistics for monitoring
    /// </summary>
    Task<ThrottleStatistics> GetThrottleStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Throttle statistics
/// </summary>
public class ThrottleStatistics
{
    public int TotalAlertsReceived { get; set; }
    public int TotalAlertsThrottled { get; set; }
    public double ThrottleRate => TotalAlertsReceived > 0 ? (double)TotalAlertsThrottled / TotalAlertsReceived : 0;
    public Dictionary<AlertSeverity, int> ThrottledBySeverity { get; set; } = new();
    public Dictionary<string, int> TopThrottledAlertKeys { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}