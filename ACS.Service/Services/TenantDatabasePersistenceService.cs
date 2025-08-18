using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;

namespace ACS.Service.Services;

public class TenantDatabasePersistenceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<TenantDatabasePersistenceService> _logger;

    public TenantDatabasePersistenceService(
        ApplicationDbContext dbContext,
        ILogger<TenantDatabasePersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #region User-Group Persistence

    public async Task PersistAddUserToGroupAsync(int userId, int groupId)
    {
        try
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                throw new InvalidOperationException($"User {userId} not found");

            var group = await _dbContext.Groups.FindAsync(groupId);
            if (group == null)
                throw new InvalidOperationException($"Group {groupId} not found");

            user.GroupId = groupId;
            user.Group = group;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Added user {UserId} to group {GroupId}", userId, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist add user {UserId} to group {GroupId}", userId, groupId);
            throw;
        }
    }

    public async Task PersistRemoveUserFromGroupAsync(int userId, int groupId)
    {
        try
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                throw new InvalidOperationException($"User {userId} not found");

            if (user.GroupId == groupId)
            {
                user.GroupId = 0;
                user.Group = null!;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Persisted: Removed user {UserId} from group {GroupId}", userId, groupId);
            }
            else
            {
                _logger.LogWarning("User {UserId} was not in group {GroupId}", userId, groupId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist remove user {UserId} from group {GroupId}", userId, groupId);
            throw;
        }
    }

    #endregion

    #region User-Role Persistence

    public async Task PersistAssignUserToRoleAsync(int userId, int roleId)
    {
        try
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                throw new InvalidOperationException($"User {userId} not found");

            var role = await _dbContext.Roles.FindAsync(roleId);
            if (role == null)
                throw new InvalidOperationException($"Role {roleId} not found");

            user.RoleId = roleId;
            user.Role = role;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Assigned user {UserId} to role {RoleId}", userId, roleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist assign user {UserId} to role {RoleId}", userId, roleId);
            throw;
        }
    }

    public async Task PersistUnAssignUserFromRoleAsync(int userId, int roleId)
    {
        try
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                throw new InvalidOperationException($"User {userId} not found");

            if (user.RoleId == roleId)
            {
                user.RoleId = 0;
                user.Role = null!;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Persisted: Unassigned user {UserId} from role {RoleId}", userId, roleId);
            }
            else
            {
                _logger.LogWarning("User {UserId} was not assigned to role {RoleId}", userId, roleId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist unassign user {UserId} from role {RoleId}", userId, roleId);
            throw;
        }
    }

    #endregion

    #region Group-Role Persistence

    public async Task PersistAddRoleToGroupAsync(int groupId, int roleId)
    {
        try
        {
            var role = await _dbContext.Roles.FindAsync(roleId);
            if (role == null)
                throw new InvalidOperationException($"Role {roleId} not found");

            var group = await _dbContext.Groups.FindAsync(groupId);
            if (group == null)
                throw new InvalidOperationException($"Group {groupId} not found");

            role.GroupId = groupId;
            role.Group = group;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Added role {RoleId} to group {GroupId}", roleId, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist add role {RoleId} to group {GroupId}", roleId, groupId);
            throw;
        }
    }

    public async Task PersistRemoveRoleFromGroupAsync(int groupId, int roleId)
    {
        try
        {
            var role = await _dbContext.Roles.FindAsync(roleId);
            if (role == null)
                throw new InvalidOperationException($"Role {roleId} not found");

            if (role.GroupId == groupId)
            {
                role.GroupId = 0;
                role.Group = null!;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Persisted: Removed role {RoleId} from group {GroupId}", roleId, groupId);
            }
            else
            {
                _logger.LogWarning("Role {RoleId} was not in group {GroupId}", roleId, groupId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist remove role {RoleId} from group {GroupId}", roleId, groupId);
            throw;
        }
    }

    #endregion

    #region Group-Group Persistence

    public async Task PersistAddGroupToGroupAsync(int parentGroupId, int childGroupId)
    {
        try
        {
            var childGroup = await _dbContext.Groups.FindAsync(childGroupId);
            if (childGroup == null)
                throw new InvalidOperationException($"Child group {childGroupId} not found");

            var parentGroup = await _dbContext.Groups.FindAsync(parentGroupId);
            if (parentGroup == null)
                throw new InvalidOperationException($"Parent group {parentGroupId} not found");

            // Check for circular references at database level
            if (await WouldCreateCircularReferenceAsync(childGroupId, parentGroupId))
                throw new InvalidOperationException($"Adding group {childGroupId} to group {parentGroupId} would create a circular reference");

            childGroup.ParentGroupId = parentGroupId;
            childGroup.ParentGroup = parentGroup;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Added group {ChildGroupId} to group {ParentGroupId}", childGroupId, parentGroupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist add group {ChildGroupId} to group {ParentGroupId}", childGroupId, parentGroupId);
            throw;
        }
    }

    public async Task PersistRemoveGroupFromGroupAsync(int parentGroupId, int childGroupId)
    {
        try
        {
            var childGroup = await _dbContext.Groups.FindAsync(childGroupId);
            if (childGroup == null)
                throw new InvalidOperationException($"Child group {childGroupId} not found");

            if (childGroup.ParentGroupId == parentGroupId)
            {
                childGroup.ParentGroupId = 0;
                childGroup.ParentGroup = null;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Persisted: Removed group {ChildGroupId} from group {ParentGroupId}", childGroupId, parentGroupId);
            }
            else
            {
                _logger.LogWarning("Group {ChildGroupId} was not a child of group {ParentGroupId}", childGroupId, parentGroupId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist remove group {ChildGroupId} from group {ParentGroupId}", childGroupId, parentGroupId);
            throw;
        }
    }

    private async Task<bool> WouldCreateCircularReferenceAsync(int childGroupId, int parentGroupId)
    {
        // Traverse up the parent hierarchy to check if parentGroupId is already a descendant of childGroupId
        var currentGroupId = parentGroupId;
        var visited = new HashSet<int>();

        while (currentGroupId > 0 && !visited.Contains(currentGroupId))
        {
            visited.Add(currentGroupId);

            if (currentGroupId == childGroupId)
                return true;

            var group = await _dbContext.Groups.FindAsync(currentGroupId);
            currentGroupId = group?.ParentGroupId ?? 0;
        }

        return false;
    }

    #endregion

    #region Permission Persistence

    public async Task PersistAddPermissionToEntityAsync(int entityId, Permission permission)
    {
        try
        {
            // Find or create permission scheme for the entity
            var permissionScheme = await _dbContext.EntityPermissions
                .FirstOrDefaultAsync(ps => ps.EntityId == entityId);

            if (permissionScheme == null)
            {
                // Create new permission scheme
                var schemeType = await GetOrCreateSchemeTypeAsync(permission.Scheme);
                
                permissionScheme = new PermissionScheme
                {
                    EntityId = entityId,
                    SchemeTypeId = schemeType.Id,
                    SchemeType = schemeType
                };

                _dbContext.EntityPermissions.Add(permissionScheme);
                await _dbContext.SaveChangesAsync(); // Save to get ID
            }

            // Find or create resource
            var resource = await _dbContext.Resources
                .FirstOrDefaultAsync(r => r.Uri == permission.Uri);

            if (resource == null)
            {
                resource = new Resource
                {
                    Uri = permission.Uri
                };

                _dbContext.Resources.Add(resource);
                await _dbContext.SaveChangesAsync(); // Save to get ID
            }

            // Find verb type
            var verbType = await _dbContext.VerbTypes
                .FirstOrDefaultAsync(vt => vt.VerbName == permission.HttpVerb.ToString());

            if (verbType == null)
                throw new InvalidOperationException($"Verb type {permission.HttpVerb} not found");

            // Check if URI access already exists
            var existingUriAccess = await _dbContext.UriAccesses
                .FirstOrDefaultAsync(ua => ua.EntityPermissionId == permissionScheme.Id &&
                                          ua.ResourceId == resource.Id &&
                                          ua.VerbTypeId == verbType.Id);

            if (existingUriAccess != null)
            {
                // Update existing permission
                existingUriAccess.Grant = permission.Grant;
                existingUriAccess.Deny = permission.Deny;
            }
            else
            {
                // Create new URI access
                var uriAccess = new UriAccess
                {
                    EntityPermissionId = permissionScheme.Id,
                    PermissionScheme = permissionScheme,
                    ResourceId = resource.Id,
                    Resource = resource,
                    VerbTypeId = verbType.Id,
                    VerbType = verbType,
                    Grant = permission.Grant,
                    Deny = permission.Deny
                };

                _dbContext.UriAccesses.Add(uriAccess);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Added permission {Uri}:{HttpVerb} to entity {EntityId}", 
                permission.Uri, permission.HttpVerb, entityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist add permission {Uri}:{HttpVerb} to entity {EntityId}", 
                permission.Uri, permission.HttpVerb, entityId);
            throw;
        }
    }

    public async Task PersistRemovePermissionFromEntityAsync(int entityId, Permission permission)
    {
        try
        {
            // Find the permission scheme for the entity
            var permissionScheme = await _dbContext.EntityPermissions
                .FirstOrDefaultAsync(ps => ps.EntityId == entityId);

            if (permissionScheme == null)
            {
                _logger.LogWarning("No permission scheme found for entity {EntityId}", entityId);
                return;
            }

            // Find the resource
            var resource = await _dbContext.Resources
                .FirstOrDefaultAsync(r => r.Uri == permission.Uri);

            if (resource == null)
            {
                _logger.LogWarning("Resource {Uri} not found", permission.Uri);
                return;
            }

            // Find the verb type
            var verbType = await _dbContext.VerbTypes
                .FirstOrDefaultAsync(vt => vt.VerbName == permission.HttpVerb.ToString());

            if (verbType == null)
            {
                _logger.LogWarning("Verb type {HttpVerb} not found", permission.HttpVerb);
                return;
            }

            // Find and remove the URI access
            var uriAccess = await _dbContext.UriAccesses
                .FirstOrDefaultAsync(ua => ua.EntityPermissionId == permissionScheme.Id &&
                                          ua.ResourceId == resource.Id &&
                                          ua.VerbTypeId == verbType.Id);

            if (uriAccess != null)
            {
                _dbContext.UriAccesses.Remove(uriAccess);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Persisted: Removed permission {Uri}:{HttpVerb} from entity {EntityId}", 
                    permission.Uri, permission.HttpVerb, entityId);
            }
            else
            {
                _logger.LogWarning("Permission {Uri}:{HttpVerb} not found for entity {EntityId}", 
                    permission.Uri, permission.HttpVerb, entityId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist remove permission {Uri}:{HttpVerb} from entity {EntityId}", 
                permission.Uri, permission.HttpVerb, entityId);
            throw;
        }
    }

    private async Task<SchemeType> GetOrCreateSchemeTypeAsync(Scheme scheme)
    {
        var schemeType = await _dbContext.Set<SchemeType>()
            .FirstOrDefaultAsync(st => st.SchemeName == scheme.ToString());

        if (schemeType == null)
        {
            schemeType = new SchemeType
            {
                SchemeName = scheme.ToString()
            };

            _dbContext.Set<SchemeType>().Add(schemeType);
            await _dbContext.SaveChangesAsync();
        }

        return schemeType;
    }

    #endregion

    #region CREATE Operations - Phase 2 requirement

    public async Task PersistCreateUserAsync(int userId, string name, int? groupId = null, int? roleId = null)
    {
        try
        {
            var user = new Data.Models.User
            {
                Id = userId,
                Name = name,
                GroupId = groupId ?? 0,
                RoleId = roleId ?? 0
            };

            // Set navigation properties if IDs are provided
            if (groupId.HasValue && groupId.Value > 0)
            {
                var group = await _dbContext.Groups.FindAsync(groupId.Value);
                user.Group = group ?? throw new InvalidOperationException($"Group {groupId.Value} not found");
            }

            if (roleId.HasValue && roleId.Value > 0)
            {
                var role = await _dbContext.Roles.FindAsync(roleId.Value);
                user.Role = role ?? throw new InvalidOperationException($"Role {roleId.Value} not found");
            }

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Created user {UserId} with name '{UserName}'", userId, name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist create user {UserId} with name '{UserName}'", userId, name);
            throw;
        }
    }

    public async Task PersistCreateGroupAsync(int groupId, string name, int? parentGroupId = null)
    {
        try
        {
            var group = new Data.Models.Group
            {
                Id = groupId,
                Name = name,
                ParentGroupId = parentGroupId ?? 0
            };

            // Set navigation property if parent ID is provided
            if (parentGroupId.HasValue && parentGroupId.Value > 0)
            {
                var parentGroup = await _dbContext.Groups.FindAsync(parentGroupId.Value);
                group.ParentGroup = parentGroup ?? throw new InvalidOperationException($"Parent group {parentGroupId.Value} not found");
                
                // Check for circular references
                if (await WouldCreateCircularReferenceAsync(groupId, parentGroupId.Value))
                    throw new InvalidOperationException($"Adding group {groupId} to parent group {parentGroupId.Value} would create a circular reference");
            }

            _dbContext.Groups.Add(group);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Created group {GroupId} with name '{GroupName}'", groupId, name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist create group {GroupId} with name '{GroupName}'", groupId, name);
            throw;
        }
    }

    public async Task PersistCreateRoleAsync(int roleId, string name, int? groupId = null)
    {
        try
        {
            var role = new Data.Models.Role
            {
                Id = roleId,
                Name = name,
                GroupId = groupId ?? 0
            };

            // Set navigation property if group ID is provided
            if (groupId.HasValue && groupId.Value > 0)
            {
                var group = await _dbContext.Groups.FindAsync(groupId.Value);
                role.Group = group ?? throw new InvalidOperationException($"Group {groupId.Value} not found");
            }

            _dbContext.Roles.Add(role);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Created role {RoleId} with name '{RoleName}'", roleId, name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist create role {RoleId} with name '{RoleName}'", roleId, name);
            throw;
        }
    }

    #endregion

    #region Bulk Operations

    public async Task<int> SaveChangesAsync()
    {
        try
        {
            var result = await _dbContext.SaveChangesAsync();
            _logger.LogDebug("Saved {ChangeCount} changes to database", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save changes to database");
            throw;
        }
    }

    public async Task BeginTransactionAsync()
    {
        await _dbContext.Database.BeginTransactionAsync();
        _logger.LogDebug("Began database transaction");
    }

    public async Task CommitTransactionAsync()
    {
        if (_dbContext.Database.CurrentTransaction != null)
        {
            await _dbContext.Database.CommitTransactionAsync();
            _logger.LogDebug("Committed database transaction");
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_dbContext.Database.CurrentTransaction != null)
        {
            await _dbContext.Database.RollbackTransactionAsync();
            _logger.LogDebug("Rolled back database transaction");
        }
    }

    #endregion
}