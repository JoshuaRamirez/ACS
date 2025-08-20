using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ACS.Alerting;

/// <summary>
/// In-memory implementation of alert storage for development and testing
/// </summary>
public class InMemoryAlertStorage : IAlertStorage
{
    private readonly ILogger<InMemoryAlertStorage> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, AlertHistory> _alerts;
    private readonly object _lockObject = new();

    public InMemoryAlertStorage(ILogger<InMemoryAlertStorage> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _alerts = new ConcurrentDictionary<string, AlertHistory>();
    }

    public async Task StoreAlertAsync(AlertHistory alert, CancellationToken cancellationToken = default)
    {
        if (alert == null) throw new ArgumentNullException(nameof(alert));

        _alerts.AddOrUpdate(alert.Id, alert, (key, existing) =>
        {
            _logger.LogWarning("Alert with ID {AlertId} already exists, updating", alert.Id);
            return alert;
        });

        _logger.LogDebug("Stored alert {AlertId} with severity {Severity}", alert.Id, alert.Severity);

        await Task.CompletedTask;
    }

    public async Task UpdateAlertAsync(AlertHistory alert, CancellationToken cancellationToken = default)
    {
        if (alert == null) throw new ArgumentNullException(nameof(alert));

        if (!_alerts.ContainsKey(alert.Id))
        {
            _logger.LogWarning("Alert with ID {AlertId} not found for update", alert.Id);
            return;
        }

        _alerts.TryUpdate(alert.Id, alert, _alerts[alert.Id]);
        _logger.LogDebug("Updated alert {AlertId}", alert.Id);

        await Task.CompletedTask;
    }

    public async Task<AlertHistory?> GetAlertAsync(string alertId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(alertId)) return null;

        _alerts.TryGetValue(alertId, out var alert);
        return await Task.FromResult(alert);
    }

    public async Task<IEnumerable<AlertHistory>> GetAlertHistoryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
    {
        var alerts = _alerts.Values
            .Where(a => a.CreatedAt >= startTime && a.CreatedAt <= endTime)
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        return await Task.FromResult(alerts);
    }

    public async Task<IEnumerable<AlertHistory>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        var activeAlerts = _alerts.Values
            .Where(a => a.Status == AlertStatus.Active)
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        return await Task.FromResult(activeAlerts);
    }

    public async Task<IEnumerable<AlertHistory>> GetAlertsByTenantAsync(string tenantId, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var query = _alerts.Values.Where(a => a.TenantId == tenantId);
        
        if (since.HasValue)
            query = query.Where(a => a.CreatedAt >= since.Value);

        var alerts = query.OrderByDescending(a => a.CreatedAt).ToList();
        return await Task.FromResult(alerts);
    }

    public async Task<IEnumerable<AlertHistory>> GetAlertsBySeverityAsync(AlertSeverity severity, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var query = _alerts.Values.Where(a => a.Severity == severity);
        
        if (since.HasValue)
            query = query.Where(a => a.CreatedAt >= since.Value);

        var alerts = query.OrderByDescending(a => a.CreatedAt).ToList();
        return await Task.FromResult(alerts);
    }

    public async Task<AlertStatistics> GetAlertStatisticsAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
    {
        var alertsInRange = _alerts.Values
            .Where(a => a.CreatedAt >= startTime && a.CreatedAt <= endTime)
            .ToList();

        var statistics = new AlertStatistics
        {
            TotalAlerts = alertsInRange.Count,
            StartTime = startTime,
            EndTime = endTime,
            GeneratedAt = DateTime.UtcNow
        };

        // Group by severity
        foreach (var severityGroup in alertsInRange.GroupBy(a => a.Severity))
        {
            statistics.AlertsBySeverity[severityGroup.Key] = severityGroup.Count();
        }

        // Group by category
        foreach (var categoryGroup in alertsInRange.GroupBy(a => a.Category))
        {
            statistics.AlertsByCategory[categoryGroup.Key] = categoryGroup.Count();
        }

        // Group by source
        foreach (var sourceGroup in alertsInRange.GroupBy(a => a.Source))
        {
            statistics.AlertsBySource[sourceGroup.Key] = sourceGroup.Count();
        }

        // Group by tenant
        foreach (var tenantGroup in alertsInRange.GroupBy(a => a.TenantId))
        {
            if (!string.IsNullOrEmpty(tenantGroup.Key))
                statistics.AlertsByTenant[tenantGroup.Key] = tenantGroup.Count();
        }

        // Count by status
        statistics.ActiveAlerts = alertsInRange.Count(a => a.Status == AlertStatus.Active);
        statistics.AcknowledgedAlerts = alertsInRange.Count(a => a.Status == AlertStatus.Acknowledged);
        statistics.ResolvedAlerts = alertsInRange.Count(a => a.Status == AlertStatus.Resolved);

        // Calculate average resolution and acknowledgment times
        var resolvedAlerts = alertsInRange.Where(a => a.Status == AlertStatus.Resolved && a.ResolvedAt.HasValue).ToList();
        if (resolvedAlerts.Any())
        {
            statistics.AverageResolutionTimeMinutes = resolvedAlerts
                .Average(a => (a.ResolvedAt!.Value - a.CreatedAt).TotalMinutes);
        }

        var acknowledgedAlerts = alertsInRange.Where(a => a.AcknowledgedAt.HasValue).ToList();
        if (acknowledgedAlerts.Any())
        {
            statistics.AverageAcknowledgmentTimeMinutes = acknowledgedAlerts
                .Average(a => (a.AcknowledgedAt!.Value - a.CreatedAt).TotalMinutes);
        }

        return await Task.FromResult(statistics);
    }

    public async Task CleanupOldAlertsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow - retentionPeriod;
        var alertsToRemove = _alerts.Values
            .Where(a => a.CreatedAt < cutoffTime && a.Status == AlertStatus.Resolved)
            .Select(a => a.Id)
            .ToList();

        int removedCount = 0;
        foreach (var alertId in alertsToRemove)
        {
            if (_alerts.TryRemove(alertId, out var _))
                removedCount++;
        }

        if (removedCount > 0)
        {
            _logger.LogInformation("Cleaned up {RemovedCount} old alerts older than {CutoffTime}", 
                removedCount, cutoffTime);
        }

        await Task.CompletedTask;
    }

    public async Task<IEnumerable<AlertHistory>> SearchAlertsAsync(AlertSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        if (criteria == null) return Enumerable.Empty<AlertHistory>();

        var query = _alerts.Values.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(criteria.TextSearch))
        {
            var searchTerm = criteria.TextSearch.ToLowerInvariant();
            query = query.Where(a => 
                a.Title.ToLowerInvariant().Contains(searchTerm) ||
                a.Message.ToLowerInvariant().Contains(searchTerm) ||
                a.Source.ToLowerInvariant().Contains(searchTerm));
        }

        if (criteria.Severities?.Any() == true)
            query = query.Where(a => criteria.Severities.Contains(a.Severity));

        if (criteria.Categories?.Any() == true)
            query = query.Where(a => criteria.Categories.Contains(a.Category));

        if (criteria.Statuses?.Any() == true)
            query = query.Where(a => criteria.Statuses.Contains(a.Status));

        if (criteria.Sources?.Any() == true)
            query = query.Where(a => criteria.Sources.Contains(a.Source));

        if (criteria.TenantIds?.Any() == true)
            query = query.Where(a => !string.IsNullOrEmpty(a.TenantId) && criteria.TenantIds.Contains(a.TenantId));

        if (criteria.UserIds?.Any() == true)
            query = query.Where(a => !string.IsNullOrEmpty(a.UserId) && criteria.UserIds.Contains(a.UserId));

        if (criteria.Tags?.Any() == true)
        {
            query = query.Where(a => a.Tags.Any(tag => criteria.Tags.Contains(tag)));
        }

        if (criteria.StartTime.HasValue)
            query = query.Where(a => a.CreatedAt >= criteria.StartTime.Value);

        if (criteria.EndTime.HasValue)
            query = query.Where(a => a.CreatedAt <= criteria.EndTime.Value);

        // Apply ordering
        query = !string.IsNullOrEmpty(criteria.OrderBy) ? criteria.OrderBy.ToLowerInvariant() switch
        {
            "createdat" => criteria.OrderDescending ? 
                query.OrderByDescending(a => a.CreatedAt) : 
                query.OrderBy(a => a.CreatedAt),
            "severity" => criteria.OrderDescending ? 
                query.OrderByDescending(a => a.Severity) : 
                query.OrderBy(a => a.Severity),
            "title" => criteria.OrderDescending ? 
                query.OrderByDescending(a => a.Title) : 
                query.OrderBy(a => a.Title),
            "source" => criteria.OrderDescending ? 
                query.OrderByDescending(a => a.Source) : 
                query.OrderBy(a => a.Source),
            _ => criteria.OrderDescending ? 
                query.OrderByDescending(a => a.CreatedAt) : 
                query.OrderBy(a => a.CreatedAt)
        } : query.OrderByDescending(a => a.CreatedAt);

        // Apply pagination
        if (criteria.Skip.HasValue)
            query = query.Skip(criteria.Skip.Value);

        if (criteria.Take.HasValue)
            query = query.Take(criteria.Take.Value);

        var results = query.ToList();
        return await Task.FromResult(results);
    }
}