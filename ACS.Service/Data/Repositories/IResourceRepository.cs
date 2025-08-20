using ACS.Service.Data.Models;
using System.Linq.Expressions;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Repository interface for Resource-specific operations
/// </summary>
public interface IResourceRepository : IRepository<Resource>
{
    /// <summary>
    /// Find resource by URI
    /// </summary>
    Task<Resource?> FindByUriAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find resources matching URI pattern
    /// </summary>
    Task<IEnumerable<Resource>> FindByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find resources with access permissions
    /// </summary>
    Task<IEnumerable<Resource>> FindResourcesWithAccessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Find unused resources (resources without any permissions)
    /// </summary>
    Task<IEnumerable<Resource>> FindUnusedResourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get resource with all access permissions
    /// </summary>
    Task<Resource?> GetResourceWithPermissionsAsync(int resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find resources by verb type
    /// </summary>
    Task<IEnumerable<Resource>> FindByVerbTypeAsync(string verbName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get resources with permission counts
    /// </summary>
    Task<IEnumerable<ResourceWithPermissionCount>> GetResourcesWithPermissionCountsAsync(Expression<Func<Resource, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if URI exists (excluding specific resource)
    /// </summary>
    Task<bool> UriExistsAsync(string uri, int? excludeResourceId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get resource statistics
    /// </summary>
    Task<ResourceStatistics> GetResourceStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Find resources by entity permission
    /// </summary>
    Task<IEnumerable<Resource>> FindResourcesByEntityAsync(int entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find resources accessible by user
    /// </summary>
    Task<IEnumerable<Resource>> FindResourcesAccessibleByUserAsync(int userId, string? verbFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Match URI against resource patterns with wildcards
    /// </summary>
    Task<IEnumerable<Resource>> FindMatchingResourcePatternsAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get resource access matrix for analysis
    /// </summary>
    Task<ResourceAccessMatrix> GetResourceAccessMatrixAsync(IEnumerable<int>? resourceIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find resources with conflicting permissions (grant and deny on same resource)
    /// </summary>
    Task<IEnumerable<ResourceConflict>> FindResourceConflictsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resource with permission count information
/// </summary>
public class ResourceWithPermissionCount
{
    public Resource Resource { get; set; } = null!;
    public int TotalPermissions { get; set; }
    public int GrantPermissions { get; set; }
    public int DenyPermissions { get; set; }
    public int UniqueEntities { get; set; }
    public IEnumerable<string> VerbTypes { get; set; } = new List<string>();
}

/// <summary>
/// Resource statistics model
/// </summary>
public class ResourceStatistics
{
    public int TotalResources { get; set; }
    public int ResourcesWithPermissions { get; set; }
    public int UnusedResources { get; set; }
    public int ResourcesWithPatterns { get; set; }
    public Dictionary<string, int> ResourcesByType { get; set; } = new();
    public Dictionary<string, int> VerbDistribution { get; set; } = new();
    public int TotalUriAccesses { get; set; }
    public int GrantAccessCount { get; set; }
    public int DenyAccessCount { get; set; }
}

/// <summary>
/// Resource access matrix for comprehensive analysis
/// </summary>
public class ResourceAccessMatrix
{
    public IEnumerable<Resource> Resources { get; set; } = new List<Resource>();
    public IEnumerable<Entity> Entities { get; set; } = new List<Entity>();
    public IEnumerable<VerbType> VerbTypes { get; set; } = new List<VerbType>();
    public Dictionary<string, AccessPermission> AccessMap { get; set; } = new();
}

/// <summary>
/// Access permission for matrix
/// </summary>
public class AccessPermission
{
    public int ResourceId { get; set; }
    public int EntityId { get; set; }
    public int VerbTypeId { get; set; }
    public bool IsGrant { get; set; }
    public bool IsDeny { get; set; }
    public string SchemeType { get; set; } = string.Empty;
}

/// <summary>
/// Resource conflict information
/// </summary>
public class ResourceConflict
{
    public Resource Resource { get; set; } = null!;
    public VerbType VerbType { get; set; } = null!;
    public IEnumerable<Entity> ConflictingEntities { get; set; } = new List<Entity>();
    public string ConflictType { get; set; } = string.Empty; // "Grant-Deny", "Multiple-Grant", "Multiple-Deny"
    public string Severity { get; set; } = string.Empty; // "High", "Medium", "Low"
}