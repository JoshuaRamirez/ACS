namespace ACS.WebApi.DTOs;

/// <summary>
/// DTO for permission list response from gRPC service
/// </summary>
public record PermissionListResponse(
    IList<PermissionResponse> Permissions,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>
/// DTO for individual permission response from gRPC service
/// </summary>
public record PermissionResponse(
    int EntityId,
    string Uri,
    string HttpVerb,
    bool Grant,
    bool Deny,
    string Scheme
);

/// <summary>
/// DTO for granting permission request
/// </summary>
public record GrantPermissionRequest
{
    public int EntityId { get; init; }
    public string Uri { get; init; } = string.Empty;
    public string HttpVerb { get; init; } = string.Empty;
    public string Scheme { get; init; } = string.Empty;
}

/// <summary>
/// DTO for denying permission request
/// </summary>
public record DenyPermissionRequest
{
    public int EntityId { get; init; }
    public string Uri { get; init; } = string.Empty;
    public string HttpVerb { get; init; } = string.Empty;
    public string Scheme { get; init; } = string.Empty;
}