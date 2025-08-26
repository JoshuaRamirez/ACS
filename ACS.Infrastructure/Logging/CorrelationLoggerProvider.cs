using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace ACS.Infrastructure.Logging;

/// <summary>
/// Logger provider that enriches log entries with correlation information
/// </summary>
public class CorrelationLoggerProvider : ILoggerProvider
{
    private readonly ICorrelationService _correlationService;
    private readonly ILoggerProvider _innerProvider;
    private readonly ConcurrentDictionary<string, CorrelationLogger> _loggers = new();

    public CorrelationLoggerProvider(
        ICorrelationService correlationService,
        ILoggerProvider innerProvider)
    {
        _correlationService = correlationService;
        _innerProvider = innerProvider;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name =>
        {
            var innerLogger = _innerProvider.CreateLogger(name);
            return new CorrelationLogger(innerLogger, _correlationService);
        });
    }

    public void Dispose()
    {
        _loggers.Clear();
        _innerProvider?.Dispose();
    }
}

/// <summary>
/// Logger that automatically includes correlation information in log entries
/// </summary>
public class CorrelationLogger : ILogger
{
    private readonly ILogger _innerLogger;
    private readonly ICorrelationService _correlationService;

    public CorrelationLogger(ILogger innerLogger, ICorrelationService correlationService)
    {
        _innerLogger = innerLogger;
        _correlationService = correlationService;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        // Enhance scope with correlation information
        var correlationContext = _correlationService.GetContext();
        var enhancedState = new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationContext.CorrelationId,
            ["RequestId"] = correlationContext.RequestId,
            ["TraceId"] = correlationContext.TraceId,
            ["SpanId"] = correlationContext.SpanId,
            ["UserId"] = correlationContext.UserId,
            ["TenantId"] = correlationContext.TenantId,
            ["SessionId"] = correlationContext.SessionId
        };

        // Add original state if it's a dictionary
        if (state is IEnumerable<KeyValuePair<string, object?>> stateDict)
        {
            foreach (var kvp in stateDict)
            {
                enhancedState[kvp.Key] = kvp.Value;
            }
        }
        else if (state != null)
        {
            enhancedState["OriginalState"] = state;
        }

        return _innerLogger.BeginScope(enhancedState) ?? new NoOpDisposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _innerLogger.IsEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel, 
        EventId eventId, 
        TState state, 
        Exception? exception, 
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        // Create enhanced state with correlation information
        var correlationContext = _correlationService.GetContext();
        var enhancedState = new LogState<TState>
        {
            OriginalState = state,
            CorrelationId = correlationContext.CorrelationId,
            RequestId = correlationContext.RequestId,
            TraceId = correlationContext.TraceId,
            SpanId = correlationContext.SpanId,
            UserId = correlationContext.UserId,
            TenantId = correlationContext.TenantId,
            SessionId = correlationContext.SessionId,
            Timestamp = correlationContext.Timestamp
        };

        // Enhanced formatter that includes correlation information
        string EnhancedFormatter(LogState<TState> enhancedState, Exception? ex)
        {
            var originalMessage = formatter(enhancedState.OriginalState, ex);
            
            var correlationInfo = new List<string>();
            if (!string.IsNullOrEmpty(enhancedState.CorrelationId))
                correlationInfo.Add($"CorrelationId={enhancedState.CorrelationId}");
            if (!string.IsNullOrEmpty(enhancedState.RequestId))
                correlationInfo.Add($"RequestId={enhancedState.RequestId}");
            if (!string.IsNullOrEmpty(enhancedState.TraceId))
                correlationInfo.Add($"TraceId={enhancedState.TraceId}");
            if (!string.IsNullOrEmpty(enhancedState.UserId))
                correlationInfo.Add($"UserId={enhancedState.UserId}");
            if (!string.IsNullOrEmpty(enhancedState.TenantId))
                correlationInfo.Add($"TenantId={enhancedState.TenantId}");

            if (correlationInfo.Any())
            {
                return $"[{string.Join(", ", correlationInfo)}] {originalMessage}";
            }

            return originalMessage;
        }

        _innerLogger.Log(logLevel, eventId, enhancedState, exception, EnhancedFormatter);
    }
}

/// <summary>
/// Enhanced log state that includes correlation information
/// </summary>
public class LogState<TState> : IReadOnlyList<KeyValuePair<string, object?>>
{
    public TState OriginalState { get; set; } = default!;
    public string CorrelationId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? SessionId { get; set; }
    public DateTime Timestamp { get; set; }

    public KeyValuePair<string, object?> this[int index]
    {
        get
        {
            return index switch
            {
                0 => new("CorrelationId", CorrelationId),
                1 => new("RequestId", RequestId),
                2 => new("TraceId", TraceId),
                3 => new("SpanId", SpanId),
                4 => new("UserId", UserId),
                5 => new("TenantId", TenantId),
                6 => new("SessionId", SessionId),
                7 => new("Timestamp", Timestamp),
                8 => new("OriginalState", OriginalState),
                _ => throw new IndexOutOfRangeException()
            };
        }
    }

    public int Count => 9;

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        yield return new("CorrelationId", CorrelationId);
        yield return new("RequestId", RequestId);
        yield return new("TraceId", TraceId);
        yield return new("SpanId", SpanId);
        yield return new("UserId", UserId);
        yield return new("TenantId", TenantId);
        yield return new("SessionId", SessionId);
        yield return new("Timestamp", Timestamp);
        yield return new("OriginalState", OriginalState);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// Extensions for adding correlation logging
/// </summary>
public static class CorrelationLoggingExtensions
{
    /// <summary>
    /// Adds correlation logging provider to the logging builder
    /// </summary>
    public static ILoggingBuilder AddCorrelationLogging(this ILoggingBuilder builder)
    {
        builder.Services.AddSingleton<ICorrelationService, CorrelationService>();
        return builder;
    }

    /// <summary>
    /// Creates a scoped logger with current correlation context
    /// </summary>
    public static IDisposable BeginCorrelationScope(this ILogger logger, ICorrelationService correlationService)
    {
        var context = correlationService.GetContext();
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = context.CorrelationId,
            ["RequestId"] = context.RequestId,
            ["TraceId"] = context.TraceId ?? string.Empty,
            ["UserId"] = context.UserId ?? string.Empty,
            ["TenantId"] = context.TenantId ?? string.Empty
        }) ?? new NoOpDisposable();
    }
}

internal class NoOpDisposable : IDisposable
{
    public void Dispose()
    {
        // No operation needed
    }
}