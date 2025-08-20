namespace ACS.Service.Domain.Events;

/// <summary>
/// Base interface for domain event handlers
/// </summary>
public interface IDomainEventHandler
{
    /// <summary>
    /// Types of events this handler can process
    /// </summary>
    IEnumerable<Type> HandledEventTypes { get; }
    
    /// <summary>
    /// Priority for handler execution (higher values execute first)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Whether this handler can process events in parallel
    /// </summary>
    bool SupportsParallelProcessing { get; }
}

/// <summary>
/// Generic interface for typed domain event handlers
/// </summary>
/// <typeparam name="TEvent">Type of event this handler processes</typeparam>
public interface IDomainEventHandler<in TEvent> : IDomainEventHandler where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles the domain event
    /// </summary>
    /// <param name="domainEvent">The event to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of handling the event</returns>
    Task<EventHandlerResult> HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base abstract class for domain event handlers
/// </summary>
/// <typeparam name="TEvent">Type of event this handler processes</typeparam>
public abstract class DomainEventHandler<TEvent> : IDomainEventHandler<TEvent> where TEvent : IDomainEvent
{
    public virtual IEnumerable<Type> HandledEventTypes => new[] { typeof(TEvent) };
    public virtual int Priority => 100;
    public virtual bool SupportsParallelProcessing => true;

    public abstract Task<EventHandlerResult> HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for event handlers that require database access
/// </summary>
public interface IRequiresDbContext
{
    /// <summary>
    /// Sets the database context for the handler
    /// </summary>
    void SetDbContext(object dbContext);
}

/// <summary>
/// Interface for event handlers that support batching
/// </summary>
/// <typeparam name="TEvent">Type of events to batch</typeparam>
public interface IBatchEventHandler<in TEvent> : IDomainEventHandler where TEvent : IDomainEvent
{
    /// <summary>
    /// Maximum batch size for processing events
    /// </summary>
    int MaxBatchSize { get; }
    
    /// <summary>
    /// Maximum time to wait for batching events
    /// </summary>
    TimeSpan BatchTimeout { get; }
    
    /// <summary>
    /// Handles a batch of events
    /// </summary>
    /// <param name="events">Events to process in batch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results for each event</returns>
    Task<Dictionary<TEvent, EventHandlerResult>> HandleBatchAsync(IEnumerable<TEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for event handlers that support conditional processing
/// </summary>
/// <typeparam name="TEvent">Type of event</typeparam>
public interface IConditionalEventHandler<in TEvent> : IDomainEventHandler<TEvent> where TEvent : IDomainEvent
{
    /// <summary>
    /// Determines if this handler should process the given event
    /// </summary>
    /// <param name="domainEvent">Event to evaluate</param>
    /// <returns>True if the event should be processed by this handler</returns>
    Task<bool> ShouldHandleAsync(TEvent domainEvent);
}

/// <summary>
/// Interface for event handlers that can retry failed operations
/// </summary>
public interface IRetryableEventHandler
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    int MaxRetryAttempts { get; }
    
    /// <summary>
    /// Delay between retry attempts
    /// </summary>
    TimeSpan RetryDelay { get; }
    
    /// <summary>
    /// Determines if an exception should trigger a retry
    /// </summary>
    /// <param name="exception">Exception that occurred</param>
    /// <returns>True if the operation should be retried</returns>
    bool ShouldRetry(Exception exception);
}

/// <summary>
/// Interface for event handlers that need to run in a specific order
/// </summary>
public interface IOrderedEventHandler
{
    /// <summary>
    /// Execution order (lower values execute first)
    /// </summary>
    int Order { get; }
}

/// <summary>
/// Interface for event handlers that perform cleanup operations
/// </summary>
public interface ICleanupEventHandler : IDomainEventHandler
{
    /// <summary>
    /// Performs cleanup after event processing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for handlers that require special security context
/// </summary>
public interface ISecurityEventHandler : IDomainEventHandler
{
    /// <summary>
    /// Required security clearance level
    /// </summary>
    string RequiredSecurityLevel { get; }
    
    /// <summary>
    /// Whether this handler processes sensitive data
    /// </summary>
    bool ProcessesSensitiveData { get; }
}

/// <summary>
/// Event handler registration information
/// </summary>
public class EventHandlerRegistration
{
    public Type HandlerType { get; set; } = null!;
    public Type EventType { get; set; } = null!;
    public int Priority { get; set; }
    public bool SupportsParallelProcessing { get; set; }
    public string? HandlerName { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Event processing context
/// </summary>
public class EventProcessingContext
{
    public IDomainEvent Event { get; set; } = null!;
    public string? CorrelationId { get; set; }
    public DateTime ProcessingStartedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ProcessingMetadata { get; set; } = new();
    public int AttemptCount { get; set; } = 1;
    public List<string> ProcessingLog { get; set; } = new();
    public CancellationToken CancellationToken { get; set; }
    
    public void LogProcessingStep(string step)
    {
        ProcessingLog.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {step}");
    }
}

/// <summary>
/// Event handler execution statistics
/// </summary>
public class EventHandlerStatistics
{
    public string HandlerName { get; set; } = string.Empty;
    public Type HandlerType { get; set; } = null!;
    public int EventsProcessed { get; set; }
    public int EventsSucceeded { get; set; }
    public int EventsFailed { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public TimeSpan AverageProcessingTime => EventsProcessed > 0 ? 
        TimeSpan.FromMilliseconds(TotalProcessingTime.TotalMilliseconds / EventsProcessed) : 
        TimeSpan.Zero;
    public DateTime FirstEventAt { get; set; }
    public DateTime LastEventAt { get; set; }
    public Dictionary<string, int> EventTypeCounts { get; set; } = new();
    public Dictionary<string, int> ErrorCounts { get; set; } = new();
}

/// <summary>
/// Event processing pipeline configuration
/// </summary>
public class EventProcessingPipelineConfig
{
    public int MaxConcurrentHandlers { get; set; } = 10;
    public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableMetrics { get; set; } = true;
    public bool EnableDetailedLogging { get; set; } = false;
    public Dictionary<Type, int> EventTypeMaxRetries { get; set; } = new();
    public Dictionary<Type, TimeSpan> EventTypeTimeouts { get; set; } = new();
    public bool StopProcessingOnCriticalError { get; set; } = true;
    public bool EnableCircuitBreaker { get; set; } = true;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(1);
}