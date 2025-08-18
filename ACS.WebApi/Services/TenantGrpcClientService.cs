using ACS.Core.Grpc;
using ACS.WebApi.DTOs;
using Grpc.Net.Client;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf;
using ACS.Service.Infrastructure;
using ACS.Service.Domain;
using ACS.Infrastructure;
using System.Diagnostics;

namespace ACS.WebApi.Services;

public class TenantGrpcClientService
{
    private readonly ILogger<TenantGrpcClientService> _logger;
    private readonly ITenantContextService _tenantContextService;
    private readonly CircuitBreakerService _circuitBreaker;

    public TenantGrpcClientService(
        ILogger<TenantGrpcClientService> logger,
        ITenantContextService tenantContextService,
        CircuitBreakerService circuitBreaker)
    {
        _logger = logger;
        _tenantContextService = tenantContextService;
        _circuitBreaker = circuitBreaker;
    }

    public async Task<ApiResponse<T>> ExecuteCommandAsync<T>(Func<VerticalService.VerticalServiceClient, Task<T>> operation)
    {
        var tenantId = string.Empty;
        using var activity = TelemetryService.StartGrpcClientActivity("ExecuteCommand", tenantId);
        
        try
        {
            tenantId = _tenantContextService.GetTenantId();
            activity?.SetTag("tenant.id", tenantId);
            
            var channel = _tenantContextService.GetGrpcChannel();
            
            if (channel == null)
            {
                activity?.SetTag("error.reason", "channel_not_found");
                _logger.LogWarning("gRPC channel not found for tenant {TenantId}", tenantId);
                return new ApiResponse<T>(false, default, $"Tenant process not available for tenant {tenantId}");
            }

            activity?.SetTag("grpc.channel.state", channel.State.ToString());

            // Use circuit breaker pattern for resilience
            var result = await _circuitBreaker.ExecuteAsync(tenantId, async () =>
            {
                using var cbActivity = TelemetryService.StartCircuitBreakerActivity("execute", tenantId);
                
                var client = new VerticalService.VerticalServiceClient(channel);
                
                // Execute with retry logic inside circuit breaker
                const int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    using var retryActivity = TelemetryService.ActivitySource.StartActivity($"grpc.retry.attempt_{attempt}");
                    retryActivity?.SetTag("tenant.id", tenantId);
                    retryActivity?.SetTag("retry.attempt", attempt);
                    retryActivity?.SetTag("retry.max_attempts", maxRetries);
                    
                    try
                    {
                        var commandResult = await operation(client);
                        activity?.SetTag("retry.final_attempt", attempt);
                        retryActivity?.SetTag("retry.successful", true);
                        _logger.LogDebug("Successfully executed gRPC command for tenant {TenantId} on attempt {Attempt}", tenantId, attempt);
                        return commandResult;
                    }
                    catch (RpcException rpcEx) when (attempt < maxRetries)
                    {
                        retryActivity?.SetTag("retry.successful", false);
                        retryActivity?.SetTag("grpc.status_code", rpcEx.Status.StatusCode.ToString());
                        TelemetryService.RecordError(retryActivity, rpcEx);
                        
                        _logger.LogWarning(rpcEx, "gRPC call failed for tenant {TenantId} on attempt {Attempt}/{MaxRetries}: {Status}", 
                            tenantId, attempt, maxRetries, rpcEx.Status);
                        
                        // Wait before retry
                        var delay = TimeSpan.FromMilliseconds(100 * attempt);
                        retryActivity?.SetTag("retry.delay_ms", delay.TotalMilliseconds);
                        await Task.Delay(delay);
                    }
                }
                
                // If we get here, all retries failed
                throw new RpcException(new Status(StatusCode.Unavailable, "gRPC service temporarily unavailable after retries"));
            });
            
            activity?.SetTag("operation.successful", true);
            return new ApiResponse<T>(true, result);
        }
        catch (CircuitBreakerOpenException cbEx)
        {
            activity?.SetTag("circuit_breaker.open", true);
            TelemetryService.RecordError(activity, cbEx);
            _logger.LogWarning(cbEx, "Circuit breaker is open for tenant {TenantId}", tenantId);
            return new ApiResponse<T>(false, default, "Service temporarily unavailable - circuit breaker open", new List<string> { cbEx.Message });
        }
        catch (RpcException rpcEx)
        {
            activity?.SetTag("grpc.status_code", rpcEx.Status.StatusCode.ToString());
            activity?.SetTag("grpc.status_detail", rpcEx.Status.Detail);
            TelemetryService.RecordError(activity, rpcEx);
            
            _logger.LogError(rpcEx, "gRPC error for tenant {TenantId}: {Status} - {Detail}", tenantId, rpcEx.Status, rpcEx.Status.Detail);
            
            var errorMessage = rpcEx.Status.StatusCode switch
            {
                StatusCode.Unavailable => "Tenant service is temporarily unavailable",
                StatusCode.NotFound => "Tenant service not found",
                StatusCode.InvalidArgument => "Invalid request parameters",
                StatusCode.PermissionDenied => "Permission denied",
                StatusCode.Unauthenticated => "Authentication required",
                _ => "Tenant service error"
            };
            
            return new ApiResponse<T>(false, default, errorMessage, new List<string> { rpcEx.Status.Detail });
        }
        catch (Exception ex)
        {
            TelemetryService.RecordError(activity, ex);
            _logger.LogError(ex, "Unexpected error executing gRPC command for tenant {TenantId}: {Error}", tenantId, ex.Message);
            return new ApiResponse<T>(false, default, "Internal server error", new List<string> { ex.Message });
        }
    }

    public async Task<ApiResponse<bool>> ExecuteVoidCommandAsync(Func<VerticalService.VerticalServiceClient, Task> operation)
    {
        var result = await ExecuteCommandAsync<bool>(async client =>
        {
            await operation(client);
            return true;
        });
        
        return new ApiResponse<bool>(result.Success, result.Data, result.Message, result.Errors);
    }

    public async Task<ApiResponse<CheckPermissionResponse>> CheckPermissionAsync(CheckPermissionRequest request)
    {
        return await ExecuteCommandAsync(async client =>
        {
            var tenantId = _tenantContextService.GetTenantId();
            
            // CheckPermission needs to be converted to use the simple gRPC service
            // For now, we'll use a simple permission check command
            var checkPermissionCommand = new Service.Infrastructure.CheckPermissionCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user",
                request.EntityId,
                request.Uri,
                request.HttpVerb
            );

            var commandRequest = CreateCommandRequest(checkPermissionCommand);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                // Deserialize the boolean result for permission check
                var hasPermission = ProtoSerializer.Deserialize<bool>(response.ResultData.ToByteArray());
                
                return new CheckPermissionResponse(
                    hasPermission,
                    request.Uri,
                    request.HttpVerb,
                    request.EntityId,
                    "Entity",
                    hasPermission ? "Permission granted" : "Permission denied"
                );
            }
            
            return new CheckPermissionResponse(
                false,
                request.Uri,
                request.HttpVerb,
                request.EntityId,
                "Entity",
                response.ErrorMessage ?? "Permission check failed"
            );
        });
    }

    // User operations
    public async Task<ApiResponse<UserListResponse>> GetUsersAsync(Service.Infrastructure.GetUsersCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            using var activity = TelemetryService.ActivitySource.StartActivity("grpc.command.GetUsers");
            TelemetryService.RecordCommandProcessing(activity, nameof(GetUsersCommand), command.RequestId);
            activity?.SetTag("pagination.page", command.Page);
            activity?.SetTag("pagination.page_size", command.PageSize);
            
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var users = ProtoSerializer.Deserialize<List<User>>(response.ResultData.ToByteArray());
                var userResponses = users.Select(u => new UserResponse(
                    u.Id, 
                    u.Name, 
                    u.Parents.FirstOrDefault()?.Id, 
                    null, // GroupName - we'll need to fetch this separately if needed
                    null, // RoleId - we'll need to fetch this separately if needed  
                    null, // RoleName
                    new List<PermissionResponse>(), // Permissions - we'll need to fetch these separately if needed
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                )).ToList();
                var result = new UserListResponse(userResponses, users.Count, command.Page, command.PageSize);
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage ?? "Failed to retrieve users"));
        });
    }

    public async Task<ApiResponse<UserResponse>> GetUserAsync(Service.Infrastructure.GetUserCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var user = ProtoSerializer.Deserialize<User>(response.ResultData.ToByteArray());
                var result = new UserResponse(
                    user.Id,
                    user.Name,
                    user.Parents.FirstOrDefault()?.Id,
                    null, // GroupName
                    null, // RoleId
                    null, // RoleName
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                );
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.NotFound, response.ErrorMessage ?? "User not found"));
        });
    }

    public async Task<ApiResponse<UserResponse>> CreateUserAsync(Service.Infrastructure.CreateUserCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var user = ProtoSerializer.Deserialize<User>(response.ResultData.ToByteArray());
                var result = new UserResponse(
                    user.Id,
                    user.Name,
                    user.Parents.FirstOrDefault()?.Id,
                    null, // GroupName
                    null, // RoleId
                    null, // RoleName
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                );
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage ?? "Failed to create user"));
        });
    }

    public async Task<ApiResponse<UserResponse>> UpdateUserAsync(Service.Infrastructure.UpdateUserCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var user = ProtoSerializer.Deserialize<User>(response.ResultData.ToByteArray());
                var result = new UserResponse(
                    user.Id,
                    user.Name,
                    user.Parents.FirstOrDefault()?.Id,
                    null, // GroupName
                    null, // RoleId
                    null, // RoleName
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                );
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage ?? "Failed to update user"));
        });
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(Service.Infrastructure.DeleteUserCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            return response.Success;
        });
    }

    // Group operations
    public async Task<ApiResponse<GroupListResponse>> GetGroupsAsync(Service.Infrastructure.GetGroupsCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var groups = ProtoSerializer.Deserialize<List<Group>>(response.ResultData.ToByteArray());
                var groupResponses = groups.Select(g => new GroupResponse(
                    g.Id,
                    g.Name,
                    g.Parents.FirstOrDefault()?.Id,
                    null, // ParentGroupName
                    new List<GroupResponse>(), // ChildGroups
                    new List<UserResponse>(), // Users
                    new List<RoleResponse>(), // Roles
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                )).ToList();
                var result = new GroupListResponse(groupResponses, groups.Count, command.Page, command.PageSize);
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage ?? "Failed to retrieve groups"));
        });
    }

    public async Task<ApiResponse<GroupResponse>> GetGroupAsync(Service.Infrastructure.GetGroupCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var group = ProtoSerializer.Deserialize<Group>(response.ResultData.ToByteArray());
                var result = new GroupResponse(
                    group.Id,
                    group.Name,
                    group.Parents.FirstOrDefault()?.Id,
                    null, // ParentGroupName
                    new List<GroupResponse>(), // ChildGroups
                    new List<UserResponse>(), // Users
                    new List<RoleResponse>(), // Roles
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                );
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.NotFound, response.ErrorMessage ?? "Group not found"));
        });
    }

    public async Task<ApiResponse<GroupResponse>> CreateGroupAsync(Service.Infrastructure.CreateGroupCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var group = ProtoSerializer.Deserialize<Group>(response.ResultData.ToByteArray());
                var result = new GroupResponse(
                    group.Id,
                    group.Name,
                    group.Parents.FirstOrDefault()?.Id,
                    null, // ParentGroupName
                    new List<GroupResponse>(), // ChildGroups
                    new List<UserResponse>(), // Users
                    new List<RoleResponse>(), // Roles
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                );
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage ?? "Failed to create group"));
        });
    }

    public async Task<ApiResponse<GroupResponse>> UpdateGroupAsync(Service.Infrastructure.UpdateGroupCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var group = ProtoSerializer.Deserialize<Group>(response.ResultData.ToByteArray());
                var result = new GroupResponse(
                    group.Id,
                    group.Name,
                    group.Parents.FirstOrDefault()?.Id,
                    null, // ParentGroupName
                    new List<GroupResponse>(), // ChildGroups
                    new List<UserResponse>(), // Users
                    new List<RoleResponse>(), // Roles
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                );
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage ?? "Failed to update group"));
        });
    }

    public async Task<ApiResponse<bool>> DeleteGroupAsync(Service.Infrastructure.DeleteGroupCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            return response.Success;
        });
    }

    // Role operations
    public async Task<ApiResponse<RoleListResponse>> GetRolesAsync(Service.Infrastructure.GetRolesCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var roles = ProtoSerializer.Deserialize<List<Role>>(response.ResultData.ToByteArray());
                var roleResponses = roles.Select(r => new RoleResponse(
                    r.Id,
                    r.Name,
                    r.Parents.FirstOrDefault()?.Id,
                    null, // GroupName
                    new List<UserResponse>(), // Users
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                )).ToList();
                var result = new RoleListResponse(roleResponses, roles.Count, command.Page, command.PageSize);
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage ?? "Failed to retrieve roles"));
        });
    }

    public async Task<ApiResponse<RoleResponse>> GetRoleAsync(Service.Infrastructure.GetRoleCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var role = ProtoSerializer.Deserialize<Role>(response.ResultData.ToByteArray());
                var result = new RoleResponse(
                    role.Id,
                    role.Name,
                    role.Parents.FirstOrDefault()?.Id,
                    null, // GroupName
                    new List<UserResponse>(), // Users
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                );
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.NotFound, response.ErrorMessage ?? "Role not found"));
        });
    }

    public async Task<ApiResponse<RoleResponse>> CreateRoleAsync(Service.Infrastructure.CreateRoleCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var role = ProtoSerializer.Deserialize<Role>(response.ResultData.ToByteArray());
                var result = new RoleResponse(
                    role.Id,
                    role.Name,
                    role.Parents.FirstOrDefault()?.Id,
                    null, // GroupName
                    new List<UserResponse>(), // Users
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                );
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage ?? "Failed to create role"));
        });
    }

    public async Task<ApiResponse<RoleResponse>> UpdateRoleAsync(Service.Infrastructure.UpdateRoleCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var role = ProtoSerializer.Deserialize<Role>(response.ResultData.ToByteArray());
                var result = new RoleResponse(
                    role.Id,
                    role.Name,
                    role.Parents.FirstOrDefault()?.Id,
                    null, // GroupName
                    new List<UserResponse>(), // Users
                    new List<PermissionResponse>(), // Permissions
                    DateTime.UtcNow, // CreatedAt - placeholder
                    null // UpdatedAt
                );
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage ?? "Failed to update role"));
        });
    }

    public async Task<ApiResponse<bool>> DeleteRoleAsync(Service.Infrastructure.DeleteRoleCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            return response.Success;
        });
    }

    // Permission operations
    public async Task<ApiResponse<PermissionListResponse>> GetEntityPermissionsAsync(Service.Infrastructure.GetEntityPermissionsCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (response.Success && response.ResultData.Length > 0)
            {
                var permissions = ProtoSerializer.Deserialize<List<Permission>>(response.ResultData.ToByteArray());
                var permissionResponses = permissions.Select(p => new PermissionResponse(
                    command.EntityId, p.Uri, p.HttpVerb.ToString(), p.Grant, p.Deny, p.Scheme.ToString())).ToList();
                var result = new PermissionListResponse(permissionResponses, permissions.Count, command.Page, command.PageSize);
                
                return result;
            }
            
            throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage ?? "Failed to retrieve permissions"));
        });
    }

    public async Task<ApiResponse<bool>> RemovePermissionAsync(Service.Infrastructure.RemovePermissionCommand command)
    {
        return await ExecuteCommandAsync(async (client) =>
        {
            var commandRequest = CreateCommandRequest(command);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            return response.Success;
        });
    }

    private CommandRequest CreateCommandRequest(Service.Infrastructure.WebRequestCommand command)
    {
        var tenantId = _tenantContextService.GetTenantId();
        var serializedCommand = ProtoSerializer.Serialize(command);

        return new CommandRequest
        {
            CommandType = command.GetType().Name,
            CommandData = Google.Protobuf.ByteString.CopyFrom(serializedCommand),
            CorrelationId = command.RequestId
        };
    }


    public async Task<ApiResponse<bool>> AddUserToGroupAsync(AddUserToGroupRequest request)
    {
        return await ExecuteVoidCommandAsync(async client =>
        {
            var addUserToGroupCommand = new Service.Infrastructure.AddUserToGroupCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                request.UserId,
                request.GroupId
            );

            var commandRequest = CreateCommandRequest(addUserToGroupCommand);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (!response.Success)
            {
                throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage));
            }
        });
    }

    public async Task<ApiResponse<bool>> AssignUserToRoleAsync(AssignUserToRoleRequest request)
    {
        return await ExecuteVoidCommandAsync(async client =>
        {
            var assignUserToRoleCommand = new Service.Infrastructure.AssignUserToRoleCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                request.UserId,
                request.RoleId
            );

            var commandRequest = CreateCommandRequest(assignUserToRoleCommand);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (!response.Success)
            {
                throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage));
            }
        });
    }

    public async Task<ApiResponse<bool>> GrantPermissionAsync(GrantPermissionRequest request)
    {
        return await ExecuteVoidCommandAsync(async client =>
        {
            // Parse HTTP verb and scheme
            if (!System.Enum.TryParse<Service.Domain.HttpVerb>(request.HttpVerb, true, out var httpVerb))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid HTTP verb"));
            }
            
            if (!System.Enum.TryParse<Service.Domain.Scheme>(request.Scheme, true, out var scheme))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid scheme"));
            }

            var grantPermissionCommand = new Service.Infrastructure.GrantPermissionCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                request.EntityId,
                request.Uri,
                httpVerb,
                scheme
            );

            var commandRequest = CreateCommandRequest(grantPermissionCommand);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (!response.Success)
            {
                throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage));
            }
        });
    }

    public async Task<ApiResponse<bool>> DenyPermissionAsync(DenyPermissionRequest request)
    {
        return await ExecuteVoidCommandAsync(async client =>
        {
            // Parse HTTP verb and scheme
            if (!System.Enum.TryParse<Service.Domain.HttpVerb>(request.HttpVerb, true, out var httpVerb))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid HTTP verb"));
            }
            
            if (!System.Enum.TryParse<Service.Domain.Scheme>(request.Scheme, true, out var scheme))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid scheme"));
            }

            var denyPermissionCommand = new Service.Infrastructure.DenyPermissionCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                request.EntityId,
                request.Uri,
                httpVerb,
                scheme
            );

            var commandRequest = CreateCommandRequest(denyPermissionCommand);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (!response.Success)
            {
                throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage));
            }
        });
    }

    public async Task<ApiResponse<bool>> AddGroupToGroupAsync(AddGroupToGroupRequest request)
    {
        return await ExecuteVoidCommandAsync(async client =>
        {
            var addGroupToGroupCommand = new Service.Infrastructure.AddGroupToGroupCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                request.ParentGroupId,
                request.ChildGroupId
            );

            var commandRequest = CreateCommandRequest(addGroupToGroupCommand);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (!response.Success)
            {
                throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage));
            }
        });
    }

    public async Task<ApiResponse<bool>> AddRoleToGroupAsync(AddRoleToGroupRequest request)
    {
        return await ExecuteVoidCommandAsync(async client =>
        {
            var addRoleToGroupCommand = new Service.Infrastructure.AddRoleToGroupCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                request.GroupId,
                request.RoleId
            );

            var commandRequest = CreateCommandRequest(addRoleToGroupCommand);
            var response = await client.ExecuteCommandAsync(commandRequest).ResponseAsync;
            
            if (!response.Success)
            {
                throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage));
            }
        });
    }
}