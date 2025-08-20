using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Infrastructure.Monitoring;

/// <summary>
/// Middleware for collecting HTTP request metrics
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMetricsCollector _metrics;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(
        RequestDelegate next,
        IMetricsCollector metrics,
        ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? "unknown";
        var method = context.Request.Method;
        
        // Track active requests
        _metrics.RecordGauge(ApplicationMetrics.Api.ActiveRequests, 1,
            new KeyValuePair<string, object?>(MetricTags.Endpoint, path),
            new KeyValuePair<string, object?>(MetricTags.Method, method));

        try
        {
            // Record request start
            _metrics.IncrementCounter(ApplicationMetrics.Api.RequestCount,
                tags: new[]
                {
                    new KeyValuePair<string, object?>(MetricTags.Endpoint, path),
                    new KeyValuePair<string, object?>(MetricTags.Method, method)
                });

            // Capture response size
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            // Record response metrics
            stopwatch.Stop();
            RecordResponseMetrics(context, stopwatch.Elapsed, responseBody.Length);

            // Copy response to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordErrorMetrics(context, ex, stopwatch.Elapsed);
            throw;
        }
        finally
        {
            // Decrement active requests
            _metrics.RecordGauge(ApplicationMetrics.Api.ActiveRequests, -1,
                new KeyValuePair<string, object?>(MetricTags.Endpoint, path),
                new KeyValuePair<string, object?>(MetricTags.Method, method));
        }
    }

    private void RecordResponseMetrics(HttpContext context, TimeSpan duration, long responseSize)
    {
        var path = context.Request.Path.Value ?? "unknown";
        var method = context.Request.Method;
        var statusCode = context.Response.StatusCode;

        var tags = new[]
        {
            new KeyValuePair<string, object?>(MetricTags.Endpoint, path),
            new KeyValuePair<string, object?>(MetricTags.Method, method),
            new KeyValuePair<string, object?>(MetricTags.StatusCode, statusCode)
        };

        // Record duration
        _metrics.RecordHistogram(ApplicationMetrics.Api.RequestDuration, duration.TotalMilliseconds, tags);

        // Record response size
        _metrics.RecordHistogram(ApplicationMetrics.Api.ResponseSize, responseSize, tags);

        // Record errors
        if (statusCode >= 400)
        {
            _metrics.IncrementCounter(ApplicationMetrics.Api.RequestErrors, tags: tags);
            
            if (statusCode >= 500)
            {
                _metrics.IncrementCounter(ApplicationMetrics.Errors.TotalErrors, tags: tags);
            }
        }

        // Record business metrics based on endpoint
        RecordBusinessMetrics(context, duration);
    }

    private void RecordErrorMetrics(HttpContext context, Exception exception, TimeSpan duration)
    {
        var path = context.Request.Path.Value ?? "unknown";
        var method = context.Request.Method;
        var errorType = exception.GetType().Name;

        var tags = new[]
        {
            new KeyValuePair<string, object?>(MetricTags.Endpoint, path),
            new KeyValuePair<string, object?>(MetricTags.Method, method),
            new KeyValuePair<string, object?>(MetricTags.ErrorType, errorType)
        };

        _metrics.IncrementCounter(ApplicationMetrics.Errors.UnhandledExceptions, tags: tags);
        _metrics.RecordHistogram(ApplicationMetrics.Api.RequestDuration, duration.TotalMilliseconds, tags);

        _logger.LogError(exception, "Unhandled exception in request {Method} {Path}", method, path);
    }

    private void RecordBusinessMetrics(HttpContext context, TimeSpan duration)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var method = context.Request.Method;
        var statusCode = context.Response.StatusCode;

        // Authentication metrics
        if (path.Contains("/auth/login"))
        {
            _metrics.IncrementCounter(ApplicationMetrics.Auth.LoginAttempts);
            
            if (statusCode == 200)
            {
                _metrics.IncrementCounter(ApplicationMetrics.Auth.LoginSuccess);
                _metrics.IncrementCounter(ApplicationMetrics.Auth.TokensIssued);
            }
            else if (statusCode == 401 || statusCode == 403)
            {
                _metrics.IncrementCounter(ApplicationMetrics.Auth.LoginFailure);
            }
        }

        // User management metrics
        if (path.Contains("/users"))
        {
            if (method == "POST" && statusCode == 201)
            {
                _metrics.IncrementCounter(ApplicationMetrics.Business.UsersCreated);
            }
            else if (method == "DELETE" && statusCode == 204)
            {
                _metrics.IncrementCounter(ApplicationMetrics.Business.UsersDeleted);
            }
        }

        // Group management metrics
        if (path.Contains("/groups") && method == "POST" && statusCode == 201)
        {
            _metrics.IncrementCounter(ApplicationMetrics.Business.GroupsCreated);
        }

        // Role assignment metrics
        if (path.Contains("/roles") && method == "POST" && statusCode == 200)
        {
            _metrics.IncrementCounter(ApplicationMetrics.Business.RolesAssigned);
        }

        // Permission check metrics
        if (path.Contains("/permissions/check"))
        {
            _metrics.IncrementCounter(ApplicationMetrics.Business.PermissionChecks);
            
            if (statusCode == 200)
            {
                _metrics.IncrementCounter(ApplicationMetrics.Business.PermissionsGranted);
            }
            else if (statusCode == 403)
            {
                _metrics.IncrementCounter(ApplicationMetrics.Business.PermissionsDenied);
            }
        }

        // Resource access metrics
        if (path.Contains("/resources"))
        {
            _metrics.IncrementCounter(ApplicationMetrics.Business.ResourcesAccessed);
        }

        // Record tenant metrics if available
        if (context.Items.TryGetValue("TenantId", out var tenantId) && tenantId != null)
        {
            _metrics.IncrementCounter(ApplicationMetrics.Tenant.TenantRequests,
                tags: new[]
                {
                    new KeyValuePair<string, object?>(MetricTags.TenantId, tenantId.ToString())
                });
        }
    }
}