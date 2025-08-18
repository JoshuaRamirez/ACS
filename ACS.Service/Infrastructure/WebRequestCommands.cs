using ACS.Service.Domain;

namespace ACS.Service.Infrastructure;

public abstract record WebRequestCommand(string RequestId, DateTime Timestamp, string UserId);

// User management commands
public record CreateUserCommand(string RequestId, DateTime Timestamp, string UserId, 
    string Name) : WebRequestCommand(RequestId, Timestamp, UserId);

public record AddUserToGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId, int GroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record RemoveUserFromGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId, int GroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

// Group management commands
public record CreateGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    string Name, int? ParentGroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record AddGroupToGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int ChildGroupId, int ParentGroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

// Role management commands
public record CreateRoleCommand(string RequestId, DateTime Timestamp, string UserId, 
    string Name, int? GroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record AssignUserToRoleCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId, int RoleId) : WebRequestCommand(RequestId, Timestamp, UserId);

// Permission management commands
public record GrantPermissionCommand(string RequestId, DateTime Timestamp, string UserId, 
    int EntityId, string Uri, HttpVerb Verb, Scheme Scheme) : WebRequestCommand(RequestId, Timestamp, UserId);

public record DenyPermissionCommand(string RequestId, DateTime Timestamp, string UserId, 
    int EntityId, string Uri, HttpVerb Verb, Scheme Scheme) : WebRequestCommand(RequestId, Timestamp, UserId);

// Query commands (for permission evaluation)
public record EvaluatePermissionCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId, string Uri, HttpVerb Verb) : WebRequestCommand(RequestId, Timestamp, UserId);

// Query commands for entities
public record GetUsersCommand(string RequestId, DateTime Timestamp, string UserId, 
    int Page, int PageSize) : WebRequestCommand(RequestId, Timestamp, UserId);

public record GetUserCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record UpdateUserCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId, string Name) : WebRequestCommand(RequestId, Timestamp, UserId);

public record DeleteUserCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record GetGroupsCommand(string RequestId, DateTime Timestamp, string UserId, 
    int Page, int PageSize) : WebRequestCommand(RequestId, Timestamp, UserId);

public record GetGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int GroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record UpdateGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int GroupId, string Name) : WebRequestCommand(RequestId, Timestamp, UserId);

public record DeleteGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int GroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record GetRolesCommand(string RequestId, DateTime Timestamp, string UserId, 
    int Page, int PageSize) : WebRequestCommand(RequestId, Timestamp, UserId);

public record GetRoleCommand(string RequestId, DateTime Timestamp, string UserId, 
    int RoleId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record UpdateRoleCommand(string RequestId, DateTime Timestamp, string UserId, 
    int RoleId, string Name) : WebRequestCommand(RequestId, Timestamp, UserId);

public record DeleteRoleCommand(string RequestId, DateTime Timestamp, string UserId, 
    int RoleId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record GetEntityPermissionsCommand(string RequestId, DateTime Timestamp, string UserId, 
    int EntityId, int Page, int PageSize) : WebRequestCommand(RequestId, Timestamp, UserId);

public record RemovePermissionCommand(string RequestId, DateTime Timestamp, string UserId, 
    int EntityId, string Uri, HttpVerb Verb) : WebRequestCommand(RequestId, Timestamp, UserId);


public record CheckPermissionCommand(string RequestId, DateTime Timestamp, string UserId, 
    int EntityId, string Uri, string HttpVerb) : WebRequestCommand(RequestId, Timestamp, UserId);

public record AddRoleToGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int GroupId, int RoleId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record RemoveRoleFromGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int GroupId, int RoleId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record UnAssignUserFromRoleCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId, int RoleId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record RemoveGroupFromGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int ParentGroupId, int ChildGroupId) : WebRequestCommand(RequestId, Timestamp, UserId);