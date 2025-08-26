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
}