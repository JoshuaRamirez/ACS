# Phase 3: gRPC Integration & Process Management (FIXED)

This document provides the corrected Phase 3 implementation plan that properly builds on Phase 2's binary serialization and domain-first architecture.

**Timeline**: 1 week  
**Priority**: Critical  
**Dependencies**: Phase 2 binary serialization and domain commands

## Key Corrections from Original Plan

1. **Uses Binary Serialization** - Leverages the protobuf binary serialization from Phase 2
2. **Per-Tenant gRPC Services** - Each tenant process hosts its own gRPC service
3. **Process Lifecycle Management** - Proper start/stop/restart of tenant processes
4. **Command-Based Communication** - Uses DomainCommand infrastructure

## Architecture Overview

```
┌─────────────────┐
│   ACS.WebApi    │
│  (HTTP Gateway) │
└────────┬────────┘
         │ Routes by TenantId
         ▼
┌─────────────────────────────────┐
│   TenantProcessManager          │
│   - Process lifecycle            │
│   - gRPC channel management      │
│   - Health monitoring            │
└────────┬────────────────────────┘
         │ gRPC (Binary)
         ▼
┌─────────────────┐  ┌─────────────────┐
│ VerticalHost    │  │ VerticalHost    │
│ (Tenant1)       │  │ (Tenant2)       │
│ Port: 5001      │  │ Port: 5002      │
└─────────────────┘  └─────────────────┘
```

## Implementation Steps

### Step 1: Define Binary gRPC Contract

**File**: `ACS.Core/Protos/vertical_service.proto`

```protobuf
syntax = "proto3";

package acs.vertical.v1;

service VerticalService {
  // Single command execution endpoint using binary serialization
  rpc ExecuteCommand(CommandRequest) returns (CommandResponse);
  
  // Health check for process monitoring
  rpc HealthCheck(HealthRequest) returns (HealthResponse);
}

message CommandRequest {
  string command_type = 1;  // Type name for deserialization
  bytes command_data = 2;    // Binary protobuf serialized DomainCommand
  string correlation_id = 3; // For request tracking
}

message CommandResponse {
  bool success = 1;
  bytes result_data = 2;     // Binary protobuf serialized result
  string error_message = 3;
  string correlation_id = 4;
}

message HealthRequest {}

message HealthResponse {
  bool healthy = 1;
  int64 uptime_seconds = 2;
  int32 active_connections = 3;
  int64 commands_processed = 4;
}
```

### Step 2: Create TenantProcessManager

**File**: `ACS.Infrastructure/Services/TenantProcessManager.cs`

```csharp
using System.Diagnostics;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.Services;

public class TenantProcessManager
{
    private readonly ILogger<TenantProcessManager> _logger;
    private readonly Dictionary<string, TenantProcess> _processes = new();
    private readonly object _lock = new();
    private int _nextPort = 5001;

    public class TenantProcess
    {
        public string TenantId { get; set; }
        public Process Process { get; set; }
        public int Port { get; set; }
        public GrpcChannel Channel { get; set; }
        public VerticalService.VerticalServiceClient Client { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsHealthy { get; set; }
    }

    public async Task<TenantProcess> StartTenantProcessAsync(string tenantId)
    {
        lock (_lock)
        {
            if (_processes.ContainsKey(tenantId))
            {
                return _processes[tenantId];
            }

            var port = _nextPort++;
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project ACS.VerticalHost -- --tenant {tenantId} --port {port}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Environment =
                    {
                        ["TENANT_ID"] = tenantId,
                        ["GRPC_PORT"] = port.ToString()
                    }
                }
            };

            process.Start();

            // Create gRPC channel
            var channel = GrpcChannel.ForAddress($"http://localhost:{port}");
            var client = new VerticalService.VerticalServiceClient(channel);

            var tenantProcess = new TenantProcess
            {
                TenantId = tenantId,
                Process = process,
                Port = port,
                Channel = channel,
                Client = client,
                StartTime = DateTime.UtcNow,
                IsHealthy = false
            };

            _processes[tenantId] = tenantProcess;

            // Wait for process to be ready
            await WaitForHealthyAsync(tenantProcess);

            _logger.LogInformation("Started tenant process for {TenantId} on port {Port}", 
                tenantId, port);

            return tenantProcess;
        }
    }

    private async Task WaitForHealthyAsync(TenantProcess process, int maxRetries = 30)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var health = await process.Client.HealthCheckAsync(new HealthRequest());
                if (health.Healthy)
                {
                    process.IsHealthy = true;
                    return;
                }
            }
            catch
            {
                // Expected during startup
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Tenant process {process.TenantId} failed to become healthy");
    }

    public async Task StopTenantProcessAsync(string tenantId)
    {
        lock (_lock)
        {
            if (!_processes.TryGetValue(tenantId, out var process))
                return;

            try
            {
                process.Channel?.Dispose();
                
                if (!process.Process.HasExited)
                {
                    process.Process.Kill();
                    process.Process.WaitForExit(5000);
                }

                process.Process.Dispose();
            }
            finally
            {
                _processes.Remove(tenantId);
            }
        }

        _logger.LogInformation("Stopped tenant process for {TenantId}", tenantId);
    }

    public async Task<TenantProcess> GetOrStartProcessAsync(string tenantId)
    {
        lock (_lock)
        {
            if (_processes.TryGetValue(tenantId, out var existing) && existing.IsHealthy)
            {
                return existing;
            }
        }

        return await StartTenantProcessAsync(tenantId);
    }
}
```

### Step 3: Implement gRPC Service in VerticalHost

**File**: `ACS.VerticalHost/Services/VerticalGrpcService.cs`

```csharp
using Grpc.Core;
using ACS.Service.Services;
using Google.Protobuf;

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
        try
        {
            // Deserialize command using binary protobuf
            var commandType = Type.GetType(request.CommandType);
            var command = ProtoSerializer.Deserialize(commandType, request.CommandData);

            // Execute via domain service
            var result = await _domainService.ExecuteCommandAsync((DomainCommand)command);

            // Serialize result using binary protobuf
            var resultData = ProtoSerializer.Serialize(result);

            Interlocked.Increment(ref _commandsProcessed);

            return new CommandResponse
            {
                Success = true,
                ResultData = ByteString.CopyFrom(resultData),
                CorrelationId = request.CorrelationId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {CommandType}", request.CommandType);

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
        return Task.FromResult(new HealthResponse
        {
            Healthy = true,
            UptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds,
            ActiveConnections = 1, // Can be enhanced with real metrics
            CommandsProcessed = _commandsProcessed
        });
    }
}
```

### Step 4: Update WebApi Controllers to Use gRPC

**File**: `ACS.WebApi/Controllers/UsersController.cs` (Example)

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly TenantProcessManager _processManager;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        TenantProcessManager processManager,
        ILogger<UsersController> logger)
    {
        _processManager = processManager;
        _logger = logger;
    }

    [HttpPost("{userId}/groups/{groupId}")]
    public async Task<IActionResult> AddUserToGroup(int userId, int groupId)
    {
        try
        {
            // Get tenant from request context
            var tenantId = GetTenantId();
            
            // Get or start tenant process
            var process = await _processManager.GetOrStartProcessAsync(tenantId);

            // Create command
            var command = new AddUserToGroupCommand 
            { 
                UserId = userId, 
                GroupId = groupId 
            };

            // Serialize command to binary
            var commandData = ProtoSerializer.Serialize(command);

            // Execute via gRPC
            var request = new CommandRequest
            {
                CommandType = command.GetType().AssemblyQualifiedName,
                CommandData = ByteString.CopyFrom(commandData),
                CorrelationId = HttpContext.TraceIdentifier
            };

            var response = await process.Client.ExecuteCommandAsync(request);

            if (response.Success)
            {
                return Ok();
            }

            return StatusCode(500, response.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to group {GroupId}", 
                userId, groupId);
            return StatusCode(500, ex.Message);
        }
    }

    private string GetTenantId()
    {
        // Extract from JWT, header, or route
        return Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";
    }
}
```

### Step 5: Configure VerticalHost for gRPC Hosting

**File**: `ACS.VerticalHost/Program.cs` (Updates)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Get configuration from command line or environment
var tenantId = builder.Configuration["TENANT_ID"] ?? 
    throw new InvalidOperationException("TENANT_ID is required");
    
var grpcPort = int.Parse(builder.Configuration["GRPC_PORT"] ?? "5001");

// Configure gRPC
builder.Services.AddGrpc();
builder.Services.AddSingleton<VerticalGrpcService>();

// Configure Kestrel for gRPC
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(grpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

var app = builder.Build();

// Map gRPC service
app.MapGrpcService<VerticalGrpcService>();

// Initialize domain service
var domainService = app.Services.GetRequiredService<AccessControlDomainService>();
await domainService.LoadEntityGraphAsync();

app.Run();
```

## Testing Strategy

### 1. Process Management Tests
- Test starting/stopping tenant processes
- Test process recovery after crash
- Test multiple concurrent tenant processes

### 2. gRPC Communication Tests
- Test binary serialization roundtrip
- Test command execution via gRPC
- Test error handling and retries

### 3. Performance Tests
- Measure gRPC latency vs direct calls
- Test throughput under load
- Monitor resource usage per tenant

## Migration from Phase 2

1. **No Breaking Changes** - Phase 2 domain services work unchanged
2. **Gradual Rollout** - Can run both modes simultaneously
3. **Fallback Support** - Can disable gRPC and use in-process if needed

## Success Criteria

- [ ] Each tenant runs in isolated process
- [ ] Binary protobuf serialization working
- [ ] Process lifecycle management implemented
- [ ] Health monitoring operational
- [ ] All commands executable via gRPC
- [ ] Performance targets met (<10ms overhead)

## Next Steps (Phase 4)

- Implement process pooling for efficiency
- Add circuit breakers and retry policies
- Implement distributed caching
- Add metrics and monitoring dashboards