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
/// Clean architecture VerticalGrpcService that uses CommandBuffer for all CQRS operations.
/// Acts as the gRPC endpoint that receives commands/queries from HTTP API
/// and routes them through the standardized command buffer system.
/// All operations follow pure CQRS patterns (ICommand, ICommand<T>, IQuery<T>).
/// </summary>
public class VerticalGrpcService : VerticalService.VerticalServiceBase
{
    private readonly ICommandBuffer _commandBuffer;
    private readonly ILogger<VerticalGrpcService> _logger;
    private readonly string _tenantId;
    private long _commandsProcessed = 0;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public VerticalGrpcService(
        ICommandBuffer commandBuffer,
        ACS.Service.Infrastructure.TenantConfiguration config,
        ILogger<VerticalGrpcService> logger)
    {
        _commandBuffer = commandBuffer;
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

            byte[] resultData = Array.Empty<byte>();

            using var commandActivity = VerticalHostTelemetryService.StartCommandProcessingActivity(
                request.CommandType, request.CorrelationId);

            // Determine if this is a command or query and whether it has a result using clean CQRS interfaces
            var isQuery = commandType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));
            var isCommandWithResult = commandType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
            var isVoidCommand = commandType.GetInterfaces().Any(i => i == typeof(ICommand));
            
            if (isQuery)
            {
                // Execute query through CommandBuffer
                commandActivity?.SetTag("operation.type", "query");
                commandActivity?.SetTag("command.has_result", true);
                
                var result = await ExecuteQueryThroughBufferAsync((dynamic)command);
                commandActivity?.SetTag("command.result_type", result?.GetType().Name ?? "null");
                
                if (result != null)
                {
                    // Serialize result using binary protobuf
                    resultData = ProtoSerializer.Serialize(result);
                    commandActivity?.SetTag("result.serialized_size", resultData.Length);
                }
            }
            else if (isCommandWithResult)
            {
                // Execute command with result through CommandBuffer
                commandActivity?.SetTag("operation.type", "command_with_result");
                commandActivity?.SetTag("command.has_result", true);
                
                var result = await ExecuteCommandWithResultThroughBufferAsync((dynamic)command);
                commandActivity?.SetTag("command.result_type", result?.GetType().Name ?? "null");
                
                if (result != null)
                {
                    // Serialize result using binary protobuf
                    resultData = ProtoSerializer.Serialize(result);
                    commandActivity?.SetTag("result.serialized_size", resultData.Length);
                }
            }
            else if (isVoidCommand)
            {
                // Execute void command through CommandBuffer
                commandActivity?.SetTag("operation.type", "void_command");
                await ExecuteVoidCommandThroughBufferAsync((dynamic)command);
                commandActivity?.SetTag("command.has_result", false);
            }
            else
            {
                throw new InvalidOperationException($"Command type {commandType.Name} does not implement a recognized CQRS interface (ICommand, ICommand<T>, or IQuery<T>)");
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

    private async Task ExecuteVoidCommandThroughBufferAsync(dynamic command)
    {
        // All void commands go through the CommandBuffer for consistent sequential processing
        await _commandBuffer.ExecuteCommandAsync((ICommand)command);
    }

    private async Task<object> ExecuteCommandWithResultThroughBufferAsync(dynamic command)
    {
        // All commands with results go through the CommandBuffer for consistent sequential processing
        return await _commandBuffer.ExecuteCommandAsync(command);
    }

    private async Task<object> ExecuteQueryThroughBufferAsync(dynamic query)
    {
        // All queries go through the CommandBuffer for consistent processing
        return await _commandBuffer.ExecuteQueryAsync(query);
    }
}