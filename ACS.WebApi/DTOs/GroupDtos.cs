namespace ACS.WebApi.DTOs;

/// <summary>
/// DTO for group list response from gRPC service
/// </summary>
public record GroupListResponse(
    IList<GroupResponse> Groups,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>
/// DTO for individual group response from gRPC service
/// </summary>
public record GroupResponse(
    int Id,
    string Name,
    int? ParentGroupId,
    string? ParentGroupName,
    IList<GroupResponse> ChildGroups,
    IList<UserResponse> Users,
    IList<RoleResponse> Roles,
    IList<PermissionResponse> Permissions,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// DTO for adding group to group
/// </summary>
public record AddGroupToGroupRequest
{
    public int ParentGroupId { get; init; }
    public int ChildGroupId { get; init; }
}

/// <summary>
/// DTO for adding role to group
/// </summary>
public record AddRoleToGroupRequest
{
    public int GroupId { get; init; }
    public int RoleId { get; init; }
}