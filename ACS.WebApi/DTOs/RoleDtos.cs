namespace ACS.WebApi.DTOs;

/// <summary>
/// DTO for role list response from gRPC service
/// </summary>
public record RoleListResponse(
    IList<RoleResponse> Roles,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>
/// DTO for individual role response from gRPC service
/// </summary>
public record RoleResponse(
    int Id,
    string Name,
    int? GroupId,
    string? GroupName,
    IList<UserResponse> Users,
    IList<PermissionResponse> Permissions,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);