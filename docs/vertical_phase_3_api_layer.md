# Phase 3: WebApi Gateway Transformation

## Overview

Phase 3 transforms ACS.WebApi into a high-performance HTTP gateway that communicates with dedicated ACS.VerticalHost processes via gRPC. This phase implements the complete separation between HTTP processing and business logic processing, with each tenant running in its own isolated process for optimal resource utilization and fault isolation.

**Timeline**: 2 weeks  
**Priority**: Critical  
**Persona**: Lead Developer

## Objectives

- Implement HTTP gateway that routes requests to dedicated tenant processes via gRPC
- Create multi-tenant aware controllers that communicate with ACS.VerticalHost processes
- Build comprehensive error handling and validation in process-per-tenant context
- Establish API documentation and testing framework for distributed architecture
- Optimize request-response patterns for gRPC communication with tenant processes

## Implementation Tasks

### 1. Base Controller Infrastructure

#### 1.1 Create BaseTenantController
**File**: `ACS.WebApi/Controllers/BaseTenantController.cs` (CREATE NEW)

```csharp
[ApiController]
public abstract class BaseTenantController : ControllerBase
{
    protected readonly ILogger _logger;

    protected BaseTenantController(ILogger logger)
    {
        _logger = logger;
    }

    protected ChannelBase GetGrpcChannel()
    {
        var grpcChannel = HttpContext.Items["GrpcChannel"] as ChannelBase;
        if (grpcChannel == null)
        {
            throw new InvalidOperationException("gRPC channel not found in request context");
        }
        return grpcChannel;
    }

    protected TenantProcessDiscoveryService.TenantProcessInfo GetTenantProcessInfo()
    {
        var processInfo = HttpContext.Items["TenantProcessInfo"] as TenantProcessDiscoveryService.TenantProcessInfo;
        if (processInfo == null)
        {
            throw new InvalidOperationException("Tenant process info not found in request context");
        }
        return processInfo;
    }

    protected string GetTenantId()
    {
        var tenantId = HttpContext.Items["TenantId"] as string;
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new InvalidOperationException("Tenant ID not found in request context");
        }
        return tenantId;
    }

    protected string GetRequestId()
    {
        return HttpContext.TraceIdentifier;
    }

    protected string GetCurrentUserId()
    {
        // TODO: Extract from JWT token or authentication context
        // For now, return a placeholder
        return HttpContext.User?.Identity?.Name ?? "anonymous";
    }

    protected async Task<IActionResult> SendCommandAsync<T>(T command) where T : IVerticalCommand
    {
        try
        {
            var grpcChannel = GetGrpcChannel();
            var verticalClient = new VerticalService.VerticalServiceClient(grpcChannel);

            var grpcRequest = new CommandRequest
            {
                TenantId = GetTenantId(),
                CommandId = command.CommandId,
                CommandType = command.CommandType,
                CommandData = Any.Pack(command),
                Timestamp = Timestamp.FromDateTime(command.Timestamp)
            };

            var response = await verticalClient.SubmitCommandAsync(grpcRequest);
            
            if (response.Success)
            {
                _logger.LogDebug("Successfully sent command {CommandType} with ID {CommandId} to tenant process {ProcessId}", 
                    command.CommandType, command.CommandId, GetTenantProcessInfo().ProcessId);
                
                return Accepted(new { CommandId = command.CommandId, Status = "Submitted" });
            }
            else
            {
                _logger.LogWarning("Command {CommandType} with ID {CommandId} failed in tenant process: {Error}", 
                    command.CommandType, command.CommandId, response.ErrorMessage);
                
                return BadRequest(new { Error = response.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command {CommandType} with ID {CommandId} to tenant process", 
                command.CommandType, command.CommandId);
            
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    protected async Task<IActionResult> ExecuteQueryAsync<TRequest, TResponse>(TRequest request) 
        where TRequest : class
        where TResponse : class
    {
        try
        {
            var grpcChannel = GetGrpcChannel();
            var verticalClient = new VerticalService.VerticalServiceClient(grpcChannel);

            var grpcRequest = new QueryRequest
            {
                TenantId = GetTenantId(),
                QueryId = Guid.NewGuid().ToString(),
                QueryType = typeof(TRequest).Name,
                QueryData = Any.Pack(request)
            };

            var response = await verticalClient.ExecuteQueryAsync(grpcRequest);
            
            if (response.Success)
            {
                var result = response.ResultData.Unpack<TResponse>();
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Query {QueryType} failed in tenant process: {Error}", 
                    typeof(TRequest).Name, response.ErrorMessage);
                return BadRequest(new { Error = response.ErrorMessage });
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Query operation failed for tenant {TenantId}: {Error}", 
                GetTenantId(), ex.Message);
            return NotFound(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query for tenant {TenantId}", GetTenantId());
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }
}
```

#### 1.2 Create Request/Response Models
**File**: `ACS.WebApi/Models/ApiModels.cs` (CREATE NEW)

```csharp
// User models
public record CreateUserRequest(string Name);
public record UserResponse(int Id, string Name, DateTime CreatedAt);
public record UserDetailResponse(int Id, string Name, DateTime CreatedAt, 
    List<GroupResponse> Groups, List<RoleResponse> Roles);

// Group models
public record CreateGroupRequest(string Name, int? ParentGroupId = null);
public record GroupResponse(int Id, string Name, DateTime CreatedAt);
public record GroupDetailResponse(int Id, string Name, DateTime CreatedAt,
    List<GroupResponse> ChildGroups, List<GroupResponse> ParentGroups,
    List<UserResponse> Users, List<RoleResponse> Roles);

// Role models
public record CreateRoleRequest(string Name, int? GroupId = null);
public record RoleResponse(int Id, string Name, DateTime CreatedAt);
public record RoleDetailResponse(int Id, string Name, DateTime CreatedAt,
    List<UserResponse> Users, List<GroupResponse> Groups);

// Permission models
public record GrantPermissionRequest(string Uri, HttpVerb Verb, Scheme Scheme);
public record DenyPermissionRequest(string Uri, HttpVerb Verb, Scheme Scheme);
public record PermissionResponse(int Id, string Uri, HttpVerb Verb, Scheme Scheme, 
    bool Grant, bool Deny, DateTime CreatedAt);

// Permission evaluation
public record EvaluatePermissionRequest(int UserId, string Uri, HttpVerb Verb);
public record PermissionEvaluationResponse(bool HasPermission, string Reason, 
    List<PermissionResponse> AppliedPermissions);

// Common responses
public record CommandResponse(string RequestId, string Status);
public record ErrorResponse(string Error, string? Details = null);
```

### 2. Users Controller

#### 2.1 Create UsersController
**File**: `ACS.WebApi/Controllers/UsersController.cs` (CREATE NEW)

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : BaseTenantController
{
    public UsersController(ILogger<UsersController> logger) : base(logger)
    {
    }

    /// <summary>
    /// Get all users for the current tenant
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        return await ExecuteQueryAsync(async () =>
        {
            var tenantServices = GetTenantServices();
            var entityGraph = tenantServices.GetRequiredService<InMemoryEntityGraph>();
            
            var users = entityGraph.Users.Values
                .Select(u => new UserResponse(u.Id, u.Name, DateTime.UtcNow)) // TODO: Add CreatedAt to domain
                .ToList();
            
            return users;
        });
    }

    /// <summary>
    /// Get a specific user by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id)
    {
        return await ExecuteQueryAsync(async () =>
        {
            var tenantServices = GetTenantServices();
            var entityGraph = tenantServices.GetRequiredService<InMemoryEntityGraph>();
            
            var user = entityGraph.GetUser(id);
            
            var groups = user.GroupMemberships
                .Select(g => new GroupResponse(g.Id, g.Name, DateTime.UtcNow))
                .ToList();
            
            var roles = user.RoleMemberships
                .Select(r => new RoleResponse(r.Id, r.Name, DateTime.UtcNow))
                .ToList();
            
            return new UserDetailResponse(user.Id, user.Name, DateTime.UtcNow, groups, roles);
        });
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ErrorResponse("User name is required"));
        }

        var command = new CreateUserCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            Name: request.Name
        );

        return await EnqueueCommandAsync(command);
    }

    /// <summary>
    /// Add a user to a group
    /// </summary>
    [HttpPost("{id:int}/groups/{groupId:int}")]
    public async Task<IActionResult> AddUserToGroup(int id, int groupId)
    {
        var command = new AddUserToGroupCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            TargetUserId: id,
            GroupId: groupId
        );

        return await EnqueueCommandAsync(command);
    }

    /// <summary>
    /// Remove a user from a group
    /// </summary>
    [HttpDelete("{id:int}/groups/{groupId:int}")]
    public async Task<IActionResult> RemoveUserFromGroup(int id, int groupId)
    {
        var command = new RemoveUserFromGroupCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            TargetUserId: id,
            GroupId: groupId
        );

        return await EnqueueCommandAsync(command);
    }

    /// <summary>
    /// Assign a user to a role
    /// </summary>
    [HttpPost("{id:int}/roles/{roleId:int}")]
    public async Task<IActionResult> AssignUserToRole(int id, int roleId)
    {
        var command = new AssignUserToRoleCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            TargetUserId: id,
            RoleId: roleId
        );

        return await EnqueueCommandAsync(command);
    }

    /// <summary>
    /// Get all permissions for a user (with hierarchy resolution)
    /// </summary>
    [HttpGet("{id:int}/permissions")]
    public async Task<IActionResult> GetUserPermissions(int id)
    {
        return await ExecuteQueryAsync(async () =>
        {
            var tenantServices = GetTenantServices();
            var entityGraph = tenantServices.GetRequiredService<InMemoryEntityGraph>();
            
            var user = entityGraph.GetUser(id);
            
            // Use existing domain permission aggregation logic
            var permissions = user.Permissions // This uses the existing Entity.AggregatePermissions() logic
                .Select(p => new PermissionResponse(p.Id, p.Uri, p.HttpVerb, p.Scheme, 
                    p.Grant, p.Deny, DateTime.UtcNow))
                .ToList();
            
            return permissions;
        });
    }

    /// <summary>
    /// Evaluate if a user has a specific permission
    /// </summary>
    [HttpPost("{id:int}/permissions/evaluate")]
    public async Task<IActionResult> EvaluateUserPermission(int id, [FromBody] EvaluatePermissionRequest request)
    {
        return await ExecuteQueryAsync(async () =>
        {
            var tenantServices = GetTenantServices();
            var entityGraph = tenantServices.GetRequiredService<InMemoryEntityGraph>();
            
            var user = entityGraph.GetUser(id);
            
            // Use existing domain permission evaluation logic
            var hasPermission = user.HasPermission(request.Uri, request.Verb);
            
            // TODO: Enhance to provide detailed reasoning
            var appliedPermissions = user.Permissions
                .Where(p => p.Uri == request.Uri && p.HttpVerb == request.Verb)
                .Select(p => new PermissionResponse(p.Id, p.Uri, p.HttpVerb, p.Scheme, 
                    p.Grant, p.Deny, DateTime.UtcNow))
                .ToList();
            
            var reason = hasPermission ? "Permission granted through hierarchy" : "Permission denied or not found";
            
            return new PermissionEvaluationResponse(hasPermission, reason, appliedPermissions);
        });
    }
}
```

### 3. Groups Controller

#### 3.1 Create GroupsController
**File**: `ACS.WebApi/Controllers/GroupsController.cs` (CREATE NEW)

```csharp
[ApiController]
[Route("api/[controller]")]
public class GroupsController : BaseTenantController
{
    public GroupsController(ILogger<GroupsController> logger) : base(logger)
    {
    }

    /// <summary>
    /// Get all groups for the current tenant
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetGroups()
    {
        return await ExecuteQueryAsync(async () =>
        {
            var tenantServices = GetTenantServices();
            var entityGraph = tenantServices.GetRequiredService<InMemoryEntityGraph>();
            
            var groups = entityGraph.Groups.Values
                .Select(g => new GroupResponse(g.Id, g.Name, DateTime.UtcNow))
                .ToList();
            
            return groups;
        });
    }

    /// <summary>
    /// Get a specific group by ID with relationships
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetGroup(int id)
    {
        return await ExecuteQueryAsync(async () =>
        {
            var tenantServices = GetTenantServices();
            var entityGraph = tenantServices.GetRequiredService<InMemoryEntityGraph>();
            
            var group = entityGraph.GetGroup(id);
            
            var childGroups = group.Groups
                .Select(g => new GroupResponse(g.Id, g.Name, DateTime.UtcNow))
                .ToList();
            
            var parentGroups = group.ParentGroups
                .Select(g => new GroupResponse(g.Id, g.Name, DateTime.UtcNow))
                .ToList();
            
            var users = group.Users
                .Select(u => new UserResponse(u.Id, u.Name, DateTime.UtcNow))
                .ToList();
            
            var roles = group.Roles
                .Select(r => new RoleResponse(r.Id, r.Name, DateTime.UtcNow))
                .ToList();
            
            return new GroupDetailResponse(group.Id, group.Name, DateTime.UtcNow,
                childGroups, parentGroups, users, roles);
        });
    }

    /// <summary>
    /// Create a new group
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ErrorResponse("Group name is required"));
        }

        var command = new CreateGroupCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            Name: request.Name,
            ParentGroupId: request.ParentGroupId
        );

        return await EnqueueCommandAsync(command);
    }

    /// <summary>
    /// Add a child group to this group
    /// </summary>
    [HttpPost("{id:int}/groups/{childGroupId:int}")]
    public async Task<IActionResult> AddGroupToGroup(int id, int childGroupId)
    {
        if (id == childGroupId)
        {
            return BadRequest(new ErrorResponse("Cannot add group to itself"));
        }

        var command = new AddGroupToGroupCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            ChildGroupId: childGroupId,
            ParentGroupId: id
        );

        return await EnqueueCommandAsync(command);
    }

    /// <summary>
    /// Get the group hierarchy starting from this group
    /// </summary>
    [HttpGet("{id:int}/hierarchy")]
    public async Task<IActionResult> GetGroupHierarchy(int id)
    {
        return await ExecuteQueryAsync(async () =>
        {
            var tenantServices = GetTenantServices();
            var entityGraph = tenantServices.GetRequiredService<InMemoryEntityGraph>();
            
            var rootGroup = entityGraph.GetGroup(id);
            
            return BuildGroupHierarchy(rootGroup);
        });
    }

    private object BuildGroupHierarchy(Group group)
    {
        return new
        {
            group.Id,
            group.Name,
            Users = group.Users.Select(u => new { u.Id, u.Name }).ToList(),
            Roles = group.Roles.Select(r => new { r.Id, r.Name }).ToList(),
            ChildGroups = group.Groups.Select(BuildGroupHierarchy).ToList()
        };
    }

    /// <summary>
    /// Grant permission to this group
    /// </summary>
    [HttpPost("{id:int}/permissions/grant")]
    public async Task<IActionResult> GrantPermissionToGroup(int id, [FromBody] GrantPermissionRequest request)
    {
        var command = new GrantPermissionCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            EntityId: id,
            Uri: request.Uri,
            Verb: request.Verb,
            Scheme: request.Scheme
        );

        return await EnqueueCommandAsync(command);
    }

    /// <summary>
    /// Deny permission to this group
    /// </summary>
    [HttpPost("{id:int}/permissions/deny")]
    public async Task<IActionResult> DenyPermissionToGroup(int id, [FromBody] DenyPermissionRequest request)
    {
        var command = new DenyPermissionCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            EntityId: id,
            Uri: request.Uri,
            Verb: request.Verb,
            Scheme: request.Scheme
        );

        return await EnqueueCommandAsync(command);
    }
}
```

### 4. Roles Controller

#### 4.1 Create RolesController
**File**: `ACS.WebApi/Controllers/RolesController.cs` (CREATE NEW)

```csharp
[ApiController]
[Route("api/[controller]")]
public class RolesController : BaseTenantController
{
    public RolesController(ILogger<RolesController> logger) : base(logger)
    {
    }

    /// <summary>
    /// Get all roles for the current tenant
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        return await ExecuteQueryAsync(async () =>
        {
            var tenantServices = GetTenantServices();
            var entityGraph = tenantServices.GetRequiredService<InMemoryEntityGraph>();
            
            var roles = entityGraph.Roles.Values
                .Select(r => new RoleResponse(r.Id, r.Name, DateTime.UtcNow))
                .ToList();
            
            return roles;
        });
    }

    /// <summary>
    /// Get a specific role by ID with relationships
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRole(int id)
    {
        return await ExecuteQueryAsync(async () =>
        {
            var tenantServices = GetTenantServices();
            var entityGraph = tenantServices.GetRequiredService<InMemoryEntityGraph>();
            
            var role = entityGraph.GetRole(id);
            
            var users = role.Users
                .Select(u => new UserResponse(u.Id, u.Name, DateTime.UtcNow))
                .ToList();
            
            var groups = role.GroupMemberships
                .Select(g => new GroupResponse(g.Id, g.Name, DateTime.UtcNow))
                .ToList();
            
            return new RoleDetailResponse(role.Id, role.Name, DateTime.UtcNow, users, groups);
        });
    }

    /// <summary>
    /// Create a new role
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ErrorResponse("Role name is required"));
        }

        var command = new CreateRoleCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            Name: request.Name,
            GroupId: request.GroupId
        );

        return await EnqueueCommandAsync(command);
    }

    /// <summary>
    /// Get all permissions for this role
    /// </summary>
    [HttpGet("{id:int}/permissions")]
    public async Task<IActionResult> GetRolePermissions(int id)
    {
        return await ExecuteQueryAsync(async () =>
        {
            var tenantServices = GetTenantServices();
            var entityGraph = tenantServices.GetRequiredService<InMemoryEntityGraph>();
            
            var role = entityGraph.GetRole(id);
            
            var permissions = role.Permissions
                .Select(p => new PermissionResponse(p.Id, p.Uri, p.HttpVerb, p.Scheme, 
                    p.Grant, p.Deny, DateTime.UtcNow))
                .ToList();
            
            return permissions;
        });
    }

    /// <summary>
    /// Grant permission to this role
    /// </summary>
    [HttpPost("{id:int}/permissions/grant")]
    public async Task<IActionResult> GrantPermissionToRole(int id, [FromBody] GrantPermissionRequest request)
    {
        var command = new GrantPermissionCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            EntityId: id,
            Uri: request.Uri,
            Verb: request.Verb,
            Scheme: request.Scheme
        );

        return await EnqueueCommandAsync(command);
    }

    /// <summary>
    /// Deny permission to this role
    /// </summary>
    [HttpPost("{id:int}/permissions/deny")]
    public async Task<IActionResult> DenyPermissionToRole(int id, [FromBody] DenyPermissionRequest request)
    {
        var command = new DenyPermissionCommand(
            RequestId: GetRequestId(),
            Timestamp: DateTime.UtcNow,
            UserId: GetCurrentUserId(),
            EntityId: id,
            Uri: request.Uri,
            Verb: request.Verb,
            Scheme: request.Scheme
        );

        return await EnqueueCommandAsync(command);
    }
}
```

### 5. Application Startup Configuration

#### 5.1 Update Program.cs
**File**: `ACS.WebApi/Program.cs` (Enhance Existing)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "ACS Multi-Tenant Access Control API", 
        Version = "v1",
        Description = "High-performance LMAX-style multi-tenant access control system"
    });
    
    // Include XML comments for API documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Register tenant process infrastructure
builder.Services.AddSingleton<TenantProcessDiscoveryService>();

// Add logging
builder.Services.AddLogging();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ACS API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
    app.UseCors("AllowAll");
}

app.UseHttpsRedirection();

// Add tenant process resolution middleware BEFORE authorization
app.UseMiddleware<TenantProcessResolutionMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Add health check endpoint
app.MapGet("/health", async (IServiceProvider serviceProvider) =>
{
    var tenantProvider = serviceProvider.GetRequiredService<TenantServiceProvider>();
    return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
});

// Add tenant process status endpoint
app.MapGet("/tenants/{tenantId}/status", async (string tenantId, IServiceProvider serviceProvider) =>
{
    try
    {
        var processDiscovery = serviceProvider.GetRequiredService<TenantProcessDiscoveryService>();
        var processInfo = await processDiscovery.GetOrStartTenantProcessAsync(tenantId);
        
        // Get health status from the tenant process via gRPC
        var grpcChannel = GrpcChannel.ForAddress(processInfo.GrpcEndpoint);
        var verticalClient = new VerticalService.VerticalServiceClient(grpcChannel);
        
        var healthRequest = new TenantHealthRequest { TenantId = tenantId };
        var healthResponse = await verticalClient.GetTenantHealthAsync(healthRequest);
        
        return Results.Ok(new 
        { 
            TenantId = tenantId,
            ProcessId = processInfo.ProcessId,
            GrpcEndpoint = processInfo.GrpcEndpoint,
            Status = healthResponse.IsHealthy ? "Healthy" : "Unhealthy",
            StartTime = processInfo.StartTime,
            CommandsProcessed = healthResponse.CommandsProcessed,
            MemoryUsageBytes = healthResponse.MemoryUsageBytes
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error getting tenant process status: {ex.Message}");
    }
});

app.Run();

// Make Program accessible for testing
public partial class Program { }
```

### 6. Error Handling and Validation

#### 6.1 Create Global Exception Handler
**File**: `ACS.WebApi/Middleware/GlobalExceptionMiddleware.cs` (CREATE NEW)

```csharp
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred for request {RequestPath}", 
                context.Request.Path);
            
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            InvalidOperationException => new ErrorResponse(exception.Message),
            ArgumentException => new ErrorResponse(exception.Message),
            UnauthorizedAccessException => new ErrorResponse("Unauthorized access"),
            _ => new ErrorResponse("An internal server error occurred")
        };

        context.Response.StatusCode = exception switch
        {
            InvalidOperationException => 400,
            ArgumentException => 400,
            UnauthorizedAccessException => 401,
            _ => 500
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(jsonResponse);
    }
}
```

## Testing Strategy

### Unit Tests
- **Controllers**: Test request validation, error handling, command generation
- **BaseTenantController**: Test tenant resolution and command enqueueing
- **Models**: Test request/response serialization
- **Middleware**: Test exception handling and tenant resolution

### Integration Tests
- **End-to-End API Flow**: Test complete request processing pipeline
- **Multi-Tenant Isolation**: Verify tenant separation in API calls
- **Performance**: Test API throughput and response times
- **Error Scenarios**: Test various failure modes and error responses

### API Documentation Tests
- **Swagger Generation**: Verify complete API documentation
- **Response Models**: Validate all response schemas
- **Example Requests**: Test documented examples work correctly

## Success Criteria

### Functional Requirements
- ✅ All CRUD operations available for Users, Groups, Roles
- ✅ Permission management and evaluation endpoints functional
- ✅ Multi-tenant isolation maintained at API layer
- ✅ Immediate response pattern maintained (202 Accepted for mutations)
- ✅ Query operations provide real-time data from in-memory graph

### Performance Requirements
- ✅ API can handle 10,000+ requests per second per tenant
- ✅ Response times under 10ms for query operations
- ✅ Command enqueueing completes in under 1ms
- ✅ Memory usage per API request minimal and predictable

### Technical Requirements
- ✅ Comprehensive API documentation via Swagger
- ✅ Proper HTTP status codes and error responses
- ✅ Input validation and sanitization
- ✅ Comprehensive logging and monitoring
- ✅ Thread-safe multi-tenant request handling

## Next Phase Dependencies

Phase 3 completion enables:
- **Phase 4**: Advanced features can utilize complete API surface
- **Phase 5**: Production readiness can build upon stable API layer
- **Phase 6**: Scaling features can leverage established API patterns

## Risk Mitigation

### API Performance
- Load testing with realistic request patterns
- Memory profiling under sustained load
- Response time monitoring and optimization

### Multi-Tenant Security
- Comprehensive testing of tenant isolation
- Validation of authorization patterns
- Security review of tenant resolution logic

### Error Handling
- Testing of various failure scenarios
- Validation of error response consistency
- Monitoring and alerting for error rates