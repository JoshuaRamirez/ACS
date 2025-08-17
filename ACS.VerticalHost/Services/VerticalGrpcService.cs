using ACS.Core.Vertical.V1;
using ACS.Service.Services;
using ACS.Service.Domain;
using ACS.Service.Infrastructure;
using Grpc.Core;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using System.Diagnostics;
using System.Text.Json;

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

            // Get command data as JSON string from Any
            var jsonData = GetJsonFromAny(request.CommandData);

            // Parse and execute command based on type
            var result = request.CommandType switch
            {
                "AddUserToGroup" => await ExecuteAddUserToGroup(jsonData),
                "RemoveUserFromGroup" => await ExecuteRemoveUserFromGroup(jsonData),
                "AssignUserToRole" => await ExecuteAssignUserToRole(jsonData),
                "UnAssignUserFromRole" => await ExecuteUnAssignUserFromRole(jsonData),
                "AddRoleToGroup" => await ExecuteAddRoleToGroup(jsonData),
                "RemoveRoleFromGroup" => await ExecuteRemoveRoleFromGroup(jsonData),
                "AddGroupToGroup" => await ExecuteAddGroupToGroup(jsonData),
                "RemoveGroupFromGroup" => await ExecuteRemoveGroupFromGroup(jsonData),
                "AddPermissionToEntity" => await ExecuteAddPermissionToEntity(jsonData),
                "RemovePermissionFromEntity" => await ExecuteRemovePermissionFromEntity(jsonData),
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

            // Get query data as JSON string from Any
            var jsonData = GetJsonFromAny(request.QueryData);

            // Parse and execute query based on type
            var result = request.QueryType switch
            {
                "CheckPermission" => await ExecuteCheckPermission(jsonData),
                "GetUser" => await ExecuteGetUser(jsonData),
                "GetGroup" => await ExecuteGetGroup(jsonData),
                "GetRole" => await ExecuteGetRole(jsonData),
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

    private string GetJsonFromAny(Any anyData)
    {
        // For now, we'll assume the Any contains a StringValue with JSON data
        // In a full implementation, this would be more sophisticated
        try
        {
            var stringValue = anyData.Unpack<StringValue>();
            return stringValue.Value;
        }
        catch
        {
            // Fallback: try to extract from TypeUrl and Value
            return System.Text.Encoding.UTF8.GetString(anyData.Value.ToByteArray());
        }
    }

    private Any PackJsonResult(object result)
    {
        var json = JsonSerializer.Serialize(result);
        return Any.Pack(new StringValue { Value = json });
    }

    #endregion

    #region Command Execution Methods

    private async Task<bool> ExecuteAddUserToGroup(string jsonData)
    {
        var data = JsonSerializer.Deserialize<AddUserToGroupData>(jsonData);
        var command = new ACS.Service.Services.AddUserToGroupCommand
        {
            UserId = data!.UserId,
            GroupId = data.GroupId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteRemoveUserFromGroup(string jsonData)
    {
        var data = JsonSerializer.Deserialize<RemoveUserFromGroupData>(jsonData);
        var command = new ACS.Service.Services.RemoveUserFromGroupCommand
        {
            UserId = data!.UserId,
            GroupId = data.GroupId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteAssignUserToRole(string jsonData)
    {
        var data = JsonSerializer.Deserialize<AssignUserToRoleData>(jsonData);
        var command = new ACS.Service.Services.AssignUserToRoleCommand
        {
            UserId = data!.UserId,
            RoleId = data.RoleId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteUnAssignUserFromRole(string jsonData)
    {
        var data = JsonSerializer.Deserialize<UnAssignUserFromRoleData>(jsonData);
        var command = new ACS.Service.Services.UnAssignUserFromRoleCommand
        {
            UserId = data!.UserId,
            RoleId = data.RoleId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteAddRoleToGroup(string jsonData)
    {
        var data = JsonSerializer.Deserialize<AddRoleToGroupData>(jsonData);
        var command = new ACS.Service.Services.AddRoleToGroupCommand
        {
            GroupId = data!.GroupId,
            RoleId = data.RoleId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteRemoveRoleFromGroup(string jsonData)
    {
        var data = JsonSerializer.Deserialize<RemoveRoleFromGroupData>(jsonData);
        var command = new ACS.Service.Services.RemoveRoleFromGroupCommand
        {
            GroupId = data!.GroupId,
            RoleId = data.RoleId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteAddGroupToGroup(string jsonData)
    {
        var data = JsonSerializer.Deserialize<AddGroupToGroupData>(jsonData);
        var command = new ACS.Service.Services.AddGroupToGroupCommand
        {
            ParentGroupId = data!.ParentGroupId,
            ChildGroupId = data.ChildGroupId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteRemoveGroupFromGroup(string jsonData)
    {
        var data = JsonSerializer.Deserialize<RemoveGroupFromGroupData>(jsonData);
        var command = new ACS.Service.Services.RemoveGroupFromGroupCommand
        {
            ParentGroupId = data!.ParentGroupId,
            ChildGroupId = data.ChildGroupId
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteAddPermissionToEntity(string jsonData)
    {
        var data = JsonSerializer.Deserialize<AddPermissionToEntityData>(jsonData);
        var permission = new Permission
        {
            Id = data!.Permission.Id,
            Uri = data.Permission.Uri,
            HttpVerb = (HttpVerb)data.Permission.HttpVerb,
            Grant = data.Permission.Grant,
            Deny = data.Permission.Deny,
            Scheme = (Scheme)data.Permission.Scheme
        };
        
        var command = new ACS.Service.Services.AddPermissionToEntityCommand
        {
            EntityId = data.EntityId,
            Permission = permission
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    private async Task<bool> ExecuteRemovePermissionFromEntity(string jsonData)
    {
        var data = JsonSerializer.Deserialize<RemovePermissionFromEntityData>(jsonData);
        var permission = new Permission
        {
            Id = data!.Permission.Id,
            Uri = data.Permission.Uri,
            HttpVerb = (HttpVerb)data.Permission.HttpVerb,
            Grant = data.Permission.Grant,
            Deny = data.Permission.Deny,
            Scheme = (Scheme)data.Permission.Scheme
        };
        
        var command = new ACS.Service.Services.RemovePermissionFromEntityCommand
        {
            EntityId = data.EntityId,
            Permission = permission
        };
        return await _domainService.ExecuteCommandAsync(command);
    }

    #endregion

    #region Query Execution Methods

    private async Task<Any> ExecuteCheckPermission(string jsonData)
    {
        var data = JsonSerializer.Deserialize<CheckPermissionData>(jsonData);
        var command = new ACS.Service.Services.CheckPermissionCommand
        {
            EntityId = data!.EntityId,
            Uri = data.Uri,
            HttpVerb = (HttpVerb)data.HttpVerb
        };
        
        var result = await _domainService.ExecuteCommandAsync(command);
        return Any.Pack(new BoolValue { Value = result });
    }

    private async Task<Any> ExecuteGetUser(string jsonData)
    {
        var data = JsonSerializer.Deserialize<GetUserData>(jsonData);
        var command = new ACS.Service.Services.GetUserCommand
        {
            UserId = data!.UserId
        };
        
        var user = await _domainService.ExecuteCommandAsync(command);
        
        var userData = new UserData
        {
            Id = user.Id,
            Name = user.Name
        };
        
        return PackJsonResult(userData);
    }

    private async Task<Any> ExecuteGetGroup(string jsonData)
    {
        var data = JsonSerializer.Deserialize<GetGroupData>(jsonData);
        var command = new ACS.Service.Services.GetGroupCommand
        {
            GroupId = data!.GroupId
        };
        
        var group = await _domainService.ExecuteCommandAsync(command);
        
        var groupData = new GroupData
        {
            Id = group.Id,
            Name = group.Name
        };
        
        return PackJsonResult(groupData);
    }

    private async Task<Any> ExecuteGetRole(string jsonData)
    {
        var data = JsonSerializer.Deserialize<GetRoleData>(jsonData);
        var command = new ACS.Service.Services.GetRoleCommand
        {
            RoleId = data!.RoleId
        };
        
        var role = await _domainService.ExecuteCommandAsync(command);
        
        var roleData = new RoleData
        {
            Id = role.Id,
            Name = role.Name
        };
        
        return PackJsonResult(roleData);
    }

    #endregion
}

// Simple data transfer objects for JSON serialization
public class AddUserToGroupData
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
}

public class RemoveUserFromGroupData
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
}

public class AssignUserToRoleData
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
}

public class UnAssignUserFromRoleData
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
}

public class AddRoleToGroupData
{
    public int GroupId { get; set; }
    public int RoleId { get; set; }
}

public class RemoveRoleFromGroupData
{
    public int GroupId { get; set; }
    public int RoleId { get; set; }
}

public class AddGroupToGroupData
{
    public int ParentGroupId { get; set; }
    public int ChildGroupId { get; set; }
}

public class RemoveGroupFromGroupData
{
    public int ParentGroupId { get; set; }
    public int ChildGroupId { get; set; }
}

public class PermissionData
{
    public int Id { get; set; }
    public string Uri { get; set; } = string.Empty;
    public int HttpVerb { get; set; }
    public bool Grant { get; set; }
    public bool Deny { get; set; }
    public int Scheme { get; set; }
}

public class AddPermissionToEntityData
{
    public int EntityId { get; set; }
    public PermissionData Permission { get; set; } = null!;
}

public class RemovePermissionFromEntityData
{
    public int EntityId { get; set; }
    public PermissionData Permission { get; set; } = null!;
}

public class CheckPermissionData
{
    public int EntityId { get; set; }
    public string Uri { get; set; } = string.Empty;
    public int HttpVerb { get; set; }
}

public class GetUserData
{
    public int UserId { get; set; }
}

public class GetGroupData
{
    public int GroupId { get; set; }
}

public class GetRoleData
{
    public int RoleId { get; set; }
}

public class UserData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class GroupData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RoleData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}