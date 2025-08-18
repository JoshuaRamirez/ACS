using System.Security.Claims;

namespace ACS.WebApi.Services;

public class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public UserContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    
    public string GetCurrentUserId()
    {
        var userId = GetClaim(ClaimTypes.NameIdentifier) ?? GetClaim("sub");
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User ID not found in authentication context");
        }
        return userId;
    }
    
    public string GetCurrentUserName()
    {
        var userName = GetClaim(ClaimTypes.Name) ?? GetClaim("name") ?? GetClaim("username");
        return userName ?? GetCurrentUserId();
    }
    
    public string GetTenantId()
    {
        // Try to get tenant ID from claims first
        var tenantId = GetClaim("tenant_id") ?? GetClaim("tid");
        
        // Fall back to header if not in claims
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
    
    private string? GetClaim(string claimType)
    {
        var context = _httpContextAccessor.HttpContext;
        return context?.User?.FindFirst(claimType)?.Value;
    }
}