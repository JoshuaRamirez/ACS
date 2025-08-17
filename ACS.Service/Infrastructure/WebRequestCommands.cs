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