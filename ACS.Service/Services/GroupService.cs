using ACS.Service.Domain;
using ACS.Service.Infrastructure;
using ACS.Service.Data;
using ACS.Service.Delegates.Queries;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

/// <summary>
/// Flat service for Group operations in LMAX architecture
/// Works directly with in-memory entity graph and fire-and-forget persistence
/// No service-to-service dependencies
/// </summary>
public class GroupService : IGroupService
{
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<GroupService> _logger;

    public GroupService(
        InMemoryEntityGraph entityGraph,
        ApplicationDbContext dbContext,
        ILogger<GroupService> logger)
    {
        _entityGraph = entityGraph;
        _dbContext = dbContext;
        _logger = logger;
    }

    #region Basic CRUD Operations

    public Task<IEnumerable<Domain.Group>> GetAllGroupsAsync()
    {
        // Use Query object for data access
        var getGroupsQuery = new GetGroupsQuery
        {
            Page = 1,
            PageSize = 1000, // Get all groups for now
            EntityGraph = _entityGraph
        };

        var result = getGroupsQuery.Execute();
        
        _logger.LogDebug("Retrieved {GroupCount} groups", result.Count);
        return Task.FromResult<IEnumerable<Domain.Group>>(result);
    }

    public Task<Domain.Group?> GetGroupByIdAsync(int groupId)
    {
        // Use Query object for data access
        var getGroupQuery = new GetGroupByIdQuery
        {
            GroupId = groupId,
            EntityGraph = _entityGraph
        };

        var result = getGroupQuery.Execute();
        
        _logger.LogDebug("Retrieved group {GroupId}, found: {Found}", groupId, result != null);
        return Task.FromResult(result);
    }

    public async Task<Domain.Group> CreateGroupAsync(string name, string description, string createdBy)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Group name cannot be null or empty", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(createdBy))
            {
                throw new ArgumentException("CreatedBy cannot be null or empty", nameof(createdBy));
            }

            // Direct domain operations - no service dependencies
            var newId = _entityGraph.GetNextGroupId();
            var group = new Domain.Group
            {
                Id = newId,
                Name = name
            };
            
            _entityGraph.Groups[newId] = group;

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created group {GroupId} with name '{GroupName}' by {CreatedBy} (persistence queued)", 
                newId, name, createdBy);

            return group;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for group creation");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating group");
            throw new InvalidOperationException($"Failed to create group: {ex.Message}", ex);
        }
    }

    public async Task<Domain.Group> UpdateGroupAsync(int groupId, string name, string description, string updatedBy)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Group name cannot be null or empty", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(updatedBy))
            {
                throw new ArgumentException("UpdatedBy cannot be null or empty", nameof(updatedBy));
            }

            // Use Query object to get group
            var getGroupQuery = new GetGroupByIdQuery
            {
                GroupId = groupId,
                EntityGraph = _entityGraph
            };

            var existingGroup = getGroupQuery.Execute();
            if (existingGroup == null)
            {
                throw new InvalidOperationException($"Group {groupId} not found");
            }

            // Update group name directly (groups don't have complex business logic like users)
            existingGroup.Name = name;

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated group {GroupId} with name '{GroupName}' by {UpdatedBy} (persistence queued)", 
                groupId, name, updatedBy);

            return existingGroup;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for group update: {GroupId}", groupId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during group update: {GroupId}", groupId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating group {GroupId}", groupId);
            throw new InvalidOperationException($"Failed to update group {groupId}: {ex.Message}", ex);
        }
    }

    public async Task DeleteGroupAsync(int groupId, string deletedBy)
    {
        try
        {
            if (groupId <= 0)
            {
                throw new ArgumentException("Group ID must be a positive integer", nameof(groupId));
            }

            if (string.IsNullOrWhiteSpace(deletedBy))
            {
                throw new ArgumentException("DeletedBy cannot be null or empty", nameof(deletedBy));
            }

            // Use Query object to check if group exists
            var getGroupQuery = new GetGroupByIdQuery
            {
                GroupId = groupId,
                EntityGraph = _entityGraph
            };

            var existingGroup = getGroupQuery.Execute();
            if (existingGroup == null)
            {
                throw new InvalidOperationException($"Group {groupId} not found");
            }

            // Remove from in-memory graph
            _entityGraph.Groups.Remove(groupId);

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted group {GroupId} by {DeletedBy} (persistence queued)", groupId, deletedBy);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for group deletion: {GroupId}", groupId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during group deletion: {GroupId}", groupId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting group {GroupId}", groupId);
            throw new InvalidOperationException($"Failed to delete group {groupId}: {ex.Message}", ex);
        }
    }

    #endregion

    #region Hierarchy Management

    public async Task<IEnumerable<Domain.Group>> GetGroupHierarchyAsync(int groupId)
    {
        // For hierarchy operations, we can work with the in-memory graph directly
        var group = await GetGroupByIdAsync(groupId);
        if (group == null)
        {
            return Enumerable.Empty<Domain.Group>();
        }

        var hierarchy = new List<Domain.Group> { group };
        
        // Get all descendants recursively
        var descendants = GetDescendants(group);
        hierarchy.AddRange(descendants);

        _logger.LogDebug("Retrieved hierarchy for group {GroupId} with {Count} groups", 
            groupId, hierarchy.Count);

        return hierarchy;
    }

    public async Task<IEnumerable<Domain.Group>> GetChildGroupsAsync(int parentGroupId)
    {
        var parentGroup = await GetGroupByIdAsync(parentGroupId);
        if (parentGroup == null)
        {
            return Enumerable.Empty<Domain.Group>();
        }

        var childGroups = parentGroup.Children.OfType<Domain.Group>().ToList();
        
        _logger.LogDebug("Retrieved {Count} child groups for parent {ParentGroupId}", 
            childGroups.Count, parentGroupId);

        return childGroups;
    }

    public async Task<IEnumerable<Domain.Group>> GetParentGroupsAsync(int childGroupId)
    {
        var childGroup = await GetGroupByIdAsync(childGroupId);
        if (childGroup == null)
        {
            return Enumerable.Empty<Domain.Group>();
        }

        var parentGroups = childGroup.Parents.OfType<Domain.Group>().ToList();
        
        _logger.LogDebug("Retrieved {Count} parent groups for child {ChildGroupId}", 
            parentGroups.Count, childGroupId);

        return parentGroups;
    }

    public async Task AddGroupToGroupAsync(int parentGroupId, int childGroupId, string createdBy)
    {
        try
        {
            if (parentGroupId <= 0)
            {
                throw new ArgumentException("Parent group ID must be a positive integer", nameof(parentGroupId));
            }

            if (childGroupId <= 0)
            {
                throw new ArgumentException("Child group ID must be a positive integer", nameof(childGroupId));
            }

            if (string.IsNullOrWhiteSpace(createdBy))
            {
                throw new ArgumentException("CreatedBy cannot be null or empty", nameof(createdBy));
            }

            // Use Query objects to get groups
            var getParentGroupQuery = new GetGroupByIdQuery
            {
                GroupId = parentGroupId,
                EntityGraph = _entityGraph
            };

            var getChildGroupQuery = new GetGroupByIdQuery
            {
                GroupId = childGroupId,
                EntityGraph = _entityGraph
            };

            var parentGroup = getParentGroupQuery.Execute();
            var childGroup = getChildGroupQuery.Execute();

            if (parentGroup == null)
            {
                throw new InvalidOperationException($"Parent group {parentGroupId} not found");
            }

            if (childGroup == null)
            {
                throw new InvalidOperationException($"Child group {childGroupId} not found");
            }

            // Use rich domain object business logic (includes cycle prevention, limits, etc.)
            parentGroup.AddGroup(childGroup, createdBy);

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Added group {ChildGroupId} to group {ParentGroupId} by {CreatedBy} (persistence queued)", 
                childGroupId, parentGroupId, createdBy);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument for adding group {ChildGroupId} to group {ParentGroupId}", 
                childGroupId, parentGroupId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation adding group {ChildGroupId} to group {ParentGroupId}", 
                childGroupId, parentGroupId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding group {ChildGroupId} to group {ParentGroupId}", 
                childGroupId, parentGroupId);
            throw new InvalidOperationException($"Failed to add group {childGroupId} to group {parentGroupId}: {ex.Message}", ex);
        }
    }

    public async Task RemoveGroupFromGroupAsync(int parentGroupId, int childGroupId, string removedBy)
    {
        try
        {
            if (parentGroupId <= 0)
            {
                throw new ArgumentException("Parent group ID must be a positive integer", nameof(parentGroupId));
            }

            if (childGroupId <= 0)
            {
                throw new ArgumentException("Child group ID must be a positive integer", nameof(childGroupId));
            }

            if (string.IsNullOrWhiteSpace(removedBy))
            {
                throw new ArgumentException("RemovedBy cannot be null or empty", nameof(removedBy));
            }

            // Use Query objects to get groups
            var getParentGroupQuery = new GetGroupByIdQuery
            {
                GroupId = parentGroupId,
                EntityGraph = _entityGraph
            };

            var getChildGroupQuery = new GetGroupByIdQuery
            {
                GroupId = childGroupId,
                EntityGraph = _entityGraph
            };

            var parentGroup = getParentGroupQuery.Execute();
            var childGroup = getChildGroupQuery.Execute();

            if (parentGroup == null)
            {
                throw new InvalidOperationException($"Parent group {parentGroupId} not found");
            }

            if (childGroup == null)
            {
                throw new InvalidOperationException($"Child group {childGroupId} not found");
            }

            // Use rich domain object business logic (includes validation, normalizer execution, and events)
            parentGroup.RemoveGroup(childGroup, removedBy);

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Removed group {ChildGroupId} from group {ParentGroupId} by {RemovedBy} (persistence queued)", 
                childGroupId, parentGroupId, removedBy);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument for removing group {ChildGroupId} from group {ParentGroupId}", 
                childGroupId, parentGroupId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation removing group {ChildGroupId} from group {ParentGroupId}", 
                childGroupId, parentGroupId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error removing group {ChildGroupId} from group {ParentGroupId}", 
                childGroupId, parentGroupId);
            throw new InvalidOperationException($"Failed to remove group {childGroupId} from group {parentGroupId}: {ex.Message}", ex);
        }
    }

    public async Task<bool> ValidateGroupHierarchyAsync(int parentGroupId, int childGroupId)
    {
        try
        {
            var parentGroup = await GetGroupByIdAsync(parentGroupId);
            var childGroup = await GetGroupByIdAsync(childGroupId);

            if (parentGroup == null || childGroup == null)
            {
                return false;
            }

            // Check for circular reference - would create a cycle
            if (WouldCreateCircularReference(childGroup, parentGroup))
            {
                _logger.LogWarning("Hierarchy validation failed: adding group {ChildGroupId} to {ParentGroupId} would create circular reference", 
                    childGroupId, parentGroupId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating group hierarchy for parent {ParentGroupId} and child {ChildGroupId}", 
                parentGroupId, childGroupId);
            return false;
        }
    }

    #endregion

    #region Private Helper Methods

    private IEnumerable<Domain.Group> GetDescendants(Domain.Group group)
    {
        var descendants = new List<Domain.Group>();
        var visited = new HashSet<int>();

        void CollectDescendants(Domain.Group currentGroup)
        {
            if (visited.Contains(currentGroup.Id))
            {
                return; // Prevent infinite loops
            }

            visited.Add(currentGroup.Id);

            foreach (var child in currentGroup.Children.OfType<Domain.Group>())
            {
                descendants.Add(child);
                CollectDescendants(child);
            }
        }

        CollectDescendants(group);
        return descendants;
    }

    private bool WouldCreateCircularReference(Domain.Group childGroup, Domain.Group parentGroup)
    {
        // Check if parentGroup is already a descendant of childGroup
        var visited = new HashSet<int>();
        var queue = new Queue<Entity>();
        queue.Enqueue(childGroup);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current.Id))
                continue;

            visited.Add(current.Id);

            if (current.Id == parentGroup.Id)
                return true;

            foreach (var child in current.Children)
            {
                if (!visited.Contains(child.Id))
                    queue.Enqueue(child);
            }
        }

        return false;
    }

    #endregion

    #region User Management

    public Task<bool> WouldCreateCycleAsync(int parentGroupId, int childGroupId)
    {
        // TODO: Implement cycle detection logic
        _logger.LogWarning("WouldCreateCycleAsync not yet implemented");
        return Task.FromResult(false);
    }

    public Task<IEnumerable<Domain.User>> GetGroupUsersAsync(int groupId, bool includeInherited = false)
    {
        try
        {
            var group = _entityGraph.Groups.GetValueOrDefault(groupId);
            if (group == null)
            {
                return Task.FromResult(Enumerable.Empty<Domain.User>());
            }

            var users = group.Children.OfType<Domain.User>().ToList();
            
            _logger.LogDebug("Retrieved {Count} users for group {GroupId}", users.Count, groupId);
            return Task.FromResult<IEnumerable<Domain.User>>(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users for group {GroupId}", groupId);
            return Task.FromResult(Enumerable.Empty<Domain.User>());
        }
    }

    public async Task AddUserToGroupAsync(int userId, int groupId, string addedBy)
    {
        try
        {
            var user = _entityGraph.Users.GetValueOrDefault(userId);
            var group = _entityGraph.Groups.GetValueOrDefault(groupId);

            if (user == null)
            {
                throw new InvalidOperationException($"User {userId} not found");
            }

            if (group == null)
            {
                throw new InvalidOperationException($"Group {groupId} not found");
            }

            group.AddUser(user, addedBy);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Added user {UserId} to group {GroupId} by {AddedBy}", 
                userId, groupId, addedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to group {GroupId}", userId, groupId);
            throw;
        }
    }

    public async Task RemoveUserFromGroupAsync(int userId, int groupId, string removedBy)
    {
        try
        {
            var user = _entityGraph.Users.GetValueOrDefault(userId);
            var group = _entityGraph.Groups.GetValueOrDefault(groupId);

            if (user == null)
            {
                throw new InvalidOperationException($"User {userId} not found");
            }

            if (group == null)
            {
                throw new InvalidOperationException($"Group {groupId} not found");
            }

            group.RemoveUser(user, removedBy);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Removed user {UserId} from group {GroupId} by {RemovedBy}", 
                userId, groupId, removedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from group {GroupId}", userId, groupId);
            throw;
        }
    }

    public Task<bool> IsUserInGroupAsync(int userId, int groupId, bool checkInherited = false)
    {
        try
        {
            var group = _entityGraph.Groups.GetValueOrDefault(groupId);
            if (group == null)
            {
                return Task.FromResult(false);
            }

            var isInGroup = group.Children.OfType<Domain.User>().Any(u => u.Id == userId);
            
            _logger.LogDebug("User {UserId} is in group {GroupId}: {IsInGroup}", userId, groupId, isInGroup);
            return Task.FromResult(isInGroup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is in group {GroupId}", userId, groupId);
            return Task.FromResult(false);
        }
    }

    #endregion

    #region Role Management

    public Task<IEnumerable<Domain.Role>> GetGroupRolesAsync(int groupId, bool includeInherited = false)
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

    public async Task AddRoleToGroupAsync(int groupId, int roleId, string addedBy)
    {
        try
        {
            var group = _entityGraph.Groups.GetValueOrDefault(groupId);
            var role = _entityGraph.Roles.GetValueOrDefault(roleId);

            if (group == null)
            {
                throw new InvalidOperationException($"Group {groupId} not found");
            }

            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found");
            }

            group.AddRole(role, addedBy);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Added role {RoleId} to group {GroupId} by {AddedBy}", 
                roleId, groupId, addedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding role {RoleId} to group {GroupId}", roleId, groupId);
            throw;
        }
    }

    public async Task RemoveRoleFromGroupAsync(int groupId, int roleId, string removedBy)
    {
        try
        {
            var group = _entityGraph.Groups.GetValueOrDefault(groupId);
            var role = _entityGraph.Roles.GetValueOrDefault(roleId);

            if (group == null)
            {
                throw new InvalidOperationException($"Group {groupId} not found");
            }

            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found");
            }

            group.RemoveRole(role, removedBy);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Removed role {RoleId} from group {GroupId} by {RemovedBy}", 
                roleId, groupId, removedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from group {GroupId}", roleId, groupId);
            throw;
        }
    }

    #endregion

    #region Permission Management

    public Task<IEnumerable<Permission>> GetGroupPermissionsAsync(int groupId, bool includeInherited = false)
    {
        try
        {
            var group = _entityGraph.Groups.GetValueOrDefault(groupId);
            if (group == null)
            {
                return Task.FromResult(Enumerable.Empty<Permission>());
            }

            var permissions = group.Permissions.ToList();
            
            _logger.LogDebug("Retrieved {Count} permissions for group {GroupId}", permissions.Count, groupId);
            return Task.FromResult<IEnumerable<Permission>>(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for group {GroupId}", groupId);
            return Task.FromResult(Enumerable.Empty<Permission>());
        }
    }

    public async Task<bool> HasPermissionAsync(int groupId, string resource, string action)
    {
        try
        {
            var permissions = await GetGroupPermissionsAsync(groupId);
            var hasPermission = permissions.Any(p => p.Resource == resource && p.Action == action);
            
            _logger.LogDebug("Group {GroupId} has permission {Resource}:{Action}: {HasPermission}", 
                groupId, resource, action, hasPermission);
            return hasPermission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission for group {GroupId}", groupId);
            return false;
        }
    }

    #endregion

    #region Bulk Operations (Stub implementations)

    public Task<IEnumerable<Domain.Group>> CreateGroupsBulkAsync(IEnumerable<(string Name, string Description)> groups, string createdBy)
    {
        _logger.LogWarning("CreateGroupsBulkAsync not yet implemented");
        throw new NotImplementedException("Bulk operations not yet implemented");
    }

    public Task AddUsersToGroupBulkAsync(int groupId, IEnumerable<int> userIds, string addedBy)
    {
        _logger.LogWarning("AddUsersToGroupBulkAsync not yet implemented");
        throw new NotImplementedException("Bulk operations not yet implemented");
    }

    public Task AddGroupsToGroupBulkAsync(int parentGroupId, IEnumerable<int> childGroupIds, string addedBy)
    {
        _logger.LogWarning("AddGroupsToGroupBulkAsync not yet implemented");
        throw new NotImplementedException("Bulk operations not yet implemented");
    }

    #endregion

    #region Search and Filtering (Stub implementations)

    public Task<IEnumerable<Domain.Group>> SearchGroupsAsync(string searchTerm)
    {
        _logger.LogWarning("SearchGroupsAsync not yet implemented - returning empty collection");
        return Task.FromResult(Enumerable.Empty<Domain.Group>());
    }

    public Task<IEnumerable<Domain.Group>> GetGroupsByUserAsync(int userId)
    {
        try
        {
            var user = _entityGraph.Users.GetValueOrDefault(userId);
            if (user == null)
            {
                return Task.FromResult(Enumerable.Empty<Domain.Group>());
            }

            var groups = user.Parents.OfType<Domain.Group>().ToList();
            
            _logger.LogDebug("Retrieved {Count} groups for user {UserId}", groups.Count, userId);
            return Task.FromResult<IEnumerable<Domain.Group>>(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving groups for user {UserId}", userId);
            return Task.FromResult(Enumerable.Empty<Domain.Group>());
        }
    }

    public Task<IEnumerable<Domain.Group>> GetGroupsByRoleAsync(int roleId)
    {
        try
        {
            var role = _entityGraph.Roles.GetValueOrDefault(roleId);
            if (role == null)
            {
                return Task.FromResult(Enumerable.Empty<Domain.Group>());
            }

            var groups = role.Parents.OfType<Domain.Group>().ToList();
            
            _logger.LogDebug("Retrieved {Count} groups for role {RoleId}", groups.Count, roleId);
            return Task.FromResult<IEnumerable<Domain.Group>>(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving groups for role {RoleId}", roleId);
            return Task.FromResult(Enumerable.Empty<Domain.Group>());
        }
    }

    #endregion
}