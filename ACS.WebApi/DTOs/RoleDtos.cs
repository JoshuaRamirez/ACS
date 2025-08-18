namespace ACS.WebApi.DTOs;

public record CreateRoleRequest(string Name, int? GroupId = null);

public record UpdateRoleRequest(string Name, int? GroupId = null);

public record RoleResponse(
    int Id,
    string Name,
    int? GroupId,
    string? GroupName,
    List<UserResponse> Users,
    List<PermissionResponse> Permissions,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record RoleListResponse(
    List<RoleResponse> Roles,
    int TotalCount,
    int Page,
    int PageSize
);