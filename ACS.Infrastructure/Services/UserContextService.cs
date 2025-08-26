using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ACS.Infrastructure.Services;

/// <summary>
/// Implementation of user context service for accessing current user information
/// </summary>
public class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserContextService> _logger;
    
    public UserContextService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserContextService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }
    
    public string GetCurrentUserId()
    {
        var userId = GetClaim(ClaimTypes.NameIdentifier) ?? 
                    GetClaim("sub") ?? 
                    GetClaim("user_id") ?? 
                    GetClaim("uid");
                    
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User ID not found in authentication context");
        }
        
        return userId;
    }
    
    public string GetCurrentUserName()
    {
        var userName = GetClaim(ClaimTypes.Name) ?? 
                      GetClaim("name") ?? 
                      GetClaim("username") ?? 
                      GetClaim("preferred_username");
                      
        return userName ?? GetCurrentUserId();
    }
    
    public string GetTenantId()
    {
        // Try standard tenant claim types
        var tenantId = GetClaim("tenant_id") ?? 
                      GetClaim("tid") ?? 
                      GetClaim("tenantid") ?? 
                      GetClaim("tenant");
        
        // Fall back to request header
        if (string.IsNullOrEmpty(tenantId))
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.Request?.Headers?.ContainsKey("X-Tenant-ID") == true)
            {
                tenantId = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
            }
        }
        
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new UnauthorizedAccessException("Tenant ID not found in authentication context or headers");
        }
        
        return tenantId;
    }
    
    public bool IsAuthenticated()
    {
        var context = _httpContextAccessor.HttpContext;
        return context?.User?.Identity?.IsAuthenticated == true;
    }
    
    public IEnumerable<string> GetUserRoles()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User == null)
        {
            return Enumerable.Empty<string>();
        }
        
        var roleClaims = context.User.FindAll(ClaimTypes.Role)
            .Concat(context.User.FindAll("role"))
            .Concat(context.User.FindAll("roles"));
            
        return roleClaims.Select(c => c.Value).Distinct();
    }
    
    public bool HasRole(string role)
    {
        if (string.IsNullOrEmpty(role))
        {
            return false;
        }
        
        return GetUserRoles().Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    }
    
    public string? GetUserEmail()
    {
        return GetClaim(ClaimTypes.Email) ?? 
               GetClaim("email") ?? 
               GetClaim("email_address");
    }
    
    public string? GetClaim(string claimType)
    {
        if (string.IsNullOrEmpty(claimType))
        {
            return null;
        }
        
        var context = _httpContextAccessor.HttpContext;
        return context?.User?.FindFirst(claimType)?.Value;
    }
    
    public IEnumerable<(string Type, string Value)> GetAllClaims()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User == null)
        {
            return Enumerable.Empty<(string Type, string Value)>();
        }
        
        return context.User.Claims.Select(c => (c.Type, c.Value));
    }
}