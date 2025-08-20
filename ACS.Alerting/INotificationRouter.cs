namespace ACS.Alerting;

/// <summary>
/// Interface for routing and delivering notifications through various channels
/// </summary>
public interface INotificationRouter
{
    /// <summary>
    /// Send a notification through the specified channel
    /// </summary>
    Task<NotificationDelivery> SendNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connectivity to a notification channel
    /// </summary>
    Task<bool> TestChannelAsync(NotificationChannel channel, string target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available notification channels
    /// </summary>
    Task<IEnumerable<NotificationChannel>> GetAvailableChannelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Notification request
/// </summary>
public class NotificationRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public NotificationChannel Channel { get; set; }
    public string Target { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Medium;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan? DeliveryTimeout { get; set; }
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Notification priority levels
/// </summary>
public enum NotificationPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Emergency = 4
}