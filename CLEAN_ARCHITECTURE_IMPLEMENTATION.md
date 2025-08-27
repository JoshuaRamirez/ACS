# Clean Architecture Implementation - API Separation

## Overview

This document outlines the complete clean separation between the HTTP API and business logic layers, enforcing proper architectural boundaries and the command buffer pattern.

## Architecture

### Current State: ❌ Boundary Violations
```
HTTP API ─┬─► IUserService (VIOLATION)
          ├─► IGroupService (VIOLATION) 
          ├─► IRoleService (VIOLATION)
          ├─► IResourceService (VIOLATION)
          ├─► Database Context (VIOLATION)
          └─► Domain Models (VIOLATION)
```

### Clean State: ✅ Proper Separation
```
HTTP API ──► IVerticalHostClient ──► gRPC ──► VerticalHost ──► Command Buffer ──► Business Logic
   │                                                              │
   │                                                              ├─► Domain Services
   │                                                              ├─► In-Memory Graph  
   │                                                              ├─► Normalizers
   │                                                              └─► Database
   │
   └─► ZERO business dependencies
```

## Key Components

### 1. HTTP API Layer (`ACS.WebApi`)

**Role**: Pure HTTP proxy/gateway
**Responsibilities**: 
- HTTP request/response mapping
- Authentication/authorization 
- Input validation
- Routing to VerticalHost

**ONLY Allowed Dependencies**:
- `IVerticalHostClient` - Pure gRPC proxy client
- HTTP infrastructure services (security, telemetry, etc.)
- HTTP contract mapping (no business logic)

**FORBIDDEN Dependencies**:
- ❌ `IUserService`, `IGroupService`, `IRoleService`, `IResourceService`
- ❌ `ApplicationDbContext`
- ❌ Domain models
- ❌ Business logic services
- ❌ Normalizers

### 2. VerticalHost Layer (`ACS.VerticalHost`)

**Role**: Business logic and command processing
**Responsibilities**:
- Command buffer/queue management
- Sequential command processing
- In-memory entity graph
- All business logic
- Database operations
- Domain model operations

**Contains ALL Business Services**:
- ✅ `IUserService`, `IGroupService`, `IRoleService`
- ✅ `ApplicationDbContext`
- ✅ Domain models
- ✅ Normalizers
- ✅ Command buffer
- ✅ In-memory entity graph

## Implementation Details

### HTTP API Controllers (Clean)

```csharp
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IVerticalHostClient _verticalClient; // ONLY dependency
    
    [HttpGet]
    public async Task<ActionResult<GetUsersResourceResponse>> GetUsers([FromQuery] GetUsersResource request)
    {
        // Pure proxy - no business logic
        var response = await _verticalClient.GetUsersAsync(request);
        return Ok(response);
    }
}
```

### VerticalHost Client

```csharp
public class VerticalHostClient : IVerticalHostClient
{
    public async Task<GetUsersResourceResponse> GetUsersAsync(GetUsersResource request, CancellationToken cancellationToken = default)
    {
        var command = new GetUsersCommand
        {
            Page = request.Page,
            PageSize = request.PageSize,
            Search = request.Search
        };

        return await ExecuteCommandAsync<GetUsersCommand, GetUsersResourceResponse>(command, cancellationToken);
    }
}
```

### Command Buffer (VerticalHost)

```csharp
public class CommandBuffer : ICommandBuffer
{
    private readonly Channel<BufferedCommand> _commandChannel;
    
    // Queries execute immediately (fast reads)
    public async Task<TResponse> ExecuteQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        // Direct execution - no queuing
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetService<IQueryHandler<IQuery<TResponse>, TResponse>>();
        return await handler.HandleAsync(query, cancellationToken);
    }
    
    // Commands are buffered and processed sequentially
    public async Task ExecuteCommandAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        await _commandWriter.WriteAsync(new BufferedCommand { Command = command }, cancellationToken);
    }
}
```

### Architectural Boundary Enforcement

```csharp
public static class CleanArchitectureServiceCollectionExtensions
{
    public static IServiceCollection AddHttpProxyServices(this IServiceCollection services)
    {
        // ONLY allowed services
        services.AddScoped<IVerticalHostClient, VerticalHostClient>();
        
        // Explicitly forbidden business services
        services.AddForbiddenServiceDetection();
        
        return services;
    }
}

internal class ArchitecturalBoundaryValidator : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Check for forbidden service registrations
        var violations = CheckForBusinessServiceViolations();
        
        if (violations.Any())
        {
            throw new InvalidOperationException("ARCHITECTURAL BOUNDARY VIOLATION: HTTP API cannot depend on business services");
        }
        
        return Task.CompletedTask;
    }
}
```

## Benefits of Clean Separation

### 1. **Clear Responsibilities**
- HTTP API: Pure gateway (HTTP concerns only)
- VerticalHost: All business logic and data access

### 2. **Performance**
- Command buffer provides sequential processing
- In-memory entity graph for fast queries  
- Fire-and-forget command processing

### 3. **Scalability**
- HTTP API can scale independently
- Multiple HTTP instances can share one VerticalHost
- Command buffer prevents race conditions

### 4. **Maintainability**
- Clear architectural boundaries
- No accidental business logic in HTTP layer
- Enforced at compile time and runtime

### 5. **Testability**
- HTTP layer tests pure proxy behavior
- Business logic tests isolated in VerticalHost
- No mixing of concerns

## Migration Steps

### Phase 1: Create Clean Components ✅
- [x] `IVerticalHostClient` interface and implementation
- [x] `CommandBuffer` with LMAX pattern
- [x] Clean `UsersController_Clean` example
- [x] Architectural boundary enforcement
- [x] Updated `Program_Clean.cs` files

### Phase 2: Replace Existing Controllers
- [ ] Update all controllers to use `IVerticalHostClient` only
- [ ] Remove all business service dependencies
- [ ] Update DI registration

### Phase 3: Testing and Validation
- [ ] Add integration tests for clean separation
- [ ] Performance testing of command buffer
- [ ] Validate architectural boundaries

## Command Flow Example

1. **HTTP Request**: `GET /api/users?page=1&pageSize=20`
2. **HTTP Controller**: `UsersController.GetUsers()` receives request
3. **Proxy Call**: Controller calls `_verticalClient.GetUsersAsync(request)`
4. **gRPC Serialization**: Request serialized to gRPC command
5. **VerticalHost**: Receives gRPC command
6. **Query Processing**: Query executed immediately (no buffering for reads)
7. **Business Logic**: `UserService` processes with in-memory graph
8. **Response**: Results serialized back through gRPC
9. **HTTP Response**: Controller returns mapped response

## Write Command Flow Example

1. **HTTP Request**: `POST /api/users` with user data
2. **HTTP Controller**: `UsersController.CreateUser()` receives request  
3. **Proxy Call**: Controller calls `_verticalClient.CreateUserAsync(request)`
4. **gRPC Serialization**: Command serialized to gRPC
5. **VerticalHost**: Receives gRPC command
6. **Command Buffer**: Command enqueued for sequential processing
7. **Sequential Processing**: Command processed in order
8. **Business Logic**: `UserService` creates user + normalizes
9. **Database Update**: Changes persisted
10. **Response**: Success/failure returned through gRPC
11. **HTTP Response**: Controller returns result

## Monitoring and Health Checks

### HTTP API Health
- VerticalHost connectivity
- gRPC channel health
- Circuit breaker status

### VerticalHost Health  
- Command buffer status
- Entity graph health
- Database connectivity
- Performance metrics

## Configuration

### HTTP API (`appsettings.json`)
```json
{
  "HttpProxy": {
    "EnforceStrictBoundaries": true,
    "DefaultCommandTimeout": "00:00:30",
    "MaxConcurrentCalls": 100
  }
}
```

### VerticalHost Configuration
```json
{
  "CommandBuffer": {
    "ChannelCapacity": 10000,
    "EnableDetailedLogging": false
  },
  "EntityGraph": {
    "PreloadOnStartup": true,
    "RefreshIntervalMinutes": 60
  }
}
```

This clean architecture enforces proper separation of concerns and provides a solid foundation for scalable, maintainable code.