using ACS.Service.Domain;

namespace ACS.Service.Services;

/// <summary>
/// Service for managing groups and group hierarchies with cycle prevention
/// </summary>
public interface IGroupService
{
    // Basic CRUD operations
    Task<IEnumerable<Group>> GetAllGroupsAsync();
    Task<Group?> GetGroupByIdAsync(int groupId);
    Task<Group> CreateGroupAsync(string name, string description, string createdBy);
    Task<Group> UpdateGroupAsync(int groupId, string name, string description, string updatedBy);
    Task DeleteGroupAsync(int groupId, string deletedBy);
    
    // Hierarchy management
    Task<IEnumerable<Group>> GetGroupHierarchyAsync(int groupId);
    Task<IEnumerable<Group>> GetChildGroupsAsync(int parentGroupId);
    Task<IEnumerable<Group>> GetParentGroupsAsync(int childGroupId);
    Task AddGroupToGroupAsync(int parentGroupId, int childGroupId, string createdBy);
    Task RemoveGroupFromGroupAsync(int parentGroupId, int childGroupId, string removedBy);
    Task<bool> ValidateGroupHierarchyAsync(int parentGroupId, int childGroupId);
    Task<bool> WouldCreateCycleAsync(int parentGroupId, int childGroupId);
    
    // User management
    Task<IEnumerable<User>> GetGroupUsersAsync(int groupId, bool includeNested = false);
    Task AddUserToGroupAsync(int userId, int groupId, string createdBy);
    Task RemoveUserFromGroupAsync(int userId, int groupId, string removedBy);
    Task<bool> IsUserInGroupAsync(int userId, int groupId, bool checkNested = true);
    
    // Role management
    Task<IEnumerable<Role>> GetGroupRolesAsync(int groupId, bool includeInherited = false);
    Task AddRoleToGroupAsync(int roleId, int groupId, string createdBy);
    Task RemoveRoleFromGroupAsync(int roleId, int groupId, string removedBy);
    
    // Permission evaluation
    Task<IEnumerable<Permission>> GetGroupPermissionsAsync(int groupId, bool includeInherited = true);
    Task<bool> HasPermissionAsync(int groupId, string resource, string action);
    
    // Bulk operations
    Task<IEnumerable<Group>> CreateGroupsBulkAsync(IEnumerable<(string Name, string Description)> groups, string createdBy);
    Task AddUsersToGroupBulkAsync(int groupId, IEnumerable<int> userIds, string createdBy);
    Task AddGroupsToGroupBulkAsync(int parentGroupId, IEnumerable<int> childGroupIds, string createdBy);
    
    // Search and filtering
    Task<IEnumerable<Group>> SearchGroupsAsync(string searchTerm);
    Task<IEnumerable<Group>> GetGroupsByUserAsync(int userId);
    Task<IEnumerable<Group>> GetGroupsByRoleAsync(int roleId);
}