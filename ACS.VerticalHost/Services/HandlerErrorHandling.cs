using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.VerticalHost.Services;

/// <summary>
/// Standardized error handling utilities for all handlers in the VerticalHost.
/// Provides consistent error logging, exception handling, and response patterns.
/// 
/// LOGGING STANDARDS:
/// - Debug: Query results, operation parameters, detailed execution info
/// - Information: Command completions, major state changes, business events
/// - Warning: Business rule violations, authentication failures, validation errors
/// - Error: System failures, infrastructure issues, unexpected exceptions
/// 
/// CORRELATION ID: Always include correlation ID in all log messages
/// STRUCTURED LOGGING: Use structured parameters for searchability
/// ACTIVITY TRACKING: Add telemetry tags for observability
/// </summary>
public static class HandlerErrorHandling
{
    /// <summary>
    /// Standard error handling pattern for command handlers that return a result.
    /// Logs the error appropriately and re-throws to maintain clean architecture principles.
    /// </summary>
    /// <typeparam name="TResult">The expected result type</typeparam>
    /// <param name="logger">Logger instance</param>
    /// <param name="exception">The caught exception</param>
    /// <param name="context">Context information for logging</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>Never returns - always throws</returns>
    /// <exception cref="Exception">Always re-throws the original exception</exception>
    public static TResult HandleCommandError<TResult>(
        ILogger logger, 
        Exception exception, 
        string context, 
        string? correlationId = null)
    {
        // Log with structured logging for monitoring and debugging
        using var activity = Activity.Current;
        activity?.SetTag("error.type", exception.GetType().Name);
        activity?.SetTag("error.context", context);
        
        if (!string.IsNullOrEmpty(correlationId))
        {
            activity?.SetTag("correlation.id", correlationId);
        }

        // Use appropriate log level based on exception type
        var logLevel = GetLogLevel(exception);
        
        logger.Log(logLevel, exception, 
            "Error in {Context}. CorrelationId: {CorrelationId}", 
            context, correlationId ?? "none");

        // Always re-throw to maintain clean architecture - don't swallow exceptions
        throw exception;
    }

    /// <summary>
    /// Standard error handling pattern for query handlers.
    /// Similar to command handling but may have different logging considerations.
    /// </summary>
    /// <typeparam name="TResult">The expected result type</typeparam>
    /// <param name="logger">Logger instance</param>
    /// <param name="exception">The caught exception</param>
    /// <param name="context">Context information for logging</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>Never returns - always throws</returns>
    /// <exception cref="Exception">Always re-throws the original exception</exception>
    public static TResult HandleQueryError<TResult>(
        ILogger logger, 
        Exception exception, 
        string context, 
        string? correlationId = null)
    {
        // Same pattern as commands - queries should also not swallow exceptions
        return HandleCommandError<TResult>(logger, exception, context, correlationId);
    }

    /// <summary>
    /// Standard success logging for commands.
    /// Provides consistent success message format across all handlers.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="context">Context information for logging</param>
    /// <param name="details">Additional details for the log message</param>
    /// <param name="correlationId">Optional correlation ID</param>
    public static void LogCommandSuccess(
        ILogger logger, 
        string context, 
        object? details = null, 
        string? correlationId = null)
    {
        using var activity = Activity.Current;
        activity?.SetTag("operation.success", true);
        activity?.SetTag("operation.context", context);
        
        if (!string.IsNullOrEmpty(correlationId))
        {
            activity?.SetTag("correlation.id", correlationId);
        }

        logger.LogInformation(
            "{Context} completed successfully. Details: {@Details}. CorrelationId: {CorrelationId}", 
            context, details, correlationId ?? "none");
    }

    /// <summary>
    /// Standard success logging for queries.
    /// Provides consistent success message format with performance information.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="context">Context information for logging</param>
    /// <param name="resultInfo">Information about the query results</param>
    /// <param name="correlationId">Optional correlation ID</param>
    public static void LogQuerySuccess(
        ILogger logger, 
        string context, 
        object? resultInfo = null, 
        string? correlationId = null)
    {
        using var activity = Activity.Current;
        activity?.SetTag("query.success", true);
        activity?.SetTag("query.context", context);
        
        if (!string.IsNullOrEmpty(correlationId))
        {
            activity?.SetTag("correlation.id", correlationId);
        }

        logger.LogDebug(
            "Query completed successfully: {Context}. Result: {@ResultInfo}. CorrelationId: {CorrelationId}", 
            context, resultInfo, correlationId ?? "none");
    }

    /// <summary>
    /// Standard operation start logging for tracking handler execution.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="context">Context information for logging</param>
    /// <param name="parameters">Operation parameters for logging</param>
    /// <param name="correlationId">Optional correlation ID</param>
    public static void LogOperationStart(
        ILogger logger, 
        string context, 
        object? parameters = null, 
        string? correlationId = null)
    {
        using var activity = Activity.Current;
        activity?.SetTag("operation.start", true);
        activity?.SetTag("operation.context", context);
        
        if (!string.IsNullOrEmpty(correlationId))
        {
            activity?.SetTag("correlation.id", correlationId);
        }

        logger.LogDebug(
            "Starting {Context}. Parameters: {@Parameters}. CorrelationId: {CorrelationId}", 
            context, parameters, correlationId ?? "none");
    }

    /// <summary>
    /// Determines the appropriate log level based on exception type.
    /// Business logic errors may be Information/Warning, while system errors are Error level.
    /// </summary>
    /// <param name="exception">The exception to categorize</param>
    /// <returns>Appropriate log level</returns>
    private static LogLevel GetLogLevel(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException => LogLevel.Warning,
            ArgumentException => LogLevel.Warning,
            InvalidOperationException => LogLevel.Warning,
            NotSupportedException => LogLevel.Warning,
            UnauthorizedAccessException => LogLevel.Warning,
            _ => LogLevel.Error
        };
    }
}

/// <summary>
/// Standard result wrapper for operations that may have business logic failures
/// but should not throw exceptions (like authentication failures).
/// </summary>
/// <typeparam name="T">The result type</typeparam>
public class OperationResult<T>
{
    public bool Success { get; init; }
    public T? Result { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public static OperationResult<T> SuccessResult(T result)
        => new() { Success = true, Result = result };

    public static OperationResult<T> FailureResult(string errorMessage, string? errorCode = null)
        => new() { Success = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
}

/// <summary>
/// Extension methods for consistent handler patterns.
/// </summary>
public static class HandlerExtensions
{
    /// <summary>
    /// Gets a correlation ID from the current activity or generates a new one.
    /// </summary>
    /// <returns>Correlation ID string</returns>
    public static string GetCorrelationId()
    {
        return Activity.Current?.Id ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Creates a consistent context string for logging.
    /// </summary>
    /// <param name="handlerName">The handler class name</param>
    /// <param name="operation">The operation being performed</param>
    /// <returns>Formatted context string</returns>
    public static string GetContext(string handlerName, string operation)
    {
        return $"{handlerName}.{operation}";
    }
}