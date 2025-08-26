using ACS.WebApi.Resources;
using ACS.Service.Responses;

namespace ACS.WebApi.Mapping;

/// <summary>
/// Extension methods for mapping service response objects to REST resources
/// Clean, composable mappings with LINQ-style collection mappers
/// </summary>
public static class ServiceToResourceExtensions
{
    /// <summary>
    /// Maps UserResponse to UserResource
    /// </summary>
    public static UserResource? ToResource(this UserResponse response)
    {
        return response.User?.ToResource();
    }

    /// <summary>
    /// Maps UsersResponse to UserCollectionResource with pagination
    /// </summary>
    public static UserCollectionResource ToCollectionResource(this UsersResponse response)
    {
        return new UserCollectionResource
        {
            Users = response.Users.ToResources(),
            TotalCount = response.TotalCount,
            Page = response.Page,
            PageSize = response.PageSize
        };
    }

    /// <summary>
    /// Maps CreateUserResponse to UserResource
    /// </summary>
    public static UserResource? ToResource(this CreateUserResponse response)
    {
        return response.User?.ToResource();
    }

    /// <summary>
    /// Maps UpdateUserResponse to UserResource
    /// </summary>
    public static UserResource? ToResource(this UpdateUserResponse response)
    {
        return response.User?.ToResource();
    }

    /// <summary>
    /// Maps GroupResponse to GroupResource
    /// </summary>
    public static GroupResource? ToResource(this GroupResponse response)
    {
        return response.Group?.ToResource();
    }

    /// <summary>
    /// Maps GroupsResponse to GroupCollectionResource with pagination
    /// </summary>
    public static GroupCollectionResource ToCollectionResource(this GroupsResponse response)
    {
        return new GroupCollectionResource
        {
            Groups = response.Groups.ToResources(),
            TotalCount = response.TotalCount,
            Page = response.Page,
            PageSize = response.PageSize
        };
    }

    /// <summary>
    /// Maps CreateGroupResponse to GroupResource
    /// </summary>
    public static GroupResource? ToResource(this CreateGroupResponse response)
    {
        return response.Group?.ToResource();
    }

    /// <summary>
    /// Maps UpdateGroupResponse to GroupResource
    /// </summary>
    public static GroupResource? ToResource(this UpdateGroupResponse response)
    {
        return response.Group?.ToResource();
    }

    /// <summary>
    /// Maps RoleResponse to RoleResource
    /// </summary>
    public static RoleResource? ToResource(this RoleResponse response)
    {
        return response.Role?.ToResource();
    }

    /// <summary>
    /// Maps RolesResponse to RoleCollectionResource with pagination
    /// </summary>
    public static RoleCollectionResource ToCollectionResource(this RolesResponse response)
    {
        return new RoleCollectionResource
        {
            Roles = response.Roles.ToResources(),
            TotalCount = response.TotalCount,
            Page = response.Page,
            PageSize = response.PageSize
        };
    }

    /// <summary>
    /// Maps CreateRoleResponse to RoleResource
    /// </summary>
    public static RoleResource? ToResource(this CreateRoleResponse response)
    {
        return response.Role?.ToResource();
    }

    /// <summary>
    /// Maps UpdateRoleResponse to RoleResource
    /// </summary>
    public static RoleResource? ToResource(this UpdateRoleResponse response)
    {
        return response.Role?.ToResource();
    }

    /// <summary>
    /// Maps PermissionResponse to PermissionResource
    /// </summary>
    public static PermissionResource? ToResource(this PermissionResponse response)
    {
        return response.Permission?.ToResource();
    }

    /// <summary>
    /// Maps PermissionsResponse to PermissionCollectionResource with pagination
    /// </summary>
    public static PermissionCollectionResource ToCollectionResource(this PermissionsResponse response)
    {
        return new PermissionCollectionResource
        {
            Permissions = response.Permissions.ToResources(),
            TotalCount = response.TotalCount,
            Page = response.Page,
            PageSize = response.PageSize
        };
    }

    /// <summary>
    /// Maps CreatePermissionResponse to PermissionResource
    /// </summary>
    public static PermissionResource? ToResource(this CreatePermissionResponse response)
    {
        return response.Permission?.ToResource();
    }

    /// <summary>
    /// Maps UpdatePermissionResponse to PermissionResource
    /// </summary>
    public static PermissionResource? ToResource(this UpdatePermissionResponse response)
    {
        return response.Permission?.ToResource();
    }

    /// <summary>
    /// Maps PermissionCheckResponse to PermissionCheckResource
    /// </summary>
    public static PermissionCheckResource ToResource(this PermissionCheckResponse response, int entityId, string entityType, string resource, string action, string? scope = null)
    {
        return new PermissionCheckResource
        {
            EntityId = entityId,
            EntityType = entityType,
            Resource = resource,
            Action = action,
            Scope = scope,
            HasPermission = response.HasPermission,
            Reason = response.Reason
        };
    }

    /// <summary>
    /// Maps EntityPermissionsResponse to collection of PermissionResources
    /// LINQ-style composable collection mapper
    /// </summary>
    public static ICollection<PermissionResource> ToDirectPermissionResources(this EntityPermissionsResponse response)
    {
        return response.DirectPermissions.ToResources();
    }

    /// <summary>
    /// Maps EntityPermissionsResponse to collection of inherited PermissionResources
    /// LINQ-style composable collection mapper
    /// </summary>
    public static ICollection<PermissionResource> ToInheritedPermissionResources(this EntityPermissionsResponse response)
    {
        return response.InheritedPermissions.ToResources();
    }

    /// <summary>
    /// Maps EntityPermissionsResponse to collection of all PermissionResources
    /// LINQ-style composable collection mapper
    /// </summary>
    public static ICollection<PermissionResource> ToAllPermissionResources(this EntityPermissionsResponse response)
    {
        return response.AllPermissions.ToResources();
    }

    /// <summary>
    /// Maps service response to ApiResponse wrapper
    /// </summary>
    public static ApiResponse<T> ToApiResponse<T>(this T data, bool success = true, string? message = null, ICollection<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = success,
            Data = data,
            Message = message,
            Errors = errors ?? new List<string>(),
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps service response errors to ApiResponse wrapper
    /// </summary>
    public static ApiResponse<T> ToErrorApiResponse<T>(this ICollection<string> errors, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Message = message,
            Errors = errors,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps service response errors to ApiResponse wrapper (IList overload)
    /// </summary>
    public static ApiResponse<T> ToErrorApiResponse<T>(this IList<string> errors, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Message = message,
            Errors = errors,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps service response errors to ApiResponse wrapper (array overload)
    /// </summary>
    public static ApiResponse<T> ToErrorApiResponse<T>(this string[] errors, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Message = message,
            Errors = errors,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps service response errors to ApiResponse wrapper (IEnumerable overload)
    /// </summary>
    public static ApiResponse<T> ToErrorApiResponse<T>(this IEnumerable<string> errors, string? message = null)
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

    /// <summary>
    /// Maps BulkUserResponse to BulkOperationResultResource
    /// LINQ-style composable collection mapper
    /// </summary>
    public static BulkOperationResultResource<T> ToBulkResultResource<T>(
        this BulkUserResponse<T> response,
        Func<T, object> successMapper)
    {
        return new BulkOperationResultResource<T>
        {
            TotalRequested = response.TotalRequested,
            SuccessCount = response.SuccessCount,
            FailureCount = response.FailureCount,
            SuccessfulItems = response.SuccessfulItems.Select(successMapper).Cast<T>().ToList(),
            Errors = response.Errors.Select(e => new BulkOperationErrorResource
            {
                ItemIndex = e.ItemIndex,
                Error = e.Error,
                Details = e.Details
            }).ToList()
        };
    }

    /// <summary>
    /// Maps BulkGroupResponse to BulkOperationResultResource
    /// LINQ-style composable collection mapper
    /// </summary>
    public static BulkOperationResultResource<T> ToBulkResultResource<T>(
        this BulkGroupResponse<T> response,
        Func<T, object> successMapper)
    {
        return new BulkOperationResultResource<T>
        {
            TotalRequested = response.TotalRequested,
            SuccessCount = response.SuccessCount,
            FailureCount = response.FailureCount,
            SuccessfulItems = response.SuccessfulItems.Select(successMapper).Cast<T>().ToList(),
            Errors = response.Errors.Select(e => new BulkOperationErrorResource
            {
                ItemIndex = e.ItemIndex,
                Error = e.Error,
                Details = e.Details
            }).ToList()
        };
    }

    /// <summary>
    /// Maps BulkRoleResponse to BulkOperationResultResource
    /// LINQ-style composable collection mapper
    /// </summary>
    public static BulkOperationResultResource<T> ToBulkResultResource<T>(
        this BulkRoleResponse<T> response,
        Func<T, object> successMapper)
    {
        return new BulkOperationResultResource<T>
        {
            TotalRequested = response.TotalRequested,
            SuccessCount = response.SuccessCount,
            FailureCount = response.FailureCount,
            SuccessfulItems = response.SuccessfulItems.Select(successMapper).Cast<T>().ToList(),
            Errors = response.Errors.Select(e => new BulkOperationErrorResource
            {
                ItemIndex = e.ItemIndex,
                Error = e.Error,
                Details = e.Details
            }).ToList()
        };
    }

    /// <summary>
    /// Generic collection mapper with LINQ composability
    /// Highly composable with LINQ operations
    /// </summary>
    public static CollectionResource<TResource> ToCollectionResource<TService, TResource>(
        this IEnumerable<TService> serviceItems,
        Func<TService, TResource> mapper,
        int totalCount,
        int page,
        int pageSize)
    {
        return new CollectionResource<TResource>
        {
            Items = serviceItems.Select(mapper).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}

/// <summary>
/// Extension method for domain Permission to PermissionResource mapping
/// </summary>
public static class PermissionMappingExtensions
{
    public static PermissionResource ToResource(this ACS.Service.Domain.Permission permission)
    {
        return new PermissionResource
        {
            Id = permission.Id,
            Resource = permission.Resource,
            Action = permission.Action,
            Scope = permission.Scope,
            CreatedAt = DateTime.UtcNow, // TODO: Get from domain when available
            CreatedBy = "system" // TODO: Get from domain when available
        };
    }

    public static ICollection<PermissionResource> ToResources(this IEnumerable<ACS.Service.Domain.Permission> permissions)
    {
        return permissions.Select(p => p.ToResource()).ToList();
    }
}