using ACS.Service.Domain;

namespace ACS.Service.Requests;

/// <summary>
/// Service request for retrieving a single role
/// </summary>
public record GetRoleRequest
{
    public int RoleId { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for retrieving multiple roles with pagination
/// </summary>
public record GetRolesRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public bool? IsCriticalRole { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for creating a new role
/// </summary>
public record CreateRoleRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? GroupId { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for updating an existing role
/// </summary>
public record UpdateRoleRequest
{
    public int RoleId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for patching role fields
/// </summary>
public record PatchRoleRequest
{
    public int RoleId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for deleting a role
/// </summary>
public record DeleteRoleRequest
{
    public int RoleId { get; init; }
    public string DeletedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for assigning role to user
/// </summary>
public record AssignRoleToUserServiceRequest
{
    public int RoleId { get; init; }
    public int UserId { get; init; }
    public string AssignedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for unassigning role from user
/// </summary>
public record UnassignRoleFromUserServiceRequest
{
    public int RoleId { get; init; }
    public int UserId { get; init; }
    public string UnassignedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for adding role to group
/// </summary>
public record AddRoleToGroupServiceRequest
{
    public int RoleId { get; init; }
    public int GroupId { get; init; }
    public string AddedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for removing role from group
/// </summary>
public record RemoveRoleFromGroupServiceRequest
{
    public int RoleId { get; init; }
    public int GroupId { get; init; }
    public string RemovedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for bulk role operations
/// </summary>
public record BulkRoleRequest<T>
{
    public ICollection<T> Items { get; init; } = new List<T>();
    public string Operation { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}