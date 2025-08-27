using Grpc.Core;
using ACS.Core.Grpc;
using ACS.Service.Services;
using ACS.Service.Requests;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Diagnostics;
using ACS.Infrastructure;
using Infrastructure = ACS.Service.Infrastructure;
using ACS.VerticalHost.Commands;

namespace ACS.VerticalHost.Services;

/// <summary>
/// Enhanced VerticalGrpcService that uses CommandBuffer for sequential processing
/// Acts as the gRPC endpoint that receives commands from HTTP API
/// and routes them through the command buffer system
/// </summary>
public class VerticalGrpcService : VerticalService.VerticalServiceBase
{
    private readonly ICommandBuffer _commandBuffer;
    private readonly IUserService _userService; // For backward compatibility
    private readonly IGroupService _groupService; // For backward compatibility
    private readonly IRoleService _roleService; // For backward compatibility
    private readonly ILogger<VerticalGrpcService> _logger;
    private readonly string _tenantId;
    private long _commandsProcessed = 0;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public VerticalGrpcService(
        ICommandBuffer commandBuffer,
        IUserService userService,
        IGroupService groupService,
        IRoleService roleService,
        ACS.Service.Infrastructure.TenantConfiguration config,
        ILogger<VerticalGrpcService> logger)
    {
        _commandBuffer = commandBuffer;
        _userService = userService;
        _groupService = groupService;
        _roleService = roleService;
        _tenantId = config.TenantId;
        _logger = logger;
    }

    public override async Task<CommandResponse> ExecuteCommand(
        CommandRequest request, 
        ServerCallContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var activity = VerticalHostTelemetryService.StartGrpcServiceActivity("ExecuteCommand");
        activity?.SetTag("command.type", request.CommandType);
        activity?.SetTag("command.correlation_id", request.CorrelationId);
        
        try
        {
            _logger.LogDebug("Executing command {CommandType} for tenant {TenantId}", 
                request.CommandType, _tenantId);

            // Parse the command type
            var commandType = Type.GetType(request.CommandType);
            if (commandType == null)
            {
                throw new InvalidOperationException($"Unknown command type: {request.CommandType}");
            }

            // Deserialize command using binary protobuf
            var command = ProtoSerializer.Deserialize(commandType, request.CommandData.ToByteArray());
            if (command == null)
            {
                throw new InvalidOperationException($"Failed to deserialize command of type: {request.CommandType}");
            }

            // Check if command has a result type
            var isVoidCommand = !commandType.IsGenericType;
            byte[] resultData = Array.Empty<byte>();

            using var commandActivity = VerticalHostTelemetryService.StartCommandProcessingActivity(
                request.CommandType, request.CorrelationId);

            if (isVoidCommand)
            {
                // Execute void command
                await ExecuteVoidCommandAsync((DomainCommand)command);
                commandActivity?.SetTag("command.has_result", false);
            }
            else
            {
                // Execute command with result
                commandActivity?.SetTag("command.has_result", true);
                
                var result = await ExecuteCommandWithResultAsync((dynamic)command);
                commandActivity?.SetTag("command.result_type", result?.GetType().Name ?? "null");
                
                if (result != null)
                {
                    // Serialize result using binary protobuf
                    resultData = ProtoSerializer.Serialize(result);
                    commandActivity?.SetTag("result.serialized_size", resultData.Length);
                }
            }

            Interlocked.Increment(ref _commandsProcessed);
            
            stopwatch.Stop();
            
            // Record telemetry metrics
            VerticalHostTelemetryService.RecordCommandMetrics(activity, stopwatch.Elapsed, true);
            VerticalHostTelemetryService.RecordCommandMetrics(commandActivity, stopwatch.Elapsed, true);
            
            _logger.LogInformation("Command {CommandType} executed successfully in {ElapsedMs}ms", 
                request.CommandType, stopwatch.ElapsedMilliseconds);

            return new CommandResponse
            {
                Success = true,
                ResultData = ByteString.CopyFrom(resultData),
                CorrelationId = request.CorrelationId
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Record error in telemetry
            VerticalHostTelemetryService.RecordError(activity, ex);
            VerticalHostTelemetryService.RecordCommandMetrics(activity, stopwatch.Elapsed, false);
            
            _logger.LogError(ex, "Error executing command {CommandType} after {ElapsedMs}ms", 
                request.CommandType, stopwatch.ElapsedMilliseconds);

            return new CommandResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                CorrelationId = request.CorrelationId
            };
        }
    }

    public override Task<HealthResponse> HealthCheck(
        HealthRequest request,
        ServerCallContext context)
    {
        using var activity = VerticalHostTelemetryService.StartGrpcServiceActivity("HealthCheck");
        
        var uptime = DateTime.UtcNow - _startTime;
        var commandsProcessed = Interlocked.Read(ref _commandsProcessed);
        
        activity?.SetTag("health.uptime_seconds", uptime.TotalSeconds);
        activity?.SetTag("health.commands_processed", commandsProcessed);
        activity?.SetTag("health.status", "healthy");
        
        var response = new HealthResponse
        {
            Healthy = true,
            UptimeSeconds = (long)uptime.TotalSeconds,
            ActiveConnections = 1, // Can be enhanced with real metrics
            CommandsProcessed = commandsProcessed
        };

        _logger.LogDebug("Health check: Uptime={Uptime}s, Commands={Commands}", 
            response.UptimeSeconds, response.CommandsProcessed);

        return Task.FromResult(response);
    }

    private async Task ExecuteVoidCommandAsync(object command)
    {
        await (command switch
        {
            ACS.VerticalHost.Commands.AddUserToGroupCommand cmd => ExecuteAddUserToGroup(cmd),
            ACS.VerticalHost.Commands.RemoveUserFromGroupCommand cmd => ExecuteRemoveUserFromGroup(cmd),
            AssignUserToRoleCommand cmd => ExecuteAssignUserToRole(cmd),
            UnAssignUserFromRoleCommand cmd => ExecuteUnAssignUserFromRole(cmd),
            AddRoleToGroupCommand cmd => ExecuteAddRoleToGroup(cmd),
            RemoveRoleFromGroupCommand cmd => ExecuteRemoveRoleFromGroup(cmd),
            AddGroupToGroupCommand cmd => ExecuteAddGroupToGroup(cmd),
            RemoveGroupFromGroupCommand cmd => ExecuteRemoveGroupFromGroup(cmd),
            _ => throw new NotSupportedException($"Void command type {command.GetType().Name} is not supported")
        });
    }

    private async Task<object> ExecuteCommandWithResultAsync(dynamic command)
    {
        return command switch
        {
            ACS.VerticalHost.Commands.CreateUserCommand cmd => await ExecuteCreateUser(cmd),
            CreateGroupCommand cmd => await ExecuteCreateGroup(cmd),
            CreateRoleCommand cmd => await ExecuteCreateRole(cmd),
            ACS.VerticalHost.Commands.UpdateUserCommand cmd => await ExecuteUpdateUser(cmd),
            UpdateGroupCommand cmd => await ExecuteUpdateGroup(cmd),
            UpdateRoleCommand cmd => await ExecuteUpdateRole(cmd),
            ACS.VerticalHost.Commands.DeleteUserCommand cmd => await ExecuteDeleteUser(cmd),
            DeleteGroupCommand cmd => await ExecuteDeleteGroup(cmd),
            DeleteRoleCommand cmd => await ExecuteDeleteRole(cmd),
            ACS.VerticalHost.Commands.GetUserCommand cmd => ExecuteGetUser(cmd),
            GetGroupCommand cmd => ExecuteGetGroup(cmd),
            GetRoleCommand cmd => ExecuteGetRole(cmd),
            ACS.VerticalHost.Commands.GetUsersCommand cmd => ExecuteGetUsers(cmd),
            GetGroupsCommand cmd => ExecuteGetGroups(cmd),
            GetRolesCommand cmd => ExecuteGetRoles(cmd),
            _ => throw new NotSupportedException($"Result command type {command.GetType().Name} is not supported")
        };
    }

    // User Commands
    private async Task<object> ExecuteCreateUser(ACS.VerticalHost.Commands.CreateUserCommand cmd)
    {
        var request = new CreateUserRequest
        {
            Name = cmd.Name ?? throw new ArgumentNullException(nameof(cmd.Name)),
            CreatedBy = cmd.CreatedBy ?? "system"
        };
        
        var response = await _userService.CreateAsync(request);
        return response.User ?? throw new InvalidOperationException("User creation failed");
    }

    private async Task<object> ExecuteUpdateUser(ACS.VerticalHost.Commands.UpdateUserCommand cmd)
    {
        var request = new UpdateUserRequest
        {
            UserId = cmd.UserId,
            Name = cmd.Name ?? throw new ArgumentNullException(nameof(cmd.Name)),
            UpdatedBy = cmd.UpdatedBy ?? "system"
        };
        
        var response = await _userService.UpdateAsync(request);
        return response.User ?? throw new InvalidOperationException("User update failed");
    }

    private async Task<object> ExecuteDeleteUser(ACS.VerticalHost.Commands.DeleteUserCommand cmd)
    {
        var request = new DeleteUserRequest
        {
            UserId = cmd.UserId,
            DeletedBy = cmd.DeletedBy ?? "system"
        };
        
        var response = await _userService.DeleteAsync(request);
        return response.Success;
    }

    private object ExecuteGetUser(ACS.VerticalHost.Commands.GetUserCommand cmd)
    {
        var request = new GetUserRequest
        {
            UserId = cmd.UserId,
            RequestedBy = "system"
        };
        
        var response = _userService.GetByIdAsync(request).Result;
        return response.User ?? throw new InvalidOperationException($"User {cmd.UserId} not found");
    }

    private object ExecuteGetUsers(ACS.VerticalHost.Commands.GetUsersCommand cmd)
    {
        var request = new GetUsersRequest
        {
            Page = cmd.Page,
            PageSize = cmd.PageSize,
            RequestedBy = "system"
        };
        
        var response = _userService.GetAllAsync(request).Result;
        return response.Users ?? new List<ACS.Service.Domain.User>();
    }

    // Group Commands
    private async Task<object> ExecuteCreateGroup(CreateGroupCommand cmd)
    {
        var group = await _groupService.CreateGroupAsync(
            cmd.Name ?? throw new ArgumentNullException(nameof(cmd.Name)),
            "", // description
            cmd.CreatedBy ?? "system");
        return group;
    }

    private async Task<object> ExecuteUpdateGroup(UpdateGroupCommand cmd)
    {
        await _groupService.UpdateGroupAsync(cmd.GroupId, 
            cmd.Name ?? throw new ArgumentNullException(nameof(cmd.Name)),
            "", // description
            cmd.UpdatedBy ?? "system");
        return true;
    }

    private async Task<object> ExecuteDeleteGroup(DeleteGroupCommand cmd)
    {
        await _groupService.DeleteGroupAsync(cmd.GroupId, cmd.DeletedBy ?? "system");
        return true;
    }

    private object ExecuteGetGroup(GetGroupCommand cmd)
    {
        var group = _groupService.GetGroupByIdAsync(cmd.GroupId).Result;
        return group ?? throw new InvalidOperationException($"Group {cmd.GroupId} not found");
    }

    private object ExecuteGetGroups(GetGroupsCommand cmd)
    {
        var groups = _groupService.GetAllGroupsAsync().Result;
        return groups ?? new List<ACS.Service.Domain.Group>();
    }

    // Role Commands
    private async Task<object> ExecuteCreateRole(CreateRoleCommand cmd)
    {
        var role = await _roleService.CreateRoleAsync(
            cmd.Name ?? throw new ArgumentNullException(nameof(cmd.Name)),
            "", // description
            cmd.CreatedBy ?? "system");
        return role;
    }

    private async Task<object> ExecuteUpdateRole(UpdateRoleCommand cmd)
    {
        await _roleService.UpdateRoleAsync(cmd.RoleId, 
            cmd.Name ?? throw new ArgumentNullException(nameof(cmd.Name)),
            "", // description
            cmd.UpdatedBy ?? "system");
        return true;
    }

    private async Task<object> ExecuteDeleteRole(DeleteRoleCommand cmd)
    {
        await _roleService.DeleteRoleAsync(cmd.RoleId, cmd.DeletedBy ?? "system");
        return true;
    }

    private object ExecuteGetRole(GetRoleCommand cmd)
    {
        var role = _roleService.GetRoleByIdAsync(cmd.RoleId).Result;
        return role ?? throw new InvalidOperationException($"Role {cmd.RoleId} not found");
    }

    private object ExecuteGetRoles(GetRolesCommand cmd)
    {
        var roles = _roleService.GetAllRolesAsync().Result;
        return roles ?? new List<ACS.Service.Domain.Role>();
    }

    // Relationship Commands (Void)
    private async Task ExecuteAddUserToGroup(ACS.VerticalHost.Commands.AddUserToGroupCommand cmd)
    {
        await _groupService.AddUserToGroupAsync(cmd.UserId, cmd.GroupId, cmd.AddedBy ?? "system");
    }

    private async Task ExecuteRemoveUserFromGroup(ACS.VerticalHost.Commands.RemoveUserFromGroupCommand cmd)
    {
        await _groupService.RemoveUserFromGroupAsync(cmd.UserId, cmd.GroupId, cmd.RemovedBy ?? "system");
    }

    private async Task ExecuteAssignUserToRole(AssignUserToRoleCommand cmd)
    {
        await _roleService.AssignUserToRoleAsync(cmd.UserId, cmd.RoleId, cmd.AssignedBy ?? "system");
    }

    private async Task ExecuteUnAssignUserFromRole(UnAssignUserFromRoleCommand cmd)
    {
        await _roleService.UnassignUserFromRoleAsync(cmd.UserId, cmd.RoleId, cmd.UnassignedBy ?? "system");
    }

    private async Task ExecuteAddRoleToGroup(AddRoleToGroupCommand cmd)
    {
        await _groupService.AddRoleToGroupAsync(cmd.RoleId, cmd.GroupId, cmd.AssignedBy ?? "system");
    }

    private async Task ExecuteRemoveRoleFromGroup(RemoveRoleFromGroupCommand cmd)
    {
        await _groupService.RemoveRoleFromGroupAsync(cmd.GroupId, cmd.RoleId, cmd.RemovedBy ?? "system");
    }

    private async Task ExecuteAddGroupToGroup(AddGroupToGroupCommand cmd)
    {
        await _groupService.AddGroupToGroupAsync(cmd.ParentGroupId, cmd.ChildGroupId, cmd.AddedBy ?? "system");
    }

    private async Task ExecuteRemoveGroupFromGroup(RemoveGroupFromGroupCommand cmd)
    {
        await _groupService.RemoveGroupFromGroupAsync(cmd.ParentGroupId, cmd.ChildGroupId, cmd.RemovedBy ?? "system");
    }
}