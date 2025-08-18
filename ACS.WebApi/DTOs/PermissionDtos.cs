namespace ACS.WebApi.DTOs;

public record GrantPermissionRequest(
    int EntityId,
    string Uri,
    string HttpVerb,
    string Scheme = "ApiUriAuthorization"
);

public record DenyPermissionRequest(
    int EntityId,
    string Uri,
    string HttpVerb,
    string Scheme = "ApiUriAuthorization"
);

public record CheckPermissionRequest(
    int EntityId,
    string Uri,
    string HttpVerb
);

public record PermissionResponse(
    int Id,
    string Uri,
    string HttpVerb,
    bool Grant,
    bool Deny,
    string Scheme
);

public record CheckPermissionResponse(
    bool HasPermission,
    string Uri,
    string HttpVerb,
    int EntityId,
    string EntityType,
    string Reason
);

public record PermissionListResponse(
    List<PermissionResponse> Permissions,
    int TotalCount,
    int Page,
    int PageSize
);