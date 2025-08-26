using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ACS.Infrastructure.RateLimiting;

/// <summary>
/// ASP.NET Core middleware for tenant-aware rate limiting
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingConfiguration _configuration;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration.GetSection("RateLimit").Get<RateLimitingConfiguration>() 
                        ?? new RateLimitingConfiguration();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_configuration.Enabled)
        {
            await _next(context);
            return;
        }

        // Skip rate limiting for excluded paths
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (_configuration.ExcludePaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var rateLimitService = context.RequestServices.GetRequiredService<IRateLimitingService>();
        
        try
        {
            var tenantId = GetTenantId(context);
            var rateLimitKey = GetRateLimitKey(context, tenantId);
            var policy = GetApplicablePolicy(context, tenantId);

            var result = await rateLimitService.CheckRateLimitAsync(tenantId, rateLimitKey, policy);

            // Add rate limit headers to response
            AddRateLimitHeaders(context, result);

            if (!result.IsAllowed)
            {
                await HandleRateLimitExceeded(context, result);
                return;
            }

            // Track successful requests
            context.Items["RateLimitResult"] = result;
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rate limiting middleware");
            
            // On error, allow request to proceed (fail open)
            await _next(context);
        }
    }

    private string GetTenantId(HttpContext context)
    {
        // Try multiple sources for tenant identification
        
        // 1. Check context items (set by tenant resolution middleware)
        if (context.Items.TryGetValue("TenantId", out var tenantIdObj))
        {
            return tenantIdObj?.ToString() ?? "unknown";
        }

        // 2. Check custom header
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValues))
        {
            return headerValues.FirstOrDefault() ?? "unknown";
        }

        // 3. Check query parameter
        if (context.Request.Query.TryGetValue("tenantId", out var queryValues))
        {
            return queryValues.FirstOrDefault() ?? "unknown";
        }

        // 4. Check JWT claims (if available)
        var tenantClaim = context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantClaim))
        {
            return tenantClaim;
        }

        // 5. Fallback to subdomain or host-based detection
        var host = context.Request.Host.Host;
        if (host.Contains('.'))
        {
            var parts = host.Split('.');
            if (parts.Length >= 3) // subdomain.domain.tld
            {
                return parts[0];
            }
        }

        return "default";
    }

    private string GetRateLimitKey(HttpContext context, string tenantId)
    {
        var keyBuilder = new List<string>();

        // Include tenant ID
        keyBuilder.Add(tenantId);

        switch (_configuration.KeyStrategy)
        {
            case RateLimitKeyStrategy.IpAddress:
                keyBuilder.Add(GetClientIpAddress(context));
                break;
                
            case RateLimitKeyStrategy.User:
                var userId = context.User?.Identity?.Name ?? "anonymous";
                keyBuilder.Add(userId);
                break;
                
            case RateLimitKeyStrategy.UserAndEndpoint:
                var userIdWithEndpoint = context.User?.Identity?.Name ?? "anonymous";
                var endpoint = GetEndpointIdentifier(context);
                keyBuilder.Add($"{userIdWithEndpoint}:{endpoint}");
                break;
                
            case RateLimitKeyStrategy.ApiKey:
                var apiKey = GetApiKey(context);
                keyBuilder.Add(apiKey ?? "no-key");
                break;
                
            case RateLimitKeyStrategy.Combined:
            default:
                keyBuilder.Add(GetClientIpAddress(context));
                keyBuilder.Add(context.User?.Identity?.Name ?? "anonymous");
                keyBuilder.Add(GetEndpointIdentifier(context));
                break;
        }

        return string.Join(":", keyBuilder);
    }

    private RateLimitPolicy GetApplicablePolicy(HttpContext context, string tenantId)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var method = context.Request.Method.ToUpperInvariant();

        // Check for endpoint-specific policies
        foreach (var endpointPolicy in _configuration.EndpointPolicies)
        {
            if (IsEndpointMatch(path, method, endpointPolicy))
            {
                return CreatePolicy(endpointPolicy, tenantId);
            }
        }

        // Check for tenant-specific policies
        if (_configuration.TenantPolicies.TryGetValue(tenantId, out var tenantPolicy))
        {
            return CreatePolicy(tenantPolicy, tenantId);
        }

        // Return default policy
        return CreatePolicy(_configuration.DefaultPolicy, tenantId);
    }

    private bool IsEndpointMatch(string path, string method, EndpointRateLimitPolicy endpointPolicy)
    {
        var pathMatches = endpointPolicy.PathPattern == "*" ||
                         path.StartsWith(endpointPolicy.PathPattern, StringComparison.OrdinalIgnoreCase);
        
        var methodMatches = endpointPolicy.HttpMethods.Contains("*") ||
                           endpointPolicy.HttpMethods.Contains(method);

        return pathMatches && methodMatches;
    }

    private RateLimitPolicy CreatePolicy(RateLimitPolicyConfig config, string tenantId)
    {
        return new RateLimitPolicy
        {
            RequestLimit = config.RequestLimit,
            WindowSizeSeconds = config.WindowSizeSeconds,
            Algorithm = config.Algorithm,
            UseDistributedStorage = config.UseDistributedStorage,
            PolicyName = $"{config.PolicyName}_{tenantId}",
            Priority = config.Priority,
            CustomHeaders = config.CustomHeaders
        };
    }

    private void AddRateLimitHeaders(HttpContext context, RateLimitResult result)
    {
        context.Response.Headers.TryAdd("X-RateLimit-Limit", result.RequestLimit.ToString());
        context.Response.Headers.TryAdd("X-RateLimit-Remaining", result.RemainingRequests.ToString());
        context.Response.Headers.TryAdd("X-RateLimit-Reset", result.ResetTimeSeconds.ToString());
        context.Response.Headers.TryAdd("X-RateLimit-Policy", result.PolicyName);

        if (result.RetryAfter.HasValue)
        {
            context.Response.Headers.TryAdd("Retry-After", ((int)result.RetryAfter.Value.TotalSeconds).ToString());
        }
    }

    private async Task HandleRateLimitExceeded(HttpContext context, RateLimitResult result)
    {
        context.Response.StatusCode = (int)result.StatusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = "rate_limit_exceeded",
            message = "Too many requests. Please try again later.",
            details = new
            {
                limit = result.RequestLimit,
                remaining = result.RemainingRequests,
                resetTime = result.ResetTimeSeconds,
                policy = result.PolicyName,
                retryAfter = result.RetryAfter?.TotalSeconds
            }
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _logger.LogWarning("Rate limit exceeded for policy {PolicyName}. Limit: {Limit}, Reset in: {ResetTime}s",
            result.PolicyName, result.RequestLimit, result.ResetTimeSeconds);

        await context.Response.WriteAsync(json);
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP headers (load balancer, proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string GetEndpointIdentifier(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        
        // Normalize path by removing trailing slashes and query parameters
        path = path.TrimEnd('/');
        if (string.IsNullOrEmpty(path))
            path = "/";

        return $"{method}:{path}";
    }

    private string? GetApiKey(HttpContext context)
    {
        // Check Authorization header
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader[7..]; // Remove "ApiKey " prefix
        }

        // Check custom header
        var apiKeyHeader = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKeyHeader))
        {
            return apiKeyHeader;
        }

        // Check query parameter
        var apiKeyQuery = context.Request.Query["apikey"].FirstOrDefault();
        return apiKeyQuery;
    }
}