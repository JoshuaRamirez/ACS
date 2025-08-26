using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ACS.Service.Services;

namespace ACS.Service.Infrastructure;

public class TenantAccessControlHostedService : BackgroundService
{
    private readonly string _tenantId;
    private readonly TenantRingBuffer _ringBuffer;
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly IUserService _userService;
    private readonly IGroupService _groupService;
    private readonly IRoleService _roleService;
    private readonly CommandTranslationService _commandTranslator;
    private readonly ILogger<TenantAccessControlHostedService> _logger;

    public TenantAccessControlHostedService(
        TenantConfiguration tenantConfig,
        TenantRingBuffer ringBuffer,
        InMemoryEntityGraph entityGraph,
        IUserService userService,
        IGroupService groupService,
        IRoleService roleService,
        CommandTranslationService commandTranslator,
        ILogger<TenantAccessControlHostedService> logger)
    {
        _tenantId = tenantConfig.TenantId;
        _ringBuffer = ringBuffer;
        _entityGraph = entityGraph;
        _userService = userService;
        _groupService = groupService;
        _roleService = roleService;
        _commandTranslator = commandTranslator;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Access Control Hosted Service for tenant {TenantId}", _tenantId);
        
        // Entity graph initialization is handled by the individual services
        // No explicit loading needed as services handle their own initialization
        
        _logger.LogInformation("Entity graph loaded and normalizers hydrated for tenant {TenantId}", _tenantId);
        
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Access Control event processor started for tenant {TenantId}", _tenantId);
        
        try
        {
            await foreach (var command in _ringBuffer.ReadAllAsync(stoppingToken))
            {
                await ProcessCommandAsync(command);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Access Control event processor stopping for tenant {TenantId}", _tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Access Control event processor for tenant {TenantId}", _tenantId);
            throw;
        }
    }

    private async Task ProcessCommandAsync(WebRequestCommand command)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogDebug("Processing command {CommandType} with ID {RequestId} for tenant {TenantId}", 
                command.GetType().Name, command.RequestId, _tenantId);
            
            var commandDescription = _commandTranslator.GetCommandDescription(command);
            _logger.LogInformation("Executing: {CommandDescription} (RequestId: {RequestId})", 
                commandDescription, command.RequestId);
            
            // Translate web command to domain command
            var domainCommand = _commandTranslator.TranslateCommand(command);
            
            // Route commands to appropriate services
            await RouteCommandToService(command, domainCommand, commandDescription);
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogDebug("Successfully processed command {RequestId} for tenant {TenantId} in {Duration}ms", 
                command.RequestId, _tenantId, duration.TotalMilliseconds);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning("Unsupported command {CommandType} for tenant {TenantId}: {Message}", 
                command.GetType().Name, _tenantId, ex.Message);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Error processing command {RequestId} for tenant {TenantId} after {Duration}ms: {Error}", 
                command.RequestId, _tenantId, duration.TotalMilliseconds, ex.Message);
            
            // In a production system, you might want to:
            // 1. Send error response back to waiting client
            // 2. Dead letter the command for retry
            // 3. Trigger monitoring alerts
            // 4. Apply circuit breaker patterns
            throw;
        }
    }

    private async Task RouteCommandToService(WebRequestCommand webCommand, object domainCommand, string commandDescription)
    {
        // Route to appropriate service based on command type
        switch (webCommand)
        {
            // User commands
            case CreateUserCommand createUser:
                await _userService.CreateAsync(new Service.Requests.CreateUserRequest 
                { 
                    Name = createUser.Name, 
                    CreatedBy = createUser.UserId 
                });
                _logger.LogInformation("Successfully executed user creation: {CommandDescription}", commandDescription);
                break;

            case UpdateUserCommand updateUser:
                await _userService.UpdateAsync(new Service.Requests.UpdateUserRequest 
                { 
                    UserId = updateUser.TargetUserId, 
                    Name = updateUser.Name, 
                    UpdatedBy = updateUser.UserId 
                });
                _logger.LogInformation("Successfully executed user update: {CommandDescription}", commandDescription);
                break;

            case DeleteUserCommand deleteUser:
                await _userService.DeleteAsync(new Service.Requests.DeleteUserRequest 
                { 
                    UserId = deleteUser.TargetUserId, 
                    DeletedBy = deleteUser.UserId 
                });
                _logger.LogInformation("Successfully executed user deletion: {CommandDescription}", commandDescription);
                break;

            case AddUserToGroupCommand addUserToGroup:
                await _userService.AddToGroupAsync(new Service.Requests.AddUserToGroupRequest 
                { 
                    UserId = addUserToGroup.TargetUserId, 
                    GroupId = addUserToGroup.GroupId, 
                    AddedBy = addUserToGroup.UserId 
                });
                _logger.LogInformation("Successfully executed add user to group: {CommandDescription}", commandDescription);
                break;

            case AssignUserToRoleCommand assignUserToRole:
                await _userService.AssignToRoleAsync(new Service.Requests.AssignUserToRoleRequest 
                { 
                    UserId = assignUserToRole.TargetUserId, 
                    RoleId = assignUserToRole.RoleId, 
                    AssignedBy = assignUserToRole.UserId 
                });
                _logger.LogInformation("Successfully executed assign user to role: {CommandDescription}", commandDescription);
                break;

            // Group commands
            case CreateGroupCommand createGroup:
                await _groupService.CreateGroupAsync(
                    name: createGroup.Name, 
                    description: string.Empty, // No description in command
                    createdBy: createGroup.UserId
                );
                _logger.LogInformation("Successfully executed group creation: {CommandDescription}", commandDescription);
                break;

            case UpdateGroupCommand updateGroup:
                await _groupService.UpdateGroupAsync(
                    groupId: updateGroup.GroupId, 
                    name: updateGroup.Name, 
                    description: string.Empty, // No description in command
                    updatedBy: updateGroup.UserId
                );
                _logger.LogInformation("Successfully executed group update: {CommandDescription}", commandDescription);
                break;

            case DeleteGroupCommand deleteGroup:
                await _groupService.DeleteGroupAsync(
                    groupId: deleteGroup.GroupId, 
                    deletedBy: deleteGroup.UserId
                );
                _logger.LogInformation("Successfully executed group deletion: {CommandDescription}", commandDescription);
                break;

            // Role commands  
            case CreateRoleCommand createRole:
                await _roleService.CreateRoleAsync(
                    name: createRole.Name, 
                    description: string.Empty, // No description in command
                    createdBy: createRole.UserId
                );
                _logger.LogInformation("Successfully executed role creation: {CommandDescription}", commandDescription);
                break;

            case UpdateRoleCommand updateRole:
                await _roleService.UpdateRoleAsync(
                    roleId: updateRole.RoleId, 
                    name: updateRole.Name, 
                    description: string.Empty, // No description in command
                    updatedBy: updateRole.UserId
                );
                _logger.LogInformation("Successfully executed role update: {CommandDescription}", commandDescription);
                break;

            case DeleteRoleCommand deleteRole:
                await _roleService.DeleteRoleAsync(
                    roleId: deleteRole.RoleId, 
                    deletedBy: deleteRole.UserId
                );
                _logger.LogInformation("Successfully executed role deletion: {CommandDescription}", commandDescription);
                break;

            // Query commands - these need to be handled differently as they return data
            case GetUserCommand _:
            case GetUsersCommand _:
            case GetGroupCommand _:
            case GetGroupsCommand _:
            case GetRoleCommand _:
            case GetRolesCommand _:
                // For now, just log that we received a query
                _logger.LogInformation("Received query command: {CommandDescription}", commandDescription);
                break;

            default:
                _logger.LogWarning("Unsupported command type for processing: {CommandType}", webCommand.GetType().Name);
                throw new NotSupportedException($"Command type {webCommand.GetType().Name} is not supported");
        }
    }
}