using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Delegates.Normalizers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

public class GroupService : IGroupService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<GroupService> _logger;
    private readonly IPermissionEvaluationService _permissionService;

    public GroupService(
        ApplicationDbContext dbContext,
        ILogger<GroupService> logger,
        IPermissionEvaluationService permissionService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _permissionService = permissionService;
    }

    #region Basic CRUD Operations

    public async Task<IEnumerable<Domain.Group>> GetAllGroupsAsync()
    {
        var groups = await _dbContext.Groups
            .Include(g => g.Entity)
            .Include(g => g.UserGroups)
                .ThenInclude(ug => ug.User)
            .Include(g => g.GroupRoles)
                .ThenInclude(gr => gr.Role)
            .ToListAsync();

        return groups.Select(ConvertToDomainGroup);
    }

    public async Task<Domain.Group?> GetGroupByIdAsync(int groupId)
    {
        var group = await _dbContext.Groups
            .Include(g => g.Entity)
            .Include(g => g.UserGroups)
                .ThenInclude(ug => ug.User)
            .Include(g => g.GroupRoles)
                .ThenInclude(gr => gr.Role)
            .Include(g => g.ChildGroupRelations)
                .ThenInclude(gh => gh.ChildGroup)
            .Include(g => g.ParentGroupRelations)
                .ThenInclude(gh => gh.ParentGroup)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        return group != null ? ConvertToDomainGroup(group) : null;
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

            // Check if group name already exists
            var existingGroup = await _dbContext.Groups
                .FirstOrDefaultAsync(g => g.Name == name);
            if (existingGroup != null)
            {
                _logger.LogWarning("Attempted to create group with duplicate name: {GroupName}", name);
                throw new InvalidOperationException($"Group with name '{name}' already exists");
            }

            // Create entity first
            var entity = new Data.Models.Entity
            {
                EntityType = "Group",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Entities.Add(entity);
            await _dbContext.SaveChangesAsync();

            // Create group
            var group = new Data.Models.Group
            {
                Name = name,
                Description = description,
                EntityId = entity.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Groups.Add(group);
            await _dbContext.SaveChangesAsync();

            // Log audit
            await LogAuditAsync("CreateGroup", "Group", group.Id, createdBy, 
                $"Created group '{name}'");

            _logger.LogInformation("Created group {GroupId} with name {GroupName} by {CreatedBy}", 
                group.Id, name, createdBy);

            return ConvertToDomainGroup(group);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for group creation: {GroupName}", name);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during group creation: {GroupName}", name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating group {GroupName}", name);
            throw new InvalidOperationException($"Failed to create group '{name}': {ex.Message}", ex);
        }
    }

    public async Task<Domain.Group> UpdateGroupAsync(int groupId, string name, string description, string updatedBy)
    {
        try
        {
            if (groupId <= 0)
            {
                throw new ArgumentException("Group ID must be a positive integer", nameof(groupId));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Group name cannot be null or empty", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(updatedBy))
            {
                throw new ArgumentException("UpdatedBy cannot be null or empty", nameof(updatedBy));
            }

            var group = await _dbContext.Groups.FindAsync(groupId);
            if (group == null)
            {
                _logger.LogWarning("Attempted to update non-existent group {GroupId}", groupId);
                throw new InvalidOperationException($"Group {groupId} not found");
            }

            var oldName = group.Name;
            group.Name = name;
            group.Description = description;
            group.UpdatedAt = DateTime.UtcNow;

            _dbContext.Groups.Update(group);
            await _dbContext.SaveChangesAsync();

            // Log audit
            await LogAuditAsync("UpdateGroup", "Group", group.Id, updatedBy,
                $"Updated group from '{oldName}' to '{name}'");

            _logger.LogInformation("Updated group {GroupId} with name {GroupName} by {UpdatedBy}",
                groupId, name, updatedBy);

            return ConvertToDomainGroup(group);
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

            var group = await _dbContext.Groups
                .Include(g => g.Entity)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
            {
                _logger.LogWarning("Attempted to delete non-existent group {GroupId}", groupId);
                throw new InvalidOperationException($"Group {groupId} not found");
            }

            // Check for active dependencies
            var hasActiveDependencies = await HasActiveDependenciesAsync(groupId);
            if (hasActiveDependencies)
            {
                _logger.LogWarning("Cannot delete group {GroupId} due to active dependencies", groupId);
                throw new InvalidOperationException($"Cannot delete group {groupId} as it has active dependencies (child groups, users, or role assignments). Remove dependencies first.");
            }

            var groupName = group.Name;

            // Delete group (cascading will handle relationships)
            _dbContext.Groups.Remove(group);
            if (group.Entity != null)
            {
                _dbContext.Entities.Remove(group.Entity);
            }
            await _dbContext.SaveChangesAsync();

            // Log audit
            await LogAuditAsync("DeleteGroup", "Group", groupId, deletedBy,
                $"Deleted group '{groupName}'");

            _logger.LogInformation("Deleted group {GroupId} by {DeletedBy}", groupId, deletedBy);
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
        var hierarchy = new List<Domain.Group>();
        var visited = new HashSet<int>();
        
        await BuildHierarchyRecursive(groupId, hierarchy, visited);
        
        return hierarchy;
    }

    private async Task BuildHierarchyRecursive(int groupId, List<Domain.Group> hierarchy, HashSet<int> visited)
    {
        if (visited.Contains(groupId))
            return;

        visited.Add(groupId);

        var group = await GetGroupByIdAsync(groupId);
        if (group == null)
            return;

        hierarchy.Add(group);

        // Get child groups
        var childRelations = await _dbContext.GroupHierarchies
            .Where(gh => gh.ParentGroupId == groupId)
            .ToListAsync();

        foreach (var relation in childRelations)
        {
            await BuildHierarchyRecursive(relation.ChildGroupId, hierarchy, visited);
        }
    }

    public async Task<IEnumerable<Domain.Group>> GetChildGroupsAsync(int parentGroupId)
    {
        var childRelations = await _dbContext.GroupHierarchies
            .Include(gh => gh.ChildGroup)
            .Where(gh => gh.ParentGroupId == parentGroupId)
            .ToListAsync();

        var childGroups = new List<Domain.Group>();
        foreach (var relation in childRelations)
        {
            var domainGroup = ConvertToDomainGroup(relation.ChildGroup);
            if (domainGroup != null)
                childGroups.Add(domainGroup);
        }

        return childGroups;
    }

    public async Task<IEnumerable<Domain.Group>> GetParentGroupsAsync(int childGroupId)
    {
        var parentRelations = await _dbContext.GroupHierarchies
            .Include(gh => gh.ParentGroup)
            .Where(gh => gh.ChildGroupId == childGroupId)
            .ToListAsync();

        var parentGroups = new List<Domain.Group>();
        foreach (var relation in parentRelations)
        {
            var domainGroup = ConvertToDomainGroup(relation.ParentGroup);
            if (domainGroup != null)
                parentGroups.Add(domainGroup);
        }

        return parentGroups;
    }

    public async Task AddGroupToGroupAsync(int parentGroupId, int childGroupId, string createdBy)
    {
        // Validate hierarchy to prevent cycles
        if (!await ValidateGroupHierarchyAsync(parentGroupId, childGroupId))
        {
            throw new InvalidOperationException(
                $"Adding group {childGroupId} to group {parentGroupId} would create a cycle");
        }

        await AddGroupToGroupNormalizer.ExecuteAsync(_dbContext, parentGroupId, childGroupId, createdBy);
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("AddGroupToGroup", "GroupHierarchy", 0, createdBy,
            $"Added group {childGroupId} as child of group {parentGroupId}");

        _logger.LogInformation("Added group {ChildGroupId} to parent group {ParentGroupId} by {CreatedBy}",
            childGroupId, parentGroupId, createdBy);
    }

    public async Task RemoveGroupFromGroupAsync(int parentGroupId, int childGroupId, string removedBy)
    {
        var relation = await _dbContext.GroupHierarchies
            .FirstOrDefaultAsync(gh => gh.ParentGroupId == parentGroupId && gh.ChildGroupId == childGroupId);

        if (relation != null)
        {
            _dbContext.GroupHierarchies.Remove(relation);
            await _dbContext.SaveChangesAsync();

            // Log audit
            await LogAuditAsync("RemoveGroupFromGroup", "GroupHierarchy", 0, removedBy,
                $"Removed group {childGroupId} from parent group {parentGroupId}");

            _logger.LogInformation("Removed group {ChildGroupId} from parent group {ParentGroupId} by {RemovedBy}",
                childGroupId, parentGroupId, removedBy);
        }
    }

    public async Task<bool> ValidateGroupHierarchyAsync(int parentGroupId, int childGroupId)
    {
        return !await WouldCreateCycleAsync(parentGroupId, childGroupId);
    }

    public async Task<bool> WouldCreateCycleAsync(int parentGroupId, int childGroupId)
    {
        // Check if parentGroupId is the same as childGroupId
        if (parentGroupId == childGroupId)
            return true;

        // Check if childGroupId is already an ancestor of parentGroupId
        var visited = new HashSet<int>();
        return await IsAncestorOfAsync(childGroupId, parentGroupId, visited);
    }

    private async Task<bool> IsAncestorOfAsync(int potentialAncestorId, int groupId, HashSet<int> visited)
    {
        if (visited.Contains(groupId))
            return false;

        visited.Add(groupId);

        // Get all parent groups of the current group
        var parentRelations = await _dbContext.GroupHierarchies
            .Where(gh => gh.ChildGroupId == groupId)
            .ToListAsync();

        foreach (var relation in parentRelations)
        {
            if (relation.ParentGroupId == potentialAncestorId)
                return true;

            if (await IsAncestorOfAsync(potentialAncestorId, relation.ParentGroupId, visited))
                return true;
        }

        return false;
    }

    #endregion

    #region User Management

    public async Task<IEnumerable<Domain.User>> GetGroupUsersAsync(int groupId, bool includeNested = false)
    {
        var users = new List<Domain.User>();
        var processedGroups = new HashSet<int>();

        await GetGroupUsersRecursive(groupId, users, includeNested, processedGroups);

        return users.DistinctBy(u => u.Id);
    }

    private async Task GetGroupUsersRecursive(int groupId, List<Domain.User> users, 
        bool includeNested, HashSet<int> processedGroups)
    {
        if (processedGroups.Contains(groupId))
            return;

        processedGroups.Add(groupId);

        // Get direct users
        var userGroups = await _dbContext.UserGroups
            .Include(ug => ug.User)
            .Where(ug => ug.GroupId == groupId)
            .ToListAsync();

        foreach (var userGroup in userGroups)
        {
            users.Add(ConvertToDomainUser(userGroup.User));
        }

        // If includeNested, get users from child groups
        if (includeNested)
        {
            var childGroups = await _dbContext.GroupHierarchies
                .Where(gh => gh.ParentGroupId == groupId)
                .ToListAsync();

            foreach (var childGroup in childGroups)
            {
                await GetGroupUsersRecursive(childGroup.ChildGroupId, users, true, processedGroups);
            }
        }
    }

    public async Task AddUserToGroupAsync(int userId, int groupId, string createdBy)
    {
        await AddUserToGroupNormalizer.ExecuteAsync(_dbContext, userId, groupId, createdBy);
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("AddUserToGroup", "UserGroup", 0, createdBy,
            $"Added user {userId} to group {groupId}");

        _logger.LogInformation("Added user {UserId} to group {GroupId} by {CreatedBy}",
            userId, groupId, createdBy);
    }

    public async Task RemoveUserFromGroupAsync(int userId, int groupId, string removedBy)
    {
        await RemoveUserFromGroupNormalizer.ExecuteAsync(_dbContext, userId, groupId, removedBy);
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("RemoveUserFromGroup", "UserGroup", 0, removedBy,
            $"Removed user {userId} from group {groupId}");

        _logger.LogInformation("Removed user {UserId} from group {GroupId} by {RemovedBy}",
            userId, groupId, removedBy);
    }

    public async Task<bool> IsUserInGroupAsync(int userId, int groupId, bool checkNested = true)
    {
        // Check direct membership
        var directMembership = await _dbContext.UserGroups
            .AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId);

        if (directMembership)
            return true;

        if (!checkNested)
            return false;

        // Check nested groups
        var userGroups = await _dbContext.UserGroups
            .Where(ug => ug.UserId == userId)
            .Select(ug => ug.GroupId)
            .ToListAsync();

        foreach (var userGroupId in userGroups)
        {
            if (await IsGroupDescendantOfAsync(userGroupId, groupId))
                return true;
        }

        return false;
    }

    private async Task<bool> IsGroupDescendantOfAsync(int groupId, int ancestorId)
    {
        var visited = new HashSet<int>();
        return await IsGroupDescendantOfRecursive(groupId, ancestorId, visited);
    }

    private async Task<bool> IsGroupDescendantOfRecursive(int groupId, int ancestorId, HashSet<int> visited)
    {
        if (visited.Contains(groupId))
            return false;

        visited.Add(groupId);

        var parentRelations = await _dbContext.GroupHierarchies
            .Where(gh => gh.ChildGroupId == groupId)
            .ToListAsync();

        foreach (var relation in parentRelations)
        {
            if (relation.ParentGroupId == ancestorId)
                return true;

            if (await IsGroupDescendantOfRecursive(relation.ParentGroupId, ancestorId, visited))
                return true;
        }

        return false;
    }

    #endregion

    #region Role Management

    public async Task<IEnumerable<Domain.Role>> GetGroupRolesAsync(int groupId, bool includeInherited = false)
    {
        var roles = new List<Domain.Role>();
        var processedGroups = new HashSet<int>();

        await GetGroupRolesRecursive(groupId, roles, includeInherited, processedGroups);

        return roles.DistinctBy(r => r.Id);
    }

    private async Task GetGroupRolesRecursive(int groupId, List<Domain.Role> roles,
        bool includeInherited, HashSet<int> processedGroups)
    {
        if (processedGroups.Contains(groupId))
            return;

        processedGroups.Add(groupId);

        // Get direct roles
        var groupRoles = await _dbContext.GroupRoles
            .Include(gr => gr.Role)
            .Where(gr => gr.GroupId == groupId)
            .ToListAsync();

        foreach (var groupRole in groupRoles)
        {
            roles.Add(ConvertToDomainRole(groupRole.Role));
        }

        // If includeInherited, get roles from parent groups
        if (includeInherited)
        {
            var parentGroups = await _dbContext.GroupHierarchies
                .Where(gh => gh.ChildGroupId == groupId)
                .ToListAsync();

            foreach (var parentGroup in parentGroups)
            {
                await GetGroupRolesRecursive(parentGroup.ParentGroupId, roles, true, processedGroups);
            }
        }
    }

    public async Task AddRoleToGroupAsync(int roleId, int groupId, string createdBy)
    {
        await AddRoleToGroupNormalizer.ExecuteAsync(_dbContext, roleId, groupId, createdBy);
        await _dbContext.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("AddRoleToGroup", "GroupRole", 0, createdBy,
            $"Added role {roleId} to group {groupId}");

        _logger.LogInformation("Added role {RoleId} to group {GroupId} by {CreatedBy}",
            roleId, groupId, createdBy);
    }

    public async Task RemoveRoleFromGroupAsync(int roleId, int groupId, string removedBy)
    {
        var relation = await _dbContext.GroupRoles
            .FirstOrDefaultAsync(gr => gr.RoleId == roleId && gr.GroupId == groupId);

        if (relation != null)
        {
            _dbContext.GroupRoles.Remove(relation);
            await _dbContext.SaveChangesAsync();

            // Log audit
            await LogAuditAsync("RemoveRoleFromGroup", "GroupRole", 0, removedBy,
                $"Removed role {roleId} from group {groupId}");

            _logger.LogInformation("Removed role {RoleId} from group {GroupId} by {RemovedBy}",
                roleId, groupId, removedBy);
        }
    }

    #endregion

    #region Permission Evaluation

    public async Task<IEnumerable<Domain.Permission>> GetGroupPermissionsAsync(int groupId, bool includeInherited = true)
    {
        return await _permissionService.GetEntityPermissionsAsync(groupId, includeInherited);
    }

    public async Task<bool> HasPermissionAsync(int groupId, string resource, string action)
    {
        return await _permissionService.HasPermissionAsync(groupId, resource, action);
    }

    #endregion

    #region Bulk Operations

    public async Task<IEnumerable<Domain.Group>> CreateGroupsBulkAsync(
        IEnumerable<(string Name, string Description)> groups, string createdBy)
    {
        var createdGroups = new List<Domain.Group>();

        foreach (var (name, description) in groups)
        {
            var group = await CreateGroupAsync(name, description, createdBy);
            createdGroups.Add(group);
        }

        _logger.LogInformation("Created {Count} groups in bulk by {CreatedBy}",
            createdGroups.Count, createdBy);

        return createdGroups;
    }

    public async Task AddUsersToGroupBulkAsync(int groupId, IEnumerable<int> userIds, string createdBy)
    {
        foreach (var userId in userIds)
        {
            await AddUserToGroupAsync(userId, groupId, createdBy);
        }

        _logger.LogInformation("Added {Count} users to group {GroupId} in bulk by {CreatedBy}",
            userIds.Count(), groupId, createdBy);
    }

    public async Task AddGroupsToGroupBulkAsync(int parentGroupId, IEnumerable<int> childGroupIds, string createdBy)
    {
        foreach (var childGroupId in childGroupIds)
        {
            await AddGroupToGroupAsync(parentGroupId, childGroupId, createdBy);
        }

        _logger.LogInformation("Added {Count} child groups to parent group {ParentGroupId} in bulk by {CreatedBy}",
            childGroupIds.Count(), parentGroupId, createdBy);
    }

    #endregion

    #region Search and Filtering

    public async Task<IEnumerable<Domain.Group>> SearchGroupsAsync(string searchTerm)
    {
        var groups = await _dbContext.Groups
            .Where(g => g.Name.Contains(searchTerm) || 
                       (g.Description != null && g.Description.Contains(searchTerm)))
            .ToListAsync();

        return groups.Select(ConvertToDomainGroup);
    }

    public async Task<IEnumerable<Domain.Group>> GetGroupsByUserAsync(int userId)
    {
        var groups = await _dbContext.UserGroups
            .Include(ug => ug.Group)
            .Where(ug => ug.UserId == userId)
            .Select(ug => ug.Group)
            .ToListAsync();

        return groups.Select(ConvertToDomainGroup);
    }

    public async Task<IEnumerable<Domain.Group>> GetGroupsByRoleAsync(int roleId)
    {
        var groups = await _dbContext.GroupRoles
            .Include(gr => gr.Group)
            .Where(gr => gr.RoleId == roleId)
            .Select(gr => gr.Group)
            .ToListAsync();

        return groups.Select(ConvertToDomainGroup);
    }

    #endregion

    #region Helper Methods

    private Domain.Group ConvertToDomainGroup(Data.Models.Group dataGroup)
    {
        var domainGroup = new Domain.Group
        {
            Id = dataGroup.Id,
            Name = dataGroup.Name
        };

        // Note: Additional relationship mapping would be done here if needed
        return domainGroup;
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

    private Domain.Role ConvertToDomainRole(Data.Models.Role dataRole)
    {
        var domainRole = new Domain.Role
        {
            Id = dataRole.Id,
            Name = dataRole.Name
        };

        return domainRole;
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
    
    private async Task<bool> HasActiveDependenciesAsync(int groupId)
    {
        try
        {
            // Check if group has child groups
            var hasChildGroups = await _dbContext.GroupHierarchies
                .AnyAsync(gh => gh.ParentGroupId == groupId);
            
            // Check if group has users
            var hasUsers = await _dbContext.UserGroups
                .AnyAsync(ug => ug.GroupId == groupId);
            
            // Check if group has role assignments
            var hasRoles = await _dbContext.GroupRoles
                .AnyAsync(gr => gr.GroupId == groupId);
            
            return hasChildGroups || hasUsers || hasRoles;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking dependencies for group {GroupId}", groupId);
            // Return true to be safe and prevent deletion if we can't verify
            return true;
        }
    }

    #endregion
}