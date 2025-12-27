using System.ComponentModel.DataAnnotations;
using ACS.Service.Domain.Validation;

namespace ACS.Service.Domain;

[ValidPermissionCombination]
[ResourceAccessPatternBusinessRule(RestrictedPatterns = new[] { "/system/admin", "/config/secrets" })]
public class Permission
{
    public int Id { get; set; }
    public int EntityId { get; set; }
    
    [Required]
    [ValidUriPattern(AllowWildcards = true, AllowParameters = true, AllowedSchemes = new[] { "http", "https" })]
    public string Uri { get; set; } = string.Empty;
    
    [Required]
    public HttpVerb HttpVerb { get; set; }
    
    public bool Grant { get; set; }
    public bool Deny { get; set; }
    
    [Required]
    public Scheme Scheme { get; set; }
    
    // Additional properties expected by services
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;

    // Extended properties for handler compatibility
    public int PermissionId => Id;
    public string PermissionName => $"{HttpVerb}:{Uri}";
    public string? PermissionDescription => $"Permission to {HttpVerb} {Uri}";
    public int? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public bool IsInherited { get; set; }
    public string? InheritedFrom { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public string? GrantedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}
