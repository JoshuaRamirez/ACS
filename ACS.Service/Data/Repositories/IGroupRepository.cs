using ACS.Service.Data.Models;
using System.Linq.Expressions;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Repository interface for Group-specific operations
/// </summary>
public interface IGroupRepository : IRepository<Group>
{
    /// <summary>
    /// Find group by name
    /// </summary>
    Task<Group?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find groups containing a user
    /// </summary>
    Task<IEnumerable<Group>> FindGroupsForUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find child groups of a parent group
    /// </summary>
    Task<IEnumerable<Group>> FindChildGroupsAsync(int parentGroupId, bool recursive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find parent groups of a child group
    /// </summary>
    Task<IEnumerable<Group>> FindParentGroupsAsync(int childGroupId, bool recursive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a group is ancestor of another group
    /// </summary>
    Task<bool> IsAncestorOfAsync(int ancestorGroupId, int descendantGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if adding a parent-child relationship would create a cycle
    /// </summary>
    Task<bool> WouldCreateCycleAsync(int parentGroupId, int childGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get group with all users and roles
    /// </summary>
    Task<Group?> GetGroupWithUsersAndRolesAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get group hierarchy tree
    /// </summary>
    Task<IEnumerable<GroupHierarchyNode>> GetGroupHierarchyTreeAsync(int? rootGroupId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find groups by role
    /// </summary>
    Task<IEnumerable<Group>> FindGroupsByRoleAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get groups with member counts
    /// </summary>
    Task<IEnumerable<GroupWithMemberCount>> GetGroupsWithMemberCountsAsync(Expression<Func<Group, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find root groups (groups with no parents)
    /// </summary>
    Task<IEnumerable<Group>> FindRootGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Find leaf groups (groups with no children)
    /// </summary>
    Task<IEnumerable<Group>> FindLeafGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get group statistics
    /// </summary>
    Task<GroupStatistics> GetGroupStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if group name exists (excluding specific group)
    /// </summary>
    Task<bool> GroupNameExistsAsync(string name, int? excludeGroupId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add user to group
    /// </summary>
    Task AddUserToGroupAsync(int userId, int groupId, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove user from group
    /// </summary>
    Task RemoveUserFromGroupAsync(int userId, int groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add parent-child group relationship
    /// </summary>
    Task AddGroupHierarchyAsync(int parentGroupId, int childGroupId, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove parent-child group relationship
    /// </summary>
    Task RemoveGroupHierarchyAsync(int parentGroupId, int childGroupId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Group hierarchy node for tree representation
/// </summary>
public class GroupHierarchyNode
{
    public Group Group { get; set; } = null!;
    public IEnumerable<GroupHierarchyNode> Children { get; set; } = new List<GroupHierarchyNode>();
    public int Level { get; set; }
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// Group with member count information
/// </summary>
public class GroupWithMemberCount
{
    public Group Group { get; set; } = null!;
    public int UserCount { get; set; }
    public int RoleCount { get; set; }
    public int ChildGroupCount { get; set; }
}

/// <summary>
/// Group statistics model
/// </summary>
public class GroupStatistics
{
    public int TotalGroups { get; set; }
    public int RootGroups { get; set; }
    public int LeafGroups { get; set; }
    public int MaxHierarchyDepth { get; set; }
    public int TotalUserMemberships { get; set; }
    public int TotalRoleAssignments { get; set; }
    public Dictionary<string, int> GroupsByType { get; set; } = new();
}