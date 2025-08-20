using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.Logging;

/// <summary>
/// Enriches System.Diagnostics.Activity with correlation information
/// </summary>
public static class ActivityEnricher
{
    /// <summary>
    /// Creates an activity with correlation information
    /// </summary>
    public static Activity? StartActivityWithCorrelation(
        string activityName,
        ICorrelationService correlationService,
        ActivityKind kind = ActivityKind.Internal)
    {
        var activity = Activity.StartActivity(activityName, kind);
        if (activity == null)
            return null;

        var context = correlationService.GetContext();
        
        // Add correlation tags
        activity.SetTag("correlation.id", context.CorrelationId);
        activity.SetTag("correlation.request_id", context.RequestId);
        
        if (!string.IsNullOrEmpty(context.UserId))
            activity.SetTag("user.id", context.UserId);
        
        if (!string.IsNullOrEmpty(context.TenantId))
            activity.SetTag("tenant.id", context.TenantId);
        
        if (!string.IsNullOrEmpty(context.SessionId))
            activity.SetTag("session.id", context.SessionId);

        // Add custom properties
        foreach (var prop in context.Properties)
        {
            if (prop.Value != null)
            {
                activity.SetTag($"custom.{prop.Key}", prop.Value.ToString());
            }
        }

        return activity;
    }

    /// <summary>
    /// Enriches an existing activity with correlation information
    /// </summary>
    public static void EnrichActivity(
        Activity activity,
        ICorrelationService correlationService)
    {
        var context = correlationService.GetContext();
        
        activity.SetTag("correlation.id", context.CorrelationId);
        activity.SetTag("correlation.request_id", context.RequestId);
        
        if (!string.IsNullOrEmpty(context.UserId))
            activity.SetTag("user.id", context.UserId);
        
        if (!string.IsNullOrEmpty(context.TenantId))
            activity.SetTag("tenant.id", context.TenantId);
    }

    /// <summary>
    /// Creates a child activity for nested operations
    /// </summary>
    public static Activity? StartChildActivity(
        string activityName,
        ICorrelationService correlationService,
        ActivityKind kind = ActivityKind.Internal)
    {
        var childCorrelationId = correlationService.CreateChildCorrelationId();
        var activity = Activity.StartActivity(activityName, kind);
        
        if (activity == null)
            return null;

        var context = correlationService.GetContext();
        
        // Set parent information
        activity.SetTag("correlation.parent_id", context.CorrelationId);
        activity.SetTag("correlation.id", childCorrelationId);
        activity.SetTag("correlation.request_id", context.RequestId);
        
        if (!string.IsNullOrEmpty(context.UserId))
            activity.SetTag("user.id", context.UserId);
        
        if (!string.IsNullOrEmpty(context.TenantId))
            activity.SetTag("tenant.id", context.TenantId);

        return activity;
    }

    /// <summary>
    /// Adds exception information to activity
    /// </summary>
    public static void RecordException(Activity activity, Exception exception)
    {
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        activity.SetTag("exception.stacktrace", exception.StackTrace);
        
        if (exception.InnerException != null)
        {
            activity.SetTag("exception.inner_type", exception.InnerException.GetType().FullName);
            activity.SetTag("exception.inner_message", exception.InnerException.Message);
        }
    }

    /// <summary>
    /// Adds business context to activity
    /// </summary>
    public static void AddBusinessContext(
        Activity activity,
        string operation,
        string? entityType = null,
        string? entityId = null,
        Dictionary<string, object>? additionalContext = null)
    {
        activity.SetTag("business.operation", operation);
        
        if (!string.IsNullOrEmpty(entityType))
            activity.SetTag("business.entity_type", entityType);
        
        if (!string.IsNullOrEmpty(entityId))
            activity.SetTag("business.entity_id", entityId);

        if (additionalContext != null)
        {
            foreach (var kvp in additionalContext)
            {
                activity.SetTag($"business.{kvp.Key}", kvp.Value?.ToString());
            }
        }
    }
}

/// <summary>
/// Extensions for ILogger to work with activities and correlation
/// </summary>
public static class LoggerActivityExtensions
{
    /// <summary>
    /// Logs with current activity information
    /// </summary>
    public static void LogWithActivity<T>(
        this ILogger<T> logger,
        LogLevel logLevel,
        string message,
        params object[] args)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            using var scope = logger.BeginScope(new Dictionary<string, object>
            {
                ["ActivityId"] = activity.Id ?? string.Empty,
                ["ActivityName"] = activity.OperationName,
                ["TraceId"] = activity.TraceId.ToString(),
                ["SpanId"] = activity.SpanId.ToString()
            });
            
            logger.Log(logLevel, message, args);
        }
        else
        {
            logger.Log(logLevel, message, args);
        }
    }

    /// <summary>
    /// Logs an operation start with activity
    /// </summary>
    public static IDisposable LogOperation<T>(
        this ILogger<T> logger,
        string operationName,
        object? parameters = null)
    {
        var activity = Activity.Current;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        logger.LogInformation("Operation started: {OperationName} {Parameters}", 
            operationName, parameters);

        return new OperationScope(() =>
        {
            stopwatch.Stop();
            logger.LogInformation("Operation completed: {OperationName} in {ElapsedMs}ms",
                operationName, stopwatch.ElapsedMilliseconds);
        });
    }

    /// <summary>
    /// Logs an exception with activity context
    /// </summary>
    public static void LogExceptionWithActivity<T>(
        this ILogger<T> logger,
        Exception exception,
        string message,
        params object[] args)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            ActivityEnricher.RecordException(activity, exception);
        }

        logger.LogError(exception, message, args);
    }

    private class OperationScope : IDisposable
    {
        private readonly Action _onDispose;

        public OperationScope(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose?.Invoke();
        }
    }
}