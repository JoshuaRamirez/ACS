using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Delegates.Normalizers;
using ACS.Service.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

public class RoleService : IRoleService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RoleService> _logger;
    private readonly IPermissionEvaluationService _permissionService;

    public RoleService(
        ApplicationDbContext dbContext,
        ILogger<RoleService> logger,
        IPermissionEvaluationService permissionService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _permissionService = permissionService;
    }

    #region Basic CRUD Operations

    public async Task<IEnumerable<Domain.Role>> GetAllRolesAsync()
    {
        var roles = await _dbContext.Roles
            .Include(r => r.Entity)
            .Include(r => r.UserRoles)
                .ThenInclude(ur => ur.User)
            .Include(r => r.GroupRoles)
                .ThenInclude(gr => gr.Group)
            .ToListAsync();

        return roles.Select(ConvertToDomainRole);
    }

    public async Task<Domain.Role?> GetRoleByIdAsync(int roleId)
    {
        var role = await _dbContext.Roles
            .Include(r => r.Entity)
            .Include(r => r.UserRoles)
                .ThenInclude(ur => ur.User)
            .Include(r => r.GroupRoles)
                .ThenInclude(gr => gr.Group)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        return role != null ? ConvertToDomainRole(role) : null;
    }

    public async Task<Domain.Role> CreateRoleAsync(string name, string description, string createdBy)
    {
        // Create entity first
        var entity = new Data.Models.Entity
        {
            EntityType = "Role",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Entities.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Create role
        var role = new Data.Models.Role
        {
            Name = name,
            Description = description,
            EntityId = entity.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("CreateRole", "Role", role.Id, createdBy,
            $"Created role '{name}'");

        _logger.LogInformation("Created role {RoleId} with name {RoleName} by {CreatedBy}",
            role.Id, name, createdBy);

        return ConvertToDomainRole(role);
    }

    public async Task<Domain.Role> UpdateRoleAsync(int roleId, string name, string description, string updatedBy)
    {
        var role = await _dbContext.Roles.FindAsync(roleId);
        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found");
        }

        var oldName = role.Name;
        role.Name = name;
        role.Description = description;
        role.UpdatedAt = DateTime.UtcNow;

        _dbContext.Roles.Update(role);
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("UpdateRole", "Role", role.Id, updatedBy,
            $"Updated role from '{oldName}' to '{name}'");

        _logger.LogInformation("Updated role {RoleId} with name {RoleName} by {UpdatedBy}",
            roleId, name, updatedBy);

        return ConvertToDomainRole(role);
    }

    public async Task DeleteRoleAsync(int roleId, string deletedBy)
    {
        var role = await _dbContext.Roles
            .Include(r => r.Entity)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found");
        }

        // Check if role is assigned to any users or groups
        var hasAssignments = await _dbContext.UserRoles.AnyAsync(ur => ur.RoleId == roleId) ||
                            await _dbContext.GroupRoles.AnyAsync(gr => gr.RoleId == roleId);

        if (hasAssignments)
        {
            throw new InvalidOperationException($"Cannot delete role {roleId} as it is assigned to users or groups");
        }

        var roleName = role.Name;

        // Delete role (cascading will handle relationships)
        _dbContext.Roles.Remove(role);
        if (role.Entity != null)
        {
            _dbContext.Entities.Remove(role.Entity);
        }
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("DeleteRole", "Role", roleId, deletedBy,
            $"Deleted role '{roleName}'");

        _logger.LogInformation("Deleted role {RoleId} by {DeletedBy}", roleId, deletedBy);
    }

    #endregion

    #region User Assignment

    public async Task<IEnumerable<Domain.User>> GetRoleUsersAsync(int roleId)
    {
        // Get direct user assignments
        var directUsers = await _dbContext.UserRoles
            .Include(ur => ur.User)
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.User)
            .ToListAsync();

        // Get users through group assignments
        var groupUsers = await _dbContext.GroupRoles
            .Where(gr => gr.RoleId == roleId)
            .SelectMany(gr => gr.Group.UserGroups.Select(ug => ug.User))
            .ToListAsync();

        var allUsers = directUsers.Union(groupUsers).DistinctBy(u => u.Id);
        return allUsers.Select(ConvertToDomainUser);
    }

    public async Task AssignUserToRoleAsync(int userId, int roleId, string assignedBy)
    {
        await AssignUserToRoleNormalizer.ExecuteAsync(_dbContext, userId, roleId, assignedBy);
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("AssignUserToRole", "UserRole", 0, assignedBy,
            $"Assigned user {userId} to role {roleId}");

        _logger.LogInformation("Assigned user {UserId} to role {RoleId} by {AssignedBy}",
            userId, roleId, assignedBy);
    }

    public async Task UnassignUserFromRoleAsync(int userId, int roleId, string unassignedBy)
    {
        await UnAssignUserFromRoleNormalizer.ExecuteAsync(_dbContext, userId, roleId, unassignedBy);
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("UnassignUserFromRole", "UserRole", 0, unassignedBy,
            $"Unassigned user {userId} from role {roleId}");

        _logger.LogInformation("Unassigned user {UserId} from role {RoleId} by {UnassignedBy}",
            userId, roleId, unassignedBy);
    }

    public async Task<bool> IsUserInRoleAsync(int userId, int roleId)
    {
        // Check direct assignment
        var directAssignment = await _dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (directAssignment)
            return true;

        // Check through group membership
        var groupAssignment = await _dbContext.UserGroups
            .Where(ug => ug.UserId == userId)
            .Join(_dbContext.GroupRoles,
                ug => ug.GroupId,
                gr => gr.GroupId,
                (ug, gr) => gr.RoleId)
            .AnyAsync(r => r == roleId);

        return groupAssignment;
    }

    public async Task<IEnumerable<Domain.Role>> GetUserRolesAsync(int userId, bool includeGroupRoles = true)
    {
        // Get direct role assignments
        var directRoles = await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role)
            .ToListAsync();

        var allRoles = new List<Data.Models.Role>(directRoles);

        // Get roles through group membership
        if (includeGroupRoles)
        {
            var groupRoles = await _dbContext.UserGroups
                .Where(ug => ug.UserId == userId)
                .SelectMany(ug => ug.Group.GroupRoles.Select(gr => gr.Role))
                .ToListAsync();

            allRoles.AddRange(groupRoles);
        }

        return allRoles.DistinctBy(r => r.Id).Select(ConvertToDomainRole);
    }

    #endregion

    #region Group Assignment

    public async Task<IEnumerable<Domain.Group>> GetRoleGroupsAsync(int roleId)
    {
        var groups = await _dbContext.GroupRoles
            .Include(gr => gr.Group)
            .Where(gr => gr.RoleId == roleId)
            .Select(gr => gr.Group)
            .ToListAsync();

        return groups.Select(ConvertToDomainGroup);
    }

    public async Task AssignRoleToGroupAsync(int roleId, int groupId, string assignedBy)
    {
        await AddRoleToGroupNormalizer.ExecuteAsync(_dbContext, roleId, groupId, assignedBy);
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("AssignRoleToGroup", "GroupRole", 0, assignedBy,
            $"Assigned role {roleId} to group {groupId}");

        _logger.LogInformation("Assigned role {RoleId} to group {GroupId} by {AssignedBy}",
            roleId, groupId, assignedBy);
    }

    public async Task UnassignRoleFromGroupAsync(int roleId, int groupId, string unassignedBy)
    {
        var relation = await _dbContext.GroupRoles
            .FirstOrDefaultAsync(gr => gr.RoleId == roleId && gr.GroupId == groupId);

        if (relation != null)
        {
            _dbContext.GroupRoles.Remove(relation);
            await _dbContext.SaveChangesAsync();

            // Log audit
            await LogAuditAsync("UnassignRoleFromGroup", "GroupRole", 0, unassignedBy,
                $"Unassigned role {roleId} from group {groupId}");

            _logger.LogInformation("Unassigned role {RoleId} from group {GroupId} by {UnassignedBy}",
                roleId, groupId, unassignedBy);
        }
    }

    public async Task<bool> IsRoleInGroupAsync(int roleId, int groupId)
    {
        return await _dbContext.GroupRoles
            .AnyAsync(gr => gr.RoleId == roleId && gr.GroupId == groupId);
    }

    #endregion

    #region Permission Management

    public async Task<IEnumerable<Domain.Permission>> GetRolePermissionsAsync(int roleId)
    {
        return await _permissionService.GetEntityPermissionsAsync(roleId, false);
    }

    public async Task AddPermissionToRoleAsync(int roleId, Domain.Permission permission, string addedBy)
    {
        await AddPermissionToEntity.ExecuteAsync(_dbContext, permission, roleId);
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("AddPermissionToRole", "Permission", 0, addedBy,
            $"Added permission {permission.Uri} {permission.HttpVerb} to role {roleId}");

        _logger.LogInformation("Added permission {Resource} {Action} to role {RoleId} by {AddedBy}",
            permission.Uri, permission.HttpVerb, roleId, addedBy);
    }

    public async Task RemovePermissionFromRoleAsync(int roleId, string resource, string action, string removedBy)
    {
        // This would need RemovePermissionFromEntity normalizer to be implemented
        // For now, we'll implement it directly
        var schemeType = await _dbContext.SchemeTypes
            .FirstOrDefaultAsync(st => st.SchemeName == "ApiUriAuthorization");

        if (schemeType == null)
            return;

        var permissionScheme = await _dbContext.EntityPermissions
            .FirstOrDefaultAsync(ps => ps.EntityId == roleId && ps.SchemeTypeId == schemeType.Id);

        if (permissionScheme == null)
            return;

        var uriAccess = await _dbContext.UriAccesses
            .Include(ua => ua.Resource)
            .Include(ua => ua.VerbType)
            .FirstOrDefaultAsync(ua => ua.PermissionSchemeId == permissionScheme.Id &&
                                      ua.Resource.Uri == resource &&
                                      ua.VerbType.VerbName == action);

        if (uriAccess != null)
        {
            _dbContext.UriAccesses.Remove(uriAccess);
            await _dbContext.SaveChangesAsync();

            // Log audit
            await LogAuditAsync("RemovePermissionFromRole", "Permission", 0, removedBy,
                $"Removed permission {resource} {action} from role {roleId}");

            _logger.LogInformation("Removed permission {Resource} {Action} from role {RoleId} by {RemovedBy}",
                resource, action, roleId, removedBy);
        }
    }

    public async Task<bool> RoleHasPermissionAsync(int roleId, string resource, string action)
    {
        return await _permissionService.HasPermissionAsync(roleId, resource, action);
    }

    #endregion

    #region Role Hierarchy

    // Note: Role hierarchy is not currently in the database schema
    // These methods would need additional tables to be implemented
    public async Task<IEnumerable<Domain.Role>> GetChildRolesAsync(int parentRoleId)
    {
        // Would require RoleHierarchy table
        _logger.LogWarning("Role hierarchy not implemented in current schema");
        return await Task.FromResult(Enumerable.Empty<Domain.Role>());
    }

    public async Task<IEnumerable<Domain.Role>> GetParentRolesAsync(int childRoleId)
    {
        // Would require RoleHierarchy table
        _logger.LogWarning("Role hierarchy not implemented in current schema");
        return await Task.FromResult(Enumerable.Empty<Domain.Role>());
    }

    public async Task AddRoleHierarchyAsync(int parentRoleId, int childRoleId, string createdBy)
    {
        // Would require RoleHierarchy table
        _logger.LogWarning("Role hierarchy not implemented in current schema");
        await Task.CompletedTask;
    }

    public async Task RemoveRoleHierarchyAsync(int parentRoleId, int childRoleId, string removedBy)
    {
        // Would require RoleHierarchy table
        _logger.LogWarning("Role hierarchy not implemented in current schema");
        await Task.CompletedTask;
    }

    #endregion

    #region Bulk Operations

    public async Task<IEnumerable<Domain.Role>> CreateRolesBulkAsync(
        IEnumerable<(string Name, string Description)> roles, string createdBy)
    {
        var createdRoles = new List<Domain.Role>();

        foreach (var (name, description) in roles)
        {
            var role = await CreateRoleAsync(name, description, createdBy);
            createdRoles.Add(role);
        }

        _logger.LogInformation("Created {Count} roles in bulk by {CreatedBy}",
            createdRoles.Count, createdBy);

        return createdRoles;
    }

    public async Task AssignUsersToRoleBulkAsync(int roleId, IEnumerable<int> userIds, string assignedBy)
    {
        foreach (var userId in userIds)
        {
            await AssignUserToRoleAsync(userId, roleId, assignedBy);
        }

        _logger.LogInformation("Assigned {Count} users to role {RoleId} in bulk by {AssignedBy}",
            userIds.Count(), roleId, assignedBy);
    }

    public async Task AddPermissionsToRoleBulkAsync(int roleId, IEnumerable<Domain.Permission> permissions, string addedBy)
    {
        foreach (var permission in permissions)
        {
            await AddPermissionToRoleAsync(roleId, permission, addedBy);
        }

        _logger.LogInformation("Added {Count} permissions to role {RoleId} in bulk by {AddedBy}",
            permissions.Count(), roleId, addedBy);
    }

    #endregion

    #region Search and Filtering

    public async Task<IEnumerable<Domain.Role>> SearchRolesAsync(string searchTerm)
    {
        var roles = await _dbContext.Roles
            .Where(r => r.Name.Contains(searchTerm) ||
                       (r.Description != null && r.Description.Contains(searchTerm)))
            .ToListAsync();

        return roles.Select(ConvertToDomainRole);
    }

    public async Task<IEnumerable<Domain.Role>> GetRolesByPermissionAsync(string resource, string action)
    {
        var roles = await _dbContext.UriAccesses
            .Include(ua => ua.Resource)
            .Include(ua => ua.VerbType)
            .Include(ua => ua.PermissionScheme)
                .ThenInclude(ps => ps.Entity)
            .Where(ua => ua.Resource.Uri == resource && 
                        ua.VerbType.VerbName == action &&
                        ua.PermissionScheme.Entity.EntityType == "Role")
            .Select(ua => ua.PermissionScheme.EntityId)
            .Distinct()
            .Join(_dbContext.Roles, id => id, r => r.EntityId, (id, r) => r)
            .ToListAsync();

        return roles.Select(ConvertToDomainRole);
    }

    public async Task<IEnumerable<Domain.Role>> GetRolesByGroupAsync(int groupId)
    {
        var roles = await _dbContext.GroupRoles
            .Include(gr => gr.Role)
            .Where(gr => gr.GroupId == groupId)
            .Select(gr => gr.Role)
            .ToListAsync();

        return roles.Select(ConvertToDomainRole);
    }

    #endregion

    #region Role Templates and Cloning

    public async Task<Domain.Role> CloneRoleAsync(int sourceRoleId, string newRoleName, string clonedBy)
    {
        var sourceRole = await _dbContext.Roles
            .Include(r => r.Entity)
            .FirstOrDefaultAsync(r => r.Id == sourceRoleId);

        if (sourceRole == null)
        {
            throw new InvalidOperationException($"Source role {sourceRoleId} not found");
        }

        // Create new role
        var newRole = await CreateRoleAsync(newRoleName, 
            $"Cloned from {sourceRole.Name}", clonedBy);

        // Copy permissions
        var sourcePermissions = await GetRolePermissionsAsync(sourceRoleId);
        foreach (var permission in sourcePermissions)
        {
            await AddPermissionToRoleAsync(newRole.Id, permission, clonedBy);
        }

        // Log audit
        await LogAuditAsync("CloneRole", "Role", newRole.Id, clonedBy,
            $"Cloned role from {sourceRole.Name} to {newRoleName}");

        _logger.LogInformation("Cloned role {SourceRoleId} to new role {NewRoleId} with name {NewRoleName} by {ClonedBy}",
            sourceRoleId, newRole.Id, newRoleName, clonedBy);

        return newRole;
    }

    public async Task<Domain.Role> CreateRoleFromTemplateAsync(string templateName, string roleName, string createdBy)
    {
        // Define role templates
        var templates = new Dictionary<string, (string Description, List<(string Resource, string Action, bool Grant)> Permissions)>
        {
            ["Admin"] = ("Administrator role with full access", new List<(string, string, bool)>
            {
                ("*", "GET", true),
                ("*", "POST", true),
                ("*", "PUT", true),
                ("*", "DELETE", true)
            }),
            ["ReadOnly"] = ("Read-only access role", new List<(string, string, bool)>
            {
                ("*", "GET", true),
                ("*", "POST", false),
                ("*", "PUT", false),
                ("*", "DELETE", false)
            }),
            ["Operator"] = ("Operator role with modify access", new List<(string, string, bool)>
            {
                ("*", "GET", true),
                ("*", "POST", true),
                ("*", "PUT", true),
                ("*", "DELETE", false)
            })
        };

        if (!templates.ContainsKey(templateName))
        {
            throw new InvalidOperationException($"Template {templateName} not found");
        }

        var template = templates[templateName];
        var role = await CreateRoleAsync(roleName, template.Description, createdBy);

        // Add template permissions
        foreach (var (resource, action, grant) in template.Permissions)
        {
            var permission = new Domain.Permission
            {
                Uri = resource,
                HttpVerb = Enum.Parse<Domain.HttpVerb>(action),
                Grant = grant,
                Deny = !grant,
                Scheme = Domain.Scheme.ApiUriAuthorization
            };

            await AddPermissionToRoleAsync(role.Id, permission, createdBy);
        }

        // Log audit
        await LogAuditAsync("CreateRoleFromTemplate", "Role", role.Id, createdBy,
            $"Created role {roleName} from template {templateName}");

        _logger.LogInformation("Created role {RoleId} with name {RoleName} from template {TemplateName} by {CreatedBy}",
            role.Id, roleName, templateName, createdBy);

        return role;
    }

    #endregion

    #region Helper Methods

    private Domain.Role ConvertToDomainRole(Data.Models.Role dataRole)
    {
        var domainRole = new Domain.Role
        {
            Id = dataRole.Id,
            Name = dataRole.Name
        };

        return domainRole;
    }

    private Domain.User ConvertToDomainUser(Data.Models.User dataUser)
    {
        var domainUser = new Domain.User
        {
            Id = dataUser.Id,
            Name = dataUser.Name
        };

        return domainUser;
    }

    private Domain.Group ConvertToDomainGroup(Data.Models.Group dataGroup)
    {
        var domainGroup = new Domain.Group
        {
            Id = dataGroup.Id,
            Name = dataGroup.Name
        };

        return domainGroup;
    }

    private async Task LogAuditAsync(string action, string entityType, int entityId,
        string changedBy, string changeDetails)
    {
        var auditLog = new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            ChangeType = action,
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = changeDetails
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();
    }

    #endregion
}