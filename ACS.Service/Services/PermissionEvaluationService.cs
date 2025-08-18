using ACS.Service.Data;
using ACS.Service.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

public class PermissionEvaluationService : IPermissionEvaluationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PermissionEvaluationService> _logger;

    public PermissionEvaluationService(ApplicationDbContext dbContext, ILogger<PermissionEvaluationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(int entityId, string uri, HttpVerb httpVerb)
    {
        var permissions = await GetEffectivePermissionsAsync(entityId);
        var permission = permissions.FirstOrDefault(p => p.Uri == uri && p.HttpVerb == httpVerb);

        return permission != null && permission.Grant && !permission.Deny;
    }

    public async Task<List<Permission>> GetEffectivePermissionsAsync(int entityId)
    {
        // Get entity type to determine permission resolution strategy
        var entity = await _dbContext.Entities.FindAsync(entityId);
        if (entity == null)
        {
            _logger.LogWarning("Entity {EntityId} not found", entityId);
            return new List<Permission>();
        }

        var allPermissions = new List<Permission>();

        switch (entity.EntityType)
        {
            case "User":
                allPermissions = await GetUserPermissionsAsync(entityId);
                break;
            case "Group":
                allPermissions = await GetGroupPermissionsAsync(entityId);
                break;
            case "Role":
                allPermissions = await GetRolePermissionsAsync(entityId);
                break;
            default:
                _logger.LogWarning("Unknown entity type {EntityType} for entity {EntityId}", entity.EntityType, entityId);
                return new List<Permission>();
        }

        // Apply permission resolution rules (deny overrides allow)
        return ResolvePermissionConflicts(allPermissions);
    }

    public async Task<bool> CanUserAccessResourceAsync(int userId, string uri, HttpVerb httpVerb)
    {
        return await HasPermissionAsync(userId, uri, httpVerb);
    }

    public async Task<List<Permission>> GetUserPermissionsAsync(int userId)
    {
        var permissions = new List<Permission>();

        // Get direct permissions for the user's entity
        var userEntity = await _dbContext.Users
            .Include(u => u.Entity)
            .ThenInclude(e => e.EntityPermissions)
            .ThenInclude(ep => ep.SchemeType)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (userEntity?.Entity?.EntityPermissions != null)
        {
            permissions.AddRange(ConvertPermissionSchemes(userEntity.Entity.EntityPermissions));
        }

        // Get permissions from roles assigned to the user
        var userRoles = await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .ThenInclude(r => r.Entity)
            .ThenInclude(e => e.EntityPermissions)
            .ThenInclude(ep => ep.SchemeType)
            .Where(ur => ur.UserId == userId)
            .ToListAsync();

        foreach (var userRole in userRoles)
        {
            if (userRole.Role?.Entity?.EntityPermissions != null)
            {
                permissions.AddRange(ConvertPermissionSchemes(userRole.Role.Entity.EntityPermissions));
            }
        }

        // Get permissions from groups the user belongs to
        var userGroups = await _dbContext.UserGroups
            .Include(ug => ug.Group)
            .ThenInclude(g => g.Entity)
            .ThenInclude(e => e.EntityPermissions)
            .ThenInclude(ep => ep.SchemeType)
            .Where(ug => ug.UserId == userId)
            .ToListAsync();

        foreach (var userGroup in userGroups)
        {
            var groupPermissions = await GetGroupPermissionsAsync(userGroup.GroupId);
            permissions.AddRange(groupPermissions);
        }

        return permissions;
    }

    public async Task<List<Permission>> GetGroupPermissionsAsync(int groupId)
    {
        var permissions = new List<Permission>();

        // Get direct permissions for the group's entity
        var groupEntity = await _dbContext.Groups
            .Include(g => g.Entity)
            .ThenInclude(e => e.EntityPermissions)
            .ThenInclude(ep => ep.SchemeType)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (groupEntity?.Entity?.EntityPermissions != null)
        {
            permissions.AddRange(ConvertPermissionSchemes(groupEntity.Entity.EntityPermissions));
        }

        // Get permissions from roles assigned to the group
        var groupRoles = await _dbContext.GroupRoles
            .Include(gr => gr.Role)
            .ThenInclude(r => r.Entity)
            .ThenInclude(e => e.EntityPermissions)
            .ThenInclude(ep => ep.SchemeType)
            .Where(gr => gr.GroupId == groupId)
            .ToListAsync();

        foreach (var groupRole in groupRoles)
        {
            if (groupRole.Role?.Entity?.EntityPermissions != null)
            {
                permissions.AddRange(ConvertPermissionSchemes(groupRole.Role.Entity.EntityPermissions));
            }
        }

        // Get permissions from parent groups (hierarchical inheritance)
        var parentGroups = await _dbContext.GroupHierarchies
            .Include(gh => gh.ParentGroup)
            .Where(gh => gh.ChildGroupId == groupId)
            .ToListAsync();

        foreach (var parentGroup in parentGroups)
        {
            var parentPermissions = await GetGroupPermissionsAsync(parentGroup.ParentGroupId);
            permissions.AddRange(parentPermissions);
        }

        return permissions;
    }

    public async Task<List<Permission>> GetRolePermissionsAsync(int roleId)
    {
        var permissions = new List<Permission>();

        // Get direct permissions for the role's entity
        var roleEntity = await _dbContext.Roles
            .Include(r => r.Entity)
            .ThenInclude(e => e.EntityPermissions)
            .ThenInclude(ep => ep.SchemeType)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (roleEntity?.Entity?.EntityPermissions != null)
        {
            permissions.AddRange(ConvertPermissionSchemes(roleEntity.Entity.EntityPermissions));
        }

        return permissions;
    }

    private List<Permission> ConvertPermissionSchemes(ICollection<Data.Models.PermissionScheme> permissionSchemes)
    {
        var permissions = new List<Permission>();

        foreach (var scheme in permissionSchemes)
        {
            // Get actual URI permissions from UriAccess table
            var uriAccesses = _dbContext.UriAccesses
                .Include(ua => ua.Resource)
                .Include(ua => ua.VerbType)
                .Where(ua => ua.EntityPermissionId == scheme.Id)
                .ToList();

            foreach (var uriAccess in uriAccesses)
            {
                var permission = new Permission
                {
                    Uri = uriAccess.Resource.Uri,
                    HttpVerb = Enum.Parse<HttpVerb>(uriAccess.VerbType.VerbName),
                    Grant = uriAccess.Grant,
                    Deny = uriAccess.Deny,
                    Scheme = Enum.Parse<Scheme>(scheme.SchemeType.SchemeName)
                };

                permissions.Add(permission);
            }
        }

        return permissions;
    }

    private List<Permission> ResolvePermissionConflicts(List<Permission> permissions)
    {
        var resolvedPermissions = new Dictionary<string, Permission>();

        foreach (var permission in permissions)
        {
            var key = $"{permission.Uri}:{permission.HttpVerb}";

            if (!resolvedPermissions.ContainsKey(key))
            {
                resolvedPermissions[key] = new Permission
                {
                    Uri = permission.Uri,
                    HttpVerb = permission.HttpVerb,
                    Grant = permission.Grant,
                    Deny = permission.Deny,
                    Scheme = permission.Scheme
                };
            }
            else
            {
                var existingPermission = resolvedPermissions[key];

                // Deny always overrides allow (most restrictive wins)
                if (permission.Deny)
                {
                    existingPermission.Deny = true;
                    existingPermission.Grant = false;
                }
                else if (permission.Grant && !existingPermission.Deny)
                {
                    existingPermission.Grant = true;
                }
            }
        }

        return resolvedPermissions.Values.ToList();
    }
}