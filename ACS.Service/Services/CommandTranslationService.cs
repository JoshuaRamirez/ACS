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
            
            _ => throw new NotSupportedException($"Web command type {webCommand.GetType().Name} is not supported for translation")
        };
    }

    public bool IsQueryCommand(WebRequestCommand webCommand)
    {
        return webCommand is Infrastructure.EvaluatePermissionCommand;
    }

    public bool IsMutationCommand(WebRequestCommand webCommand)
    {
        return webCommand switch
        {
            Infrastructure.CreateUserCommand => true,
            Infrastructure.AddUserToGroupCommand => true,
            Infrastructure.RemoveUserFromGroupCommand => true,
            Infrastructure.CreateGroupCommand => true,
            Infrastructure.AddGroupToGroupCommand => true,
            Infrastructure.CreateRoleCommand => true,
            Infrastructure.AssignUserToRoleCommand => true,
            Infrastructure.GrantPermissionCommand => true,
            Infrastructure.DenyPermissionCommand => true,
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
            Infrastructure.GrantPermissionCommand cmd => $"Grant permission {cmd.Uri}:{cmd.Verb} to entity {cmd.EntityId}",
            Infrastructure.DenyPermissionCommand cmd => $"Deny permission {cmd.Uri}:{cmd.Verb} to entity {cmd.EntityId}",
            Infrastructure.EvaluatePermissionCommand cmd => $"Check permission {cmd.Uri}:{cmd.Verb} for user {cmd.TargetUserId}",
            _ => webCommand.GetType().Name
        };
    }
}