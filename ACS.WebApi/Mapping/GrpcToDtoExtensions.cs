using ACS.WebApi.DTOs;
using ACS.WebApi.Resources;

namespace ACS.WebApi.Mapping;

/// <summary>
/// Extension methods for converting between gRPC DTOs and Web API Resources
/// </summary>
public static class GrpcToDtoExtensions
{
    /// <summary>
    /// Convert UserListResponse to UserCollectionResource
    /// </summary>
    public static UserCollectionResource ToCollectionResource(this UserListResponse response)
    {
        return new UserCollectionResource
        {
            Users = response.Users.Select(u => u.ToResource()).ToList(),
            TotalCount = response.TotalCount,
            Page = response.Page,
            PageSize = response.PageSize
        };
    }

    /// <summary>
    /// Convert UserResponse to UserResource
    /// </summary>
    public static UserResource ToResource(this UserResponse response)
    {
        return new UserResource
        {
            Id = response.Id,
            Name = response.Name,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt,
            CreatedBy = "", // TODO: Add tracking fields
            UpdatedBy = "", // TODO: Add tracking fields
            Groups = new List<GroupResource>(),
            Roles = new List<RoleResource>(),
            Permissions = response.Permissions.Select(p => p.ToResource()).ToList()
        };
    }

    /// <summary>
    /// Convert PermissionResponse to PermissionResource
    /// </summary>
    public static PermissionResource ToResource(this PermissionResponse response)
    {
        return new PermissionResource
        {
            Id = 0, // TODO: Add permission ID tracking
            Resource = response.Uri ?? string.Empty,
            Action = response.HttpVerb ?? string.Empty,
            Scope = response.Scheme,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System" // TODO: Get actual user context
        };
    }

    /// <summary>
    /// Convert QueryResource to simple pagination and search parameters
    /// </summary>
    public static (int page, int pageSize, string? search, string? sortBy, bool sortDescending) ToQueryParameters(this QueryResource query)
    {
        var page = query.Pagination?.ValidPage ?? 1;
        var pageSize = query.Pagination?.ValidPageSize ?? 20;
        var search = query.Search;
        
        var firstSort = query.Sort?.FirstOrDefault();
        var sortBy = firstSort?.Field;
        var sortDescending = firstSort?.Direction == SortDirection.Descending;
        
        return (page, pageSize, search, sortBy, sortDescending);
    }

    /// <summary>
    /// Convert from URL query parameters to QueryResource
    /// </summary>
    public static QueryResource FromQueryParameters(int? page, int? pageSize, string? search, string? sortBy, string? sortDirection)
    {
        return new QueryResource
        {
            Pagination = new PaginationResource
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20
            },
            Search = search,
            Sort = string.IsNullOrEmpty(sortBy) ? new List<SortResource>() : new List<SortResource>
            {
                new SortResource
                {
                    Field = sortBy,
                    Direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) 
                        ? SortDirection.Descending 
                        : SortDirection.Ascending
                }
            }
        };
    }
}