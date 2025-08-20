namespace ACS.Infrastructure.Services;

/// <summary>
/// Service for accessing current user context information
/// </summary>
public interface IUserContextService
{
    /// <summary>
    /// Gets the current user ID
    /// </summary>
    string GetCurrentUserId();
    
    /// <summary>
    /// Gets the current user name
    /// </summary>
    string GetCurrentUserName();
    
    /// <summary>
    /// Gets the tenant ID from user claims
    /// </summary>
    string GetTenantId();
    
    /// <summary>
    /// Checks if the current user is authenticated
    /// </summary>
    bool IsAuthenticated();
    
    /// <summary>
    /// Gets all user roles
    /// </summary>
    IEnumerable<string> GetUserRoles();
    
    /// <summary>
    /// Checks if user has a specific role
    /// </summary>
    bool HasRole(string role);
    
    /// <summary>
    /// Gets user email
    /// </summary>
    string? GetUserEmail();
    
    /// <summary>
    /// Gets a specific claim value
    /// </summary>
    string? GetClaim(string claimType);
    
    /// <summary>
    /// Gets all claims for the current user
    /// </summary>
    IEnumerable<(string Type, string Value)> GetAllClaims();
}