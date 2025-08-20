using System.Text.Json;

namespace ACS.Service.Domain.Events;

/// <summary>
/// Base interface for all domain events
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Unique identifier for this event instance
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// When the event occurred
    /// </summary>
    DateTime OccurredAt { get; }
    
    /// <summary>
    /// Type of the event (used for routing and processing)
    /// </summary>
    string EventType { get; }
    
    /// <summary>
    /// Version of the event schema for evolution support
    /// </summary>
    int EventVersion { get; }
    
    /// <summary>
    /// User who triggered the event (if applicable)
    /// </summary>
    string? UserId { get; }
    
    /// <summary>
    /// Correlation ID for tracking related events
    /// </summary>
    string? CorrelationId { get; }
    
    /// <summary>
    /// Additional metadata for the event
    /// </summary>
    Dictionary<string, object> Metadata { get; }
    
    /// <summary>
    /// Serialize event to JSON for storage/transmission
    /// </summary>
    string Serialize();
}

/// <summary>
/// Base abstract class for domain events
/// </summary>
public abstract class DomainEvent : IDomainEvent
{
    public Guid EventId { get; private set; } = Guid.NewGuid();
    public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;
    public abstract string EventType { get; }
    public virtual int EventVersion { get; } = 1;
    public string? UserId { get; set; }
    public string? CorrelationId { get; set; }
    public Dictionary<string, object> Metadata { get; private set; } = new();

    public virtual string Serialize()
    {
        return JsonSerializer.Serialize(this, GetType(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }
    
    protected void AddMetadata(string key, object value)
    {
        Metadata[key] = value;
    }
    
    protected void SetCorrelationId(string correlationId)
    {
        CorrelationId = correlationId;
    }
}

/// <summary>
/// Event priority levels for processing order
/// </summary>
public enum EventPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Event categories for filtering and routing
/// </summary>
public enum EventCategory
{
    Security,
    Audit,
    Business,
    System,
    Integration,
    Performance,
    Notification
}

/// <summary>
/// Interface for events that require immediate processing
/// </summary>
public interface IHighPriorityEvent : IDomainEvent
{
    EventPriority Priority { get; }
}

/// <summary>
/// Interface for events that can be processed asynchronously
/// </summary>
public interface IAsyncEvent : IDomainEvent
{
    /// <summary>
    /// Maximum retry attempts for processing
    /// </summary>
    int MaxRetries { get; }
    
    /// <summary>
    /// Delay before retry attempts
    /// </summary>
    TimeSpan RetryDelay { get; }
}

/// <summary>
/// Interface for events that should trigger notifications
/// </summary>
public interface INotificationEvent : IDomainEvent
{
    /// <summary>
    /// Recipients for notifications
    /// </summary>
    List<string> NotificationRecipients { get; }
    
    /// <summary>
    /// Notification template to use
    /// </summary>
    string NotificationTemplate { get; }
    
    /// <summary>
    /// Notification channels (Email, SMS, Push, etc.)
    /// </summary>
    List<string> NotificationChannels { get; }
}

/// <summary>
/// Interface for events that require audit logging
/// </summary>
public interface IAuditableEvent : IDomainEvent
{
    /// <summary>
    /// Entity that was affected by this event
    /// </summary>
    string? AffectedEntityType { get; }
    
    /// <summary>
    /// ID of the affected entity
    /// </summary>
    string? AffectedEntityId { get; }
    
    /// <summary>
    /// Type of operation that occurred
    /// </summary>
    string OperationType { get; }
    
    /// <summary>
    /// Previous state (for change tracking)
    /// </summary>
    object? PreviousState { get; }
    
    /// <summary>
    /// New state (for change tracking)
    /// </summary>
    object? NewState { get; }
    
    /// <summary>
    /// Business justification for the change
    /// </summary>
    string? Justification { get; }
}

/// <summary>
/// Interface for events that represent security-related activities
/// </summary>
public interface ISecurityEvent : IDomainEvent
{
    /// <summary>
    /// Security event type (Login, Logout, PermissionChange, etc.)
    /// </summary>
    string SecurityEventType { get; }
    
    /// <summary>
    /// Risk level of the security event
    /// </summary>
    SecurityRiskLevel RiskLevel { get; }
    
    /// <summary>
    /// Source IP address
    /// </summary>
    string? SourceIpAddress { get; }
    
    /// <summary>
    /// User agent string
    /// </summary>
    string? UserAgent { get; }
    
    /// <summary>
    /// Whether this event indicates suspicious activity
    /// </summary>
    bool IsSuspicious { get; }
}

public enum SecurityRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Interface for integration events that span system boundaries
/// </summary>
public interface IIntegrationEvent : IDomainEvent
{
    /// <summary>
    /// External system that should receive this event
    /// </summary>
    string? TargetSystem { get; }
    
    /// <summary>
    /// Event schema for external consumption
    /// </summary>
    string ExternalSchema { get; }
    
    /// <summary>
    /// Whether delivery confirmation is required
    /// </summary>
    bool RequiresDeliveryConfirmation { get; }
}

/// <summary>
/// Event handler result for tracking processing outcomes
/// </summary>
public class EventHandlerResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, object> ResultData { get; set; } = new();
    
    public static EventHandlerResult Success(TimeSpan processingTime, Dictionary<string, object>? data = null)
    {
        return new EventHandlerResult
        {
            IsSuccess = true,
            ProcessingTime = processingTime,
            ResultData = data ?? new Dictionary<string, object>()
        };
    }
    
    public static EventHandlerResult Failure(string errorMessage, Exception? exception = null, TimeSpan? processingTime = null)
    {
        return new EventHandlerResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            ProcessingTime = processingTime ?? TimeSpan.Zero
        };
    }
}