namespace ACS.Service.Domain.Events;

/// <summary>
/// Interface for publishing domain events
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publishes a single domain event
    /// </summary>
    /// <param name="domainEvent">Event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Publishes multiple domain events
    /// </summary>
    /// <param name="domainEvents">Events to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishBatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Publishes an event with priority
    /// </summary>
    /// <param name="domainEvent">Event to publish</param>
    /// <param name="priority">Priority level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishWithPriorityAsync(IDomainEvent domainEvent, EventPriority priority, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Schedules an event to be published at a later time
    /// </summary>
    /// <param name="domainEvent">Event to schedule</param>
    /// <param name="scheduledFor">When to publish the event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ScheduleAsync(IDomainEvent domainEvent, DateTime scheduledFor, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Publishes an event and waits for all handlers to complete
    /// </summary>
    /// <param name="domainEvent">Event to publish</param>
    /// <param name="timeout">Maximum time to wait</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results from all handlers</returns>
    Task<Dictionary<string, EventHandlerResult>> PublishAndWaitAsync(IDomainEvent domainEvent, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for storing domain events
/// </summary>
public interface IDomainEventStore
{
    /// <summary>
    /// Stores an event for processing
    /// </summary>
    /// <param name="domainEvent">Event to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StoreAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stores multiple events
    /// </summary>
    /// <param name="domainEvents">Events to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StoreBatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves events for processing
    /// </summary>
    /// <param name="batchSize">Maximum number of events to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IEnumerable<StoredEvent>> GetPendingEventsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks an event as processed
    /// </summary>
    /// <param name="eventId">ID of the event</param>
    /// <param name="result">Processing result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsProcessedAsync(Guid eventId, EventHandlerResult result, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks an event as failed
    /// </summary>
    /// <param name="eventId">ID of the event</param>
    /// <param name="error">Error information</param>
    /// <param name="retryCount">Current retry count</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsFailedAsync(Guid eventId, string error, int retryCount, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets event processing history
    /// </summary>
    /// <param name="eventId">ID of the event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<EventProcessingHistory?> GetProcessingHistoryAsync(Guid eventId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets events by correlation ID
    /// </summary>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IEnumerable<StoredEvent>> GetEventsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets events within a date range
    /// </summary>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="eventTypes">Optional filter by event types</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IEnumerable<StoredEvent>> GetEventsAsync(DateTime startDate, DateTime endDate, IEnumerable<string>? eventTypes = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cleans up old processed events
    /// </summary>
    /// <param name="olderThan">Remove events older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stored event information
/// </summary>
public class StoredEvent
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int EventVersion { get; set; }
    public string EventData { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime StoredAt { get; set; }
    public string? UserId { get; set; }
    public string? CorrelationId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public EventStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public EventPriority Priority { get; set; } = EventPriority.Normal;
    
    public IDomainEvent? DeserializeEvent()
    {
        try
        {
            var eventType = Type.GetType(EventType);
            if (eventType == null) return null;
            
            return System.Text.Json.JsonSerializer.Deserialize(EventData, eventType) as IDomainEvent;
        }
        catch
        {
            return null;
        }
    }
}

public enum EventStatus
{
    Pending,
    Processing,
    Processed,
    Failed,
    Scheduled,
    Cancelled
}

/// <summary>
/// Event processing history
/// </summary>
public class EventProcessingHistory
{
    public Guid EventId { get; set; }
    public List<EventProcessingAttempt> ProcessingAttempts { get; set; } = new();
    public DateTime FirstAttemptAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public EventStatus FinalStatus { get; set; }
    public int TotalAttempts { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public List<string> HandlersExecuted { get; set; } = new();
    public Dictionary<string, object> ProcessingMetadata { get; set; } = new();
}

/// <summary>
/// Individual processing attempt
/// </summary>
public class EventProcessingAttempt
{
    public int AttemptNumber { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt - StartedAt;
    public EventStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public List<HandlerExecutionResult> HandlerResults { get; set; } = new();
    public Dictionary<string, object> AttemptMetadata { get; set; } = new();
}

/// <summary>
/// Result of executing a specific handler
/// </summary>
public class HandlerExecutionResult
{
    public string HandlerName { get; set; } = string.Empty;
    public Type HandlerType { get; set; } = null!;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public DateTime ExecutedAt { get; set; }
    public Dictionary<string, object> HandlerMetadata { get; set; } = new();
}

/// <summary>
/// Interface for event notification services
/// </summary>
public interface IEventNotificationService
{
    /// <summary>
    /// Sends notifications for notification events
    /// </summary>
    /// <param name="notificationEvent">Event that requires notifications</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendNotificationsAsync(INotificationEvent notificationEvent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Registers a notification channel
    /// </summary>
    /// <param name="channelName">Name of the channel</param>
    /// <param name="channel">Channel implementation</param>
    void RegisterChannel(string channelName, INotificationChannel channel);
    
    /// <summary>
    /// Gets available notification channels
    /// </summary>
    IEnumerable<string> GetAvailableChannels();
}

/// <summary>
/// Interface for notification channels
/// </summary>
public interface INotificationChannel
{
    /// <summary>
    /// Channel name
    /// </summary>
    string ChannelName { get; }
    
    /// <summary>
    /// Whether this channel is currently available
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// Sends a notification through this channel
    /// </summary>
    /// <param name="recipients">Notification recipients</param>
    /// <param name="template">Template to use</param>
    /// <param name="eventData">Event data for template</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendAsync(IEnumerable<string> recipients, string template, IDomainEvent eventData, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for event streaming to external systems
/// </summary>
public interface IEventStreamingService
{
    /// <summary>
    /// Streams an integration event to external systems
    /// </summary>
    /// <param name="integrationEvent">Event to stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StreamEventAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets streaming statistics
    /// </summary>
    Task<EventStreamingStatistics> GetStatisticsAsync();
}

/// <summary>
/// Event streaming statistics
/// </summary>
public class EventStreamingStatistics
{
    public int EventsStreamed { get; set; }
    public int EventsFailed { get; set; }
    public Dictionary<string, int> EventsByTarget { get; set; } = new();
    public DateTime? LastEventAt { get; set; }
    public TimeSpan AverageStreamingTime { get; set; }
    public Dictionary<string, int> ErrorsByType { get; set; } = new();
}