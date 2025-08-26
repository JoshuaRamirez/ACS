using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ACS.Alerting;

/// <summary>
/// In-memory implementation of alert throttling
/// </summary>
public class InMemoryAlertThrottler : IAlertThrottler
{
    private readonly ILogger<InMemoryAlertThrottler> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, AlertThrottleEntry> _throttleEntries;
    private readonly ThrottleStatistics _statistics;

    public InMemoryAlertThrottler(ILogger<InMemoryAlertThrottler> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _throttleEntries = new ConcurrentDictionary<string, AlertThrottleEntry>();
        _statistics = new ThrottleStatistics();
    }

    public Task<bool> ShouldThrottleAsync(string alertKey, AlertSeverity severity, CancellationToken cancellationToken = default)
    {
        var throttleWindow = GetThrottleWindow(severity);
        var maxOccurrences = GetMaxOccurrences(severity);
        
        var now = DateTime.UtcNow;
        var entry = _throttleEntries.GetOrAdd(alertKey, _ => new AlertThrottleEntry
        {
            AlertKey = alertKey,
            Severity = severity,
            FirstOccurrence = now,
            LastOccurrence = now,
            Count = 0
        });

        lock (entry)
        {
            // Clean up old occurrences outside the throttle window
            var windowStart = now - throttleWindow;
            entry.Occurrences.RemoveAll(o => o < windowStart);

            // Update statistics
            _statistics.TotalAlertsReceived++;
            _statistics.LastUpdated = now;

            // Check if we should throttle
            if (entry.Occurrences.Count >= maxOccurrences)
            {
                _logger.LogDebug("Alert throttled: {AlertKey} (severity: {Severity}, count: {Count}, window: {WindowMinutes}min)", 
                    alertKey, severity, entry.Occurrences.Count, throttleWindow.TotalMinutes);

                // Update throttle statistics
                _statistics.TotalAlertsThrottled++;
                if (!_statistics.ThrottledBySeverity.ContainsKey(severity))
                    _statistics.ThrottledBySeverity[severity] = 0;
                _statistics.ThrottledBySeverity[severity]++;

                if (!_statistics.TopThrottledAlertKeys.ContainsKey(alertKey))
                    _statistics.TopThrottledAlertKeys[alertKey] = 0;
                _statistics.TopThrottledAlertKeys[alertKey]++;

                return Task.FromResult(true);
            }

            // Record this occurrence
            entry.Occurrences.Add(now);
            entry.LastOccurrence = now;
            entry.Count++;

            return Task.FromResult(false);
        }
    }

    public Task RecordAlertOccurrenceAsync(string alertKey, AlertSeverity severity, CancellationToken cancellationToken = default)
    {
        // This is already handled in ShouldThrottleAsync for this implementation
        return Task.CompletedTask;
    }

    public Task ResetThrottleAsync(string alertKey, CancellationToken cancellationToken = default)
    {
        if (_throttleEntries.TryRemove(alertKey, out var entry))
        {
            _logger.LogInformation("Throttle reset for alert key: {AlertKey}", alertKey);
        }

        return Task.CompletedTask;
    }

    public Task<ThrottleStatistics> GetThrottleStatisticsAsync(CancellationToken cancellationToken = default)
    {
        // Clean up statistics to only include top throttled keys
        var topThrottledKeys = _statistics.TopThrottledAlertKeys
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return Task.FromResult(new ThrottleStatistics
        {
            TotalAlertsReceived = _statistics.TotalAlertsReceived,
            TotalAlertsThrottled = _statistics.TotalAlertsThrottled,
            ThrottledBySeverity = new Dictionary<AlertSeverity, int>(_statistics.ThrottledBySeverity),
            TopThrottledAlertKeys = topThrottledKeys,
            LastUpdated = _statistics.LastUpdated
        });
    }

    private TimeSpan GetThrottleWindow(AlertSeverity severity)
    {
        var configKey = $"Alerting:Throttling:{severity}:WindowMinutes";
        var windowMinutes = _configuration.GetValue<int>(configKey, GetDefaultWindowMinutes(severity));
        return TimeSpan.FromMinutes(windowMinutes);
    }

    private int GetMaxOccurrences(AlertSeverity severity)
    {
        var configKey = $"Alerting:Throttling:{severity}:MaxOccurrences";
        return _configuration.GetValue<int>(configKey, GetDefaultMaxOccurrences(severity));
    }

    private static int GetDefaultWindowMinutes(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Emergency => 1,      // 1 minute window for emergency alerts
            AlertSeverity.Critical => 5,       // 5 minute window for critical alerts
            AlertSeverity.Warning => 15,       // 15 minute window for warnings
            AlertSeverity.Info => 60,          // 1 hour window for info alerts
            _ => 15
        };
    }

    private static int GetDefaultMaxOccurrences(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Emergency => 3,      // Allow 3 emergency alerts per window
            AlertSeverity.Critical => 2,       // Allow 2 critical alerts per window
            AlertSeverity.Warning => 1,        // Allow 1 warning per window
            AlertSeverity.Info => 1,           // Allow 1 info alert per window
            _ => 1
        };
    }

    private class AlertThrottleEntry
    {
        public string AlertKey { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public int Count { get; set; }
        public List<DateTime> Occurrences { get; set; } = new();
    }
}