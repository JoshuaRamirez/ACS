using ACS.Service.Domain;

namespace ACS.Service.Requests;

/// <summary>
/// Service request for retrieving a single group
/// </summary>
public record GetGroupRequest
{
    public int GroupId { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for retrieving multiple groups with pagination
/// </summary>
public record GetGroupsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public int? ParentGroupId { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for creating a new group
/// </summary>
public record CreateGroupRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? ParentGroupId { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for updating an existing group
/// </summary>
public record UpdateGroupRequest
{
    public int GroupId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for patching group fields
/// </summary>
public record PatchGroupRequest
{
    public int GroupId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for deleting a group
/// </summary>
public record DeleteGroupRequest
{
    public int GroupId { get; init; }
    public string DeletedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for adding user to group
/// </summary>
public record AddUserToGroupServiceRequest
{
    public int GroupId { get; init; }
    public int UserId { get; init; }
    public string AddedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for removing user from group
/// </summary>
public record RemoveUserFromGroupServiceRequest
{
    public int GroupId { get; init; }
    public int UserId { get; init; }
    public string RemovedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for adding role to group
/// </summary>
public record AddRoleToGroupRequest
{
    public int GroupId { get; init; }
    public int RoleId { get; init; }
    public string AddedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for removing role from group
/// </summary>
public record RemoveRoleFromGroupRequest
{
    public int GroupId { get; init; }
    public int RoleId { get; init; }
    public string RemovedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for adding child group to parent group
/// </summary>
public record AddChildGroupRequest
{
    public int ParentGroupId { get; init; }
    public int ChildGroupId { get; init; }
    public string AddedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for removing child group from parent group
/// </summary>
public record RemoveChildGroupRequest
{
    public int ParentGroupId { get; init; }
    public int ChildGroupId { get; init; }
    public string RemovedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for bulk group operations
/// </summary>
public record BulkGroupRequest<T>
{
    public ICollection<T> Items { get; init; } = new List<T>();
    public string Operation { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}