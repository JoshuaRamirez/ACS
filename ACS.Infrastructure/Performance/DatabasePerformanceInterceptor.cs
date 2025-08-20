using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Diagnostics;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Interceptor for monitoring database performance
/// </summary>
public class DatabasePerformanceInterceptor : DbCommandInterceptor
{
    private readonly ILogger<DatabasePerformanceInterceptor> _logger;
    private readonly Dictionary<Guid, Stopwatch> _commandTimings = new();
    private readonly object _lock = new();

    public DatabasePerformanceInterceptor(ILogger<DatabasePerformanceInterceptor> logger)
    {
        _logger = logger;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        StartTiming(eventData.CommandId);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        StopTiming(eventData.CommandId, command, eventData.Duration);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        StartTiming(eventData.CommandId);
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        StopTiming(eventData.CommandId, command, eventData.Duration);
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        StartTiming(eventData.CommandId);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        StopTiming(eventData.CommandId, command, eventData.Duration);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StartTiming(eventData.CommandId);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        StopTiming(eventData.CommandId, command, eventData.Duration);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        StartTiming(eventData.CommandId);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        StopTiming(eventData.CommandId, command, eventData.Duration);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        StartTiming(eventData.CommandId);
        return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        StopTiming(eventData.CommandId, command, eventData.Duration);
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        lock (_lock)
        {
            _commandTimings.Remove(eventData.CommandId);
        }

        _logger.LogError(eventData.Exception,
            "Database command failed: {CommandText} (Duration: {Duration}ms)",
            command.CommandText,
            eventData.Duration.TotalMilliseconds);

        base.CommandFailed(command, eventData);
    }

    public override async Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _commandTimings.Remove(eventData.CommandId);
        }

        _logger.LogError(eventData.Exception,
            "Database command failed: {CommandText} (Duration: {Duration}ms)",
            command.CommandText,
            eventData.Duration.TotalMilliseconds);

        await base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void StartTiming(Guid commandId)
    {
        lock (_lock)
        {
            _commandTimings[commandId] = Stopwatch.StartNew();
        }
    }

    private void StopTiming(Guid commandId, DbCommand command, TimeSpan duration)
    {
        Stopwatch? stopwatch;
        lock (_lock)
        {
            if (!_commandTimings.TryGetValue(commandId, out stopwatch))
            {
                return;
            }
            _commandTimings.Remove(commandId);
        }

        stopwatch?.Stop();

        // Log slow queries
        if (duration.TotalMilliseconds > 1000)
        {
            _logger.LogWarning(
                "Slow query detected: {Duration}ms - {CommandText}",
                duration.TotalMilliseconds,
                command.CommandText);
        }
        else if (duration.TotalMilliseconds > 100)
        {
            _logger.LogDebug(
                "Query executed in {Duration}ms - {CommandText}",
                duration.TotalMilliseconds,
                command.CommandText);
        }

        // Track metrics
        RecordQueryMetrics(command, duration);
    }

    private void RecordQueryMetrics(DbCommand command, TimeSpan duration)
    {
        // Extract query type (SELECT, INSERT, UPDATE, DELETE)
        var commandText = command.CommandText.TrimStart();
        var queryType = "OTHER";
        
        if (commandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            queryType = "SELECT";
        else if (commandText.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            queryType = "INSERT";
        else if (commandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            queryType = "UPDATE";
        else if (commandText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
            queryType = "DELETE";
        else if (commandText.StartsWith("MERGE", StringComparison.OrdinalIgnoreCase))
            queryType = "MERGE";

        // These metrics could be sent to telemetry system
        _logger.LogTrace(
            "Query metrics: Type={QueryType}, Duration={Duration}ms, Parameters={ParameterCount}",
            queryType,
            duration.TotalMilliseconds,
            command.Parameters.Count);
    }
}