namespace ACS.WebApi.DTOs;

public record CreateUserRequest(string Name, int? GroupId = null, int? RoleId = null);

public record UpdateUserRequest(string Name, int? GroupId = null, int? RoleId = null);

public record UserResponse(
    int Id,
    string Name,
    int? GroupId,
    string? GroupName,
    int? RoleId,
    string? RoleName,
    List<PermissionResponse> Permissions,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record AddUserToGroupRequest(int UserId, int GroupId);

public record AssignUserToRoleRequest(int UserId, int RoleId);

public record UserListResponse(
    List<UserResponse> Users,
    int TotalCount,
    int Page,
    int PageSize
);