using System.ComponentModel.DataAnnotations;
using ACS.Service.Domain.Validation;

namespace ACS.Service.Domain;

[ResourceAccessPatternBusinessRule(
    RestrictedPatterns = new[] { "/system", "/admin/critical", "/config/master" },
    RequiresApproval = new[] { "/finance/transfer", "/user/delete", "/system/shutdown" })]
public class Resource : Entity
{
    [Required]
    [ValidUriPattern(AllowWildcards = true, AllowParameters = true)]
    public string Uri { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [Required]
    [StringLength(100)]
    public string? ResourceType { get; set; }
    
    [RegularExpression(@"^(\d+\.)?(\d+\.)?(\*|\d+)$", ErrorMessage = "Version must follow semantic versioning pattern (e.g., 1.0.0)")]
    public string? Version { get; set; }
    
    public int? ParentResourceId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public List<Permission> Permissions { get; set; } = new();
    public Resource? ParentResource { get; set; }
    public List<Resource> ChildResources { get; set; } = new();

    // Pattern matching methods
    public bool MatchesUri(string requestUri)
    {
        if (string.IsNullOrWhiteSpace(Uri))
            return false;

        // Exact match
        if (Uri.Equals(requestUri, StringComparison.OrdinalIgnoreCase))
            return true;

        // Wildcard match
        if (Uri.Contains("*"))
        {
            var pattern = Uri.Replace("*", ".*");
            return System.Text.RegularExpressions.Regex.IsMatch(requestUri, $"^{pattern}$", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Parameter match (e.g., /api/users/{id})
        if (Uri.Contains("{") && Uri.Contains("}"))
        {
            var pattern = System.Text.RegularExpressions.Regex.Replace(Uri, @"\{[^}]+\}", "([^/]+)");
            return System.Text.RegularExpressions.Regex.IsMatch(requestUri, $"^{pattern}$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return false;
    }

    public Dictionary<string, string> ExtractParameters(string requestUri)
    {
        var parameters = new Dictionary<string, string>();

        if (!Uri.Contains("{") || !Uri.Contains("}"))
            return parameters;

        var parameterNames = System.Text.RegularExpressions.Regex.Matches(Uri, @"\{([^}]+)\}")
            .Select(m => m.Groups[1].Value)
            .ToList();

        var pattern = System.Text.RegularExpressions.Regex.Replace(Uri, @"\{[^}]+\}", "([^/]+)");
        var match = System.Text.RegularExpressions.Regex.Match(requestUri, $"^{pattern}$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            for (int i = 0; i < parameterNames.Count && i < match.Groups.Count - 1; i++)
            {
                parameters[parameterNames[i]] = match.Groups[i + 1].Value;
            }
        }

        return parameters;
    }

    public bool IsParentOf(Resource resource)
    {
        return resource.ParentResourceId == Id;
    }

    public bool IsChildOf(Resource resource)
    {
        return ParentResourceId == resource.Id;
    }

    public bool IsAncestorOf(Resource resource)
    {
        if (resource.ParentResourceId == Id)
            return true;

        if (resource.ParentResource != null)
            return IsAncestorOf(resource.ParentResource);

        return false;
    }

    public bool IsDescendantOf(Resource resource)
    {
        if (ParentResourceId == resource.Id)
            return true;

        if (ParentResource != null)
            return ParentResource.IsDescendantOf(resource);

        return false;
    }

    public int GetDepth()
    {
        if (ParentResource == null)
            return 0;

        return ParentResource.GetDepth() + 1;
    }

    public Resource GetRoot()
    {
        if (ParentResource == null)
            return this;

        return ParentResource.GetRoot();
    }

    public List<Resource> GetAncestors()
    {
        var ancestors = new List<Resource>();

        var current = ParentResource;
        while (current != null)
        {
            ancestors.Add(current);
            current = current.ParentResource;
        }

        return ancestors;
    }

    public List<Resource> GetDescendants()
    {
        var descendants = new List<Resource>();

        foreach (var child in ChildResources)
        {
            descendants.Add(child);
            descendants.AddRange(child.GetDescendants());
        }

        return descendants;
    }

    public override string ToString()
    {
        return $"Resource: {Uri} (Type: {ResourceType ?? "Unknown"}, Version: {Version ?? "1.0.0"})";
    }
}