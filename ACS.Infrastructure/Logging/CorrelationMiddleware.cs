using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.Logging;

/// <summary>
/// Middleware for extracting and setting correlation context from HTTP requests
/// </summary>
public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;

    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationService correlationService)
    {
        // Create correlation context for this request
        var correlationContext = CreateCorrelationContext(context);
        
        // Set the correlation context
        correlationService.SetContext(correlationContext);
        
        // Create a logger scope with correlation information
        using var loggerScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationContext.CorrelationId,
            ["RequestId"] = correlationContext.RequestId,
            ["TraceId"] = correlationContext.TraceId ?? string.Empty,
            ["UserId"] = correlationContext.UserId ?? string.Empty,
            ["TenantId"] = correlationContext.TenantId ?? string.Empty,
            ["RequestPath"] = context.Request.Path.Value ?? string.Empty,
            ["RequestMethod"] = context.Request.Method
        });

        _logger.LogInformation("Request started: {Method} {Path}", 
            context.Request.Method, context.Request.Path);

        try
        {
            await _next(context);
            
            _logger.LogInformation("Request completed: {Method} {Path} - Status: {StatusCode}", 
                context.Request.Method, context.Request.Path, context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request failed: {Method} {Path}", 
                context.Request.Method, context.Request.Path);
            throw;
        }
    }

    private static CorrelationContext CreateCorrelationContext(HttpContext httpContext)
    {
        var context = new CorrelationContext();

        // Extract correlation ID from header or generate new one
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationHeader) &&
            !string.IsNullOrEmpty(correlationHeader))
        {
            context.CorrelationId = correlationHeader.ToString();
        }

        // Extract request ID from header or generate new one
        if (httpContext.Request.Headers.TryGetValue("X-Request-ID", out var requestHeader) &&
            !string.IsNullOrEmpty(requestHeader))
        {
            context.RequestId = requestHeader.ToString();
        }

        // Extract trace ID from activity or header
        var activity = System.Diagnostics.Activity.Current;
        if (activity != null)
        {
            context.TraceId = activity.TraceId.ToString();
            context.SpanId = activity.SpanId.ToString();
            context.ParentId = activity.ParentSpanId.ToString();
        }
        else if (httpContext.Request.Headers.TryGetValue("X-Trace-ID", out var traceHeader))
        {
            context.TraceId = traceHeader.ToString();
        }

        // Extract user information from claims
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            context.UserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            context.TenantId = user.FindFirst("tenant_id")?.Value;
            context.SessionId = user.FindFirst("session_id")?.Value;
        }

        // Add request properties
        context.Properties["RequestPath"] = httpContext.Request.Path.Value ?? string.Empty;
        context.Properties["RequestMethod"] = httpContext.Request.Method;
        context.Properties["RequestQuery"] = httpContext.Request.QueryString.Value ?? string.Empty;
        context.Properties["UserAgent"] = httpContext.Request.Headers["User-Agent"].ToString();
        context.Properties["RemoteIpAddress"] = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        context.Properties["RequestSize"] = httpContext.Request.ContentLength?.ToString() ?? "0";

        return context;
    }
}

/// <summary>
/// Extension methods for adding correlation middleware
/// </summary>
public static class CorrelationMiddlewareExtensions
{
    /// <summary>
    /// Adds correlation middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationMiddleware>();
    }
}