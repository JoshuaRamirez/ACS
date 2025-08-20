using ACS.Infrastructure.Telemetry;
using System.Diagnostics;

namespace ACS.WebApi.Middleware;

/// <summary>
/// Middleware to collect business metrics and tenant usage data
/// </summary>
public class BusinessMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BusinessMetricsMiddleware> _logger;

    public BusinessMetricsMiddleware(RequestDelegate next, ILogger<BusinessMetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var tenantId = ExtractTenantId(context);
        var userId = ExtractUserId(context);
        var requestPath = context.Request.Path.Value ?? "unknown";
        var httpMethod = context.Request.Method;

        // Skip metrics collection for health checks and metrics endpoints
        if (ShouldSkipMetrics(requestPath))
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);

            // Record successful request metrics
            var duration = stopwatch.Elapsed.TotalSeconds;
            var statusCode = context.Response.StatusCode;

            OpenTelemetryConfiguration.RecordHttpRequest(httpMethod, requestPath, statusCode, duration, tenantId);

            // Record tenant usage metrics
            RecordTenantUsageMetrics(tenantId, userId, requestPath, httpMethod, statusCode, duration);

            // Record business operation metrics
            RecordBusinessOperationMetrics(requestPath, httpMethod, statusCode, duration, tenantId, userId);

            _logger.LogDebug("Request {Method} {Path} completed with status {StatusCode} for tenant {TenantId} in {Duration}ms",
                httpMethod, requestPath, statusCode, tenantId ?? "unknown", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Record error metrics
            var duration = stopwatch.Elapsed.TotalSeconds;
            var statusCode = context.Response.StatusCode == 200 ? 500 : context.Response.StatusCode; // Default to 500 if not set

            OpenTelemetryConfiguration.RecordHttpRequest(httpMethod, requestPath, statusCode, duration, tenantId);

            _logger.LogError(ex, "Request {Method} {Path} failed for tenant {TenantId} in {Duration}ms",
                httpMethod, requestPath, tenantId ?? "unknown", stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    private static string? ExtractTenantId(HttpContext context)
    {
        // Try multiple sources for tenant ID
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var headerValue))
        {
            return headerValue.FirstOrDefault();
        }

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(tenantClaim))
            {
                return tenantClaim;
            }
        }

        // Check route values
        if (context.Request.RouteValues.TryGetValue("tenantId", out var routeValue))
        {
            return routeValue?.ToString();
        }

        return null;
    }

    private static string? ExtractUserId(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            return context.User.FindFirst("sub")?.Value ?? 
                   context.User.FindFirst("user_id")?.Value ??
                   context.User.FindFirst("id")?.Value;
        }

        return null;
    }

    private static bool ShouldSkipMetrics(string requestPath)
    {
        return requestPath.StartsWith("/health") ||
               requestPath.StartsWith("/metrics") ||
               requestPath.StartsWith("/swagger") ||
               requestPath.StartsWith("/_framework") ||
               requestPath.StartsWith("/favicon.ico");
    }

    private static void RecordTenantUsageMetrics(string? tenantId, string? userId, string requestPath, string httpMethod, int statusCode, double duration)
    {
        if (string.IsNullOrEmpty(tenantId))
            return;

        var tags = new[]
        {
            new KeyValuePair<string, object?>("tenant.id", tenantId),
            new KeyValuePair<string, object?>("user.id", userId ?? "anonymous"),
            new KeyValuePair<string, object?>("http.method", httpMethod),
            new KeyValuePair<string, object?>("http.status_code", statusCode),
            new KeyValuePair<string, object?>("api.endpoint", GetEndpointCategory(requestPath))
        };

        // Record tenant-specific request count
        OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_tenant_requests_total", "Total requests per tenant")
            .Add(1, tags);

        // Record tenant-specific request duration
        OpenTelemetryConfiguration.ServiceMeter.CreateHistogram<double>("acs_tenant_request_duration_seconds", "Request duration per tenant")
            .Record(duration, tags);

        // Record API usage by category
        var category = GetApiCategory(requestPath);
        if (!string.IsNullOrEmpty(category))
        {
            var categoryTags = new[]
            {
                new KeyValuePair<string, object?>("tenant.id", tenantId),
                new KeyValuePair<string, object?>("api.category", category),
                new KeyValuePair<string, object?>("success", statusCode < 400)
            };

            OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_api_category_usage_total", "API usage by category")
                .Add(1, categoryTags);
        }
    }

    private static void RecordBusinessOperationMetrics(string requestPath, string httpMethod, int statusCode, double duration, string? tenantId, string? userId)
    {
        var operationType = GetBusinessOperationType(requestPath, httpMethod);
        if (string.IsNullOrEmpty(operationType))
            return;

        var tags = new[]
        {
            new KeyValuePair<string, object?>("operation.type", operationType),
            new KeyValuePair<string, object?>("operation.success", statusCode < 400),
            new KeyValuePair<string, object?>("tenant.id", tenantId ?? "unknown"),
            new KeyValuePair<string, object?>("user.id", userId ?? "anonymous")
        };

        OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_business_operations_total", "Total business operations")
            .Add(1, tags);

        OpenTelemetryConfiguration.ServiceMeter.CreateHistogram<double>("acs_business_operation_duration_seconds", "Business operation duration")
            .Record(duration, tags);

        // Record specific business metrics based on operation type
        RecordSpecificBusinessMetrics(operationType, statusCode, tenantId);
    }

    private static void RecordSpecificBusinessMetrics(string operationType, int statusCode, string? tenantId)
    {
        var success = statusCode < 400;
        var tags = new[]
        {
            new KeyValuePair<string, object?>("tenant.id", tenantId ?? "unknown"),
            new KeyValuePair<string, object?>("success", success)
        };

        switch (operationType)
        {
            case "user_creation":
                OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_users_created_total", "Total users created")
                    .Add(1, tags);
                break;
            case "permission_check":
                OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_permission_checks_total", "Total permission checks")
                    .Add(1, tags);
                break;
            case "role_assignment":
                OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_role_assignments_total", "Total role assignments")
                    .Add(1, tags);
                break;
            case "group_operation":
                OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_group_operations_total", "Total group operations")
                    .Add(1, tags);
                break;
            case "resource_access":
                OpenTelemetryConfiguration.ServiceMeter.CreateCounter<long>("acs_resource_accesses_total", "Total resource access attempts")
                    .Add(1, tags);
                break;
        }
    }

    private static string GetEndpointCategory(string requestPath)
    {
        var path = requestPath.ToLowerInvariant();

        if (path.StartsWith("/api/users")) return "users";
        if (path.StartsWith("/api/groups")) return "groups";
        if (path.StartsWith("/api/roles")) return "roles";
        if (path.StartsWith("/api/permissions")) return "permissions";
        if (path.StartsWith("/api/resources")) return "resources";
        if (path.StartsWith("/api/auth")) return "authentication";
        if (path.StartsWith("/api/admin")) return "administration";
        if (path.StartsWith("/api/tenants")) return "tenant_management";

        return "other";
    }

    private static string? GetApiCategory(string requestPath)
    {
        var path = requestPath.ToLowerInvariant();

        if (path.Contains("/users")) return "user_management";
        if (path.Contains("/groups")) return "group_management";
        if (path.Contains("/roles")) return "role_management";
        if (path.Contains("/permissions")) return "permission_management";
        if (path.Contains("/resources")) return "resource_management";
        if (path.Contains("/auth")) return "authentication";
        if (path.Contains("/admin")) return "administration";

        return null;
    }

    private static string? GetBusinessOperationType(string requestPath, string httpMethod)
    {
        var path = requestPath.ToLowerInvariant();
        var method = httpMethod.ToUpperInvariant();

        // User operations
        if (path.Contains("/users"))
        {
            return method switch
            {
                "POST" => "user_creation",
                "PUT" or "PATCH" => "user_update",
                "DELETE" => "user_deletion",
                _ => "user_query"
            };
        }

        // Permission operations
        if (path.Contains("/permissions") || path.Contains("/authorize"))
        {
            return "permission_check";
        }

        // Role operations
        if (path.Contains("/roles"))
        {
            return method switch
            {
                "POST" => "role_creation",
                "PUT" or "PATCH" => "role_assignment",
                _ => "role_query"
            };
        }

        // Group operations
        if (path.Contains("/groups"))
        {
            return "group_operation";
        }

        // Resource access
        if (path.Contains("/resources"))
        {
            return "resource_access";
        }

        return null;
    }
}

/// <summary>
/// Extension methods for registering the business metrics middleware
/// </summary>
public static class BusinessMetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseBusinessMetrics(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<BusinessMetricsMiddleware>();
    }
}