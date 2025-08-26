using ACS.Service.Domain;
using ACS.Service.Infrastructure;
using ACS.Service.Data;
using ACS.Service.Delegates.Queries;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

/// <summary>
/// Flat service for Role operations in LMAX architecture
/// Works directly with in-memory entity graph and fire-and-forget persistence
/// No service-to-service dependencies
/// </summary>
public class RoleService : IRoleService
{
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        InMemoryEntityGraph entityGraph,
        ApplicationDbContext dbContext,
        ILogger<RoleService> logger)
    {
        _entityGraph = entityGraph;
        _dbContext = dbContext;
        _logger = logger;
    }

    #region Basic CRUD Operations

    public Task<IEnumerable<Domain.Role>> GetAllRolesAsync()
    {
        // Use Query object for data access
        var getRolesQuery = new GetRolesQuery
        {
            Page = 1,
            PageSize = 1000, // Get all roles for now
            EntityGraph = _entityGraph
        };

        var result = getRolesQuery.Execute();
        
        _logger.LogDebug("Retrieved {RoleCount} roles", result.Count);
        return Task.FromResult<IEnumerable<Domain.Role>>(result);
    }

    public Task<Domain.Role?> GetRoleByIdAsync(int roleId)
    {
        // Use Query object for data access
        var getRoleQuery = new GetRoleByIdQuery
        {
            RoleId = roleId,
            EntityGraph = _entityGraph
        };

        var result = getRoleQuery.Execute();
        
        _logger.LogDebug("Retrieved role {RoleId}, found: {Found}", roleId, result != null);
        return Task.FromResult(result);
    }

    public async Task<Domain.Role> CreateRoleAsync(string name, string description, string createdBy)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Role name cannot be null or empty", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(createdBy))
            {
                throw new ArgumentException("CreatedBy cannot be null or empty", nameof(createdBy));
            }

            // Generate new ID and create role
            var newId = _entityGraph.GetNextRoleId();
            var role = new Domain.Role
            {
                Id = newId,
                Name = name
            };

            _entityGraph.Roles[newId] = role;

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created role {RoleId} with name '{RoleName}' by {CreatedBy} (persistence queued)", 
                newId, name, createdBy);

            return role;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for role creation");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating role");
            throw new InvalidOperationException($"Failed to create role: {ex.Message}", ex);
        }
    }

    public async Task<Domain.Role> UpdateRoleAsync(int roleId, string name, string description, string updatedBy)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Role name cannot be null or empty", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(updatedBy))
            {
                throw new ArgumentException("UpdatedBy cannot be null or empty", nameof(updatedBy));
            }

            // Use Query object to get role
            var getRoleQuery = new GetRoleByIdQuery
            {
                RoleId = roleId,
                EntityGraph = _entityGraph
            };

            var existingRole = getRoleQuery.Execute();
            if (existingRole == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found");
            }

            // Update role name directly (roles don't have complex business logic like users)
            existingRole.Name = name;

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated role {RoleId} with name '{RoleName}' by {UpdatedBy} (persistence queued)", 
                roleId, name, updatedBy);

            return existingRole;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for role update: {RoleId}", roleId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during role update: {RoleId}", roleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating role {RoleId}", roleId);
            throw new InvalidOperationException($"Failed to update role {roleId}: {ex.Message}", ex);
        }
    }

    public async Task DeleteRoleAsync(int roleId, string deletedBy)
    {
        try
        {
            if (roleId <= 0)
            {
                throw new ArgumentException("Role ID must be a positive integer", nameof(roleId));
            }

            if (string.IsNullOrWhiteSpace(deletedBy))
            {
                throw new ArgumentException("DeletedBy cannot be null or empty", nameof(deletedBy));
            }

            // Use Query object to check if role exists
            var getRoleQuery = new GetRoleByIdQuery
            {
                RoleId = roleId,
                EntityGraph = _entityGraph
            };

            var existingRole = getRoleQuery.Execute();
            if (existingRole == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found");
            }

            // Remove from in-memory graph
            _entityGraph.Roles.Remove(roleId);

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted role {RoleId} by {DeletedBy} (persistence queued)", roleId, deletedBy);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for role deletion: {RoleId}", roleId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during role deletion: {RoleId}", roleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting role {RoleId}", roleId);
            throw new InvalidOperationException($"Failed to delete role {roleId}: {ex.Message}", ex);
        }
    }

    #endregion

    #region User Assignment

    public async Task<IEnumerable<Domain.User>> GetRoleUsersAsync(int roleId)
    {
        var role = await GetRoleByIdAsync(roleId);
        if (role == null)
        {
            return Enumerable.Empty<Domain.User>();
        }

        var users = role.Children.OfType<Domain.User>().ToList();
        
        _logger.LogDebug("Retrieved {Count} users for role {RoleId}", users.Count, roleId);
        return users;
    }

    public async Task AssignUserToRoleAsync(int userId, int roleId, string assignedBy)
    {
        try
        {
            if (userId <= 0)
            {
                throw new ArgumentException("User ID must be a positive integer", nameof(userId));
            }

            if (roleId <= 0)
            {
                throw new ArgumentException("Role ID must be a positive integer", nameof(roleId));
            }

            if (string.IsNullOrWhiteSpace(assignedBy))
            {
                throw new ArgumentException("AssignedBy cannot be null or empty", nameof(assignedBy));
            }

            // Use Query objects to get entities
            var getUserQuery = new GetUserByIdQuery
            {
                UserId = userId,
                EntityGraph = _entityGraph
            };

            var getRoleQuery = new GetRoleByIdQuery
            {
                RoleId = roleId,
                EntityGraph = _entityGraph
            };

            var user = getUserQuery.Execute();
            var role = getRoleQuery.Execute();

            if (user == null)
            {
                throw new InvalidOperationException($"User {userId} not found");
            }

            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found");
            }

            // Use rich domain object business logic (includes segregation of duties, limits, etc.)
            user.AssignToRole(role, assignedBy);

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Assigned user {UserId} to role {RoleId} by {AssignedBy} (persistence queued)", 
                userId, roleId, assignedBy);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument for assigning user {UserId} to role {RoleId}", userId, roleId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation assigning user {UserId} to role {RoleId}", userId, roleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error assigning user {UserId} to role {RoleId}", userId, roleId);
            throw new InvalidOperationException($"Failed to assign user {userId} to role {roleId}: {ex.Message}", ex);
        }
    }

    public async Task UnassignUserFromRoleAsync(int userId, int roleId, string unassignedBy)
    {
        try
        {
            if (userId <= 0)
            {
                throw new ArgumentException("User ID must be a positive integer", nameof(userId));
            }

            if (roleId <= 0)
            {
                throw new ArgumentException("Role ID must be a positive integer", nameof(roleId));
            }

            if (string.IsNullOrWhiteSpace(unassignedBy))
            {
                throw new ArgumentException("UnassignedBy cannot be null or empty", nameof(unassignedBy));
            }

            // Use Query objects to get entities
            var getUserQuery = new GetUserByIdQuery
            {
                UserId = userId,
                EntityGraph = _entityGraph
            };

            var getRoleQuery = new GetRoleByIdQuery
            {
                RoleId = roleId,
                EntityGraph = _entityGraph
            };

            var user = getUserQuery.Execute();
            var role = getRoleQuery.Execute();

            if (user == null)
            {
                throw new InvalidOperationException($"User {userId} not found");
            }

            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found");
            }

            // Use rich domain object business logic (includes business rules, etc.)
            user.UnAssignFromRole(role, unassignedBy);

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Unassigned user {UserId} from role {RoleId} by {UnassignedBy} (persistence queued)", 
                userId, roleId, unassignedBy);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument for unassigning user {UserId} from role {RoleId}", userId, roleId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation unassigning user {UserId} from role {RoleId}", userId, roleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error unassigning user {UserId} from role {RoleId}", userId, roleId);
            throw new InvalidOperationException($"Failed to unassign user {userId} from role {roleId}: {ex.Message}", ex);
        }
    }

    public async Task<bool> IsUserInRoleAsync(int userId, int roleId)
    {
        try
        {
            var role = await GetRoleByIdAsync(roleId);
            if (role == null)
            {
                return false;
            }

            var isInRole = role.Children.OfType<Domain.User>().Any(u => u.Id == userId);
            
            _logger.LogDebug("User {UserId} is in role {RoleId}: {IsInRole}", userId, roleId, isInRole);
            return isInRole;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is in role {RoleId}", userId, roleId);
            return false;
        }
    }

    public Task<IEnumerable<Domain.Role>> GetUserRolesAsync(int userId, bool includeGroupRoles = true)
    {
        try
        {
            // Use Query object to get user
            var getUserQuery = new GetUserByIdQuery
            {
                UserId = userId,
                EntityGraph = _entityGraph
            };
            var user = getUserQuery.Execute();
            
            if (user == null)
            {
                return Task.FromResult(Enumerable.Empty<Domain.Role>());
            }

            // Get direct role assignments
            var directRoles = user.Parents.OfType<Domain.Role>().ToList();
            var allRoles = new List<Domain.Role>(directRoles);

            if (includeGroupRoles)
            {
                // Get roles through group membership
                var groups = user.Parents.OfType<Domain.Group>();
                foreach (var group in groups)
                {
                    var groupRoles = group.Children.OfType<Domain.Role>();
                    allRoles.AddRange(groupRoles);
                }
            }

            // Remove duplicates
            var uniqueRoles = allRoles.GroupBy(r => r.Id).Select(g => g.First()).ToList();
            
            _logger.LogDebug("Retrieved {Count} roles for user {UserId} (includeGroupRoles: {IncludeGroupRoles})", 
                uniqueRoles.Count, userId, includeGroupRoles);

            return Task.FromResult<IEnumerable<Domain.Role>>(uniqueRoles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles for user {UserId}", userId);
            return Task.FromResult(Enumerable.Empty<Domain.Role>());
        }
    }

    #endregion

    #region Group Assignment

    public async Task<IEnumerable<Domain.Group>> GetRoleGroupsAsync(int roleId)
    {
        var role = await GetRoleByIdAsync(roleId);
        if (role == null)
        {
            return Enumerable.Empty<Domain.Group>();
        }

        var groups = role.Parents.OfType<Domain.Group>().ToList();
        
        _logger.LogDebug("Retrieved {Count} groups for role {RoleId}", groups.Count, roleId);
        return groups;
    }

    public async Task AssignRoleToGroupAsync(int roleId, int groupId, string assignedBy)
    {
        try
        {
            if (roleId <= 0)
            {
                throw new ArgumentException("Role ID must be a positive integer", nameof(roleId));
            }

            if (groupId <= 0)
            {
                throw new ArgumentException("Group ID must be a positive integer", nameof(groupId));
            }

            if (string.IsNullOrWhiteSpace(assignedBy))
            {
                throw new ArgumentException("AssignedBy cannot be null or empty", nameof(assignedBy));
            }

            // Use Query objects to get entities
            var getRoleQuery = new GetRoleByIdQuery
            {
                RoleId = roleId,
                EntityGraph = _entityGraph
            };

            var getGroupQuery = new GetGroupByIdQuery
            {
                GroupId = groupId,
                EntityGraph = _entityGraph
            };

            var role = getRoleQuery.Execute();
            var group = getGroupQuery.Execute();

            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found");
            }

            if (group == null)
            {
                throw new InvalidOperationException($"Group {groupId} not found");
            }

            // Use rich domain object business logic
            group.AddRole(role, assignedBy);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Assigned role {RoleId} to group {GroupId} by {AssignedBy}", 
                roleId, groupId, assignedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleId} to group {GroupId}", roleId, groupId);
            throw;
        }
    }

    public async Task UnassignRoleFromGroupAsync(int roleId, int groupId, string unassignedBy)
    {
        try
        {
            if (roleId <= 0)
            {
                throw new ArgumentException("Role ID must be a positive integer", nameof(roleId));
            }

            if (groupId <= 0)
            {
                throw new ArgumentException("Group ID must be a positive integer", nameof(groupId));
            }

            if (string.IsNullOrWhiteSpace(unassignedBy))
            {
                throw new ArgumentException("UnassignedBy cannot be null or empty", nameof(unassignedBy));
            }

            // Use Query objects to get entities
            var getRoleQuery = new GetRoleByIdQuery
            {
                RoleId = roleId,
                EntityGraph = _entityGraph
            };

            var getGroupQuery = new GetGroupByIdQuery
            {
                GroupId = groupId,
                EntityGraph = _entityGraph
            };

            var role = getRoleQuery.Execute();
            var group = getGroupQuery.Execute();

            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found");
            }

            if (group == null)
            {
                throw new InvalidOperationException($"Group {groupId} not found");
            }

            // Use rich domain object business logic
            group.RemoveRole(role, unassignedBy);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Unassigned role {RoleId} from group {GroupId} by {UnassignedBy}", 
                roleId, groupId, unassignedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning role {RoleId} from group {GroupId}", roleId, groupId);
            throw;
        }
    }

    public Task<bool> IsRoleInGroupAsync(int roleId, int groupId)
    {
        try
        {
            var group = _entityGraph.Groups.GetValueOrDefault(groupId);
            if (group == null)
            {
                return Task.FromResult(false);
            }

            var isInGroup = group.Children.OfType<Domain.Role>().Any(r => r.Id == roleId);
            
            _logger.LogDebug("Role {RoleId} is in group {GroupId}: {IsInGroup}", roleId, groupId, isInGroup);
            return Task.FromResult(isInGroup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if role {RoleId} is in group {GroupId}", roleId, groupId);
            return Task.FromResult(false);
        }
    }

    #endregion

    #region Permission Management

    public async Task<IEnumerable<Permission>> GetRolePermissionsAsync(int roleId)
    {
        var role = await GetRoleByIdAsync(roleId);
        if (role == null)
        {
            return Enumerable.Empty<Permission>();
        }

        // Permissions are stored directly on the entity
        var permissions = role.Permissions;
        
        _logger.LogDebug("Retrieved {Count} permissions for role {RoleId}", permissions.Count, roleId);
        return permissions;
    }

    public async Task AddPermissionToRoleAsync(int roleId, Permission permission, string addedBy)
    {
        try
        {
            if (permission == null)
            {
                throw new ArgumentNullException(nameof(permission));
            }

            if (string.IsNullOrWhiteSpace(addedBy))
            {
                throw new ArgumentException("AddedBy cannot be null or empty", nameof(addedBy));
            }

            var role = await GetRoleByIdAsync(roleId);
            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found");
            }

            // Use rich domain object business logic
            role.AddPermission(permission, addedBy);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Added permission {Resource}:{Action} to role {RoleId} by {AddedBy}", 
                permission.Resource, permission.Action, roleId, addedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding permission to role {RoleId}", roleId);
            throw;
        }
    }

    public async Task RemovePermissionFromRoleAsync(int roleId, string resource, string action, string removedBy)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                throw new ArgumentException("Resource cannot be null or empty", nameof(resource));
            }

            if (string.IsNullOrWhiteSpace(action))
            {
                throw new ArgumentException("Action cannot be null or empty", nameof(action));
            }

            if (string.IsNullOrWhiteSpace(removedBy))
            {
                throw new ArgumentException("RemovedBy cannot be null or empty", nameof(removedBy));
            }

            var role = await GetRoleByIdAsync(roleId);
            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found");
            }

            // Use rich domain object business logic
            role.RemovePermission(resource, action, removedBy);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Removed permission {Resource}:{Action} from role {RoleId} by {RemovedBy}", 
                resource, action, roleId, removedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission from role {RoleId}", roleId);
            throw;
        }
    }

    public async Task<bool> RoleHasPermissionAsync(int roleId, string resource, string action)
    {
        try
        {
            var permissions = await GetRolePermissionsAsync(roleId);
            var hasPermission = permissions.Any(p => p.Resource == resource && p.Action == action);
            
            _logger.LogDebug("Role {RoleId} has permission {Resource}:{Action}: {HasPermission}", 
                roleId, resource, action, hasPermission);
            return hasPermission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission for role {RoleId}", roleId);
            return false;
        }
    }

    #endregion

    #region Role Hierarchy (Stub implementations)

    public Task<IEnumerable<Domain.Role>> GetChildRolesAsync(int parentRoleId)
    {
        // TODO: Implement role hierarchy if needed
        _logger.LogWarning("GetChildRolesAsync not yet implemented - returning empty collection");
        return Task.FromResult(Enumerable.Empty<Domain.Role>());
    }

    public Task<IEnumerable<Domain.Role>> GetParentRolesAsync(int childRoleId)
    {
        // TODO: Implement role hierarchy if needed
        _logger.LogWarning("GetParentRolesAsync not yet implemented - returning empty collection");
        return Task.FromResult(Enumerable.Empty<Domain.Role>());
    }

    public Task AddRoleHierarchyAsync(int parentRoleId, int childRoleId, string createdBy)
    {
        // TODO: Implement role hierarchy if needed
        _logger.LogWarning("AddRoleHierarchyAsync not yet implemented");
        throw new NotImplementedException("Role hierarchy not yet implemented");
    }

    public Task RemoveRoleHierarchyAsync(int parentRoleId, int childRoleId, string removedBy)
    {
        // TODO: Implement role hierarchy if needed
        _logger.LogWarning("RemoveRoleHierarchyAsync not yet implemented");
        throw new NotImplementedException("Role hierarchy not yet implemented");
    }

    #endregion

    #region Bulk Operations (Stub implementations)

    public Task<IEnumerable<Domain.Role>> CreateRolesBulkAsync(IEnumerable<(string Name, string Description)> roles, string createdBy)
    {
        // TODO: Implement bulk operations
        _logger.LogWarning("CreateRolesBulkAsync not yet implemented");
        throw new NotImplementedException("Bulk operations not yet implemented");
    }

    public Task AssignUsersToRoleBulkAsync(int roleId, IEnumerable<int> userIds, string assignedBy)
    {
        // TODO: Implement bulk operations
        _logger.LogWarning("AssignUsersToRoleBulkAsync not yet implemented");
        throw new NotImplementedException("Bulk operations not yet implemented");
    }

    public Task AddPermissionsToRoleBulkAsync(int roleId, IEnumerable<Permission> permissions, string addedBy)
    {
        // TODO: Implement bulk operations
        _logger.LogWarning("AddPermissionsToRoleBulkAsync not yet implemented");
        throw new NotImplementedException("Bulk operations not yet implemented");
    }

    #endregion

    #region Search and Filtering (Stub implementations)

    public Task<IEnumerable<Domain.Role>> SearchRolesAsync(string searchTerm)
    {
        // TODO: Implement search functionality
        _logger.LogWarning("SearchRolesAsync not yet implemented - returning empty collection");
        return Task.FromResult(Enumerable.Empty<Domain.Role>());
    }

    public Task<IEnumerable<Domain.Role>> GetRolesByPermissionAsync(string resource, string action)
    {
        // TODO: Implement search functionality
        _logger.LogWarning("GetRolesByPermissionAsync not yet implemented - returning empty collection");
        return Task.FromResult(Enumerable.Empty<Domain.Role>());
    }

    public Task<IEnumerable<Domain.Role>> GetRolesByGroupAsync(int groupId)
    {
        try
        {
            var group = _entityGraph.Groups.GetValueOrDefault(groupId);
            if (group == null)
            {
                return Task.FromResult(Enumerable.Empty<Domain.Role>());
            }

            var roles = group.Children.OfType<Domain.Role>().ToList();
            
            _logger.LogDebug("Retrieved {Count} roles for group {GroupId}", roles.Count, groupId);
            return Task.FromResult<IEnumerable<Domain.Role>>(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles for group {GroupId}", groupId);
            return Task.FromResult(Enumerable.Empty<Domain.Role>());
        }
    }

    #endregion

    #region Role Templates and Cloning (Stub implementations)

    public Task<Domain.Role> CloneRoleAsync(int sourceRoleId, string newRoleName, string clonedBy)
    {
        // TODO: Implement role cloning
        _logger.LogWarning("CloneRoleAsync not yet implemented");
        throw new NotImplementedException("Role cloning not yet implemented");
    }

    public Task<Domain.Role> CreateRoleFromTemplateAsync(string templateName, string roleName, string createdBy)
    {
        // TODO: Implement role templates
        _logger.LogWarning("CreateRoleFromTemplateAsync not yet implemented");
        throw new NotImplementedException("Role templates not yet implemented");
    }

    #endregion
}