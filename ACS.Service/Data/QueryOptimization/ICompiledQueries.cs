using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Data.QueryOptimization;

/// <summary>
/// Interface for compiled queries to improve performance
/// </summary>
public interface ICompiledQueries
{
    #region User Queries

    /// <summary>
    /// Get user by email with full security context (compiled query)
    /// </summary>
    Task<User?> GetUserByEmailWithContextAsync(ApplicationDbContext context, string email);

    /// <summary>
    /// Get user with groups and roles (compiled query)
    /// </summary>
    Task<User?> GetUserWithGroupsAndRolesAsync(ApplicationDbContext context, int userId);

    /// <summary>
    /// Get active users count (compiled query)
    /// </summary>
    Task<int> GetActiveUsersCountAsync(ApplicationDbContext context);

    /// <summary>
    /// Get users by role name (compiled query)
    /// </summary>
    Task<List<User>> GetUsersByRoleAsync(ApplicationDbContext context, string roleName);

    #endregion

    #region Group Queries

    /// <summary>
    /// Get group by name with hierarchy (compiled query)
    /// </summary>
    Task<Group?> GetGroupByNameWithHierarchyAsync(ApplicationDbContext context, string groupName);

    /// <summary>
    /// Get child groups (compiled query)
    /// </summary>
    Task<List<Group>> GetChildGroupsAsync(ApplicationDbContext context, int parentGroupId);

    /// <summary>
    /// Get groups for user (compiled query)
    /// </summary>
    Task<List<Group>> GetGroupsForUserAsync(ApplicationDbContext context, int userId);

    #endregion

    #region Role Queries

    /// <summary>
    /// Get role by name with permissions (compiled query)
    /// </summary>
    Task<Role?> GetRoleByNameWithPermissionsAsync(ApplicationDbContext context, string roleName);

    /// <summary>
    /// Get effective roles for user (compiled query)
    /// </summary>
    Task<List<Role>> GetEffectiveRolesForUserAsync(ApplicationDbContext context, int userId);

    /// <summary>
    /// Get roles with permission count (compiled query)
    /// </summary>
    Task<List<RolePermissionCount>> GetRolesWithPermissionCountAsync(ApplicationDbContext context);

    #endregion

    #region Permission Queries

    /// <summary>
    /// Check if user has permission (compiled query)
    /// </summary>
    Task<bool> UserHasPermissionAsync(ApplicationDbContext context, int userId, string resourceUri, string verb);

    /// <summary>
    /// Get user permissions for resource (compiled query)
    /// </summary>
    Task<List<UserPermission>> GetUserPermissionsForResourceAsync(ApplicationDbContext context, int userId, string resourceUri);

    /// <summary>
    /// Get all permissions for entity (compiled query)
    /// </summary>
    Task<List<EntityPermissionInfo>> GetEntityPermissionsAsync(ApplicationDbContext context, int entityId);

    #endregion

    #region Resource Queries

    /// <summary>
    /// Get resource by URI (compiled query)
    /// </summary>
    Task<Resource?> GetResourceByUriAsync(ApplicationDbContext context, string uri);

    /// <summary>
    /// Get resources accessible by user (compiled query)
    /// </summary>
    Task<List<Resource>> GetResourcesAccessibleByUserAsync(ApplicationDbContext context, int userId);

    /// <summary>
    /// Get resource usage statistics (compiled query)
    /// </summary>
    Task<List<ResourceUsageInfo>> GetResourceUsageStatsAsync(ApplicationDbContext context);

    #endregion

    #region Audit Queries

    /// <summary>
    /// Get audit logs for entity (compiled query)
    /// </summary>
    Task<List<AuditLog>> GetAuditLogsForEntityAsync(ApplicationDbContext context, string entityType, int entityId, int limit);

    /// <summary>
    /// Get recent security events (compiled query)
    /// </summary>
    Task<List<AuditLog>> GetRecentSecurityEventsAsync(ApplicationDbContext context, DateTime since);

    #endregion
}

/// <summary>
/// Role permission count model for compiled queries
/// </summary>
public class RolePermissionCount
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int PermissionCount { get; set; }
}

/// <summary>
/// User permission model for compiled queries
/// </summary>
public class UserPermission
{
    public int UserId { get; set; }
    public string ResourceUri { get; set; } = string.Empty;
    public string VerbName { get; set; } = string.Empty;
    public bool IsGrant { get; set; }
    public bool IsDeny { get; set; }
    public string Source { get; set; } = string.Empty; // "Direct", "Role", "Group"
}

/// <summary>
/// Entity permission information for compiled queries
/// </summary>
public class EntityPermissionInfo
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string ResourceUri { get; set; } = string.Empty;
    public string VerbName { get; set; } = string.Empty;
    public bool IsGrant { get; set; }
    public bool IsDeny { get; set; }
    public string SchemeType { get; set; } = string.Empty;
}

/// <summary>
/// Resource usage information for compiled queries
/// </summary>
public class ResourceUsageInfo
{
    public int ResourceId { get; set; }
    public string ResourceUri { get; set; } = string.Empty;
    public int PermissionCount { get; set; }
    public int EntityCount { get; set; }
    public int VerbCount { get; set; }
}