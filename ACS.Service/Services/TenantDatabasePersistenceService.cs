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

    public virtual async Task PersistAddUserToGroupAsync(int userId, int groupId)
    {
        try
        {
            await Delegates.Normalizers.AddUserToGroupNormalizer.ExecuteAsync(_dbContext, userId, groupId, "system");
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Added user {UserId} to group {GroupId}", userId, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist add user {UserId} to group {GroupId}", userId, groupId);
            throw;
        }
    }

    public virtual async Task PersistRemoveUserFromGroupAsync(int userId, int groupId)
    {
        try
        {
            await Delegates.Normalizers.RemoveUserFromGroupNormalizer.ExecuteAsync(_dbContext, userId, groupId);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Removed user {UserId} from group {GroupId}", userId, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist remove user {UserId} from group {GroupId}", userId, groupId);
            throw;
        }
    }

    #endregion

    #region User-Role Persistence

    public virtual async Task PersistAssignUserToRoleAsync(int userId, int roleId)
    {
        try
        {
            await Delegates.Normalizers.AssignUserToRoleNormalizer.ExecuteAsync(_dbContext, userId, roleId, "system");
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Assigned user {UserId} to role {RoleId}", userId, roleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist assign user {UserId} to role {RoleId}", userId, roleId);
            throw;
        }
    }

    public virtual async Task PersistUnAssignUserFromRoleAsync(int userId, int roleId)
    {
        try
        {
            await Delegates.Normalizers.UnAssignUserFromRoleNormalizer.ExecuteAsync(_dbContext, userId, roleId);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Unassigned user {UserId} from role {RoleId}", userId, roleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist unassign user {UserId} from role {RoleId}", userId, roleId);
            throw;
        }
    }

    #endregion

    #region Group-Role Persistence
    
    // Temporarily simplified - delegates to normalizer
    public virtual async Task PersistAddRoleToGroupAsync(int groupId, int roleId)
    {
        try
        {
            await Delegates.Normalizers.AddRoleToGroupNormalizer.ExecuteAsync(_dbContext, groupId, roleId, "system");
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Persisted: Added role {RoleId} to group {GroupId}", roleId, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist add role {RoleId} to group {GroupId}", roleId, groupId);
            throw;
        }
    }

    // TODO: Implement using normalizers when available
    public virtual async Task PersistRemoveRoleFromGroupAsync(int groupId, int roleId)
    {
        // TODO: Implement with GroupRole junction table
        await Task.CompletedTask;
        _logger.LogWarning("PersistRemoveRoleFromGroupAsync not yet implemented with junction tables");
    }

    #endregion

    #region Group-Group Persistence

    // TODO: Migrate to use normalizers
    public virtual async Task PersistAddGroupToGroupAsync(int parentGroupId, int childGroupId)
    {
        // TODO: Implement with GroupHierarchy junction table
        await Task.CompletedTask;
        _logger.LogWarning("PersistAddGroupToGroupAsync not yet implemented with junction tables");
    }

    public virtual async Task PersistRemoveGroupFromGroupAsync(int parentGroupId, int childGroupId)
    {
        // TODO: Implement with GroupHierarchy junction table
        await Task.CompletedTask;
        _logger.LogWarning("PersistRemoveGroupFromGroupAsync not yet implemented with junction tables");
    }

    private async Task<bool> WouldCreateCircularReferenceAsync(int childGroupId, int parentGroupId)
    {
        // TODO: Implement with GroupHierarchy junction table traversal
        await Task.CompletedTask;
        return false;
    }

    #endregion

    #region Permission Persistence

    public virtual async Task PersistAddPermissionToEntityAsync(int entityId, Permission permission)
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

    public virtual async Task PersistRemovePermissionFromEntityAsync(int entityId, Permission permission)
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

    public virtual async Task PersistCreateUserAsync(int userId, string name, int? groupId = null, int? roleId = null)
    {
        try
        {
            // Check if entity already exists
            var existingEntity = await _dbContext.Entities.FindAsync(userId);
            if (existingEntity == null)
            {
                // Create the user entity first
                var userEntity = new Data.Models.Entity
                {
                    Id = userId
                };
                _dbContext.Entities.Add(userEntity);
            }
            
            var user = new Data.Models.User
            {
                Id = userId,
                Name = name,
                Entity = existingEntity ?? _dbContext.Entities.Local.First(e => e.Id == userId)
            };
            _dbContext.Users.Add(user);

            // Handle group assignment through junction table
            if (groupId.HasValue && groupId.Value > 0)
            {
                var group = await _dbContext.Groups.FindAsync(groupId.Value);
                if (group == null) throw new InvalidOperationException($"Group {groupId.Value} not found");
                
                var userGroup = new Data.Models.UserGroup
                {
                    UserId = userId,
                    GroupId = groupId.Value
                };
                _dbContext.UserGroups.Add(userGroup);
            }

            // Handle role assignment through junction table
            if (roleId.HasValue && roleId.Value > 0)
            {
                var role = await _dbContext.Roles.FindAsync(roleId.Value);
                if (role == null) throw new InvalidOperationException($"Role {roleId.Value} not found");
                
                var userRole = new Data.Models.UserRole
                {
                    UserId = userId,
                    RoleId = roleId.Value
                };
                _dbContext.UserRoles.Add(userRole);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Created user {UserId} with name '{UserName}'", userId, name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist create user {UserId} with name '{UserName}'", userId, name);
            throw;
        }
    }

    public virtual async Task PersistCreateGroupAsync(int groupId, string name, int? parentGroupId = null)
    {
        try
        {
            // Check if entity already exists
            var existingEntity = await _dbContext.Entities.FindAsync(groupId);
            if (existingEntity == null)
            {
                // Create the group entity first
                var groupEntity = new Data.Models.Entity
                {
                    Id = groupId
                };
                _dbContext.Entities.Add(groupEntity);
            }
            
            var group = new Data.Models.Group
            {
                Id = groupId,
                Name = name,
                Entity = existingEntity ?? _dbContext.Entities.Local.First(e => e.Id == groupId)
            };
            _dbContext.Groups.Add(group);

            // Handle parent group assignment through junction table
            if (parentGroupId.HasValue && parentGroupId.Value > 0)
            {
                var parentGroup = await _dbContext.Groups.FindAsync(parentGroupId.Value);
                if (parentGroup == null) throw new InvalidOperationException($"Parent group {parentGroupId.Value} not found");
                
                // Check for circular references
                if (await WouldCreateCircularReferenceAsync(groupId, parentGroupId.Value))
                    throw new InvalidOperationException($"Adding group {groupId} to parent group {parentGroupId.Value} would create a circular reference");
                
                var groupHierarchy = new Data.Models.GroupHierarchy
                {
                    ChildGroupId = groupId,
                    ParentGroupId = parentGroupId.Value
                };
                _dbContext.GroupHierarchies.Add(groupHierarchy);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted: Created group {GroupId} with name '{GroupName}'", groupId, name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist create group {GroupId} with name '{GroupName}'", groupId, name);
            throw;
        }
    }

    public virtual async Task PersistCreateRoleAsync(int roleId, string name, int? groupId = null)
    {
        try
        {
            // Check if entity already exists
            var existingEntity = await _dbContext.Entities.FindAsync(roleId);
            if (existingEntity == null)
            {
                // Create the role entity first
                var roleEntity = new Data.Models.Entity
                {
                    Id = roleId
                };
                _dbContext.Entities.Add(roleEntity);
            }
            
            var role = new Data.Models.Role
            {
                Id = roleId,
                Name = name,
                Entity = existingEntity ?? _dbContext.Entities.Local.First(e => e.Id == roleId)
            };
            _dbContext.Roles.Add(role);

            // Handle group assignment through junction table if needed
            if (groupId.HasValue && groupId.Value > 0)
            {
                var group = await _dbContext.Groups.FindAsync(groupId.Value);
                if (group == null) throw new InvalidOperationException($"Group {groupId.Value} not found");
                
                var groupRole = new Data.Models.GroupRole
                {
                    GroupId = groupId.Value,
                    RoleId = roleId
                };
                _dbContext.GroupRoles.Add(groupRole);
            }

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