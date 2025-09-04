using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ACS.Core.Grpc;
using System.Diagnostics;

namespace ACS.VerticalHost.Services;

/// <summary>
/// High-performance command buffer implementing LMAX Disruptor pattern
/// Provides fire-and-forget command processing with guaranteed ordering
/// Acts as the "buffer" that queues commands and processes them sequentially
/// </summary>
public interface ICommandBuffer
{
    Task<TResponse> ExecuteQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
    Task ExecuteCommandAsync(ICommand command, CancellationToken cancellationToken = default);
    Task<TResponse> ExecuteCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);
    CommandBufferStats GetStats();
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Command buffer implementation using System.Threading.Channels for high-performance
/// Sequential processing ensures consistency and eliminates race conditions
/// </summary>
public class CommandBuffer : ICommandBuffer, IDisposable
{
    private readonly ILogger<CommandBuffer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private static readonly ActivitySource ActivitySource = new("ACS.VerticalHost.CommandBuffer");
    
    // High-performance channel for command queuing
    private readonly Channel<BufferedCommand> _commandChannel;
    private readonly ChannelWriter<BufferedCommand> _commandWriter;
    private readonly ChannelReader<BufferedCommand> _commandReader;
    
    // Background processing task
    private Task? _processingTask;
    private readonly CancellationTokenSource _processingCancellation = new();
    
    // Performance metrics
    private long _commandsProcessed = 0;
    private long _queriesProcessed = 0;
    private long _commandsInFlight = 0;
    private readonly DateTime _startTime = DateTime.UtcNow;
    
    // Error tracking
    private readonly ConcurrentQueue<CommandError> _recentErrors = new();
    private const int MaxRecentErrors = 100;

    public CommandBuffer(
        ILogger<CommandBuffer> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        // Create high-performance bounded channel
        var options = new BoundedChannelOptions(capacity: 10000)
        {
            FullMode = BoundedChannelFullMode.Wait, // Backpressure
            SingleReader = true,  // Single consumer for ordering
            SingleWriter = false, // Multiple producers (HTTP requests)
            AllowSynchronousContinuations = false
        };
        
        _commandChannel = Channel.CreateBounded<BufferedCommand>(options);
        _commandWriter = _commandChannel.Writer;
        _commandReader = _commandChannel.Reader;
        
        _logger.LogInformation("CommandBuffer initialized with capacity {Capacity}", options.Capacity);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting CommandBuffer processing");
        
        _processingTask = ProcessCommandsAsync(_processingCancellation.Token);
        
        _logger.LogInformation("CommandBuffer started successfully");
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping CommandBuffer processing");
        
        // Signal shutdown
        _commandWriter.Complete();
        _processingCancellation.Cancel();
        
        // Wait for processing to complete
        if (_processingTask != null)
        {
            await _processingTask;
        }
        
        _logger.LogInformation("CommandBuffer stopped successfully");
    }

    #region Query Processing (Immediate, No Buffering)

    public async Task<TResponse> ExecuteQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("CommandBuffer.ExecuteQuery");
        activity?.SetTag("query.type", query.GetType().Name);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Queries are executed immediately - no buffering/queuing
            // This provides fast read access while commands are sequentially processed
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetService<IQueryHandler<IQuery<TResponse>, TResponse>>();
            
            if (handler == null)
            {
                throw new InvalidOperationException($"No query handler found for {query.GetType().Name}");
            }
            
            var result = await handler.HandleAsync(query, cancellationToken);
            
            Interlocked.Increment(ref _queriesProcessed);
            
            activity?.SetTag("query.success", true);
            activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);
            
            _logger.LogDebug("Query {QueryType} executed in {Duration}ms", 
                query.GetType().Name, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetTag("query.success", false);
            activity?.SetTag("query.error", ex.Message);
            
            _logger.LogError(ex, "Error executing query {QueryType}", query.GetType().Name);
            throw;
        }
    }

    #endregion

    #region Command Processing (Buffered, Sequential)

    public async Task ExecuteCommandAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        await EnqueueCommandAsync(new BufferedCommand
        {
            Command = command,
            CompletionSource = new TaskCompletionSource<object?>(),
            CorrelationId = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
            EnqueuedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task<TResponse> ExecuteCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        var completionSource = new TaskCompletionSource<object?>();
        
        await EnqueueCommandAsync(new BufferedCommand
        {
            Command = command,
            CompletionSource = completionSource,
            CorrelationId = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
            EnqueuedAt = DateTime.UtcNow
        }, cancellationToken);
        
        var result = await completionSource.Task;
        return (TResponse)(result ?? default(TResponse)!);
    }

    private async Task EnqueueCommandAsync(BufferedCommand bufferedCommand, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("CommandBuffer.EnqueueCommand");
        activity?.SetTag("command.type", bufferedCommand.Command.GetType().Name);
        activity?.SetTag("command.correlation_id", bufferedCommand.CorrelationId);
        
        Interlocked.Increment(ref _commandsInFlight);
        
        try
        {
            // Enqueue command for sequential processing
            await _commandWriter.WriteAsync(bufferedCommand, cancellationToken);
            
            // Wait for command to be processed
            await bufferedCommand.CompletionSource.Task;
            
            activity?.SetTag("command.enqueued", true);
        }
        catch (Exception ex)
        {
            activity?.SetTag("command.enqueued", false);
            activity?.SetTag("command.error", ex.Message);
            
            Interlocked.Decrement(ref _commandsInFlight);
            throw;
        }
    }

    #endregion

    #region Background Command Processing

    private async Task ProcessCommandsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting background command processing");
        
        await foreach (var bufferedCommand in _commandReader.ReadAllAsync(cancellationToken))
        {
            await ProcessSingleCommandAsync(bufferedCommand);
        }
        
        _logger.LogInformation("Background command processing completed");
    }

    private async Task ProcessSingleCommandAsync(BufferedCommand bufferedCommand)
    {
        using var activity = ActivitySource.StartActivity("CommandBuffer.ProcessCommand");
        activity?.SetTag("command.type", bufferedCommand.Command.GetType().Name);
        activity?.SetTag("command.correlation_id", bufferedCommand.CorrelationId);
        
        var stopwatch = Stopwatch.StartNew();
        var queueTime = DateTime.UtcNow - bufferedCommand.EnqueuedAt;
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            // Find appropriate handler for the command
            var commandType = bufferedCommand.Command.GetType();
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
            var handler = scope.ServiceProvider.GetService(handlerType);
            
            if (handler == null)
            {
                throw new InvalidOperationException($"No command handler found for {commandType.Name}");
            }
            
            // Execute command via handler
            var handleMethod = handlerType.GetMethod("HandleAsync");
            if (handleMethod == null)
            {
                throw new InvalidOperationException($"HandleAsync method not found on handler for {commandType.Name}");
            }
            
            var result = await (Task<object?>)handleMethod.Invoke(handler, new object[] { bufferedCommand.Command, CancellationToken.None })!;
            
            // Complete the command
            bufferedCommand.CompletionSource.SetResult(result);
            
            Interlocked.Increment(ref _commandsProcessed);
            Interlocked.Decrement(ref _commandsInFlight);
            
            activity?.SetTag("command.success", true);
            activity?.SetTag("command.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("command.queue_time_ms", queueTime.TotalMilliseconds);
            
            _logger.LogDebug("Command {CommandType} processed in {Duration}ms (queued for {QueueTime}ms)", 
                commandType.Name, stopwatch.ElapsedMilliseconds, queueTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            // Record error for monitoring
            _recentErrors.Enqueue(new CommandError
            {
                CommandType = bufferedCommand.Command.GetType().Name,
                CorrelationId = bufferedCommand.CorrelationId,
                Error = ex.Message,
                OccurredAt = DateTime.UtcNow
            });
            
            // Limit error queue size
            while (_recentErrors.Count > MaxRecentErrors)
            {
                _recentErrors.TryDequeue(out _);
            }
            
            bufferedCommand.CompletionSource.SetException(ex);
            
            Interlocked.Decrement(ref _commandsInFlight);
            
            activity?.SetTag("command.success", false);
            activity?.SetTag("command.error", ex.Message);
            
            _logger.LogError(ex, "Error processing command {CommandType} (correlation: {CorrelationId})", 
                bufferedCommand.Command.GetType().Name, bufferedCommand.CorrelationId);
        }
    }

    #endregion

    #region Performance Monitoring

    public CommandBufferStats GetStats()
    {
        var uptime = DateTime.UtcNow - _startTime;
        var commandsProcessed = Interlocked.Read(ref _commandsProcessed);
        var queriesProcessed = Interlocked.Read(ref _queriesProcessed);
        var commandsInFlight = Interlocked.Read(ref _commandsInFlight);
        
        return new CommandBufferStats
        {
            UptimeSeconds = uptime.TotalSeconds,
            CommandsProcessed = commandsProcessed,
            QueriesProcessed = queriesProcessed,
            CommandsInFlight = commandsInFlight,
            CommandsPerSecond = uptime.TotalSeconds > 0 ? commandsProcessed / uptime.TotalSeconds : 0,
            QueriesPerSecond = uptime.TotalSeconds > 0 ? queriesProcessed / uptime.TotalSeconds : 0,
            RecentErrors = _recentErrors.ToList(),
            ChannelCapacity = 10000,
            ChannelUsage = commandsInFlight
        };
    }

    #endregion

    public void Dispose()
    {
        _processingCancellation?.Cancel();
        _processingTask?.Wait(TimeSpan.FromSeconds(5));
        _processingCancellation?.Dispose();
    }
}

#region Supporting Types

internal class BufferedCommand
{
    public required object Command { get; init; }
    public required TaskCompletionSource<object?> CompletionSource { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime EnqueuedAt { get; init; }
}

public class CommandBufferStats
{
    public double UptimeSeconds { get; init; }
    public long CommandsProcessed { get; init; }
    public long QueriesProcessed { get; init; }
    public long CommandsInFlight { get; init; }
    public double CommandsPerSecond { get; init; }
    public double QueriesPerSecond { get; init; }
    public List<CommandError> RecentErrors { get; init; } = new();
    public long ChannelCapacity { get; init; }
    public long ChannelUsage { get; init; }
}

public class CommandError
{
    public required string CommandType { get; init; }
    public required string CorrelationId { get; init; }
    public required string Error { get; init; }
    public required DateTime OccurredAt { get; init; }
}

// Base interfaces for CQRS pattern
public interface ICommand { }
public interface ICommand<TResponse> { }
public interface IQuery<TResponse> { }

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

#endregion