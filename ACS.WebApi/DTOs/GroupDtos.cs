namespace ACS.WebApi.DTOs;

public record CreateGroupRequest(string Name, int? ParentGroupId = null);

public record UpdateGroupRequest(string Name, int? ParentGroupId = null);

public record GroupResponse(
    int Id,
    string Name,
    int? ParentGroupId,
    string? ParentGroupName,
    List<GroupResponse> ChildGroups,
    List<UserResponse> Users,
    List<RoleResponse> Roles,
    List<PermissionResponse> Permissions,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record AddGroupToGroupRequest(int ChildGroupId, int ParentGroupId);

public record AddRoleToGroupRequest(int GroupId, int RoleId);

public record GroupListResponse(
    List<GroupResponse> Groups,
    int TotalCount,
    int Page,
    int PageSize
);