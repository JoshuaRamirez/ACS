using ACS.Core.Vertical.V1;
using ACS.Service.Services;
using ACS.Service.Domain;
using ACS.Service.Infrastructure;
using Grpc.Core;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using System.Diagnostics;

namespace ACS.VerticalHost.Services;

public class VerticalGrpcService : VerticalService.VerticalServiceBase
{
    private readonly AccessControlDomainService _domainService;
    private readonly ILogger<VerticalGrpcService> _logger;
    private readonly string _tenantId;
    private long _commandsProcessed = 0;

    public VerticalGrpcService(
        AccessControlDomainService domainService,
        TenantConfiguration tenantConfig,
        ILogger<VerticalGrpcService> logger)
    {
        _domainService = domainService;
        _tenantId = tenantConfig.TenantId;
        _logger = logger;
    }

    public override async Task<CommandResponse> SubmitCommand(CommandRequest request, ServerCallContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var commandId = request.CommandId;
        
        _logger.LogDebug("Processing command {CommandId} of type {CommandType} for tenant {TenantId}", 
            commandId, request.CommandType, request.TenantId);

        try
        {
            // Validate tenant ID
            if (request.TenantId != _tenantId)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, 
                    $"Invalid tenant ID. Expected {_tenantId}, got {request.TenantId}"));
            }

            // Parse and execute command based on type using binary protobuf
            var result = request.CommandType switch
            {
                "AddUserToGroup" => await ExecuteAddUserToGroup(UnpackCommand<ACS.Core.Vertical.V1.AddUserToGroupCommand>(request.CommandData)),
                "AssignUserToRole" => await ExecuteAssignUserToRole(UnpackCommand<ACS.Core.Vertical.V1.AssignUserToRoleCommand>(request.CommandData)),
                "GrantPermission" => await ExecuteGrantPermission(UnpackCommand<ACS.Core.Vertical.V1.GrantPermissionCommand>(request.CommandData)),
                "DenyPermission" => await ExecuteDenyPermission(UnpackCommand<ACS.Core.Vertical.V1.DenyPermissionCommand>(request.CommandData)),
                "AddRoleToGroup" => await ExecuteAddRoleToGroup(UnpackCommand<ACS.Core.Vertical.V1.AddRoleToGroupCommand>(request.CommandData)),
                "AddGroupToGroup" => await ExecuteAddGroupToGroup(UnpackCommand<ACS.Core.Vertical.V1.AddGroupToGroupCommand>(request.CommandData)),
                _ => throw new RpcException(new Status(StatusCode.InvalidArgument, 
                    $"Unknown command type: {request.CommandType}"))
            };

            Interlocked.Increment(ref _commandsProcessed);
            stopwatch.Stop();

            _logger.LogInformation("Successfully processed command {CommandId} in {ElapsedMs}ms", 
                commandId, stopwatch.ElapsedMilliseconds);

            return new CommandResponse
            {
                CommandId = commandId,
                Success = true,
                ResultData = Any.Pack(new BoolValue { Value = result }),
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing command {CommandId}: {ErrorMessage}", 
                commandId, ex.Message);

            return new CommandResponse
            {
                CommandId = commandId,
                Success = false,
                ErrorMessage = ex.Message,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public override async Task<QueryResponse> ExecuteQuery(QueryRequest request, ServerCallContext context)
    {
        var queryId = request.QueryId;
        
        _logger.LogDebug("Processing query {QueryId} of type {QueryType} for tenant {TenantId}", 
            queryId, request.QueryType, request.TenantId);

        try
        {
            // Validate tenant ID
            if (request.TenantId != _tenantId)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, 
                    $"Invalid tenant ID. Expected {_tenantId}, got {request.TenantId}"));
            }

            // Parse and execute query based on type using binary protobuf
            var result = request.QueryType switch
            {
                "CheckPermission" => await ExecuteCheckPermission(UnpackCommand<ACS.Core.Vertical.V1.CheckPermissionQuery>(request.QueryData)),
                "GetUser" => await ExecuteGetUser(UnpackCommand<ACS.Core.Vertical.V1.GetUserQuery>(request.QueryData)),
                "GetGroup" => await ExecuteGetGroup(UnpackCommand<ACS.Core.Vertical.V1.GetGroupQuery>(request.QueryData)),
                "GetRole" => await ExecuteGetRole(UnpackCommand<ACS.Core.Vertical.V1.GetRoleQuery>(request.QueryData)),
                _ => throw new RpcException(new Status(StatusCode.InvalidArgument, 
                    $"Unknown query type: {request.QueryType}"))
            };

            _logger.LogDebug("Successfully processed query {QueryId}", queryId);

            return new QueryResponse
            {
                QueryId = queryId,
                Success = true,
                ResultData = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query {QueryId}: {ErrorMessage}", 
                queryId, ex.Message);

            return new QueryResponse
            {
                QueryId = queryId,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public override Task<TenantHealthResponse> GetTenantHealth(TenantHealthRequest request, ServerCallContext context)
    {
        var memoryUsage = GC.GetTotalMemory(false);
        
        _logger.LogDebug("Health check for tenant {TenantId}", request.TenantId);

        return Task.FromResult(new TenantHealthResponse
        {
            TenantId = _tenantId,
            IsHealthy = true,
            CommandsProcessed = _commandsProcessed,
            MemoryUsageBytes = memoryUsage
        });
    }

    #region Helper Methods

    private T UnpackCommand<T>(Any anyData) where T : IMessage<T>, new()
    {
        return anyData.Unpack<T>();
    }

    private Any PackResult<T>(T result) where T : IMessage
    {
        return Any.Pack(result);
    }

    #endregion

    #region Command Execution Methods

    private async Task<bool> ExecuteAddUserToGroup(ACS.Core.Vertical.V1.AddUserToGroupCommand data)
    {
        var command = new ACS.Service.Services.AddUserToGroupCommand
        {
            UserId = data.UserId,
            GroupId = data.GroupId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteGrantPermission(ACS.Core.Vertical.V1.GrantPermissionCommand data)
    {
        var permission = new Permission
        {
            Uri = data.Uri,
            HttpVerb = System.Enum.Parse<HttpVerb>(data.HttpVerb),
            Grant = true,
            Deny = false,
            Scheme = System.Enum.Parse<Scheme>(data.Scheme)
        };
        
        var command = new ACS.Service.Services.AddPermissionToEntityCommand
        {
            EntityId = data.EntityId,
            Permission = permission
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteDenyPermission(ACS.Core.Vertical.V1.DenyPermissionCommand data)
    {
        var permission = new Permission
        {
            Uri = data.Uri,
            HttpVerb = System.Enum.Parse<HttpVerb>(data.HttpVerb),
            Grant = false,
            Deny = true,
            Scheme = System.Enum.Parse<Scheme>(data.Scheme)
        };
        
        var command = new ACS.Service.Services.AddPermissionToEntityCommand
        {
            EntityId = data.EntityId,
            Permission = permission
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteAssignUserToRole(ACS.Core.Vertical.V1.AssignUserToRoleCommand data)
    {
        var command = new ACS.Service.Services.AssignUserToRoleCommand
        {
            UserId = data.UserId,
            RoleId = data.RoleId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteAddRoleToGroup(ACS.Core.Vertical.V1.AddRoleToGroupCommand data)
    {
        var command = new ACS.Service.Services.AddRoleToGroupCommand
        {
            GroupId = data.GroupId,
            RoleId = data.RoleId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteAddGroupToGroup(ACS.Core.Vertical.V1.AddGroupToGroupCommand data)
    {
        var command = new ACS.Service.Services.AddGroupToGroupCommand
        {
            ParentGroupId = data.ParentGroupId,
            ChildGroupId = data.ChildGroupId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    #endregion

    #region Query Execution Methods

    private async Task<Any> ExecuteCheckPermission(ACS.Core.Vertical.V1.CheckPermissionQuery data)
    {
        var command = new ACS.Service.Services.CheckPermissionCommand
        {
            EntityId = data.EntityId,
            Uri = data.Uri,
            HttpVerb = System.Enum.Parse<HttpVerb>(data.HttpVerb)
        };
        
        var result = await _domainService.ExecuteCommandAsync(command);
        
        var response = new CheckPermissionResult
        {
            HasPermission = result,
            Reason = result ? "Permission granted" : "Permission denied"
        };
        
        return PackResult(response);
    }

    private async Task<Any> ExecuteGetUser(ACS.Core.Vertical.V1.GetUserQuery data)
    {
        var command = new ACS.Service.Services.GetUserCommand
        {
            UserId = data.UserId
        };
        
        var user = await _domainService.ExecuteCommandAsync(command);
        
        var response = new UserResult
        {
            Id = user.Id,
            Name = user.Name,
            GroupId = user.Parents.OfType<Group>().FirstOrDefault()?.Id ?? 0,
            GroupName = user.Parents.OfType<Group>().FirstOrDefault()?.Name ?? "",
            RoleId = user.Parents.OfType<Role>().FirstOrDefault()?.Id ?? 0,
            RoleName = user.Parents.OfType<Role>().FirstOrDefault()?.Name ?? ""
        };
        
        // Add permissions
        foreach (var permission in user.Permissions)
        {
            response.Permissions.Add(new PermissionResult
            {
                Id = permission.Id,
                Uri = permission.Uri,
                HttpVerb = permission.HttpVerb.ToString(),
                Grant = permission.Grant,
                Deny = permission.Deny,
                Scheme = permission.Scheme.ToString()
            });
        }
        
        return PackResult(response);
    }

    private async Task<Any> ExecuteGetGroup(ACS.Core.Vertical.V1.GetGroupQuery data)
    {
        var command = new ACS.Service.Services.GetGroupCommand
        {
            GroupId = data.GroupId
        };
        
        var group = await _domainService.ExecuteCommandAsync(command);
        
        var response = new GroupResult
        {
            Id = group.Id,
            Name = group.Name,
            ParentGroupId = group.Parents.OfType<Group>().FirstOrDefault()?.Id ?? 0,
            ParentGroupName = group.Parents.OfType<Group>().FirstOrDefault()?.Name ?? ""
        };
        
        // Add child group IDs
        foreach (var childGroup in group.Children.OfType<Group>())
        {
            response.ChildGroupIds.Add(childGroup.Id);
        }
        
        // Add user IDs
        foreach (var user in group.Children.OfType<User>())
        {
            response.UserIds.Add(user.Id);
        }
        
        // Add role IDs
        foreach (var role in group.Children.OfType<Role>())
        {
            response.RoleIds.Add(role.Id);
        }
        
        return PackResult(response);
    }

    private async Task<Any> ExecuteGetRole(ACS.Core.Vertical.V1.GetRoleQuery data)
    {
        var command = new ACS.Service.Services.GetRoleCommand
        {
            RoleId = data.RoleId
        };
        
        var role = await _domainService.ExecuteCommandAsync(command);
        
        var response = new RoleResult
        {
            Id = role.Id,
            Name = role.Name,
            GroupId = role.Parents.OfType<Group>().FirstOrDefault()?.Id ?? 0,
            GroupName = role.Parents.OfType<Group>().FirstOrDefault()?.Name ?? ""
        };
        
        // Add user IDs
        foreach (var user in role.Children.OfType<User>())
        {
            response.UserIds.Add(user.Id);
        }
        
        // Add permissions
        foreach (var permission in role.Permissions)
        {
            response.Permissions.Add(new PermissionResult
            {
                Id = permission.Id,
                Uri = permission.Uri,
                HttpVerb = permission.HttpVerb.ToString(),
                Grant = permission.Grant,
                Deny = permission.Deny,
                Scheme = permission.Scheme.ToString()
            });
        }
        
        return PackResult(response);
    }

    #endregion
}

