namespace ACS.Core.Commands;

public interface IVerticalCommand
{
    string CommandId { get; }
    string CommandType { get; }
    string TenantId { get; }
    DateTime Timestamp { get; }
}

public abstract class VerticalCommandBase : IVerticalCommand
{
    public string CommandId { get; init; } = Guid.NewGuid().ToString();
    public abstract string CommandType { get; }
    public string TenantId { get; init; } = default!;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public class CreateUserCommand : VerticalCommandBase
{
    public override string CommandType => "CreateUser";
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
}

public class AddUserToGroupCommand : VerticalCommandBase
{
    public override string CommandType => "AddUserToGroup";
    public string UserId { get; init; } = default!;
    public string GroupId { get; init; } = default!;
}

public class RemoveUserFromGroupCommand : VerticalCommandBase
{
    public override string CommandType => "RemoveUserFromGroup";
    public string UserId { get; init; } = default!;
    public string GroupId { get; init; } = default!;
}

public class CreateGroupCommand : VerticalCommandBase
{
    public override string CommandType => "CreateGroup";
    public string Name { get; init; } = default!;
    public string? ParentGroupId { get; init; }
}

public class AddGroupToGroupCommand : VerticalCommandBase
{
    public override string CommandType => "AddGroupToGroup";
    public string ChildGroupId { get; init; } = default!;
    public string ParentGroupId { get; init; } = default!;
}

public class CreateRoleCommand : VerticalCommandBase
{
    public override string CommandType => "CreateRole";
    public string Name { get; init; } = default!;
    public string? GroupId { get; init; }
}

public class AssignUserToRoleCommand : VerticalCommandBase
{
    public override string CommandType => "AssignUserToRole";
    public string UserId { get; init; } = default!;
    public string RoleId { get; init; } = default!;
}

public class GrantPermissionCommand : VerticalCommandBase
{
    public override string CommandType => "GrantPermission";
    public string EntityId { get; init; } = default!;
    public string Uri { get; init; } = default!;
    public string HttpVerb { get; init; } = default!;
    public string Scheme { get; init; } = default!;
}

public class DenyPermissionCommand : VerticalCommandBase
{
    public override string CommandType => "DenyPermission";
    public string EntityId { get; init; } = default!;
    public string Uri { get; init; } = default!;
    public string HttpVerb { get; init; } = default!;
    public string Scheme { get; init; } = default!;
}

public class EvaluatePermissionCommand : VerticalCommandBase
{
    public override string CommandType => "EvaluatePermission";
    public string UserId { get; init; } = default!;
    public string Uri { get; init; } = default!;
    public string HttpVerb { get; init; } = default!;
}