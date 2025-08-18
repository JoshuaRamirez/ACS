using Grpc.Core;
using ACS.Core.Grpc;
using ACS.Service.Services;
using ACS.Service.Infrastructure;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using ACS.Infrastructure;
using System.Reflection;

namespace ACS.VerticalHost.Services;

public class VerticalGrpcService : VerticalService.VerticalServiceBase
{
    private readonly AccessControlDomainService _domainService;
    private readonly ILogger<VerticalGrpcService> _logger;
    private readonly string _tenantId;
    private long _commandsProcessed = 0;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public VerticalGrpcService(
        AccessControlDomainService domainService,
        TenantConfiguration config,
        ILogger<VerticalGrpcService> logger)
    {
        _domainService = domainService;
        _tenantId = config.TenantId;
        _logger = logger;
    }

    public override async Task<CommandResponse> ExecuteCommand(
        CommandRequest request, 
        ServerCallContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
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

            if (isVoidCommand)
            {
                // Execute void command
                await _domainService.ExecuteCommandAsync((DomainCommand)command);
            }
            else
            {
                // Execute command with result
                var executeMethod = typeof(AccessControlDomainService)
                    .GetMethods()
                    .Where(m => m.Name == "ExecuteCommandAsync" && m.IsGenericMethodDefinition)
                    .FirstOrDefault();

                if (executeMethod == null)
                {
                    throw new InvalidOperationException("Could not find generic ExecuteCommandAsync method");
                }

                var resultType = commandType.GetGenericArguments()[0];
                var genericMethod = executeMethod.MakeGenericMethod(resultType);
                
                var task = genericMethod.Invoke(_domainService, new[] { command });
                if (task == null)
                {
                    throw new InvalidOperationException("Command execution returned null");
                }

                // Wait for task completion
                await (Task)task;
                
                // Get result from task
                var resultProperty = task.GetType().GetProperty("Result");
                var result = resultProperty?.GetValue(task);
                
                if (result != null)
                {
                    // Serialize result using binary protobuf
                    resultData = ProtoSerializer.Serialize(result);
                }
            }

            Interlocked.Increment(ref _commandsProcessed);
            
            stopwatch.Stop();
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
        var uptime = DateTime.UtcNow - _startTime;
        
        var response = new HealthResponse
        {
            Healthy = true,
            UptimeSeconds = (long)uptime.TotalSeconds,
            ActiveConnections = 1, // Can be enhanced with real metrics
            CommandsProcessed = Interlocked.Read(ref _commandsProcessed)
        };

        _logger.LogDebug("Health check: Uptime={Uptime}s, Commands={Commands}", 
            response.UptimeSeconds, response.CommandsProcessed);

        return Task.FromResult(response);
    }
}