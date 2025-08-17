using ACS.Service.Domain;

namespace ACS.Service.Services;

// Base command classes
public abstract class DomainCommand
{
    public TaskCompletionSource<bool>? VoidCompletionSource { get; set; }
    public object? CompletionSourceObject { get; set; }
}

public abstract class DomainCommand<TResult> : DomainCommand
{
    public TaskCompletionSource<TResult>? CompletionSource { get; set; }
    
    public DomainCommand()
    {
        CompletionSourceObject = CompletionSource;
    }
}

// User-Group Commands
public class AddUserToGroupCommand : DomainCommand<bool>
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
}

public class RemoveUserFromGroupCommand : DomainCommand<bool>
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
}

// User-Role Commands
public class AssignUserToRoleCommand : DomainCommand<bool>
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
}

public class UnAssignUserFromRoleCommand : DomainCommand<bool>
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
}

// Group-Role Commands
public class AddRoleToGroupCommand : DomainCommand<bool>
{
    public int GroupId { get; set; }
    public int RoleId { get; set; }
}

public class RemoveRoleFromGroupCommand : DomainCommand<bool>
{
    public int GroupId { get; set; }
    public int RoleId { get; set; }
}

// Group-Group Commands
public class AddGroupToGroupCommand : DomainCommand<bool>
{
    public int ParentGroupId { get; set; }
    public int ChildGroupId { get; set; }
}

public class RemoveGroupFromGroupCommand : DomainCommand<bool>
{
    public int ParentGroupId { get; set; }
    public int ChildGroupId { get; set; }
}

// Permission Commands
public class AddPermissionToEntityCommand : DomainCommand<bool>
{
    public int EntityId { get; set; }
    public Permission Permission { get; set; } = null!;
}

public class RemovePermissionFromEntityCommand : DomainCommand<bool>
{
    public int EntityId { get; set; }
    public Permission Permission { get; set; } = null!;
}

// Query Commands
public class CheckPermissionCommand : DomainCommand<bool>
{
    public int EntityId { get; set; }
    public string Uri { get; set; } = null!;
    public HttpVerb HttpVerb { get; set; }
}

public class GetUserCommand : DomainCommand<User>
{
    public int UserId { get; set; }
}

public class GetGroupCommand : DomainCommand<Group>
{
    public int GroupId { get; set; }
}

public class GetRoleCommand : DomainCommand<Role>
{
    public int RoleId { get; set; }
}