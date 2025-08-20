using ACS.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Domain.Events.EventHandlers;

/// <summary>
/// Handler for auditing domain events
/// </summary>
public class AuditEventHandler : IDomainEventHandler<IAuditableEvent>, IRequiresDbContext
{
    private readonly ILogger<AuditEventHandler> _logger;
    private ApplicationDbContext _dbContext = null!;

    public IEnumerable<Type> HandledEventTypes => new[] { typeof(IAuditableEvent) };
    public int Priority => 1000; // High priority for auditing
    public bool SupportsParallelProcessing => false; // Sequential for data consistency

    public AuditEventHandler(ILogger<AuditEventHandler> logger)
    {
        _logger = logger;
    }

    public void SetDbContext(object dbContext)
    {
        _dbContext = (ApplicationDbContext)dbContext;
    }

    public async Task<EventHandlerResult> HandleAsync(IAuditableEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing audit event: {EventType} for entity {EntityType}:{EntityId}",
                domainEvent.EventType, domainEvent.AffectedEntityType, domainEvent.AffectedEntityId);

            // Create audit log entry
            var auditLog = new Data.Models.AuditLog
            {
                EventId = domainEvent.EventId.ToString(),
                EventType = domainEvent.EventType,
                EntityType = domainEvent.AffectedEntityType,
                EntityId = domainEvent.AffectedEntityId,
                OperationType = domainEvent.OperationType,
                ChangedBy = domainEvent.UserId ?? "System",
                ChangeDate = domainEvent.OccurredAt,
                Justification = domainEvent.Justification,
                CorrelationId = domainEvent.CorrelationId
            };

            // Serialize state changes
            if (domainEvent.PreviousState != null)
            {
                auditLog.OldValues = System.Text.Json.JsonSerializer.Serialize(domainEvent.PreviousState);
            }

            if (domainEvent.NewState != null)
            {
                auditLog.NewValues = System.Text.Json.JsonSerializer.Serialize(domainEvent.NewState);
            }

            // Add metadata
            if (domainEvent.Metadata.Any())
            {
                var metadata = new Dictionary<string, object>(domainEvent.Metadata);
                auditLog.AdditionalData = System.Text.Json.JsonSerializer.Serialize(metadata);
            }

            // Add security context if available
            if (domainEvent is ISecurityEvent securityEvent)
            {
                var securityContext = new Dictionary<string, object>
                {
                    ["SecurityEventType"] = securityEvent.SecurityEventType,
                    ["RiskLevel"] = securityEvent.RiskLevel.ToString(),
                    ["IsSuspicious"] = securityEvent.IsSuspicious
                };

                if (!string.IsNullOrEmpty(securityEvent.SourceIpAddress))
                    securityContext["SourceIpAddress"] = securityEvent.SourceIpAddress;

                if (!string.IsNullOrEmpty(securityEvent.UserAgent))
                    securityContext["UserAgent"] = securityEvent.UserAgent;

                var existingData = auditLog.AdditionalData ?? "{}";
                var existingDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(existingData) ?? new();
                existingDict["SecurityContext"] = securityContext;
                auditLog.AdditionalData = System.Text.Json.JsonSerializer.Serialize(existingDict);
            }

            // Save to database
            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug("Audit event processed successfully in {ElapsedMs}ms: {EventId}",
                stopwatch.ElapsedMilliseconds, domainEvent.EventId);

            return EventHandlerResult.Success(stopwatch.Elapsed, new Dictionary<string, object>
            {
                ["AuditLogId"] = auditLog.Id,
                ["ProcessingTimeMs"] = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Error processing audit event {EventId}: {ErrorMessage}",
                domainEvent.EventId, ex.Message);

            return EventHandlerResult.Failure(
                $"Failed to process audit event: {ex.Message}",
                ex,
                stopwatch.Elapsed);
        }
    }
}

/// <summary>
/// Handler for security events that require immediate attention
/// </summary>
public class SecurityEventHandler : DomainEventHandler<ISecurityEvent>, IRequiresDbContext
{
    private readonly ILogger<SecurityEventHandler> _logger;
    private ApplicationDbContext _dbContext = null!;

    public override int Priority => 900; // Very high priority for security
    public override bool SupportsParallelProcessing => true;

    public SecurityEventHandler(ILogger<SecurityEventHandler> logger)
    {
        _logger = logger;
    }

    public void SetDbContext(object dbContext)
    {
        _dbContext = (ApplicationDbContext)dbContext;
    }

    public override async Task<EventHandlerResult> HandleAsync(ISecurityEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogWarning("Processing security event: {SecurityEventType} with risk level {RiskLevel}",
                domainEvent.SecurityEventType, domainEvent.RiskLevel);

            // Log security event details
            var securityDetails = new
            {
                EventId = domainEvent.EventId,
                EventType = domainEvent.EventType,
                SecurityEventType = domainEvent.SecurityEventType,
                RiskLevel = domainEvent.RiskLevel,
                IsSuspicious = domainEvent.IsSuspicious,
                SourceIpAddress = domainEvent.SourceIpAddress,
                UserAgent = domainEvent.UserAgent,
                UserId = domainEvent.UserId,
                OccurredAt = domainEvent.OccurredAt,
                Metadata = domainEvent.Metadata
            };

            // For high-risk events, create security incident
            if (domainEvent.RiskLevel >= SecurityRiskLevel.High)
            {
                await CreateSecurityIncidentAsync(domainEvent, cancellationToken);
            }

            // For suspicious events, increment threat counter
            if (domainEvent.IsSuspicious)
            {
                await IncrementThreatCounterAsync(domainEvent, cancellationToken);
            }

            // Log security metrics
            await LogSecurityMetricsAsync(domainEvent, cancellationToken);

            stopwatch.Stop();

            return EventHandlerResult.Success(stopwatch.Elapsed, new Dictionary<string, object>
            {
                ["SecurityEventType"] = domainEvent.SecurityEventType,
                ["RiskLevel"] = domainEvent.RiskLevel.ToString(),
                ["ProcessingTimeMs"] = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Error processing security event {EventId}: {ErrorMessage}",
                domainEvent.EventId, ex.Message);

            return EventHandlerResult.Failure(
                $"Failed to process security event: {ex.Message}",
                ex,
                stopwatch.Elapsed);
        }
    }

    private async Task CreateSecurityIncidentAsync(ISecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        // In a real implementation, this would create a security incident record
        _logger.LogWarning("Creating security incident for event {EventId} with risk level {RiskLevel}",
            securityEvent.EventId, securityEvent.RiskLevel);

        // Create incident record (simplified)
        var incident = new
        {
            IncidentId = Guid.NewGuid(),
            EventId = securityEvent.EventId,
            IncidentType = securityEvent.SecurityEventType,
            RiskLevel = securityEvent.RiskLevel,
            Status = "Open",
            CreatedAt = DateTime.UtcNow,
            SourceIp = securityEvent.SourceIpAddress,
            UserId = securityEvent.UserId,
            IsSuspicious = securityEvent.IsSuspicious
        };

        // Log incident creation
        _logger.LogWarning("Security incident created: {IncidentData}",
            System.Text.Json.JsonSerializer.Serialize(incident));
    }

    private async Task IncrementThreatCounterAsync(ISecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        // In a real implementation, this would update threat counters in the database
        _logger.LogInformation("Incrementing threat counter for suspicious event {EventId}", securityEvent.EventId);
    }

    private async Task LogSecurityMetricsAsync(ISecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        // In a real implementation, this would log metrics to monitoring system
        var metrics = new Dictionary<string, object>
        {
            ["security_event_count"] = 1,
            ["risk_level"] = securityEvent.RiskLevel.ToString(),
            ["event_type"] = securityEvent.SecurityEventType,
            ["is_suspicious"] = securityEvent.IsSuspicious ? 1 : 0
        };

        _logger.LogInformation("Security metrics logged: {Metrics}",
            System.Text.Json.JsonSerializer.Serialize(metrics));
    }
}

/// <summary>
/// Handler for high priority events that need immediate processing
/// </summary>
public class HighPriorityEventHandler : IDomainEventHandler<IHighPriorityEvent>
{
    private readonly ILogger<HighPriorityEventHandler> _logger;
    private readonly IEventNotificationService? _notificationService;

    public IEnumerable<Type> HandledEventTypes => new[] { typeof(IHighPriorityEvent) };
    public int Priority => 800; // High priority
    public bool SupportsParallelProcessing => true;

    public HighPriorityEventHandler(
        ILogger<HighPriorityEventHandler> logger,
        IEventNotificationService? notificationService = null)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<EventHandlerResult> HandleAsync(IHighPriorityEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogWarning("Processing high priority event: {EventType} with priority {Priority}",
                domainEvent.EventType, domainEvent.Priority);

            // Send immediate notifications if this is also a notification event
            if (domainEvent is INotificationEvent notificationEvent && _notificationService != null)
            {
                await _notificationService.SendNotificationsAsync(notificationEvent, cancellationToken);
            }

            // Log high priority event for monitoring
            var priorityEventData = new Dictionary<string, object>
            {
                ["EventId"] = domainEvent.EventId,
                ["EventType"] = domainEvent.EventType,
                ["Priority"] = domainEvent.Priority.ToString(),
                ["OccurredAt"] = domainEvent.OccurredAt,
                ["UserId"] = domainEvent.UserId ?? "System",
                ["CorrelationId"] = domainEvent.CorrelationId ?? "None"
            };

            // Add custom metadata
            foreach (var metadata in domainEvent.Metadata)
            {
                priorityEventData[$"Metadata_{metadata.Key}"] = metadata.Value;
            }

            _logger.LogWarning("High priority event processed: {EventData}",
                System.Text.Json.JsonSerializer.Serialize(priorityEventData));

            stopwatch.Stop();

            return EventHandlerResult.Success(stopwatch.Elapsed, new Dictionary<string, object>
            {
                ["Priority"] = domainEvent.Priority.ToString(),
                ["NotificationSent"] = domainEvent is INotificationEvent,
                ["ProcessingTimeMs"] = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Error processing high priority event {EventId}: {ErrorMessage}",
                domainEvent.EventId, ex.Message);

            return EventHandlerResult.Failure(
                $"Failed to process high priority event: {ex.Message}",
                ex,
                stopwatch.Elapsed);
        }
    }
}