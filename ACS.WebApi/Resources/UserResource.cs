namespace ACS.WebApi.Resources;

/// <summary>
/// Pure REST resource representing a User entity
/// Designed for HTTP verb operations (GET, POST, PUT, PATCH, DELETE)
/// </summary>
public record UserResource
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
    
    // Navigation properties for REST relationships
    public ICollection<GroupResource> Groups { get; init; } = new List<GroupResource>();
    public ICollection<RoleResource> Roles { get; init; } = new List<RoleResource>();
    public ICollection<PermissionResource> Permissions { get; init; } = new List<PermissionResource>();
}

/// <summary>
/// Resource for creating a new user (POST /api/users)
/// </summary>
public record CreateUserResource
{
    public string Name { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Resource for updating a user (PUT /api/users/{id})
/// </summary>
public record UpdateUserResource
{
    public string Name { get; init; } = string.Empty;
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Resource for partial user updates (PATCH /api/users/{id})
/// </summary>
public record PatchUserResource
{
    public string? Name { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Collection resource for users with pagination metadata
/// </summary>
public record UserCollectionResource
{
    public ICollection<UserResource> Users { get; init; } = new List<UserResource>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Resource for user-group relationship operations
/// </summary>
public record UserGroupRelationshipResource
{
    public int UserId { get; init; }
    public int GroupId { get; init; }
    public string AddedBy { get; init; } = string.Empty;
    public DateTime AddedAt { get; init; }
}

/// <summary>
/// Resource for user-role relationship operations
/// </summary>
public record UserRoleRelationshipResource
{
    public int UserId { get; init; }
    public int RoleId { get; init; }
    public string AssignedBy { get; init; } = string.Empty;
    public DateTime AssignedAt { get; init; }
}

/// <summary>
/// Resource for adding user to group (POST /api/users/{userId}/groups)
/// </summary>
public record AddUserToGroupResource
{
    public int UserId { get; init; }
    public int GroupId { get; init; }
}

/// <summary>
/// Resource for assigning user to role (POST /api/users/{userId}/roles)
/// </summary>
public record AssignUserToRoleResource
{
    public int UserId { get; init; }
    public int RoleId { get; init; }
}