namespace ACS.WebApi.Resources;

/// <summary>
/// Pure REST resource representing a Permission entity
/// Designed for HTTP verb operations (GET, POST, PUT, PATCH, DELETE)
/// </summary>
public record PermissionResource
{
    public int Id { get; init; }
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Scope { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    
    // Computed properties
    public string FullPermission => $"{Resource}:{Action}" + (string.IsNullOrEmpty(Scope) ? "" : $":{Scope}");
}

/// <summary>
/// Resource for creating a new permission (POST /api/permissions)
/// </summary>
public record CreatePermissionResource
{
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Scope { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Resource for updating a permission (PUT /api/permissions/{id})
/// </summary>
public record UpdatePermissionResource
{
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Scope { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Collection resource for permissions with pagination metadata
/// </summary>
public record PermissionCollectionResource
{
    public ICollection<PermissionResource> Permissions { get; init; } = new List<PermissionResource>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Resource for permission grant operations
/// </summary>
public record PermissionGrantResource
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty; // "User", "Group", "Role"
    public int PermissionId { get; init; }
    public string GrantedBy { get; init; } = string.Empty;
    public DateTime GrantedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Resource for checking permissions
/// </summary>
public record PermissionCheckResource
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Scope { get; init; }
    public bool HasPermission { get; init; }
    public string? Reason { get; init; }
}