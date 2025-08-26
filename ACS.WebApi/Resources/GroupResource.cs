namespace ACS.WebApi.Resources;

/// <summary>
/// Pure REST resource representing a Group entity
/// Designed for HTTP verb operations (GET, POST, PUT, PATCH, DELETE)
/// </summary>
public record GroupResource
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
    
    // Navigation properties for REST relationships
    public ICollection<UserResource> Users { get; init; } = new List<UserResource>();
    public ICollection<RoleResource> Roles { get; init; } = new List<RoleResource>();
    public ICollection<GroupResource> ChildGroups { get; init; } = new List<GroupResource>();
    public ICollection<GroupResource> ParentGroups { get; init; } = new List<GroupResource>();
    public ICollection<PermissionResource> Permissions { get; init; } = new List<PermissionResource>();
}

/// <summary>
/// Resource for creating a new group (POST /api/groups)
/// </summary>
public record CreateGroupResource
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public int? ParentGroupId { get; init; }
}

/// <summary>
/// Resource for updating a group (PUT /api/groups/{id})
/// </summary>
public record UpdateGroupResource
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Resource for partial group updates (PATCH /api/groups/{id})
/// </summary>
public record PatchGroupResource
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Collection resource for groups with pagination metadata
/// </summary>
public record GroupCollectionResource
{
    public ICollection<GroupResource> Groups { get; init; } = new List<GroupResource>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Resource for group hierarchy operations
/// </summary>
public record GroupHierarchyResource
{
    public int ParentGroupId { get; init; }
    public int ChildGroupId { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int Level { get; init; }
    public string Path { get; init; } = string.Empty;
}

/// <summary>
/// Resource for group-role relationship operations
/// </summary>
public record GroupRoleRelationshipResource
{
    public int GroupId { get; init; }
    public int RoleId { get; init; }
    public string AssignedBy { get; init; } = string.Empty;
    public DateTime AssignedAt { get; init; }
}