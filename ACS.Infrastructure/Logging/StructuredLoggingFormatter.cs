using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ACS.Infrastructure.Logging;

/// <summary>
/// Custom formatter for structured logging with correlation support
/// </summary>
public class StructuredLoggingFormatter : ConsoleFormatter
{
    private readonly StructuredLoggingOptions _options;

    public StructuredLoggingFormatter(IOptionsMonitor<StructuredLoggingOptions> options) 
        : base("structured")
    {
        _options = options.CurrentValue;
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        if (!_options.Enabled)
        {
            // Fall back to simple format
            textWriter.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{logEntry.LogLevel}] {logEntry.Category}: {logEntry.Formatter(logEntry.State, logEntry.Exception)}");
            return;
        }

        var logObject = CreateLogObject(logEntry, scopeProvider);

        if (_options.Format.Equals("Json", StringComparison.OrdinalIgnoreCase))
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            
            var json = JsonSerializer.Serialize(logObject, jsonOptions);
            textWriter.WriteLine(json);
        }
        else if (_options.Format.Equals("Compact", StringComparison.OrdinalIgnoreCase))
        {
            var compactLine = $"{logObject.Timestamp:HH:mm:ss.fff} [{logObject.Level}] {logObject.Category}";
            
            if (!string.IsNullOrEmpty(logObject.CorrelationId))
                compactLine += $" [{logObject.CorrelationId}]";
            
            if (!string.IsNullOrEmpty(logObject.UserId))
                compactLine += $" [User:{logObject.UserId}]";
            
            if (!string.IsNullOrEmpty(logObject.TenantId))
                compactLine += $" [Tenant:{logObject.TenantId}]";
            
            compactLine += $": {logObject.Message}";
            
            textWriter.WriteLine(compactLine);
        }
        else
        {
            // Text format with structured fields
            var textLine = $"{logObject.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{logObject.Level}] {logObject.Category}";
            
            if (_options.IncludeCorrelationId && !string.IsNullOrEmpty(logObject.CorrelationId))
                textLine += $" CorrelationId={logObject.CorrelationId}";
            
            if (_options.IncludeUserContext && !string.IsNullOrEmpty(logObject.UserId))
                textLine += $" UserId={logObject.UserId}";
            
            if (_options.IncludeUserContext && !string.IsNullOrEmpty(logObject.TenantId))
                textLine += $" TenantId={logObject.TenantId}";
            
            if (_options.IncludeRequestPath && !string.IsNullOrEmpty(logObject.RequestPath))
                textLine += $" Path={logObject.RequestPath}";
            
            textLine += $": {logObject.Message}";
            
            textWriter.WriteLine(textLine);
        }

        if (logEntry.Exception != null)
        {
            textWriter.WriteLine(logEntry.Exception.ToString());
        }
    }

    private LogObject CreateLogObject<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider)
    {
        var logObject = new LogObject
        {
            Timestamp = _options.IncludeTimestamp ? DateTime.UtcNow : DateTime.MinValue,
            Level = logEntry.LogLevel.ToString(),
            Category = GetShortCategory(logEntry.Category),
            Message = logEntry.Formatter(logEntry.State, logEntry.Exception),
            Exception = logEntry.Exception?.ToString()
        };

        // Extract correlation information from scopes
        ExtractFromScopes(scopeProvider, logObject);

        // Extract from state if it's our enhanced log state
        if (logEntry.State is IEnumerable<KeyValuePair<string, object?>> stateDict)
        {
            ExtractFromState(stateDict, logObject);
        }

        // Sanitize sensitive data if enabled
        if (_options.SanitizeSensitiveData)
        {
            SanitizeSensitiveData(logObject);
        }

        return logObject;
    }

    private void ExtractFromScopes(IExternalScopeProvider? scopeProvider, LogObject logObject)
    {
        scopeProvider?.ForEachScope<LogObject>((scope, state) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> scopeDict)
            {
                ExtractFromState(scopeDict, state);
            }
        }, logObject);
    }

    private void ExtractFromState(IEnumerable<KeyValuePair<string, object?>> stateDict, LogObject logObject)
    {
        foreach (var kvp in stateDict)
        {
            switch (kvp.Key.ToLowerInvariant())
            {
                case "correlationid":
                    if (_options.IncludeCorrelationId && kvp.Value?.ToString() is string correlationId)
                    {
                        logObject.CorrelationId = TruncateIfNeeded(correlationId, _options.MaxCorrelationIdLength);
                    }
                    break;
                    
                case "requestid":
                    if (kvp.Value?.ToString() is string requestId)
                    {
                        logObject.RequestId = requestId;
                    }
                    break;
                    
                case "traceid":
                    if (kvp.Value?.ToString() is string traceId)
                    {
                        logObject.TraceId = traceId;
                    }
                    break;
                    
                case "spanid":
                    if (kvp.Value?.ToString() is string spanId)
                    {
                        logObject.SpanId = spanId;
                    }
                    break;
                    
                case "userid":
                    if (_options.IncludeUserContext && kvp.Value?.ToString() is string userId)
                    {
                        logObject.UserId = userId;
                    }
                    break;
                    
                case "tenantid":
                    if (_options.IncludeUserContext && kvp.Value?.ToString() is string tenantId)
                    {
                        logObject.TenantId = tenantId;
                    }
                    break;
                    
                case "sessionid":
                    if (_options.IncludeUserContext && kvp.Value?.ToString() is string sessionId)
                    {
                        logObject.SessionId = sessionId;
                    }
                    break;
                    
                case "requestpath":
                    if (_options.IncludeRequestPath && kvp.Value?.ToString() is string requestPath)
                    {
                        logObject.RequestPath = requestPath;
                    }
                    break;
                    
                case "requestmethod":
                    if (_options.IncludeRequestPath && kvp.Value?.ToString() is string requestMethod)
                    {
                        logObject.RequestMethod = requestMethod;
                    }
                    break;
            }
        }
    }

    private void SanitizeSensitiveData(LogObject logObject)
    {
        // Sanitize message content
        if (!string.IsNullOrEmpty(logObject.Message))
        {
            foreach (var sensitiveProperty in _options.SensitiveProperties)
            {
                var pattern = $@"\b{sensitiveProperty}\s*[:=]\s*[^\s,}}\]]+";
                logObject.Message = System.Text.RegularExpressions.Regex.Replace(
                    logObject.Message, 
                    pattern, 
                    $"{sensitiveProperty}=***REDACTED***", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
        }
    }

    private static string TruncateIfNeeded(string value, int maxLength)
    {
        if (maxLength <= 0 || value.Length <= maxLength)
            return value;
        
        return value[..maxLength] + "...";
    }

    private static string GetShortCategory(string category)
    {
        // Shorten namespace for readability
        var parts = category.Split('.');
        if (parts.Length > 2)
        {
            return $"{parts[0]}...{parts[^1]}";
        }
        return category;
    }
}

/// <summary>
/// Structured log object for JSON serialization
/// </summary>
public class LogObject
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? CorrelationId { get; set; }
    public string? RequestId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? SessionId { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
}