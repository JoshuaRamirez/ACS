namespace ACS.WebApi.Resources;

/// <summary>
/// Pure REST resource representing a Role entity
/// Designed for HTTP verb operations (GET, POST, PUT, PATCH, DELETE)
/// </summary>
public record RoleResource
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
    public bool IsCriticalRole { get; init; }
    
    // Navigation properties for REST relationships
    public ICollection<UserResource> Users { get; init; } = new List<UserResource>();
    public ICollection<GroupResource> Groups { get; init; } = new List<GroupResource>();
    public ICollection<PermissionResource> Permissions { get; init; } = new List<PermissionResource>();
}

/// <summary>
/// Resource for creating a new role (POST /api/roles)
/// </summary>
public record CreateRoleResource
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public int? GroupId { get; init; }
}

/// <summary>
/// Resource for updating a role (PUT /api/roles/{id})
/// </summary>
public record UpdateRoleResource
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Resource for partial role updates (PATCH /api/roles/{id})
/// </summary>
public record PatchRoleResource
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Collection resource for roles with pagination metadata
/// </summary>
public record RoleCollectionResource
{
    public ICollection<RoleResource> Roles { get; init; } = new List<RoleResource>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}