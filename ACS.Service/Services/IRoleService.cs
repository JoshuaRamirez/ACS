using ACS.Service.Domain;

namespace ACS.Service.Services;

/// <summary>
/// Service for managing roles and role-based permissions
/// </summary>
public interface IRoleService
{
    // Basic CRUD operations
    Task<IEnumerable<Role>> GetAllRolesAsync();
    Task<Role?> GetRoleByIdAsync(int roleId);
    Task<Role> CreateRoleAsync(string name, string description, string createdBy);
    Task<Role> UpdateRoleAsync(int roleId, string name, string description, string updatedBy);
    Task DeleteRoleAsync(int roleId, string deletedBy);
    
    // User assignment
    Task<IEnumerable<User>> GetRoleUsersAsync(int roleId);
    Task AssignUserToRoleAsync(int userId, int roleId, string assignedBy);
    Task UnassignUserFromRoleAsync(int userId, int roleId, string unassignedBy);
    Task<bool> IsUserInRoleAsync(int userId, int roleId);
    Task<IEnumerable<Role>> GetUserRolesAsync(int userId, bool includeGroupRoles = true);
    
    // Group assignment
    Task<IEnumerable<Group>> GetRoleGroupsAsync(int roleId);
    Task AssignRoleToGroupAsync(int roleId, int groupId, string assignedBy);
    Task UnassignRoleFromGroupAsync(int roleId, int groupId, string unassignedBy);
    Task<bool> IsRoleInGroupAsync(int roleId, int groupId);
    
    // Permission management
    Task<IEnumerable<Permission>> GetRolePermissionsAsync(int roleId);
    Task AddPermissionToRoleAsync(int roleId, Permission permission, string addedBy);
    Task RemovePermissionFromRoleAsync(int roleId, string resource, string action, string removedBy);
    Task<bool> RoleHasPermissionAsync(int roleId, string resource, string action);
    
    // Role hierarchy (if implementing role inheritance)
    Task<IEnumerable<Role>> GetChildRolesAsync(int parentRoleId);
    Task<IEnumerable<Role>> GetParentRolesAsync(int childRoleId);
    Task AddRoleHierarchyAsync(int parentRoleId, int childRoleId, string createdBy);
    Task RemoveRoleHierarchyAsync(int parentRoleId, int childRoleId, string removedBy);
    
    // Bulk operations
    Task<IEnumerable<Role>> CreateRolesBulkAsync(IEnumerable<(string Name, string Description)> roles, string createdBy);
    Task AssignUsersToRoleBulkAsync(int roleId, IEnumerable<int> userIds, string assignedBy);
    Task AddPermissionsToRoleBulkAsync(int roleId, IEnumerable<Permission> permissions, string addedBy);
    
    // Search and filtering
    Task<IEnumerable<Role>> SearchRolesAsync(string searchTerm);
    Task<IEnumerable<Role>> GetRolesByPermissionAsync(string resource, string action);
    Task<IEnumerable<Role>> GetRolesByGroupAsync(int groupId);
    
    // Role templates and cloning
    Task<Role> CloneRoleAsync(int sourceRoleId, string newRoleName, string clonedBy);
    Task<Role> CreateRoleFromTemplateAsync(string templateName, string roleName, string createdBy);
}