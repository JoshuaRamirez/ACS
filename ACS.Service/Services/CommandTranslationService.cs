using ACS.Service.Infrastructure;
using ACS.Service.Domain;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

public class CommandTranslationService
{
    private readonly ILogger<CommandTranslationService> _logger;

    public CommandTranslationService(ILogger<CommandTranslationService> logger)
    {
        _logger = logger;
    }

    public DomainCommand TranslateCommand(WebRequestCommand webCommand)
    {
        _logger.LogDebug("Translating web command {CommandType} to domain command", webCommand.GetType().Name);

        return webCommand switch
        {
            Infrastructure.AddUserToGroupCommand cmd => new Services.AddUserToGroupCommand
            {
                UserId = cmd.TargetUserId,
                GroupId = cmd.GroupId
            },
            
            Infrastructure.RemoveUserFromGroupCommand cmd => new Services.RemoveUserFromGroupCommand
            {
                UserId = cmd.TargetUserId,
                GroupId = cmd.GroupId
            },
            
            Infrastructure.AssignUserToRoleCommand cmd => new Services.AssignUserToRoleCommand
            {
                UserId = cmd.TargetUserId,
                RoleId = cmd.RoleId
            },
            
            Infrastructure.AddGroupToGroupCommand cmd => new Services.AddGroupToGroupCommand
            {
                ParentGroupId = cmd.ParentGroupId,
                ChildGroupId = cmd.ChildGroupId
            },
            
            Infrastructure.GrantPermissionCommand cmd => new Services.AddPermissionToEntityCommand
            {
                EntityId = cmd.EntityId,
                Permission = new Permission
                {
                    Uri = cmd.Uri,
                    HttpVerb = cmd.Verb,
                    Scheme = cmd.Scheme,
                    Grant = true,
                    Deny = false
                }
            },
            
            Infrastructure.DenyPermissionCommand cmd => new Services.AddPermissionToEntityCommand
            {
                EntityId = cmd.EntityId,
                Permission = new Permission
                {
                    Uri = cmd.Uri,
                    HttpVerb = cmd.Verb,
                    Scheme = cmd.Scheme,
                    Grant = false,
                    Deny = true
                }
            },
            
            Infrastructure.EvaluatePermissionCommand cmd => new Services.CheckPermissionCommand
            {
                EntityId = cmd.TargetUserId,
                Uri = cmd.Uri,
                HttpVerb = cmd.Verb
            },
            
            // User CRUD operations
            Infrastructure.CreateUserCommand cmd => new Services.CreateUserCommand
            {
                Name = cmd.Name
            },
            
            Infrastructure.GetUsersCommand cmd => new Services.GetUsersCommand
            {
                Page = cmd.Page,
                PageSize = cmd.PageSize
            },
            
            Infrastructure.GetUserCommand cmd => new Services.GetUserCommand
            {
                UserId = cmd.TargetUserId
            },
            
            Infrastructure.UpdateUserCommand cmd => new Services.UpdateUserCommand
            {
                UserId = cmd.TargetUserId,
                Name = cmd.Name
            },
            
            Infrastructure.DeleteUserCommand cmd => new Services.DeleteUserCommand
            {
                UserId = cmd.TargetUserId
            },
            
            // Group CRUD operations
            Infrastructure.CreateGroupCommand cmd => new Services.CreateGroupCommand
            {
                Name = cmd.Name,
                ParentGroupId = cmd.ParentGroupId
            },
            
            Infrastructure.GetGroupsCommand cmd => new Services.GetGroupsCommand
            {
                Page = cmd.Page,
                PageSize = cmd.PageSize
            },
            
            Infrastructure.GetGroupCommand cmd => new Services.GetGroupCommand
            {
                GroupId = cmd.GroupId
            },
            
            Infrastructure.UpdateGroupCommand cmd => new Services.UpdateGroupCommand
            {
                GroupId = cmd.GroupId,
                Name = cmd.Name
            },
            
            Infrastructure.DeleteGroupCommand cmd => new Services.DeleteGroupCommand
            {
                GroupId = cmd.GroupId
            },
            
            // Role CRUD operations
            Infrastructure.CreateRoleCommand cmd => new Services.CreateRoleCommand
            {
                Name = cmd.Name,
                GroupId = cmd.GroupId
            },
            
            Infrastructure.GetRolesCommand cmd => new Services.GetRolesCommand
            {
                Page = cmd.Page,
                PageSize = cmd.PageSize
            },
            
            Infrastructure.GetRoleCommand cmd => new Services.GetRoleCommand
            {
                RoleId = cmd.RoleId
            },
            
            Infrastructure.UpdateRoleCommand cmd => new Services.UpdateRoleCommand
            {
                RoleId = cmd.RoleId,
                Name = cmd.Name
            },
            
            Infrastructure.DeleteRoleCommand cmd => new Services.DeleteRoleCommand
            {
                RoleId = cmd.RoleId
            },
            
            // Permission operations
            Infrastructure.GetEntityPermissionsCommand cmd => new Services.GetEntityPermissionsCommand
            {
                EntityId = cmd.EntityId,
                Page = cmd.Page,
                PageSize = cmd.PageSize
            },
            
            Infrastructure.RemovePermissionCommand cmd => new Services.RemovePermissionFromEntityCommand
            {
                EntityId = cmd.EntityId,
                Permission = new Permission
                {
                    Uri = cmd.Uri,
                    HttpVerb = cmd.Verb,
                    Grant = false,
                    Deny = false
                }
            },
            
            Infrastructure.CheckPermissionCommand cmd => new Services.CheckPermissionCommand
            {
                EntityId = cmd.EntityId,
                Uri = cmd.Uri,
                HttpVerb = System.Enum.Parse<HttpVerb>(cmd.HttpVerb, true)
            },
            
            Infrastructure.AddRoleToGroupCommand cmd => new Services.AddRoleToGroupCommand
            {
                GroupId = cmd.GroupId,
                RoleId = cmd.RoleId
            },
            
            Infrastructure.RemoveRoleFromGroupCommand cmd => new Services.RemoveRoleFromGroupCommand
            {
                GroupId = cmd.GroupId,
                RoleId = cmd.RoleId
            },
            
            Infrastructure.UnAssignUserFromRoleCommand cmd => new Services.UnAssignUserFromRoleCommand
            {
                UserId = cmd.TargetUserId,
                RoleId = cmd.RoleId
            },
            
            Infrastructure.RemoveGroupFromGroupCommand cmd => new Services.RemoveGroupFromGroupCommand
            {
                ParentGroupId = cmd.ParentGroupId,
                ChildGroupId = cmd.ChildGroupId
            },
            
            _ => throw new NotSupportedException($"Web command type {webCommand.GetType().Name} is not supported for translation")
        };
    }

    public bool IsQueryCommand(WebRequestCommand webCommand)
    {
        return webCommand switch
        {
            Infrastructure.EvaluatePermissionCommand => true,
            Infrastructure.CheckPermissionCommand => true,
            Infrastructure.GetUsersCommand => true,
            Infrastructure.GetUserCommand => true,
            Infrastructure.GetGroupsCommand => true,
            Infrastructure.GetGroupCommand => true,
            Infrastructure.GetRolesCommand => true,
            Infrastructure.GetRoleCommand => true,
            Infrastructure.GetEntityPermissionsCommand => true,
            _ => false
        };
    }

    public bool IsMutationCommand(WebRequestCommand webCommand)
    {
        return webCommand switch
        {
            Infrastructure.CreateUserCommand => true,
            Infrastructure.UpdateUserCommand => true,
            Infrastructure.DeleteUserCommand => true,
            Infrastructure.AddUserToGroupCommand => true,
            Infrastructure.RemoveUserFromGroupCommand => true,
            Infrastructure.CreateGroupCommand => true,
            Infrastructure.UpdateGroupCommand => true,
            Infrastructure.DeleteGroupCommand => true,
            Infrastructure.AddGroupToGroupCommand => true,
            Infrastructure.CreateRoleCommand => true,
            Infrastructure.UpdateRoleCommand => true,
            Infrastructure.DeleteRoleCommand => true,
            Infrastructure.AssignUserToRoleCommand => true,
            Infrastructure.UnAssignUserFromRoleCommand => true,
            Infrastructure.AddRoleToGroupCommand => true,
            Infrastructure.RemoveRoleFromGroupCommand => true,
            Infrastructure.RemoveGroupFromGroupCommand => true,
            Infrastructure.GrantPermissionCommand => true,
            Infrastructure.DenyPermissionCommand => true,
            Infrastructure.RemovePermissionCommand => true,
            _ => false
        };
    }

    public string GetCommandDescription(WebRequestCommand webCommand)
    {
        return webCommand switch
        {
            Infrastructure.AddUserToGroupCommand cmd => $"Add user {cmd.TargetUserId} to group {cmd.GroupId}",
            Infrastructure.RemoveUserFromGroupCommand cmd => $"Remove user {cmd.TargetUserId} from group {cmd.GroupId}",
            Infrastructure.AssignUserToRoleCommand cmd => $"Assign user {cmd.TargetUserId} to role {cmd.RoleId}",
            Infrastructure.AddGroupToGroupCommand cmd => $"Add group {cmd.ChildGroupId} to group {cmd.ParentGroupId}",
            Infrastructure.AddRoleToGroupCommand cmd => $"Add role {cmd.RoleId} to group {cmd.GroupId}",
            Infrastructure.RemoveRoleFromGroupCommand cmd => $"Remove role {cmd.RoleId} from group {cmd.GroupId}",
            Infrastructure.UnAssignUserFromRoleCommand cmd => $"Unassign user {cmd.TargetUserId} from role {cmd.RoleId}",
            Infrastructure.RemoveGroupFromGroupCommand cmd => $"Remove group {cmd.ChildGroupId} from group {cmd.ParentGroupId}",
            Infrastructure.GrantPermissionCommand cmd => $"Grant permission {cmd.Uri}:{cmd.Verb} to entity {cmd.EntityId}",
            Infrastructure.DenyPermissionCommand cmd => $"Deny permission {cmd.Uri}:{cmd.Verb} to entity {cmd.EntityId}",
            Infrastructure.EvaluatePermissionCommand cmd => $"Check permission {cmd.Uri}:{cmd.Verb} for user {cmd.TargetUserId}",
            Infrastructure.CheckPermissionCommand cmd => $"Check permission {cmd.Uri}:{cmd.HttpVerb} for entity {cmd.EntityId}",
            _ => webCommand.GetType().Name
        };
    }
}