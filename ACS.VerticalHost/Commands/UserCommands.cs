using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// User Commands
public class CreateUserCommand : ICommand<ACS.Service.Domain.User>
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? CreatedBy { get; set; }
}

public class UpdateUserCommand : ICommand<ACS.Service.Domain.User>
{
    public int UserId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public bool? IsActive { get; set; }
    public string? UpdatedBy { get; set; }
}

public class DeleteUserCommand : ICommand<DeleteUserResult>
{
    public int UserId { get; set; }
    public bool ForceDelete { get; set; }
    public int? ReassignToUserId { get; set; }
    public string? DeletedBy { get; set; }
}

public class AddUserToGroupCommand : ICommand<UserGroupOperationResult>
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public string? AddedBy { get; set; }
}

public class RemoveUserFromGroupCommand : ICommand<UserGroupOperationResult>
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public string? RemovedBy { get; set; }
}

// User Queries
public class GetUserQuery : IQuery<ACS.Service.Domain.User>
{
    public int UserId { get; set; }
    public bool IncludeRoles { get; set; }
    public bool IncludeGroups { get; set; }
    public bool IncludePermissions { get; set; }
}

public class GetUsersQuery : IQuery<List<ACS.Service.Domain.User>>
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

public class GetUserGroupsQuery : IQuery<List<ACS.Service.Domain.Group>>
{
    public int UserId { get; set; }
    public bool IncludeHierarchy { get; set; }
    public bool IncludeRoles { get; set; }
}
