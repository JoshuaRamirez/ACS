# Phase 1: Core Vertical Infrastructure Implementation

## Overview

Phase 1 establishes the foundational Vertical architecture infrastructure for multi-tenant ACS. This phase transforms the application from traditional request-response architecture to a high-performance, two-process system with complete tenant isolation and optimal resource utilization.

**Timeline**: 2-3 weeks  
**Priority**: Critical  
**Persona**: Lead Developer

## Objectives

- Implement ACS.VerticalHost worker service for single-tenant processing
- Create gRPC communication contracts between WebApi and VerticalHost instances
- Build process orchestration infrastructure for managing tenant processes
- Create ring buffer using System.Threading.Channels for single-tenant processing
- Establish one dedicated process per tenant with complete isolation
- Build tenant process discovery and routing mechanisms in WebApi gateway
- Create basic in-memory entity graph structure for single tenant per process

## Implementation Tasks

### 1. gRPC Communication Contracts (ACS.Core)

#### 1.1 Create gRPC Service Definition
**File**: `ACS.Core/Protos/vertical.proto` (CREATE NEW)

```protobuf
syntax = "proto3";

package acs.vertical.v1;

import "google/protobuf/any.proto";
import "google/protobuf/timestamp.proto";

option csharp_namespace = "ACS.Core.Vertical.V1";

service VerticalService {
  rpc SubmitCommand(CommandRequest) returns (CommandResponse);
  rpc ExecuteQuery(QueryRequest) returns (QueryResponse);
  rpc GetTenantHealth(TenantHealthRequest) returns (TenantHealthResponse);
}

message CommandRequest {
  string tenant_id = 1;
  string command_id = 2;
  string command_type = 3;
  google.protobuf.Any command_data = 4;
  google.protobuf.Timestamp timestamp = 5;
}

message CommandResponse {
  string command_id = 1;
  bool success = 2;
  google.protobuf.Any result_data = 3;
  string error_message = 4;
  int64 processing_time_ms = 5;
}

message QueryRequest {
  string tenant_id = 1;
  string query_id = 2;
  string query_type = 3;
  google.protobuf.Any query_data = 4;
}

message QueryResponse {
  string query_id = 1;
  bool success = 2;
  google.protobuf.Any result_data = 3;
  string error_message = 4;
}

message TenantHealthRequest {
  string tenant_id = 1;
}

message TenantHealthResponse {
  string tenant_id = 1;
  bool is_healthy = 2;
  int64 commands_processed = 3;
  int64 memory_usage_bytes = 4;
}
```

#### 1.2 Create Command/Query Models
**File**: `ACS.Core/Commands/VerticalCommands.cs` (CREATE NEW)

```csharp
namespace ACS.Core.Commands;

public interface IVerticalCommand
{
    string CommandId { get; }
    string CommandType { get; }
    string TenantId { get; }
    DateTime Timestamp { get; }
}

public abstract class VerticalCommandBase : IVerticalCommand
{
    public string CommandId { get; init; } = Guid.NewGuid().ToString();
    public abstract string CommandType { get; }
    public string TenantId { get; init; } = default!;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public class CreateUserCommand : VerticalCommandBase
{
    public override string CommandType => "CreateUser";
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
}

public class AddUserToGroupCommand : VerticalCommandBase
{
    public override string CommandType => "AddUserToGroup";
    public string UserId { get; init; } = default!;
    public string GroupId { get; init; } = default!;
}
```

### 2. VerticalHost Single-Tenant Worker Service (ACS.VerticalHost)

#### 2.1 Create VerticalHost Program.cs
**File**: `ACS.VerticalHost/Program.cs` (UPDATE)

```csharp
using ACS.Infrastructure;
using ACS.Service.Data;

var builder = Host.CreateApplicationBuilder(args);

// Get tenant ID from command line arguments or environment
var tenantId = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("TENANT_ID");
if (string.IsNullOrEmpty(tenantId))
{
    throw new InvalidOperationException("Tenant ID must be provided as command line argument or TENANT_ID environment variable");
}

// Configure for high-performance single-tenant processing
builder.Services.Configure<ConsoleLifetimeOptions>(options =>
    options.SuppressStatusMessages = true);

// Single-tenant service infrastructure
builder.Services.AddSingleton<TenantConfiguration>(provider => 
    new TenantConfiguration { TenantId = tenantId });

builder.Services.AddSingleton<InMemoryEntityGraph>();
builder.Services.AddSingleton<TenantRingBuffer>();
builder.Services.AddHostedService<TenantAccessControlHostedService>();

// gRPC services
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
    options.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4MB
});

// Tenant-specific database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(GetTenantConnectionString(tenantId)));

var host = builder.Build();

Console.WriteLine($"Starting VerticalHost for tenant: {tenantId}");
await host.RunAsync();

static string GetTenantConnectionString(string tenantId)
{
    var baseConnectionString = Environment.GetEnvironmentVariable("BASE_CONNECTION_STRING") 
        ?? throw new InvalidOperationException("BASE_CONNECTION_STRING environment variable required");
    
    return baseConnectionString.Replace("{TenantId}", tenantId);
}
```

#### 2.2 Create Process Discovery Service for WebApi
**File**: `ACS.Infrastructure/TenantProcessDiscoveryService.cs` (CREATE NEW)

```csharp
public class TenantProcessDiscoveryService : IDisposable
{
    private readonly ConcurrentDictionary<string, TenantProcessInfo> _tenantProcesses = new();
    private readonly ILogger<TenantProcessDiscoveryService> _logger;

    public TenantProcessDiscoveryService(ILogger<TenantProcessDiscoveryService> logger)
    {
        _logger = logger;
    }

    public class TenantProcessInfo
    {
        public string TenantId { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string GrpcEndpoint { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public bool IsHealthy { get; set; } = true;
        public DateTime LastHealthCheck { get; set; }
    }

    public async Task<TenantProcessInfo> GetOrStartTenantProcessAsync(string tenantId)
    {
        if (_tenantProcesses.TryGetValue(tenantId, out var existingProcess))
        {
            // Verify process is still running
            if (IsProcessRunning(existingProcess.ProcessId))
            {
                return existingProcess;
            }
            else
            {
                _logger.LogWarning("Tenant process {ProcessId} for tenant {TenantId} is no longer running", 
                    existingProcess.ProcessId, tenantId);
                _tenantProcesses.TryRemove(tenantId, out _);
            }
        }

        // Start new tenant process
        return await StartTenantProcessAsync(tenantId);
    }

    private async Task<TenantProcessInfo> StartTenantProcessAsync(string tenantId)
    {
        _logger.LogInformation("Starting new process for tenant {TenantId}", tenantId);

        var grpcPort = GetAvailablePort();
        var grpcEndpoint = $"http://localhost:{grpcPort}";

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project ACS.VerticalHost {tenantId} --grpc-port {grpcPort}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(processInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start process for tenant {tenantId}");
        }

        var tenantProcess = new TenantProcessInfo
        {
            TenantId = tenantId,
            ProcessId = process.Id,
            GrpcEndpoint = grpcEndpoint,
            StartTime = DateTime.UtcNow,
            IsHealthy = true,
            LastHealthCheck = DateTime.UtcNow
        };

        _tenantProcesses[tenantId] = tenantProcess;

        // Wait for process to be ready (simplified - should use health checks)
        await Task.Delay(2000);

        _logger.LogInformation("Started process {ProcessId} for tenant {TenantId} on {Endpoint}", 
            process.Id, tenantId, grpcEndpoint);

        return tenantProcess;
    }

    private int GetAvailablePort()
    {
        // Simple port assignment - in production, use more sophisticated port management
        var random = new Random();
        return random.Next(50000, 60000);
    }

    private bool IsProcessRunning(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public async Task<bool> StopTenantProcessAsync(string tenantId)
    {
        if (_tenantProcesses.TryRemove(tenantId, out var processInfo))
        {
            try
            {
                var process = Process.GetProcessById(processInfo.ProcessId);
                process.Kill();
                await process.WaitForExitAsync();
                
                _logger.LogInformation("Stopped process {ProcessId} for tenant {TenantId}", 
                    processInfo.ProcessId, tenantId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping process {ProcessId} for tenant {TenantId}", 
                    processInfo.ProcessId, tenantId);
            }
        }
        return false;
    }

    public void Dispose()
    {
        foreach (var tenantProcess in _tenantProcesses.Values)
        {
            try
            {
                var process = Process.GetProcessById(tenantProcess.ProcessId);
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing tenant process {ProcessId}", tenantProcess.ProcessId);
            }
        }
        _tenantProcesses.Clear();
    }
}
```

#### 1.2 Create Tenant Configuration Model
**File**: `ACS.Service/Infrastructure/TenantConfiguration.cs` (CREATE NEW)

```csharp
public class TenantConfiguration
{
    public string TenantId { get; set; } = string.Empty;
    public string DatabaseConnectionString { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, string> Settings { get; set; } = new();
}

public interface ITenantConfigurationProvider
{
    Task<TenantConfiguration?> GetTenantConfigurationAsync(string tenantId);
    Task<IEnumerable<TenantConfiguration>> GetAllTenantsAsync();
    Task<bool> CreateTenantAsync(TenantConfiguration configuration);
    Task<bool> UpdateTenantAsync(TenantConfiguration configuration);
    Task<bool> DeleteTenantAsync(string tenantId);
}
```

### 2. Ring Buffer Implementation

#### 2.1 Create TenantRingBuffer
**File**: `ACS.Service/Infrastructure/TenantRingBuffer.cs` (CREATE NEW)

```csharp
public class TenantRingBuffer : IDisposable
{
    private readonly Channel<WebRequestCommand> _channel;
    private readonly ILogger<TenantRingBuffer> _logger;

    public TenantRingBuffer(ILogger<TenantRingBuffer> logger)
    {
        _logger = logger;
        
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,  // LMAX single processor
            SingleWriter = false, // Multiple API threads
            AllowSynchronousContinuations = false
        };
        
        _channel = Channel.CreateBounded<WebRequestCommand>(options);
        
        _logger.LogInformation("TenantRingBuffer created with capacity {Capacity}", options.Capacity);
    }

    public async ValueTask<bool> TryEnqueueAsync(WebRequestCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await _channel.Writer.WriteAsync(command, cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Failed to enqueue command - channel is closed");
            return false;
        }
    }

    public IAsyncEnumerable<WebRequestCommand> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public void CompleteWriter()
    {
        _channel.Writer.Complete();
    }

    public void Dispose()
    {
        CompleteWriter();
    }
}
```

#### 2.2 Create Web Request Command Models
**File**: `ACS.Service/Infrastructure/WebRequestCommands.cs` (CREATE NEW)

```csharp
public abstract record WebRequestCommand(string RequestId, DateTime Timestamp, string UserId);

// User management commands
public record CreateUserCommand(string RequestId, DateTime Timestamp, string UserId, 
    string Name) : WebRequestCommand(RequestId, Timestamp, UserId);

public record AddUserToGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId, int GroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record RemoveUserFromGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId, int GroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

// Group management commands
public record CreateGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    string Name, int? ParentGroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record AddGroupToGroupCommand(string RequestId, DateTime Timestamp, string UserId, 
    int ChildGroupId, int ParentGroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

// Role management commands
public record CreateRoleCommand(string RequestId, DateTime Timestamp, string UserId, 
    string Name, int? GroupId) : WebRequestCommand(RequestId, Timestamp, UserId);

public record AssignUserToRoleCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId, int RoleId) : WebRequestCommand(RequestId, Timestamp, UserId);

// Permission management commands
public record GrantPermissionCommand(string RequestId, DateTime Timestamp, string UserId, 
    int EntityId, string Uri, HttpVerb Verb, Scheme Scheme) : WebRequestCommand(RequestId, Timestamp, UserId);

public record DenyPermissionCommand(string RequestId, DateTime Timestamp, string UserId, 
    int EntityId, string Uri, HttpVerb Verb, Scheme Scheme) : WebRequestCommand(RequestId, Timestamp, UserId);

// Query commands (for permission evaluation)
public record EvaluatePermissionCommand(string RequestId, DateTime Timestamp, string UserId, 
    int TargetUserId, string Uri, HttpVerb Verb) : WebRequestCommand(RequestId, Timestamp, UserId);
```

### 3. Single-Threaded Processing Service

#### 3.1 Create TenantAccessControlHostedService
**File**: `ACS.Service/Infrastructure/TenantAccessControlHostedService.cs` (CREATE NEW)

```csharp
public class TenantAccessControlHostedService : BackgroundService
{
    private readonly string _tenantId;
    private readonly TenantRingBuffer _ringBuffer;
    private readonly AccessControlDomainService _domainService;
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ILogger<TenantAccessControlHostedService> _logger;

    public TenantAccessControlHostedService(
        TenantRingBuffer ringBuffer,
        AccessControlDomainService domainService,
        InMemoryEntityGraph entityGraph,
        ILogger<TenantAccessControlHostedService> logger)
    {
        _tenantId = Environment.GetEnvironmentVariable("TENANT_ID") ?? "unknown";
        _ringBuffer = ringBuffer;
        _domainService = domainService;
        _entityGraph = entityGraph;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Access Control Hosted Service for tenant {TenantId}", _tenantId);
        
        // Load entire tenant entity graph into memory
        await _entityGraph.LoadFromDatabaseAsync(cancellationToken);
        
        // Hydrate normalizer collections to reference domain objects
        _entityGraph.HydrateNormalizerReferences();
        
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
        try
        {
            _logger.LogDebug("Processing command {CommandType} with ID {RequestId} for tenant {TenantId}", 
                command.GetType().Name, command.RequestId, _tenantId);
            
            await _domainService.ProcessCommandAsync(command);
            
            _logger.LogDebug("Successfully processed command {RequestId} for tenant {TenantId}", 
                command.RequestId, _tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command {RequestId} for tenant {TenantId}: {Error}", 
                command.RequestId, _tenantId, ex.Message);
            
            // In a production system, you might want to:
            // 1. Send error response back to waiting client
            // 2. Dead letter the command
            // 3. Trigger alerts
            throw;
        }
    }
}
```

### 4. Tenant Process Resolution Middleware

#### 4.1 Create TenantProcessResolutionMiddleware
**File**: `ACS.WebApi/Middleware/TenantProcessResolutionMiddleware.cs` (CREATE NEW)

```csharp
public class TenantProcessResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TenantProcessDiscoveryService _processDiscovery;
    private readonly ILogger<TenantProcessResolutionMiddleware> _logger;
    private readonly ConcurrentDictionary<string, ChannelBase> _grpcChannels = new();

    public TenantProcessResolutionMiddleware(
        RequestDelegate next, 
        TenantProcessDiscoveryService processDiscovery,
        ILogger<TenantProcessResolutionMiddleware> logger)
    {
        _next = next;
        _processDiscovery = processDiscovery;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = ExtractTenantId(context);
        
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("No tenant ID found in request");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Tenant ID is required");
            return;
        }

        try
        {
            // Get or start the tenant process
            var tenantProcess = await _processDiscovery.GetOrStartTenantProcessAsync(tenantId);
            
            // Get or create gRPC channel for the tenant process
            var grpcChannel = GetOrCreateGrpcChannel(tenantProcess.GrpcEndpoint);
            
            // Store tenant information in HttpContext
            context.Items["TenantProcessInfo"] = tenantProcess;
            context.Items["TenantId"] = tenantId;
            context.Items["GrpcChannel"] = grpcChannel;
            
            _logger.LogDebug("Resolved tenant {TenantId} to process {ProcessId} at {Endpoint} for request {RequestPath}", 
                tenantId, tenantProcess.ProcessId, tenantProcess.GrpcEndpoint, context.Request.Path);
            
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving tenant process for {TenantId}", tenantId);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Error resolving tenant process");
        }
    }

    private ChannelBase GetOrCreateGrpcChannel(string endpoint)
    {
        return _grpcChannels.GetOrAdd(endpoint, ep =>
        {
            _logger.LogDebug("Creating new gRPC channel for endpoint {Endpoint}", ep);
            return GrpcChannel.ForAddress(ep);
        });
    }

    private string ExtractTenantId(HttpContext context)
    {
        // Strategy 1: Header-based tenant resolution
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var headerTenantId))
        {
            return headerTenantId.ToString();
        }

        // Strategy 2: Subdomain-based tenant resolution
        var host = context.Request.Host.Host;
        if (host.Contains('.'))
        {
            var subdomain = host.Split('.')[0];
            if (subdomain != "www" && subdomain != "api")
            {
                return subdomain;
            }
        }

        // Strategy 3: URL path-based tenant resolution
        var pathSegments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments?.Length > 1 && pathSegments[0] == "tenants")
        {
            return pathSegments[1];
        }

        // Strategy 4: Query parameter-based tenant resolution
        if (context.Request.Query.TryGetValue("tenantId", out var queryTenantId))
        {
            return queryTenantId.ToString();
        }

        return string.Empty;
    }
}
```

### 5. In-Memory Entity Graph Structure

#### 5.1 Create InMemoryEntityGraph
**File**: `ACS.Service/Infrastructure/InMemoryEntityGraph.cs` (CREATE NEW)

```csharp
public class InMemoryEntityGraph
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<InMemoryEntityGraph> _logger;

    // Master collections of all domain objects
    public Dictionary<int, User> Users { get; private set; } = new();
    public Dictionary<int, Group> Groups { get; private set; } = new();
    public Dictionary<int, Role> Roles { get; private set; } = new();
    public Dictionary<int, Permission> Permissions { get; private set; } = new();

    // Performance metrics
    public DateTime LastLoadTime { get; private set; }
    public TimeSpan LoadDuration { get; private set; }
    public int TotalEntityCount => Users.Count + Groups.Count + Roles.Count;

    public InMemoryEntityGraph(ApplicationDbContext dbContext, ILogger<InMemoryEntityGraph> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LoadFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting entity graph load from database");

        try
        {
            // Load all entities with their relationships
            await LoadUsersAsync(cancellationToken);
            await LoadGroupsAsync(cancellationToken);
            await LoadRolesAsync(cancellationToken);
            await LoadPermissionsAsync(cancellationToken);
            
            // Build relationships between loaded entities
            BuildEntityRelationships();

            LastLoadTime = DateTime.UtcNow;
            LoadDuration = LastLoadTime - startTime;
            
            _logger.LogInformation("Entity graph loaded successfully. " +
                "Users: {UserCount}, Groups: {GroupCount}, Roles: {RoleCount}, " +
                "Load time: {LoadTime}ms", 
                Users.Count, Groups.Count, Roles.Count, LoadDuration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load entity graph from database");
            throw;
        }
    }

    private async Task LoadUsersAsync(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Users.Clear();
        foreach (var user in users)
        {
            var domainUser = new User
            {
                Id = user.Id,
                Name = user.Name
            };
            Users[user.Id] = domainUser;
        }
    }

    private async Task LoadGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = await _dbContext.Groups
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Groups.Clear();
        foreach (var group in groups)
        {
            var domainGroup = new Group
            {
                Id = group.Id,
                Name = group.Name
            };
            Groups[group.Id] = domainGroup;
        }
    }

    private async Task LoadRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Roles.Clear();
        foreach (var role in roles)
        {
            var domainRole = new Role
            {
                Id = role.Id,
                Name = role.Name
            };
            Roles[role.Id] = domainRole;
        }
    }

    private async Task LoadPermissionsAsync(CancellationToken cancellationToken)
    {
        // Load permissions from UriAccess table with proper joins
        var uriAccesses = await _dbContext.UriAccesses
            .Include(ua => ua.Resource)
            .Include(ua => ua.VerbType)
            .Include(ua => ua.PermissionScheme)
                .ThenInclude(ps => ps.SchemeType)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Permissions.Clear();
        foreach (var uriAccess in uriAccesses)
        {
            var permission = new Permission
            {
                Id = uriAccess.Id,
                Uri = uriAccess.Resource.Uri,
                HttpVerb = Enum.Parse<HttpVerb>(uriAccess.VerbType.VerbName),
                Grant = uriAccess.Grant,
                Deny = uriAccess.Deny,
                Scheme = Enum.Parse<Scheme>(uriAccess.PermissionScheme.SchemeType.SchemeName)
            };
            Permissions[permission.Id] = permission;
        }
    }

    private void BuildEntityRelationships()
    {
        // This method will be implemented in Phase 2
        // It builds the Children/Parents relationships in domain objects
        _logger.LogDebug("Building entity relationships - implementation pending for Phase 2");
    }

    public void HydrateNormalizerReferences()
    {
        _logger.LogInformation("Hydrating normalizer references to domain objects");

        // Point all normalizers to the same domain object collections
        AddUserToGroupNormalizer.Users = Users.Values.ToList();
        AddUserToGroupNormalizer.Groups = Groups.Values.ToList();

        AddRoleToGroupNormalizer.Roles = Roles.Values.ToList();
        AddRoleToGroupNormalizer.Groups = Groups.Values.ToList();

        AssignUserToRoleNormalizer.Users = Users.Values.ToList();
        AssignUserToRoleNormalizer.Roles = Roles.Values.ToList();

        // Continue for all 13 normalizers...
        _logger.LogInformation("Normalizer references hydrated successfully");
    }

    public User GetUser(int userId)
    {
        return Users.TryGetValue(userId, out var user) 
            ? user 
            : throw new InvalidOperationException($"User {userId} not found");
    }

    public Group GetGroup(int groupId)
    {
        return Groups.TryGetValue(groupId, out var group) 
            ? group 
            : throw new InvalidOperationException($"Group {groupId} not found");
    }

    public Role GetRole(int roleId)
    {
        return Roles.TryGetValue(roleId, out var role) 
            ? role 
            : throw new InvalidOperationException($"Role {roleId} not found");
    }
}
```

## Testing Strategy

### Unit Tests
- **TenantServiceProvider**: Test tenant creation, caching, disposal
- **TenantRingBuffer**: Test enqueue/dequeue operations, capacity limits
- **TenantResolutionMiddleware**: Test tenant extraction strategies
- **InMemoryEntityGraph**: Test loading and hydration processes

### Integration Tests
- **Multi-tenant isolation**: Verify complete separation between tenants
- **Ring buffer performance**: Validate throughput and latency under load
- **Entity graph loading**: Test with realistic data volumes
- **Service lifecycle**: Test startup/shutdown scenarios

## Success Criteria

### Functional Requirements
- ✅ Multiple tenants can be served simultaneously with complete isolation
- ✅ Ring buffer handles high-frequency request enqueueing without blocking
- ✅ Entity graph loads all domain objects into memory on startup
- ✅ Normalizers successfully reference domain object collections
- ✅ Single-threaded processing maintains data consistency

### Performance Requirements
- ✅ Tenant creation time under 5 seconds for typical entity volumes
- ✅ Ring buffer can handle 10,000+ requests per second
- ✅ Memory usage per tenant predictable and reasonable
- ✅ Entity graph loading time scales linearly with data volume

### Technical Requirements
- ✅ Zero modifications to existing domain models and normalizers
- ✅ Comprehensive logging and monitoring throughout infrastructure
- ✅ Graceful handling of tenant lifecycle (creation, shutdown)
- ✅ Thread safety in multi-tenant environment

## Next Phase Dependencies

Phase 1 completion enables:
- **Phase 2**: Domain integration can build upon established infrastructure
- **Phase 3**: API layer can utilize ring buffer and tenant resolution
- **Phase 4**: Advanced features can leverage high-performance foundation

## Risk Mitigation

### Memory Management
- Monitor entity graph size per tenant
- Implement memory usage alerts
- Plan for entity graph refresh strategies

### Tenant Isolation
- Comprehensive testing of tenant separation
- Validation of service provider isolation
- Testing edge cases in tenant resolution

### Performance Validation
- Early performance testing with synthetic loads
- Memory profiling under various scenarios
- Ring buffer capacity planning and tuning