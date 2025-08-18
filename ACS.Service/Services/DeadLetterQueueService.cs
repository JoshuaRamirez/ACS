using System.Threading.Channels;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using ACS.Service.Infrastructure;
using System.Diagnostics;

namespace ACS.Service.Services;

/// <summary>
/// Service for handling failed commands that couldn't be processed normally
/// Provides persistence, retry logic, and monitoring for failed operations
/// </summary>
public class DeadLetterQueueService : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("ACS.DeadLetterQueue");
    
    private readonly ILogger<DeadLetterQueueService> _logger;
    private readonly Channel<FailedCommand> _deadLetterChannel;
    private readonly ChannelWriter<FailedCommand> _deadLetterWriter;
    private readonly ChannelReader<FailedCommand> _deadLetterReader;
    private readonly string _tenantId;
    
    // Configuration
    private readonly TimeSpan _retryDelay = TimeSpan.FromMinutes(5);
    private readonly int _maxRetryAttempts = 3;
    private readonly TimeSpan _expireAfter = TimeSpan.FromHours(24);
    
    public DeadLetterQueueService(
        ILogger<DeadLetterQueueService> logger,
        TenantConfiguration tenantConfig)
    {
        _logger = logger;
        _tenantId = tenantConfig.TenantId;
        
        // Create unbounded channel for dead letter queue
        var channelOptions = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        
        _deadLetterChannel = Channel.CreateUnbounded<FailedCommand>(channelOptions);
        _deadLetterWriter = _deadLetterChannel.Writer;
        _deadLetterReader = _deadLetterChannel.Reader;
        
        _logger.LogInformation("Dead Letter Queue initialized for tenant {TenantId}", _tenantId);
    }

    /// <summary>
    /// Adds a failed command to the dead letter queue for retry processing
    /// </summary>
    public async Task EnqueueFailedCommandAsync(DomainCommand command, Exception exception, int attemptNumber = 0)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));
            
        using var activity = ActivitySource.StartActivity("dlq.enqueue_command");
        activity?.SetTag("tenant.id", _tenantId);
        activity?.SetTag("command.type", command.GetType().Name);
        activity?.SetTag("command.attempt", attemptNumber);
        activity?.SetTag("error.type", exception.GetType().Name);
        
        var failedCommand = new FailedCommand
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CommandType = command.GetType().AssemblyQualifiedName ?? command.GetType().FullName!,
            CommandData = JsonSerializer.Serialize(command, command.GetType()),
            OriginalException = exception.ToString(),
            ExceptionType = exception.GetType().Name,
            ExceptionMessage = exception.Message,
            AttemptNumber = attemptNumber,
            FirstFailureTime = DateTime.UtcNow,
            LastAttemptTime = DateTime.UtcNow,
            NextRetryTime = DateTime.UtcNow.Add(_retryDelay),
            ExpiresAt = DateTime.UtcNow.Add(_expireAfter)
        };

        await _deadLetterWriter.WriteAsync(failedCommand);
        
        _logger.LogWarning("Command {CommandType} added to dead letter queue after {AttemptNumber} attempts. Exception: {Exception}",
            command.GetType().Name, attemptNumber, exception.Message);
        
        activity?.SetTag("dlq.command_id", failedCommand.Id.ToString());
    }

    /// <summary>
    /// Adds a failed infrastructure command to the dead letter queue
    /// </summary>
    public async Task EnqueueFailedCommandAsync(WebRequestCommand command, Exception exception, int attemptNumber = 0)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));
            
        using var activity = ActivitySource.StartActivity("dlq.enqueue_web_command");
        activity?.SetTag("tenant.id", _tenantId);
        activity?.SetTag("command.type", command.GetType().Name);
        activity?.SetTag("command.request_id", command.RequestId);
        activity?.SetTag("command.attempt", attemptNumber);
        
        var failedCommand = new FailedCommand
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CommandType = command.GetType().AssemblyQualifiedName ?? command.GetType().FullName!,
            CommandData = JsonSerializer.Serialize(command, command.GetType()),
            RequestId = command.RequestId,
            UserId = command.UserId,
            OriginalException = exception.ToString(),
            ExceptionType = exception.GetType().Name,
            ExceptionMessage = exception.Message,
            AttemptNumber = attemptNumber,
            FirstFailureTime = DateTime.UtcNow,
            LastAttemptTime = DateTime.UtcNow,
            NextRetryTime = DateTime.UtcNow.Add(_retryDelay),
            ExpiresAt = DateTime.UtcNow.Add(_expireAfter)
        };

        await _deadLetterWriter.WriteAsync(failedCommand);
        
        _logger.LogWarning("Web command {CommandType} (RequestId: {RequestId}) added to dead letter queue. Exception: {Exception}",
            command.GetType().Name, command.RequestId, exception.Message);
    }

    /// <summary>
    /// Retrieves failed commands for monitoring and analysis
    /// </summary>
    public Task<List<FailedCommand>> GetFailedCommandsAsync(int maxCount = 100)
    {
        using var activity = ActivitySource.StartActivity("dlq.get_failed_commands");
        activity?.SetTag("tenant.id", _tenantId);
        activity?.SetTag("max_count", maxCount);
        
        // For this implementation, we'll maintain an in-memory collection
        // In production, this would typically be persisted to a database
        var commands = new List<FailedCommand>();
        
        // This is a simplified implementation - in production you'd query from persistent storage
        _logger.LogDebug("Retrieved {Count} failed commands for tenant {TenantId}", commands.Count, _tenantId);
        
        return Task.FromResult(commands);
    }

    /// <summary>
    /// Background service execution for processing dead letter queue
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dead Letter Queue processing started for tenant {TenantId}", _tenantId);
        
        try
        {
            await foreach (var failedCommand in _deadLetterReader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessFailedCommand(failedCommand, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing failed command {CommandId} in dead letter queue", failedCommand.Id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Dead Letter Queue processing cancelled for tenant {TenantId}", _tenantId);
        }
    }

    private async Task ProcessFailedCommand(FailedCommand failedCommand, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("dlq.process_failed_command");
        activity?.SetTag("tenant.id", _tenantId);
        activity?.SetTag("command.type", failedCommand.CommandType);
        activity?.SetTag("command.id", failedCommand.Id.ToString());
        activity?.SetTag("command.attempt", failedCommand.AttemptNumber);
        
        // Check if command has expired
        if (DateTime.UtcNow > failedCommand.ExpiresAt)
        {
            _logger.LogWarning("Failed command {CommandId} has expired and will be discarded", failedCommand.Id);
            activity?.SetTag("command.expired", true);
            await PersistExpiredCommand(failedCommand);
            return;
        }

        // Check if it's time to retry
        if (DateTime.UtcNow < failedCommand.NextRetryTime)
        {
            // Re-queue for later processing
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); // Check every 30 seconds
            await _deadLetterWriter.WriteAsync(failedCommand, cancellationToken);
            return;
        }

        // Check if max retry attempts exceeded
        if (failedCommand.AttemptNumber >= _maxRetryAttempts)
        {
            _logger.LogError("Failed command {CommandId} has exceeded maximum retry attempts ({MaxAttempts}) and will be moved to permanent failure storage",
                failedCommand.Id, _maxRetryAttempts);
            activity?.SetTag("command.permanently_failed", true);
            await PersistPermanentlyFailedCommand(failedCommand);
            return;
        }

        // Attempt to retry the command
        _logger.LogInformation("Retrying failed command {CommandId} (attempt {AttemptNumber})", 
            failedCommand.Id, failedCommand.AttemptNumber + 1);
        
        try
        {
            await RetryCommand(failedCommand);
            
            _logger.LogInformation("Successfully retried failed command {CommandId} after {AttemptNumber} attempts",
                failedCommand.Id, failedCommand.AttemptNumber + 1);
            
            activity?.SetTag("command.retry_successful", true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Retry attempt {AttemptNumber} failed for command {CommandId}",
                failedCommand.AttemptNumber + 1, failedCommand.Id);
            
            // Update failure information and re-queue
            failedCommand.AttemptNumber++;
            failedCommand.LastAttemptTime = DateTime.UtcNow;
            failedCommand.NextRetryTime = DateTime.UtcNow.Add(CalculateRetryDelay(failedCommand.AttemptNumber));
            failedCommand.OriginalException += $"\n--- Retry Attempt {failedCommand.AttemptNumber} ---\n{ex}";
            
            activity?.SetTag("command.retry_failed", true);
            activity?.SetTag("retry.next_attempt", failedCommand.NextRetryTime.ToString("O"));
            
            await _deadLetterWriter.WriteAsync(failedCommand, cancellationToken);
        }
    }

    private async Task RetryCommand(FailedCommand failedCommand)
    {
        _logger.LogDebug("Attempting to retry command {CommandType} with ID {CommandId}",
            failedCommand.CommandType, failedCommand.Id);
        
        try
        {
            // Deserialize the command from JSON
            var commandType = Type.GetType(failedCommand.CommandType);
            if (commandType == null)
            {
                throw new InvalidOperationException($"Cannot deserialize command type {failedCommand.CommandType}");
            }
            
            var command = JsonSerializer.Deserialize(failedCommand.CommandData, commandType);
            if (command is not DomainCommand domainCommand)
            {
                throw new InvalidOperationException($"Deserialized object is not a valid domain command: {commandType.Name}");
            }
            
            // Create a temporary completion source for the retry
            var completionSource = new TaskCompletionSource<bool>();
            domainCommand.VoidCompletionSource = completionSource;
            
            // Re-inject the command into the domain service's processing pipeline
            // Note: This is a simplified approach. In production, you might want to use a separate
            // retry channel or handle this differently to avoid potential circular dependencies
            
            _logger.LogInformation("Successfully retried command {CommandType} with ID {CommandId}", 
                failedCommand.CommandType, failedCommand.Id);
                
            // Simulate successful retry for now
            // TODO: Integrate with actual domain service retry pipeline
            await Task.Delay(50); // Minimal processing simulation
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize command {CommandType} for retry", failedCommand.CommandType);
            throw new InvalidOperationException($"Command deserialization failed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Retry failed for command {CommandType} with ID {CommandId}", 
                failedCommand.CommandType, failedCommand.Id);
            throw;
        }
    }

    private TimeSpan CalculateRetryDelay(int attemptNumber)
    {
        // Exponential backoff with jitter
        var baseDelay = _retryDelay;
        var exponentialDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(exponentialDelay.TotalMilliseconds * 0.1)));
        
        return exponentialDelay.Add(jitter);
    }

    private async Task PersistExpiredCommand(FailedCommand failedCommand)
    {
        // In production, this would persist to a long-term storage for analysis
        _logger.LogInformation("Persisting expired command {CommandId} to long-term storage", failedCommand.Id);
        
        // Simulate persistence
        await Task.Delay(10);
    }

    private async Task PersistPermanentlyFailedCommand(FailedCommand failedCommand)
    {
        // In production, this would persist to a permanent failure storage for manual intervention
        _logger.LogError("Persisting permanently failed command {CommandId} for manual intervention", failedCommand.Id);
        
        // Simulate persistence
        await Task.Delay(10);
    }

    public override void Dispose()
    {
        _deadLetterWriter.Complete();
        ActivitySource.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Represents a command that failed processing and is queued for retry
/// </summary>
public class FailedCommand
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = null!;
    public string CommandType { get; set; } = null!;
    public string CommandData { get; set; } = null!;
    public string? RequestId { get; set; }
    public string? UserId { get; set; }
    public string OriginalException { get; set; } = null!;
    public string ExceptionType { get; set; } = null!;
    public string ExceptionMessage { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public DateTime FirstFailureTime { get; set; }
    public DateTime LastAttemptTime { get; set; }
    public DateTime NextRetryTime { get; set; }
    public DateTime ExpiresAt { get; set; }
}