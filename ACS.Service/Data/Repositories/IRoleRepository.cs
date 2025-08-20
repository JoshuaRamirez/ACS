using ACS.Service.Data.Models;
using System.Linq.Expressions;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Repository interface for Role-specific operations
/// </summary>
public interface IRoleRepository : IRepository<Role>
{
    /// <summary>
    /// Find role by name
    /// </summary>
    Task<Role?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find roles assigned to a user
    /// </summary>
    Task<IEnumerable<Role>> FindRolesForUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find roles assigned to a group
    /// </summary>
    Task<IEnumerable<Role>> FindRolesForGroupAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find roles with specific permission
    /// </summary>
    Task<IEnumerable<Role>> FindRolesByPermissionAsync(string resourceUri, string verb, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get role with all permissions
    /// </summary>
    Task<Role?> GetRoleWithPermissionsAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get roles with permission counts
    /// </summary>
    Task<IEnumerable<RoleWithPermissionCount>> GetRolesWithPermissionCountsAsync(Expression<Func<Role, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get roles with assignment counts
    /// </summary>
    Task<IEnumerable<RoleWithAssignmentCount>> GetRolesWithAssignmentCountsAsync(Expression<Func<Role, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find unused roles (roles not assigned to any users or groups)
    /// </summary>
    Task<IEnumerable<Role>> FindUnusedRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Find roles by permission pattern
    /// </summary>
    Task<IEnumerable<Role>> FindRolesByResourcePatternAsync(string resourcePattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get role statistics
    /// </summary>
    Task<RoleStatistics> GetRoleStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if role name exists (excluding specific role)
    /// </summary>
    Task<bool> RoleNameExistsAsync(string name, int? excludeRoleId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign role to user
    /// </summary>
    Task AssignRoleToUserAsync(int userId, int roleId, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove role from user
    /// </summary>
    Task RemoveRoleFromUserAsync(int userId, int roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign role to group
    /// </summary>
    Task AssignRoleToGroupAsync(int groupId, int roleId, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove role from group
    /// </summary>
    Task RemoveRoleFromGroupAsync(int groupId, int roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get effective roles for user (direct + inherited from groups)
    /// </summary>
    Task<IEnumerable<Role>> GetEffectiveRolesForUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find roles that conflict with each other (have overlapping permissions)
    /// </summary>
    Task<IEnumerable<RoleConflict>> FindRoleConflictsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get role hierarchy based on permission inclusion
    /// </summary>
    Task<IEnumerable<RoleHierarchyNode>> GetRoleHierarchyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Role with permission count information
/// </summary>
public class RoleWithPermissionCount
{
    public Role Role { get; set; } = null!;
    public int PermissionCount { get; set; }
    public int ResourceCount { get; set; }
    public IEnumerable<string> ResourceTypes { get; set; } = new List<string>();
}

/// <summary>
/// Role with assignment count information
/// </summary>
public class RoleWithAssignmentCount
{
    public Role Role { get; set; } = null!;
    public int UserAssignmentCount { get; set; }
    public int GroupAssignmentCount { get; set; }
    public int TotalAssignmentCount => UserAssignmentCount + GroupAssignmentCount;
}

/// <summary>
/// Role conflict information
/// </summary>
public class RoleConflict
{
    public Role Role1 { get; set; } = null!;
    public Role Role2 { get; set; } = null!;
    public IEnumerable<string> ConflictingResources { get; set; } = new List<string>();
    public string ConflictType { get; set; } = string.Empty; // "Full", "Partial", "Grant-Deny"
}

/// <summary>
/// Role hierarchy node
/// </summary>
public class RoleHierarchyNode
{
    public Role Role { get; set; } = null!;
    public IEnumerable<RoleHierarchyNode> IncludedRoles { get; set; } = new List<RoleHierarchyNode>();
    public double PermissionOverlap { get; set; }
}

/// <summary>
/// Role statistics model
/// </summary>
public class RoleStatistics
{
    public int TotalRoles { get; set; }
    public int UnusedRoles { get; set; }
    public int RolesWithUsers { get; set; }
    public int RolesWithGroups { get; set; }
    public int TotalUserRoleAssignments { get; set; }
    public int TotalGroupRoleAssignments { get; set; }
    public Dictionary<string, int> RolesByResourceType { get; set; } = new();
    public Dictionary<string, int> RolesByPermissionCount { get; set; } = new();
}