using ACS.Service.Domain;

namespace ACS.Service.Requests;

/// <summary>
/// Service request for retrieving a single permission
/// </summary>
public record GetPermissionRequest
{
    public int PermissionId { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for retrieving multiple permissions with pagination
/// </summary>
public record GetPermissionsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? Resource { get; init; }
    public string? Action { get; init; }
    public string? Scope { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for creating a new permission
/// </summary>
public record CreatePermissionRequest
{
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Scope { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for updating an existing permission
/// </summary>
public record UpdatePermissionRequest
{
    public int PermissionId { get; init; }
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Scope { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for deleting a permission
/// </summary>
public record DeletePermissionRequest
{
    public int PermissionId { get; init; }
    public string DeletedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for granting permission to entity
/// </summary>
public record GrantPermissionRequest
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty; // "User", "Group", "Role"
    public int PermissionId { get; init; }
    public string GrantedBy { get; init; } = string.Empty;
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Service request for revoking permission from entity
/// </summary>
public record RevokePermissionRequest
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public int PermissionId { get; init; }
    public string RevokedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for checking if entity has permission
/// </summary>
public record CheckPermissionRequest
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Scope { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for getting entity permissions
/// </summary>
public record GetEntityPermissionsRequest
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public bool IncludeInherited { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for bulk permission operations
/// </summary>
public record BulkPermissionRequest<T>
{
    public ICollection<T> Items { get; init; } = new List<T>();
    public string Operation { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}