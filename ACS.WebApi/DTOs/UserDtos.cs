namespace ACS.WebApi.DTOs;

/// <summary>
/// DTO for user list response from gRPC service
/// </summary>
public record UserListResponse(
    IList<UserResponse> Users,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>
/// DTO for individual user response from gRPC service
/// </summary>
public record UserResponse(
    int Id,
    string Name,
    int? GroupId,
    string? GroupName,
    int? RoleId,
    string? RoleName,
    IList<PermissionResponse> Permissions,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// DTO for adding user to group
/// </summary>
public record AddUserToGroupRequest
{
    public int UserId { get; init; }
    public int GroupId { get; init; }
}

/// <summary>
/// DTO for assigning user to role
/// </summary>
public record AssignUserToRoleRequest
{
    public int UserId { get; init; }
    public int RoleId { get; init; }
}

/// <summary>
/// DTO for permission check request
/// </summary>
public record CheckPermissionRequest
{
    public int EntityId { get; init; }
    public string Uri { get; init; } = string.Empty;
    public string HttpVerb { get; init; } = string.Empty;
}

/// <summary>
/// DTO for permission check response
/// </summary>
public record CheckPermissionResponse(
    bool HasPermission,
    string Uri,
    string HttpVerb,
    int EntityId,
    string EntityType,
    string Reason
);