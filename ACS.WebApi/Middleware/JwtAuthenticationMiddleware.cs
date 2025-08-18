using ACS.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ACS.WebApi.Middleware;

/// <summary>
/// Middleware for JWT authentication in WebApi
/// </summary>
public class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtAuthenticationMiddleware> _logger;
    private readonly JwtTokenService _jwtTokenService;

    public JwtAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<JwtAuthenticationMiddleware> logger,
        JwtTokenService jwtTokenService)
    {
        _next = next;
        _logger = logger;
        _jwtTokenService = jwtTokenService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Skip authentication for certain paths
            if (ShouldSkipAuthentication(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // Extract and validate JWT token
            var token = ExtractTokenFromRequest(context.Request);
            if (!string.IsNullOrEmpty(token))
            {
                var principal = _jwtTokenService.ValidateToken(token);
                if (principal != null)
                {
                    context.User = principal;
                    
                    // Add authentication context to request items
                    var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                    var tenantId = principal.FindFirst("tenant_id")?.Value ?? string.Empty;
                    var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

                    var authContext = new AuthenticationContext
                    {
                        UserId = userId,
                        TenantId = tenantId,
                        Roles = roles,
                        Principal = principal,
                        AuthenticatedAt = DateTime.UtcNow
                    };

                    context.Items["AuthContext"] = authContext;
                    context.Items["TenantId"] = tenantId;

                    _logger.LogDebug("Authenticated user {UserId} for tenant {TenantId}", userId, tenantId);
                }
                else
                {
                    _logger.LogWarning("Invalid JWT token provided");
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Invalid token");
                    return;
                }
            }
            else if (RequiresAuthentication(context))
            {
                _logger.LogWarning("No JWT token provided for protected endpoint {Path}", context.Request.Path);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Authentication required");
                return;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in JWT authentication middleware");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Authentication error");
        }
    }

    private static string? ExtractTokenFromRequest(HttpRequest request)
    {
        // Try Authorization header first
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        // Try query parameter as fallback
        return request.Query["access_token"].FirstOrDefault();
    }

    private static bool ShouldSkipAuthentication(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;
        
        // Skip authentication for these paths
        var skipPaths = new[]
        {
            "/health",
            "/swagger",
            "/api-docs",
            "/auth/login",
            "/auth/refresh"
        };

        return skipPaths.Any(skipPath => pathValue.StartsWith(skipPath));
    }

    private static bool RequiresAuthentication(HttpContext context)
    {
        // Check if endpoint has [AllowAnonymous] attribute
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<AllowAnonymousAttribute>() != null)
        {
            return false;
        }

        // All API endpoints require authentication by default
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        return path.StartsWith("/api/");
    }
}

/// <summary>
/// Extension methods for adding JWT authentication middleware
/// </summary>
public static class JwtAuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Add JWT authentication middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseJwtAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<JwtAuthenticationMiddleware>();
    }
}