using ACS.Core.Vertical.V1;
using ACS.WebApi.DTOs;
using Grpc.Net.Client;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf;

namespace ACS.WebApi.Services;

public class TenantGrpcClientService
{
    private readonly ILogger<TenantGrpcClientService> _logger;
    private readonly ITenantContextService _tenantContextService;

    public TenantGrpcClientService(
        ILogger<TenantGrpcClientService> logger,
        ITenantContextService tenantContextService)
    {
        _logger = logger;
        _tenantContextService = tenantContextService;
    }

    public async Task<ApiResponse<T>> ExecuteCommandAsync<T>(Func<VerticalService.VerticalServiceClient, Task<T>> operation)
    {
        var tenantId = string.Empty;
        try
        {
            tenantId = _tenantContextService.GetTenantId();
            var channel = _tenantContextService.GetGrpcChannel();
            
            if (channel == null)
            {
                _logger.LogWarning("gRPC channel not found for tenant {TenantId}", tenantId);
                return new ApiResponse<T>(false, default, $"Tenant process not available for tenant {tenantId}");
            }

            var client = new VerticalService.VerticalServiceClient(channel);
            
            // Execute with retry logic
            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var result = await operation(client);
                    _logger.LogDebug("Successfully executed gRPC command for tenant {TenantId} on attempt {Attempt}", tenantId, attempt);
                    return new ApiResponse<T>(true, result);
                }
                catch (RpcException rpcEx) when (attempt < maxRetries)
                {
                    _logger.LogWarning(rpcEx, "gRPC call failed for tenant {TenantId} on attempt {Attempt}/{MaxRetries}: {Status}", 
                        tenantId, attempt, maxRetries, rpcEx.Status);
                    
                    // Wait before retry
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
                }
            }
            
            // If we get here, all retries failed
            return new ApiResponse<T>(false, default, "gRPC service temporarily unavailable");
        }
        catch (RpcException rpcEx)
        {
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
            
            var queryData = new CheckPermissionQuery
            {
                EntityId = request.EntityId,
                Uri = request.Uri,
                HttpVerb = request.HttpVerb
            };

            var queryRequest = new QueryRequest
            {
                TenantId = tenantId,
                QueryId = Guid.NewGuid().ToString(),
                QueryType = "CheckPermission",
                QueryData = Any.Pack(queryData)
            };

            var response = await client.ExecuteQueryAsync(queryRequest);
            
            if (response.Success && response.ResultData != null)
            {
                var result = response.ResultData.Unpack<CheckPermissionResult>();
                
                return new CheckPermissionResponse(
                    result.HasPermission,
                    request.Uri,
                    request.HttpVerb,
                    request.EntityId,
                    "Entity",
                    result.Reason
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

    private CommandRequest CreateCommandRequest(string commandType, IMessage commandData)
    {
        var tenantId = _tenantContextService.GetTenantId();

        return new CommandRequest
        {
            TenantId = tenantId,
            CommandId = Guid.NewGuid().ToString(),
            CommandType = commandType,
            CommandData = Any.Pack(commandData),
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
        };
    }

    public async Task<ApiResponse<bool>> AddUserToGroupAsync(AddUserToGroupRequest request)
    {
        return await ExecuteVoidCommandAsync(async client =>
        {
            var commandData = new AddUserToGroupCommand
            {
                UserId = request.UserId,
                GroupId = request.GroupId
            };

            var commandRequest = CreateCommandRequest("AddUserToGroup", commandData);
            var response = await client.SubmitCommandAsync(commandRequest);
            
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
            var commandData = new AssignUserToRoleCommand
            {
                UserId = request.UserId,
                RoleId = request.RoleId
            };

            var commandRequest = CreateCommandRequest("AssignUserToRole", commandData);
            var response = await client.SubmitCommandAsync(commandRequest);
            
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
            var commandData = new GrantPermissionCommand
            {
                EntityId = request.EntityId,
                Uri = request.Uri,
                HttpVerb = request.HttpVerb,
                Scheme = request.Scheme
            };

            var commandRequest = CreateCommandRequest("GrantPermission", commandData);
            var response = await client.SubmitCommandAsync(commandRequest);
            
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
            var commandData = new DenyPermissionCommand
            {
                EntityId = request.EntityId,
                Uri = request.Uri,
                HttpVerb = request.HttpVerb,
                Scheme = request.Scheme
            };

            var commandRequest = CreateCommandRequest("DenyPermission", commandData);
            var response = await client.SubmitCommandAsync(commandRequest);
            
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
            var commandData = new AddGroupToGroupCommand
            {
                ParentGroupId = request.ParentGroupId,
                ChildGroupId = request.ChildGroupId
            };

            var commandRequest = CreateCommandRequest("AddGroupToGroup", commandData);
            var response = await client.SubmitCommandAsync(commandRequest);
            
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
            var commandData = new AddRoleToGroupCommand
            {
                GroupId = request.GroupId,
                RoleId = request.RoleId
            };

            var commandRequest = CreateCommandRequest("AddRoleToGroup", commandData);
            var response = await client.SubmitCommandAsync(commandRequest);
            
            if (!response.Success)
            {
                throw new RpcException(new Status(StatusCode.Internal, response.ErrorMessage));
            }
        });
    }
}