using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Data.QueryOptimization;

/// <summary>
/// Implementation of compiled queries for improved performance
/// </summary>
public class CompiledQueries : ICompiledQueries
{
    #region User Queries

    private static readonly Func<ApplicationDbContext, string, Task<User?>> _getUserByEmailWithContext =
        EF.CompileAsyncQuery((ApplicationDbContext context, string email) =>
            context.Users
                .Include(u => u.Entity)
                .Include(u => u.UserGroups)
                    .ThenInclude(ug => ug.Group)
                        .ThenInclude(g => g.Entity)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.Entity)
                .FirstOrDefault(u => u.Email == email));

    public Task<User?> GetUserByEmailWithContextAsync(ApplicationDbContext context, string email)
        => _getUserByEmailWithContext(context, email);

    private static readonly Func<ApplicationDbContext, int, Task<User?>> _getUserWithGroupsAndRoles =
        EF.CompileAsyncQuery((ApplicationDbContext context, int userId) =>
            context.Users
                .Include(u => u.Entity)
                .Include(u => u.UserGroups)
                    .ThenInclude(ug => ug.Group)
                        .ThenInclude(g => g.Entity)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.Entity)
                .FirstOrDefault(u => u.Id == userId));

    public Task<User?> GetUserWithGroupsAndRolesAsync(ApplicationDbContext context, int userId)
        => _getUserWithGroupsAndRoles(context, userId);

    private static readonly Func<ApplicationDbContext, Task<int>> _getActiveUsersCount =
        EF.CompileAsyncQuery((ApplicationDbContext context) =>
            context.Users.Count(u => u.IsActive));

    public Task<int> GetActiveUsersCountAsync(ApplicationDbContext context)
        => _getActiveUsersCount(context);

    private static readonly Func<ApplicationDbContext, string, Task<List<User>>> _getUsersByRole =
        EF.CompileAsyncQuery((ApplicationDbContext context, string roleName) =>
            context.Users
                .Include(u => u.Entity)
                .Where(u => u.UserRoles.Any(ur => ur.Role.Name == roleName))
                .ToList());

    public Task<List<User>> GetUsersByRoleAsync(ApplicationDbContext context, string roleName)
        => _getUsersByRole(context, roleName);

    #endregion

    #region Group Queries

    private static readonly Func<ApplicationDbContext, string, Task<Group?>> _getGroupByNameWithHierarchy =
        EF.CompileAsyncQuery((ApplicationDbContext context, string groupName) =>
            context.Groups
                .Include(g => g.Entity)
                .Include(g => g.ParentGroupRelations)
                    .ThenInclude(gh => gh.ParentGroup)
                .Include(g => g.ChildGroupRelations)
                    .ThenInclude(gh => gh.ChildGroup)
                .FirstOrDefault(g => g.Name == groupName));

    public Task<Group?> GetGroupByNameWithHierarchyAsync(ApplicationDbContext context, string groupName)
        => _getGroupByNameWithHierarchy(context, groupName);

    private static readonly Func<ApplicationDbContext, int, Task<List<Group>>> _getChildGroups =
        EF.CompileAsyncQuery((ApplicationDbContext context, int parentGroupId) =>
            context.Groups
                .Include(g => g.Entity)
                .Where(g => g.ParentGroupRelations.Any(gh => gh.ParentGroupId == parentGroupId))
                .ToList());

    public Task<List<Group>> GetChildGroupsAsync(ApplicationDbContext context, int parentGroupId)
        => _getChildGroups(context, parentGroupId);

    private static readonly Func<ApplicationDbContext, int, Task<List<Group>>> _getGroupsForUser =
        EF.CompileAsyncQuery((ApplicationDbContext context, int userId) =>
            context.Groups
                .Include(g => g.Entity)
                .Where(g => g.UserGroups.Any(ug => ug.UserId == userId))
                .ToList());

    public Task<List<Group>> GetGroupsForUserAsync(ApplicationDbContext context, int userId)
        => _getGroupsForUser(context, userId);

    #endregion

    #region Role Queries

    private static readonly Func<ApplicationDbContext, string, Task<Role?>> _getRoleByNameWithPermissions =
        EF.CompileAsyncQuery((ApplicationDbContext context, string roleName) =>
            context.Roles
                .Include(r => r.Entity)
                .Include(r => r.UserRoles)
                .Include(r => r.GroupRoles)
                .FirstOrDefault(r => r.Name == roleName));

    public Task<Role?> GetRoleByNameWithPermissionsAsync(ApplicationDbContext context, string roleName)
        => _getRoleByNameWithPermissions(context, roleName);

    private static readonly Func<ApplicationDbContext, int, Task<List<Role>>> _getEffectiveRolesForUser =
        EF.CompileAsyncQuery((ApplicationDbContext context, int userId) =>
            // Direct roles
            context.Roles
                .Include(r => r.Entity)
                .Where(r => r.UserRoles.Any(ur => ur.UserId == userId))
                .Union(
                    // Roles from groups
                    context.Roles
                        .Include(r => r.Entity)
                        .Where(r => r.GroupRoles.Any(gr => 
                            gr.Group.UserGroups.Any(ug => ug.UserId == userId))))
                .Distinct()
                .ToList());

    public Task<List<Role>> GetEffectiveRolesForUserAsync(ApplicationDbContext context, int userId)
        => _getEffectiveRolesForUser(context, userId);

    private static readonly Func<ApplicationDbContext, Task<List<RolePermissionCount>>> _getRolesWithPermissionCount =
        EF.CompileAsyncQuery((ApplicationDbContext context) =>
            context.Roles
                .Select(r => new RolePermissionCount
                {
                    RoleId = r.Id,
                    RoleName = r.Name,
                    PermissionCount = context.UriAccesses.Count(ua =>
                        ua.PermissionScheme.EntityId == r.EntityId)
                })
                .ToList());

    public Task<List<RolePermissionCount>> GetRolesWithPermissionCountAsync(ApplicationDbContext context)
        => _getRolesWithPermissionCount(context);

    #endregion

    #region Permission Queries

    private static readonly Func<ApplicationDbContext, int, string, string, Task<bool>> _userHasPermission =
        EF.CompileAsyncQuery((ApplicationDbContext context, int userId, string resourceUri, string verb) =>
            // Direct user permission
            context.UriAccesses.Any(ua =>
                ua.Grant &&
                ua.Resource.Uri == resourceUri &&
                ua.VerbType.VerbName == verb &&
                ua.PermissionScheme.Entity.Users.Any(u => u.Id == userId)) ||
            // Role-based permission
            context.UriAccesses.Any(ua =>
                ua.Grant &&
                ua.Resource.Uri == resourceUri &&
                ua.VerbType.VerbName == verb &&
                ua.PermissionScheme.Entity.Roles.Any(r =>
                    r.UserRoles.Any(ur => ur.UserId == userId))) ||
            // Group-based permission
            context.UriAccesses.Any(ua =>
                ua.Grant &&
                ua.Resource.Uri == resourceUri &&
                ua.VerbType.VerbName == verb &&
                ua.PermissionScheme.Entity.Groups.Any(g =>
                    g.UserGroups.Any(ug => ug.UserId == userId))));

    public Task<bool> UserHasPermissionAsync(ApplicationDbContext context, int userId, string resourceUri, string verb)
        => _userHasPermission(context, userId, resourceUri, verb);

    private static readonly Func<ApplicationDbContext, int, string, Task<List<UserPermission>>> _getUserPermissionsForResource =
        EF.CompileAsyncQuery((ApplicationDbContext context, int userId, string resourceUri) =>
            // Direct permissions
            context.UriAccesses
                .Where(ua => ua.Resource.Uri == resourceUri &&
                           ua.PermissionScheme.Entity.Users.Any(u => u.Id == userId))
                .Select(ua => new UserPermission
                {
                    UserId = userId,
                    ResourceUri = resourceUri,
                    VerbName = ua.VerbType.VerbName,
                    IsGrant = ua.Grant,
                    IsDeny = ua.Deny,
                    Source = "Direct"
                })
                .Union(
                    // Role permissions
                    context.UriAccesses
                        .Where(ua => ua.Resource.Uri == resourceUri &&
                                   ua.PermissionScheme.Entity.Roles.Any(r =>
                                       r.UserRoles.Any(ur => ur.UserId == userId)))
                        .Select(ua => new UserPermission
                        {
                            UserId = userId,
                            ResourceUri = resourceUri,
                            VerbName = ua.VerbType.VerbName,
                            IsGrant = ua.Grant,
                            IsDeny = ua.Deny,
                            Source = "Role"
                        }))
                .Union(
                    // Group permissions
                    context.UriAccesses
                        .Where(ua => ua.Resource.Uri == resourceUri &&
                                   ua.PermissionScheme.Entity.Groups.Any(g =>
                                       g.UserGroups.Any(ug => ug.UserId == userId)))
                        .Select(ua => new UserPermission
                        {
                            UserId = userId,
                            ResourceUri = resourceUri,
                            VerbName = ua.VerbType.VerbName,
                            IsGrant = ua.Grant,
                            IsDeny = ua.Deny,
                            Source = "Group"
                        }))
                .ToList());

    public Task<List<UserPermission>> GetUserPermissionsForResourceAsync(ApplicationDbContext context, int userId, string resourceUri)
        => _getUserPermissionsForResource(context, userId, resourceUri);

    private static readonly Func<ApplicationDbContext, int, Task<List<EntityPermissionInfo>>> _getEntityPermissions =
        EF.CompileAsyncQuery((ApplicationDbContext context, int entityId) =>
            context.UriAccesses
                .Where(ua => ua.PermissionScheme.EntityId == entityId)
                .Select(ua => new EntityPermissionInfo
                {
                    EntityId = entityId,
                    EntityType = ua.PermissionScheme.Entity.EntityType,
                    ResourceUri = ua.Resource.Uri,
                    VerbName = ua.VerbType.VerbName,
                    IsGrant = ua.Grant,
                    IsDeny = ua.Deny,
                    SchemeType = ua.PermissionScheme.SchemeType.SchemeName
                })
                .ToList());

    public Task<List<EntityPermissionInfo>> GetEntityPermissionsAsync(ApplicationDbContext context, int entityId)
        => _getEntityPermissions(context, entityId);

    #endregion

    #region Resource Queries

    private static readonly Func<ApplicationDbContext, string, Task<Resource?>> _getResourceByUri =
        EF.CompileAsyncQuery((ApplicationDbContext context, string uri) =>
            context.Resources.FirstOrDefault(r => r.Uri == uri));

    public Task<Resource?> GetResourceByUriAsync(ApplicationDbContext context, string uri)
        => _getResourceByUri(context, uri);

    private static readonly Func<ApplicationDbContext, int, Task<List<Resource>>> _getResourcesAccessibleByUser =
        EF.CompileAsyncQuery((ApplicationDbContext context, int userId) =>
            // Resources accessible through direct permissions
            context.Resources
                .Where(r => r.UriAccesses.Any(ua =>
                    ua.Grant &&
                    ua.PermissionScheme.Entity.Users.Any(u => u.Id == userId)))
                .Union(
                    // Resources accessible through role permissions
                    context.Resources
                        .Where(r => r.UriAccesses.Any(ua =>
                            ua.Grant &&
                            ua.PermissionScheme.Entity.Roles.Any(role =>
                                role.UserRoles.Any(ur => ur.UserId == userId)))))
                .Union(
                    // Resources accessible through group permissions
                    context.Resources
                        .Where(r => r.UriAccesses.Any(ua =>
                            ua.Grant &&
                            ua.PermissionScheme.Entity.Groups.Any(g =>
                                g.UserGroups.Any(ug => ug.UserId == userId)))))
                .Distinct()
                .ToList());

    public Task<List<Resource>> GetResourcesAccessibleByUserAsync(ApplicationDbContext context, int userId)
        => _getResourcesAccessibleByUser(context, userId);

    private static readonly Func<ApplicationDbContext, Task<List<ResourceUsageInfo>>> _getResourceUsageStats =
        EF.CompileAsyncQuery((ApplicationDbContext context) =>
            context.Resources
                .Select(r => new ResourceUsageInfo
                {
                    ResourceId = r.Id,
                    ResourceUri = r.Uri,
                    PermissionCount = r.UriAccesses.Count(),
                    EntityCount = r.UriAccesses.Select(ua => ua.PermissionScheme.EntityId).Distinct().Count(),
                    VerbCount = r.UriAccesses.Select(ua => ua.VerbTypeId).Distinct().Count()
                })
                .ToList());

    public Task<List<ResourceUsageInfo>> GetResourceUsageStatsAsync(ApplicationDbContext context)
        => _getResourceUsageStats(context);

    #endregion

    #region Audit Queries

    private static readonly Func<ApplicationDbContext, string, int, int, Task<List<AuditLog>>> _getAuditLogsForEntity =
        EF.CompileAsyncQuery((ApplicationDbContext context, string entityType, int entityId, int limit) =>
            context.AuditLogs
                .Where(al => al.EntityType == entityType && al.EntityId == entityId)
                .OrderByDescending(al => al.ChangeDate)
                .Take(limit)
                .ToList());

    public Task<List<AuditLog>> GetAuditLogsForEntityAsync(ApplicationDbContext context, string entityType, int entityId, int limit)
        => _getAuditLogsForEntity(context, entityType, entityId, limit);

    private static readonly Func<ApplicationDbContext, DateTime, Task<List<AuditLog>>> _getRecentSecurityEvents =
        EF.CompileAsyncQuery((ApplicationDbContext context, DateTime since) =>
            context.AuditLogs
                .Where(al => al.ChangeDate >= since &&
                           (al.ChangeType.Contains("security") ||
                            al.ChangeDetails.Contains("login") ||
                            al.ChangeDetails.Contains("failed") ||
                            al.ChangeDetails.Contains("unauthorized")))
                .OrderByDescending(al => al.ChangeDate)
                .ToList());

    public Task<List<AuditLog>> GetRecentSecurityEventsAsync(ApplicationDbContext context, DateTime since)
        => _getRecentSecurityEvents(context, since);

    #endregion
}