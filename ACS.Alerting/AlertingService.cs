using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ACS.Alerting;

/// <summary>
/// Comprehensive alerting service implementation with throttling, routing, and delivery
/// </summary>
public class AlertingService : IAlertingService
{
    private readonly ILogger<AlertingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly INotificationRouter _notificationRouter;
    private readonly IAlertThrottler _alertThrottler;
    private readonly IAlertStorage _alertStorage;
    private readonly ConcurrentDictionary<string, AlertHistory> _activeAlerts;
    private readonly AlertingConfiguration _alertingConfig;

    public AlertingService(
        ILogger<AlertingService> logger,
        IConfiguration configuration,
        INotificationRouter notificationRouter,
        IAlertThrottler alertThrottler,
        IAlertStorage alertStorage)
    {
        _logger = logger;
        _configuration = configuration;
        _notificationRouter = notificationRouter;
        _alertThrottler = alertThrottler;
        _alertStorage = alertStorage;
        _activeAlerts = new ConcurrentDictionary<string, AlertHistory>();
        _alertingConfig = configuration.GetSection("Alerting").Get<AlertingConfiguration>() ?? new AlertingConfiguration();
    }

    public async Task SendAlertAsync(AlertRequest alert, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Processing alert: {AlertId} - {Title} ({Severity})", 
                alert.Id, alert.Title, alert.Severity);

            // Validate alert request
            if (!ValidateAlert(alert))
            {
                _logger.LogWarning("Invalid alert request: {AlertId}", alert.Id);
                return;
            }

            // Check if alert should be throttled
            var alertKey = GenerateAlertKey(alert);
            if (await ShouldThrottleAlertAsync(alertKey, alert.Severity, cancellationToken))
            {
                _logger.LogDebug("Alert throttled: {AlertId} - {AlertKey}", alert.Id, alertKey);
                return;
            }

            // Create alert history record
            var alertHistory = CreateAlertHistory(alert);
            
            // Store alert
            await _alertStorage.StoreAlertAsync(alertHistory, cancellationToken);
            
            // Add to active alerts
            _activeAlerts.TryAdd(alert.Id, alertHistory);

            // Route notifications based on severity and configuration
            var notifications = await RouteNotificationsAsync(alert, cancellationToken);
            
            // Send notifications
            var deliveryTasks = notifications.Select(async notification =>
            {
                var deliveryResult = await _notificationRouter.SendNotificationAsync(notification, cancellationToken);
                alertHistory.NotificationDeliveries.Add(deliveryResult);
                
                _logger.LogDebug("Notification sent via {Channel} for alert {AlertId}: {Success}", 
                    notification.Channel, alert.Id, deliveryResult.Success);
            });

            await Task.WhenAll(deliveryTasks);

            // Update alert with delivery results
            await _alertStorage.UpdateAlertAsync(alertHistory, cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation("Alert processed successfully: {AlertId} in {ElapsedMs}ms", 
                alert.Id, stopwatch.ElapsedMilliseconds);

            // Record metrics
            RecordAlertMetrics(alert, alertHistory.NotificationDeliveries, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to process alert: {AlertId} in {ElapsedMs}ms", 
                alert.Id, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task SendBatchAlertsAsync(IEnumerable<AlertRequest> alerts, CancellationToken cancellationToken = default)
    {
        var alertList = alerts.ToList();
        _logger.LogInformation("Processing batch of {AlertCount} alerts", alertList.Count);

        var batchTasks = alertList.Select(alert => SendAlertAsync(alert, cancellationToken));
        
        try
        {
            await Task.WhenAll(batchTasks);
            _logger.LogInformation("Successfully processed batch of {AlertCount} alerts", alertList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch of {AlertCount} alerts", alertList.Count);
            throw;
        }
    }

    public async Task<bool> ShouldThrottleAlertAsync(string alertKey, AlertSeverity severity, CancellationToken cancellationToken = default)
    {
        return await _alertThrottler.ShouldThrottleAsync(alertKey, severity, cancellationToken);
    }

    public async Task<IEnumerable<AlertHistory>> GetAlertHistoryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
    {
        return await _alertStorage.GetAlertHistoryAsync(startTime, endTime, cancellationToken);
    }

    public async Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, string? notes = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Acknowledging alert: {AlertId} by {AcknowledgedBy}", alertId, acknowledgedBy);

        try
        {
            var alert = await _alertStorage.GetAlertAsync(alertId, cancellationToken);
            if (alert == null)
            {
                _logger.LogWarning("Alert not found for acknowledgment: {AlertId}", alertId);
                return;
            }

            alert.Status = AlertStatus.Acknowledged;
            alert.AcknowledgedAt = DateTime.UtcNow;
            alert.AcknowledgedBy = acknowledgedBy;
            alert.AcknowledgmentNotes = notes;

            await _alertStorage.UpdateAlertAsync(alert, cancellationToken);

            // Update active alerts cache
            if (_activeAlerts.TryGetValue(alertId, out var activeAlert))
            {
                activeAlert.Status = AlertStatus.Acknowledged;
                activeAlert.AcknowledgedAt = alert.AcknowledgedAt;
                activeAlert.AcknowledgedBy = acknowledgedBy;
                activeAlert.AcknowledgmentNotes = notes;
            }

            _logger.LogInformation("Alert acknowledged: {AlertId}", alertId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge alert: {AlertId}", alertId);
            throw;
        }
    }

    public async Task ResolveAlertAsync(string alertId, string resolvedBy, string? resolution = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resolving alert: {AlertId} by {ResolvedBy}", alertId, resolvedBy);

        try
        {
            var alert = await _alertStorage.GetAlertAsync(alertId, cancellationToken);
            if (alert == null)
            {
                _logger.LogWarning("Alert not found for resolution: {AlertId}", alertId);
                return;
            }

            alert.Status = AlertStatus.Resolved;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolvedBy = resolvedBy;
            alert.Resolution = resolution;

            await _alertStorage.UpdateAlertAsync(alert, cancellationToken);

            // Remove from active alerts
            _activeAlerts.TryRemove(alertId, out _);

            _logger.LogInformation("Alert resolved: {AlertId}", alertId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve alert: {AlertId}", alertId);
            throw;
        }
    }

    public async Task<IEnumerable<AlertHistory>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        // Return cached active alerts and also refresh from storage
        var storageAlerts = await _alertStorage.GetActiveAlertsAsync(cancellationToken);
        
        // Update cache with any alerts from storage that we might have missed
        foreach (var alert in storageAlerts)
        {
            _activeAlerts.TryAdd(alert.Id, alert);
        }

        return _activeAlerts.Values.Where(a => a.Status == AlertStatus.Active || a.Status == AlertStatus.Acknowledged);
    }

    private bool ValidateAlert(AlertRequest alert)
    {
        if (string.IsNullOrWhiteSpace(alert.Title))
        {
            _logger.LogWarning("Alert validation failed: Title is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(alert.Message))
        {
            _logger.LogWarning("Alert validation failed: Message is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(alert.Source))
        {
            _logger.LogWarning("Alert validation failed: Source is required");
            return false;
        }

        return true;
    }

    private string GenerateAlertKey(AlertRequest alert)
    {
        // Generate a key for throttling based on source, category, and a hash of the title
        var titleHash = alert.Title.GetHashCode();
        return $"{alert.Source}:{alert.Category}:{titleHash:x8}";
    }

    private AlertHistory CreateAlertHistory(AlertRequest alert)
    {
        return new AlertHistory
        {
            Id = alert.Id,
            Title = alert.Title,
            Message = alert.Message,
            Severity = alert.Severity,
            Category = alert.Category,
            Source = alert.Source,
            TenantId = alert.TenantId,
            UserId = alert.UserId,
            Metadata = new Dictionary<string, object>(alert.Metadata),
            CreatedAt = alert.Timestamp,
            Status = AlertStatus.Active,
            Tags = new List<string>(alert.Tags),
            NotificationDeliveries = new List<NotificationDelivery>()
        };
    }

    private async Task<List<NotificationRequest>> RouteNotificationsAsync(AlertRequest alert, CancellationToken cancellationToken)
    {
        var notifications = new List<NotificationRequest>();

        // Use configured channels from alert or fall back to default routing based on severity
        var channels = alert.NotificationChannels.Any() 
            ? alert.NotificationChannels 
            : GetDefaultChannelsForSeverity(alert.Severity);

        foreach (var channel in channels)
        {
            var targets = await GetNotificationTargetsAsync(channel, alert, cancellationToken);
            
            foreach (var target in targets)
            {
                notifications.Add(new NotificationRequest
                {
                    Channel = channel,
                    Target = target,
                    Subject = FormatAlertSubject(alert),
                    Message = FormatAlertMessage(alert),
                    Priority = MapSeverityToPriority(alert.Severity),
                    Metadata = alert.Metadata
                });
            }
        }

        return notifications;
    }

    private List<NotificationChannel> GetDefaultChannelsForSeverity(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Emergency => new[] { NotificationChannel.Email, NotificationChannel.SMS, NotificationChannel.Slack, NotificationChannel.PagerDuty }.ToList(),
            AlertSeverity.Critical => new[] { NotificationChannel.Email, NotificationChannel.Slack, NotificationChannel.Teams }.ToList(),
            AlertSeverity.Warning => new[] { NotificationChannel.Email, NotificationChannel.Slack }.ToList(),
            AlertSeverity.Info => new[] { NotificationChannel.LogFile }.ToList(),
            _ => new[] { NotificationChannel.LogFile }.ToList()
        };
    }

    private Task<List<string>> GetNotificationTargetsAsync(NotificationChannel channel, AlertRequest alert, CancellationToken cancellationToken)
    {
        // In a real implementation, this would look up targets from configuration or a database
        // For now, return configured default targets
        
        var configKey = $"Alerting:Channels:{channel}:Targets";
        var configuredTargets = _configuration.GetSection(configKey).Get<string[]>();
        
        if (configuredTargets?.Any() == true)
        {
            return Task.FromResult(configuredTargets.ToList());
        }

        // Fallback defaults
        var result = channel switch
        {
            NotificationChannel.Email => new[] { "admin@company.com", "ops@company.com" }.ToList(),
            NotificationChannel.Slack => new[] { "#alerts", "#ops" }.ToList(),
            NotificationChannel.Teams => new[] { "https://webhook.office.com/webhookb2/..." }.ToList(),
            NotificationChannel.LogFile => new[] { "/var/log/alerts.log" }.ToList(),
            _ => new List<string>()
        };
        return Task.FromResult(result);
    }

    private string FormatAlertSubject(AlertRequest alert)
    {
        var prefix = alert.Severity switch
        {
            AlertSeverity.Emergency => "🚨 EMERGENCY",
            AlertSeverity.Critical => "❗ CRITICAL",
            AlertSeverity.Warning => "⚠️ WARNING",
            AlertSeverity.Info => "ℹ️ INFO",
            _ => "📋 ALERT"
        };

        return $"{prefix}: {alert.Title} [{alert.Source}]";
    }

    private string FormatAlertMessage(AlertRequest alert)
    {
        var message = $"""
            **Alert Details**
            - **Title**: {alert.Title}
            - **Severity**: {alert.Severity}
            - **Category**: {alert.Category}
            - **Source**: {alert.Source}
            - **Time**: {alert.Timestamp:yyyy-MM-dd HH:mm:ss UTC}
            {(alert.TenantId != null ? $"- **Tenant**: {alert.TenantId}" : "")}
            {(alert.UserId != null ? $"- **User**: {alert.UserId}" : "")}

            **Message**: {alert.Message}

            {(alert.Tags.Any() ? $"**Tags**: {string.Join(", ", alert.Tags)}" : "")}
            
            {(alert.Metadata.Any() ? "**Additional Information**:" : "")}
            {string.Join("\n", alert.Metadata.Select(kvp => $"- **{kvp.Key}**: {kvp.Value}"))}
            """;

        return message;
    }

    private NotificationPriority MapSeverityToPriority(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Emergency => NotificationPriority.Emergency,
            AlertSeverity.Critical => NotificationPriority.High,
            AlertSeverity.Warning => NotificationPriority.Medium,
            AlertSeverity.Info => NotificationPriority.Low,
            _ => NotificationPriority.Low
        };
    }

    private void RecordAlertMetrics(AlertRequest alert, List<NotificationDelivery> deliveries, TimeSpan processingTime)
    {
        // Record metrics for monitoring alert system performance
        _logger.LogDebug("Alert metrics - ID: {AlertId}, Severity: {Severity}, ProcessingTime: {ProcessingTimeMs}ms, " +
                        "Notifications: {NotificationCount}, Successful: {SuccessfulDeliveries}",
            alert.Id, alert.Severity, processingTime.TotalMilliseconds,
            deliveries.Count, deliveries.Count(d => d.Success));
    }
}

/// <summary>
/// Configuration for the alerting system
/// </summary>
public class AlertingConfiguration
{
    public bool Enabled { get; set; } = true;
    public int MaxConcurrentAlerts { get; set; } = 100;
    public TimeSpan DefaultThrottleWindow { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxNotificationsPerAlert { get; set; } = 10;
    public TimeSpan AlertExpiration { get; set; } = TimeSpan.FromDays(30);
    public Dictionary<string, object> ChannelConfigurations { get; set; } = new();
}