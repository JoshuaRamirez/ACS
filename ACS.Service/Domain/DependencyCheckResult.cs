namespace ACS.Service.Domain;

/// <summary>
/// Result of checking dependencies for a resource before deletion
/// </summary>
public class DependencyCheckResult
{
    /// <summary>
    /// Whether the resource can be safely deleted
    /// </summary>
    public bool CanDelete { get; set; } = true;

    /// <summary>
    /// List of dependent entities that prevent deletion
    /// </summary>
    public List<Dependency> Dependencies { get; set; } = new();

    /// <summary>
    /// Warning messages about potential issues
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Information messages about the deletion impact
    /// </summary>
    public List<string> Messages { get; set; } = new();
}

/// <summary>
/// Represents a dependency that prevents resource deletion
/// </summary>
public class Dependency
{
    /// <summary>
    /// ID of the dependent entity
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// Name of the dependent entity
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// Type of the dependent entity
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the dependency relationship
    /// </summary>
    public string DependencyType { get; set; } = string.Empty;

    /// <summary>
    /// Additional information about the dependency
    /// </summary>
    public string? Description { get; set; }
}
