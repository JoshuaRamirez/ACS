using ACS.Infrastructure.Services;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ACS.Infrastructure.Services;

/// <summary>
/// Implementation of tenant context service with async-safe context propagation
/// </summary>
public class TenantContextService : ITenantContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserContextService _userContextService;
    private readonly ILogger<TenantContextService> _logger;
    
    // AsyncLocal for cross-async boundary context propagation
    private static readonly AsyncLocal<TenantContext> _tenantContext = new();
    
    public TenantContextService(
        IHttpContextAccessor httpContextAccessor,
        IUserContextService userContextService,
        ILogger<TenantContextService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _userContextService = userContextService;
        _logger = logger;
    }

    public string? GetTenantId()
    {
        // Priority 1: AsyncLocal context (for async propagation)
        var asyncTenantId = _tenantContext.Value?.TenantId;
        if (!string.IsNullOrEmpty(asyncTenantId))
        {
            return asyncTenantId;
        }
        
        // Priority 2: HttpContext items (set by middleware)
        var httpContext = _httpContextAccessor.HttpContext;
        var httpTenantId = httpContext?.Items["TenantId"]?.ToString();
        if (!string.IsNullOrEmpty(httpTenantId))
        {
            return httpTenantId;
        }
        
        // Priority 3: User claims (from JWT token)
        try
        {
            if (_userContextService.IsAuthenticated())
            {
                return _userContextService.GetTenantId();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not get tenant ID from user context");
        }
        
        return null;
    }

    public string GetRequiredTenantId()
    {
        var tenantId = GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new UnauthorizedAccessException("Tenant ID not found in request context");
        }
        return tenantId;
    }

    public TenantProcessInfo? GetTenantProcessInfo()
    {
        // Priority 1: AsyncLocal context
        var asyncInfo = _tenantContext.Value?.ProcessInfo;
        if (asyncInfo != null)
        {
            return asyncInfo;
        }
        
        // Priority 2: HttpContext items
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Items["TenantProcessInfo"] as TenantProcessInfo;
    }

    public GrpcChannel? GetGrpcChannel()
    {
        // Priority 1: AsyncLocal context
        var asyncChannel = _tenantContext.Value?.GrpcChannel;
        if (asyncChannel != null)
        {
            return asyncChannel;
        }
        
        // Priority 2: HttpContext items
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Items["GrpcChannel"] as GrpcChannel;
    }

    public void SetTenantContext(string tenantId, TenantProcessInfo? processInfo = null, GrpcChannel? grpcChannel = null)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));
        }

        // Set in AsyncLocal for async propagation
        _tenantContext.Value = new TenantContext
        {
            TenantId = tenantId,
            ProcessInfo = processInfo,
            GrpcChannel = grpcChannel,
            SetTime = DateTime.UtcNow
        };
        
        // Also set in HttpContext for compatibility
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items["TenantId"] = tenantId;
            if (processInfo != null)
            {
                httpContext.Items["TenantProcessInfo"] = processInfo;
            }
            if (grpcChannel != null)
            {
                httpContext.Items["GrpcChannel"] = grpcChannel;
            }
        }
        
        _logger.LogDebug("Set tenant context for {TenantId}", tenantId);
    }

    public void ClearTenantContext()
    {
        _tenantContext.Value = null;
        
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items.Remove("TenantId");
            httpContext.Items.Remove("TenantProcessInfo");
            httpContext.Items.Remove("GrpcChannel");
        }
        
        _logger.LogDebug("Cleared tenant context");
    }

    public async Task<bool> ValidateTenantAccessAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return false;
            }

            // Check if user is authenticated
            if (!_userContextService.IsAuthenticated())
            {
                return false;
            }

            // Get user's tenant from claims
            var userTenantId = _userContextService.GetTenantId();
            
            // Validate that the requested tenant matches the user's tenant
            // or the user has cross-tenant access
            if (string.Equals(tenantId, userTenantId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            // TODO: Implement cross-tenant access validation
            // This would check if the user has permission to access other tenants
            var userId = _userContextService.GetCurrentUserId();
            _logger.LogWarning("User {UserId} attempted to access tenant {TenantId} but belongs to {UserTenantId}", 
                userId, tenantId, userTenantId);
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating tenant access");
            return false;
        }
    }
}

/// <summary>
/// Internal class for storing tenant context in AsyncLocal
/// </summary>
internal class TenantContext
{
    public string TenantId { get; set; } = string.Empty;
    public TenantProcessInfo? ProcessInfo { get; set; }
    public GrpcChannel? GrpcChannel { get; set; }
    public DateTime SetTime { get; set; }
}