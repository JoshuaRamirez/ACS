using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// User Commands
public class CreateUserCommand : ACS.VerticalHost.Services.ICommand
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? CreatedBy { get; set; }
}

public class UpdateUserCommand : ACS.VerticalHost.Services.ICommand
{
    public int UserId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public bool? IsActive { get; set; }
    public string? UpdatedBy { get; set; }
}

public class DeleteUserCommand : ACS.VerticalHost.Services.ICommand
{
    public int UserId { get; set; }
    public bool ForceDelete { get; set; }
    public int? ReassignToUserId { get; set; }
    public string? DeletedBy { get; set; }
}

public class AddUserToGroupCommand : ACS.VerticalHost.Services.ICommand
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public string? AddedBy { get; set; }
}

public class RemoveUserFromGroupCommand : ACS.VerticalHost.Services.ICommand
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public string? RemovedBy { get; set; }
}

// User Queries
public class GetUserQuery : ACS.VerticalHost.Services.IQuery<ACS.Service.Domain.User>
{
    public int UserId { get; set; }
    public bool IncludeRoles { get; set; }
    public bool IncludeGroups { get; set; }
    public bool IncludePermissions { get; set; }
}

public class GetUsersQuery : ACS.VerticalHost.Services.IQuery<List<ACS.Service.Domain.User>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public bool IncludeInactive { get; set; }
    public bool IncludeRoles { get; set; }
    public bool IncludeGroups { get; set; }
}

public class GetUserGroupsQuery : ACS.VerticalHost.Services.IQuery<List<ACS.Service.Domain.Group>>
{
    public int UserId { get; set; }
    public bool IncludeHierarchy { get; set; }
    public bool IncludeRoles { get; set; }
}

// gRPC Command classes (from existing code)
public class GetUsersCommand
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public bool IncludeInactive { get; set; }
    public bool IncludeRoles { get; set; }
    public bool IncludeGroups { get; set; }
}

public class GetUserCommand
{
    public int UserId { get; set; }
    public bool IncludeRoles { get; set; }
    public bool IncludeGroups { get; set; }
    public bool IncludePermissions { get; set; }
}

public class GetUserGroupsCommand
{
    public int UserId { get; set; }
    public bool IncludeHierarchy { get; set; }
    public bool IncludeRoles { get; set; }
}