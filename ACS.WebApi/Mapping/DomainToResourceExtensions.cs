using ACS.Service.Domain;
using ACS.WebApi.Resources;

namespace ACS.WebApi.Mapping;

/// <summary>
/// Extension methods for mapping domain objects to REST resources
/// Clean, composable mappings without AutoMapper dependencies
/// </summary>
public static class DomainToResourceExtensions
{
    /// <summary>
    /// Maps a domain User to UserResource
    /// </summary>
    public static UserResource ToResource(this User user)
    {
        return new UserResource
        {
            Id = user.Id,
            Name = user.Name,
            CreatedAt = DateTime.UtcNow, // TODO: Get from domain when available
            UpdatedAt = null, // TODO: Get from domain when available
            CreatedBy = "system", // TODO: Get from domain when available
            UpdatedBy = null,
            Groups = user.GroupMemberships.ToResources(),
            Roles = user.RoleMemberships.ToResources(),
            Permissions = new List<PermissionResource>() // TODO: Get computed permissions
        };
    }

    /// <summary>
    /// Maps a domain Group to GroupResource
    /// </summary>
    public static GroupResource ToResource(this Group group)
    {
        return new GroupResource
        {
            Id = group.Id,
            Name = group.Name,
            Description = null, // TODO: Add to domain when needed
            CreatedAt = DateTime.UtcNow, // TODO: Get from domain when available
            UpdatedAt = null, // TODO: Get from domain when available
            CreatedBy = "system", // TODO: Get from domain when available
            UpdatedBy = null,
            Users = group.Users.ToResources(),
            Roles = group.Children.OfType<Role>().ToResources(),
            ChildGroups = group.Children.OfType<Group>().ToResources(),
            ParentGroups = group.Parents.OfType<Group>().ToResources(),
            Permissions = new List<PermissionResource>() // TODO: Get computed permissions
        };
    }

    /// <summary>
    /// Maps a domain Role to RoleResource
    /// </summary>
    public static RoleResource ToResource(this Role role)
    {
        var criticalRoles = new[] { "Administrator", "Admin", "SystemAdmin", "Root" };
        
        return new RoleResource
        {
            Id = role.Id,
            Name = role.Name,
            Description = null, // TODO: Add to domain when needed
            CreatedAt = DateTime.UtcNow, // TODO: Get from domain when available
            UpdatedAt = null, // TODO: Get from domain when available
            CreatedBy = "system", // TODO: Get from domain when available
            UpdatedBy = null,
            IsCriticalRole = criticalRoles.Contains(role.Name, StringComparer.OrdinalIgnoreCase),
            Users = role.Children.OfType<User>().ToResources(),
            Groups = role.Parents.OfType<Group>().ToResources(),
            Permissions = new List<PermissionResource>() // TODO: Get computed permissions
        };
    }

    /// <summary>
    /// Maps collection of Users to collection of UserResources
    /// LINQ-style composable collection mapper
    /// </summary>
    public static ICollection<UserResource> ToResources(this IEnumerable<User> users)
    {
        return users.Select(u => u.ToResource()).ToList();
    }

    /// <summary>
    /// Maps collection of Groups to collection of GroupResources
    /// LINQ-style composable collection mapper
    /// </summary>
    public static ICollection<GroupResource> ToResources(this IEnumerable<Group> groups)
    {
        return groups.Select(g => g.ToResource()).ToList();
    }

    /// <summary>
    /// Maps collection of Roles to collection of RoleResources
    /// LINQ-style composable collection mapper
    /// </summary>
    public static ICollection<RoleResource> ToResources(this IEnumerable<Role> roles)
    {
        return roles.Select(r => r.ToResource()).ToList();
    }

    /// <summary>
    /// Maps collection of Users to paginated UserCollectionResource
    /// Composable with LINQ operations for filtering/sorting
    /// </summary>
    public static UserCollectionResource ToCollectionResource(
        this IEnumerable<User> users, 
        int totalCount, 
        int page, 
        int pageSize)
    {
        return new UserCollectionResource
        {
            Users = users.ToResources(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Maps collection of Groups to paginated GroupCollectionResource
    /// Composable with LINQ operations for filtering/sorting
    /// </summary>
    public static GroupCollectionResource ToCollectionResource(
        this IEnumerable<Group> groups, 
        int totalCount, 
        int page, 
        int pageSize)
    {
        return new GroupCollectionResource
        {
            Groups = groups.ToResources(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Maps collection of Roles to paginated RoleCollectionResource
    /// Composable with LINQ operations for filtering/sorting
    /// </summary>
    public static RoleCollectionResource ToCollectionResource(
        this IEnumerable<Role> roles, 
        int totalCount, 
        int page, 
        int pageSize)
    {
        return new RoleCollectionResource
        {
            Roles = roles.ToResources(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Generic collection mapper for any type
    /// Highly composable with LINQ operations
    /// </summary>
    public static CollectionResource<T> ToCollectionResource<T>(
        this IEnumerable<T> items,
        int totalCount,
        int page,
        int pageSize)
    {
        return new CollectionResource<T>
        {
            Items = items.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Creates API response wrapper with success status
    /// </summary>
    public static ApiResponse<T> ToApiResponse<T>(this T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates API response wrapper for errors
    /// </summary>
    public static ApiResponse<T> ToErrorResponse<T>(this IEnumerable<string> errors, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Message = message,
            Errors = errors.ToList(),
            Timestamp = DateTime.UtcNow
        };
    }
}