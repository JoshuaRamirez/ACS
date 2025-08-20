using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace ACS.Infrastructure.Authentication;

/// <summary>
/// gRPC server interceptor for authentication and authorization
/// </summary>
public class GrpcAuthenticationInterceptor : Interceptor
{
    private readonly ILogger<GrpcAuthenticationInterceptor> _logger;
    private readonly IServiceProvider _serviceProvider;

    public GrpcAuthenticationInterceptor(
        ILogger<GrpcAuthenticationInterceptor> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var method = context.Method;
        var peer = context.Peer; // Client address
        var requestType = typeof(TRequest).Name;
        
        try
        {
            _logger.LogTrace("gRPC authentication started for {Method} ({RequestType}) from {Peer}", 
                method, requestType, peer);

            // Extract authentication information from gRPC metadata
            var authContext = await AuthenticateRequestAsync(context);
            
            if (authContext == null)
            {
                _logger.LogWarning("GRPC AUTH FAILED: Unauthenticated gRPC request to {Method} from {Peer}", method, peer);
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
            }

            // Add authentication context to call context
            var authContextItem = new Metadata.Entry("auth-context", System.Text.Json.JsonSerializer.Serialize(new
            {
                UserId = authContext.UserId,
                TenantId = authContext.TenantId,
                Roles = authContext.Roles.ToArray()
            }));

            // Authorize the request
            var isAuthorized = await AuthorizeRequestAsync(context, authContext);
            if (!isAuthorized)
            {
                _logger.LogWarning("GRPC AUTHORIZATION DENIED: User {UserId} in tenant {TenantId} denied access to {Method} from {Peer}", 
                    authContext.UserId, authContext.TenantId, method, peer);
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Insufficient permissions"));
            }

            _logger.LogInformation("GRPC REQUEST AUTHORIZED: User {UserId} in tenant {TenantId} accessing {Method} from {Peer} in {ElapsedMs}ms", 
                authContext.UserId, authContext.TenantId, method, peer, stopwatch.ElapsedMilliseconds);

            // Continue with the request
            var response = await continuation(request, context);
            
            _logger.LogInformation("GRPC REQUEST COMPLETED: {Method} for user {UserId} completed successfully in {ElapsedMs}ms", 
                method, authContext.UserId, stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning("GRPC REQUEST FAILED: {Method} from {Peer} failed with gRPC status {StatusCode}: {Detail} after {ElapsedMs}ms", 
                method, peer, ex.StatusCode, ex.Status.Detail, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GRPC AUTH ERROR: Exception in gRPC authentication interceptor for {Method} from {Peer} after {ElapsedMs}ms", 
                method, peer, stopwatch.ElapsedMilliseconds);
            throw new RpcException(new Status(StatusCode.Internal, "Authentication error"));
        }
    }

    private async Task<AuthenticationContext?> AuthenticateRequestAsync(ServerCallContext context)
    {
        try
        {
            // Extract Bearer token from metadata
            var authHeader = context.RequestHeaders.FirstOrDefault(h => h.Key == "authorization");
            if (authHeader == null || string.IsNullOrEmpty(authHeader.Value))
            {
                return null;
            }

            var token = ExtractTokenFromHeader(authHeader.Value);
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            // Validate token using JWT service
            using var scope = _serviceProvider.CreateScope();
            var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
            
            var principal = jwtService.ValidateToken(token);
            if (principal == null)
            {
                return null;
            }

            // Extract claims
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var tenantId = principal.FindFirst("tenant_id")?.Value ?? string.Empty;
            var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            return new AuthenticationContext
            {
                UserId = userId,
                TenantId = tenantId,
                Roles = roles,
                Principal = principal,
                AuthenticatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to authenticate gRPC request");
            return null;
        }

        await Task.CompletedTask;
    }

    private async Task<bool> AuthorizeRequestAsync(ServerCallContext context, AuthenticationContext authContext)
    {
        try
        {
            // For now, implement basic authorization rules
            // In a real implementation, this would check against the ACS permission system
            
            var method = context.Method;
            var tenantId = authContext.TenantId;

            // All authenticated users can access their own tenant's data
            // More sophisticated authorization can be implemented here
            
            // Example authorization rules:
            if (method.Contains("Execute") || method.Contains("Command"))
            {
                // Command operations require elevated permissions
                if (!authContext.IsInRole("Admin") && !authContext.IsInRole("Operator"))
                {
                    return false;
                }
            }

            // Check tenant isolation - users can only access their own tenant
            var requestTenantId = ExtractTenantIdFromContext(context);
            if (!string.IsNullOrEmpty(requestTenantId) && requestTenantId != tenantId)
            {
                _logger.LogWarning("Cross-tenant access attempt: User {UserId} from tenant {UserTenant} tried to access tenant {RequestTenant}",
                    authContext.UserId, tenantId, requestTenantId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in authorization check");
            return false;
        }

        await Task.CompletedTask;
    }

    private static string? ExtractTokenFromHeader(string authHeaderValue)
    {
        if (authHeaderValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeaderValue["Bearer ".Length..].Trim();
        }
        return null;
    }

    private static string? ExtractTenantIdFromContext(ServerCallContext context)
    {
        // Try to extract tenant ID from various sources
        var tenantHeader = context.RequestHeaders.FirstOrDefault(h => h.Key == "tenant-id");
        if (tenantHeader != null)
        {
            return tenantHeader.Value;
        }

        // Could also extract from path or other metadata
        return null;
    }
}

/// <summary>
/// Extension methods for adding gRPC authentication
/// </summary>
public static class GrpcAuthenticationExtensions
{
    /// <summary>
    /// Add gRPC authentication services
    /// </summary>
    public static IServiceCollection AddGrpcAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<GrpcAuthenticationInterceptor>();
        
        return services;
    }
}