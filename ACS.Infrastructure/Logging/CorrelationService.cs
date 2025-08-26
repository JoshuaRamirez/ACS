using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ACS.Infrastructure.Logging;

/// <summary>
/// Service for managing correlation IDs across requests
/// </summary>
public class CorrelationService : ICorrelationService
{
    private static readonly AsyncLocal<CorrelationContext> _context = new();
    
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CorrelationService> _logger;
    
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string RequestIdHeader = "X-Request-ID";
    private const string TraceIdHeader = "X-Trace-ID";

    public CorrelationService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<CorrelationService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public string CorrelationId => GetCurrentContext().CorrelationId;
    public string RequestId => GetCurrentContext().RequestId;
    public string? SessionId => GetCurrentContext().SessionId;
    public string? UserId => GetCurrentContext().UserId;
    public string? TenantId => GetCurrentContext().TenantId;

    public void SetContext(CorrelationContext context)
    {
        _context.Value = context;
        
        // Also set in HTTP context if available
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items["CorrelationContext"] = context;
            
            // Set response headers for tracing
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.Headers[CorrelationIdHeader] = context.CorrelationId;
                httpContext.Response.Headers[RequestIdHeader] = context.RequestId;
                if (!string.IsNullOrEmpty(context.TraceId))
                {
                    httpContext.Response.Headers[TraceIdHeader] = context.TraceId;
                }
            }
        }
        
        _logger.LogDebug("Set correlation context: {CorrelationId}, Request: {RequestId}", 
            context.CorrelationId, context.RequestId);
    }

    public CorrelationContext GetContext()
    {
        return GetCurrentContext();
    }

    public string CreateChildCorrelationId()
    {
        var current = GetCurrentContext();
        var childId = Guid.NewGuid().ToString();
        
        _logger.LogDebug("Created child correlation ID {ChildId} from parent {ParentId}", 
            childId, current.CorrelationId);
        
        return childId;
    }

    public IDisposable BeginScope(CorrelationContext context)
    {
        return new CorrelationScope(this, context);
    }

    private CorrelationContext GetCurrentContext()
    {
        // Try AsyncLocal first
        var context = _context.Value;
        if (context != null)
        {
            return context;
        }
        
        // Try HTTP context
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue("CorrelationContext", out var httpContextValue) == true &&
            httpContextValue is CorrelationContext correlationContext)
        {
            _context.Value = correlationContext;
            return correlationContext;
        }
        
        // Create new context from HTTP headers or defaults
        context = CreateContextFromHttp();
        _context.Value = context;
        
        return context;
    }

    private CorrelationContext CreateContextFromHttp()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var context = new CorrelationContext();
        
        if (httpContext != null)
        {
            // Extract from headers
            if (httpContext.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationHeader))
            {
                context.CorrelationId = correlationHeader.ToString();
            }
            
            if (httpContext.Request.Headers.TryGetValue(RequestIdHeader, out var requestHeader))
            {
                context.RequestId = requestHeader.ToString();
            }
            
            if (httpContext.Request.Headers.TryGetValue(TraceIdHeader, out var traceHeader))
            {
                context.TraceId = traceHeader.ToString();
            }
            
            // Extract user information
            var user = httpContext.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                context.UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                context.TenantId = user.FindFirst("tenant_id")?.Value;
                context.SessionId = user.FindFirst("session_id")?.Value;
            }
            
            // Extract activity trace information
            var activity = System.Diagnostics.Activity.Current;
            if (activity != null)
            {
                context.TraceId ??= activity.TraceId.ToString();
                context.SpanId = activity.SpanId.ToString();
                context.ParentId = activity.ParentSpanId.ToString();
            }
            
            // Add request properties
            context.Properties["RequestPath"] = httpContext.Request.Path.Value;
            context.Properties["RequestMethod"] = httpContext.Request.Method;
            context.Properties["UserAgent"] = httpContext.Request.Headers["User-Agent"].ToString();
            context.Properties["RemoteIpAddress"] = httpContext.Connection.RemoteIpAddress?.ToString();
        }
        
        return context;
    }

    /// <summary>
    /// Disposable scope for managing correlation context
    /// </summary>
    private class CorrelationScope : IDisposable
    {
        private readonly CorrelationService _service;
        private readonly CorrelationContext? _previousContext;

        public CorrelationScope(CorrelationService service, CorrelationContext newContext)
        {
            _service = service;
            _previousContext = _context.Value;
            _service.SetContext(newContext);
        }

        public void Dispose()
        {
            if (_previousContext != null)
            {
                _service.SetContext(_previousContext);
            }
            else
            {
                _context.Value = new CorrelationContext();
            }
        }
    }
}