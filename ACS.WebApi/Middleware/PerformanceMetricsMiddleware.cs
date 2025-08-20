using System.Diagnostics;
using ACS.Infrastructure.Services;
using ACS.Infrastructure.Services;

namespace ACS.WebApi.Middleware;

/// <summary>
/// Middleware for collecting HTTP request performance metrics
/// </summary>
public class PerformanceMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMetricsMiddleware> _logger;

    public PerformanceMetricsMiddleware(RequestDelegate next, ILogger<PerformanceMetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path.Value ?? "unknown";
        var requestMethod = context.Request.Method;
        
        // Add request start time to context
        context.Items["RequestStartTime"] = DateTime.UtcNow;
        context.Items["RequestStopwatch"] = stopwatch;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Record error metrics
            RecordErrorMetrics(context, ex, stopwatch.Elapsed);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            
            // Record request metrics
            RecordRequestMetrics(context, stopwatch.Elapsed);
        }
    }

    private void RecordRequestMetrics(HttpContext context, TimeSpan duration)
    {
        try
        {
            var requestPath = context.Request.Path.Value ?? "unknown";
            var requestMethod = context.Request.Method;
            var statusCode = context.Response.StatusCode;
            var durationSeconds = duration.TotalSeconds;
            
            // Get tenant ID from context if available
            var tenantId = context.Items["TenantId"]?.ToString() ?? 
                          context.Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? 
                          "unknown";
            
            // Create activity for request metrics
            using var activity = TelemetryService.ActivitySource.StartActivity("http.request.metrics");
            activity?.SetTag("http.method", requestMethod);
            activity?.SetTag("http.route", requestPath);
            activity?.SetTag("http.status_code", statusCode);
            activity?.SetTag("tenant.id", tenantId);
            activity?.SetTag("request.duration_ms", duration.TotalMilliseconds);
            
            // Log detailed metrics for slow requests
            if (duration.TotalSeconds > 1.0)
            {
                _logger.LogWarning("Slow HTTP request: {Method} {Path} took {DurationMs}ms for tenant {TenantId}",
                    requestMethod, requestPath, duration.TotalMilliseconds, tenantId);
            }
            
            // Log request completion
            _logger.LogDebug("HTTP {Method} {Path} completed with {StatusCode} in {DurationMs}ms for tenant {TenantId}",
                requestMethod, requestPath, statusCode, duration.TotalMilliseconds, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record request metrics");
        }
    }

    private void RecordErrorMetrics(HttpContext context, Exception exception, TimeSpan duration)
    {
        try
        {
            var requestPath = context.Request.Path.Value ?? "unknown";
            var requestMethod = context.Request.Method;
            var tenantId = context.Items["TenantId"]?.ToString() ?? 
                          context.Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? 
                          "unknown";
            
            // Create activity for error metrics
            using var activity = TelemetryService.ActivitySource.StartActivity("http.request.error");
            activity?.SetTag("http.method", requestMethod);
            activity?.SetTag("http.route", requestPath);
            activity?.SetTag("tenant.id", tenantId);
            activity?.SetTag("error.type", exception.GetType().Name);
            activity?.SetTag("error.message", exception.Message);
            activity?.SetTag("request.duration_ms", duration.TotalMilliseconds);
            
            TelemetryService.RecordError(activity, exception);
            
            _logger.LogError(exception, "HTTP {Method} {Path} failed after {DurationMs}ms for tenant {TenantId}: {ErrorType}",
                requestMethod, requestPath, duration.TotalMilliseconds, tenantId, exception.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record error metrics");
        }
    }
}

/// <summary>
/// Extension methods for registering performance metrics middleware
/// </summary>
public static class PerformanceMetricsMiddlewareExtensions
{
    /// <summary>
    /// Add performance metrics middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UsePerformanceMetrics(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PerformanceMetricsMiddleware>();
    }
}