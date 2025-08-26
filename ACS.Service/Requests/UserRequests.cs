using ACS.Service.Domain;

namespace ACS.Service.Requests;

/// <summary>
/// Service request for retrieving a single user
/// </summary>
public record GetUserRequest
{
    public int UserId { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for retrieving multiple users with pagination
/// </summary>
public record GetUsersRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for creating a new user
/// </summary>
public record CreateUserRequest
{
    public string Name { get; init; } = string.Empty;
    public int? InitialGroupId { get; init; }
    public int? InitialRoleId { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for updating an existing user
/// </summary>
public record UpdateUserRequest
{
    public int UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for patching user fields
/// </summary>
public record PatchUserRequest
{
    public int UserId { get; init; }
    public string? Name { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for deleting a user
/// </summary>
public record DeleteUserRequest
{
    public int UserId { get; init; }
    public string DeletedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for adding user to group
/// </summary>
public record AddUserToGroupRequest
{
    public int UserId { get; init; }
    public int GroupId { get; init; }
    public string AddedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for removing user from group
/// </summary>
public record RemoveUserFromGroupRequest
{
    public int UserId { get; init; }
    public int GroupId { get; init; }
    public string RemovedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for assigning user to role
/// </summary>
public record AssignUserToRoleRequest
{
    public int UserId { get; init; }
    public int RoleId { get; init; }
    public string AssignedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for unassigning user from role
/// </summary>
public record UnassignUserFromRoleRequest
{
    public int UserId { get; init; }
    public int RoleId { get; init; }
    public string UnassignedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for bulk user operations
/// </summary>
public record BulkUserRequest<T>
{
    public ICollection<T> Items { get; init; } = new List<T>();
    public string Operation { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}