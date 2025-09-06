# ACS Architecture Guide

## Overview

ACS (Access Control System) implements a sophisticated **Vertical Slice Architecture** with **CQRS patterns** and a **Command Buffer** system for enterprise-grade performance and scalability. The system is designed as a multi-tenant, high-performance access control system with real-time capabilities.

## Core Architectural Patterns

### 1. Vertical Slice Architecture

The system is organized into vertical slices rather than traditional layered architecture:

```
HTTP API Layer (ACS.WebApi)
       ↓ gRPC calls
Command Buffer (VerticalHost)
       ↓ Sequential Processing
Business Logic Layer (ACS.Service)
       ↓ Entity Framework
Database Layer (SQL Server)
```

Each vertical slice contains all the functionality for a specific feature, from API endpoint to database persistence.

### 2. CQRS (Command Query Responsibility Segregation)

The system strictly separates read and write operations:

```csharp
// Commands (Write Operations)
public interface ICommand { }
public interface ICommand<TResult> { }

// Queries (Read Operations)  
public interface IQuery<TResult> { }

// Handler Interfaces
public interface ICommandHandler<TCommand> where TCommand : ICommand
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
```

**Example Command Implementation:**
```csharp
public class CreateUserCommand : ICommand<User>
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, User>
{
    public async Task<User> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        // Implementation delegates to business service layer
        var request = new CreateUserRequest { Name = command.Name, CreatedBy = command.CreatedBy };
        var response = await _userService.CreateAsync(request);
        return response.User;
    }
}
```

### 3. Command Buffer Pattern

The system implements a **Command Buffer** inspired by the LMAX Disruptor pattern for sequential command processing:

```csharp
// Located in ACS.VerticalHost.Program.cs
builder.Services.AddSingleton<ICommandBuffer, CommandBuffer>();
```

**Key Characteristics:**
- **Sequential Processing**: All commands processed in order
- **High Throughput**: Channel-based buffering with 10,000 capacity
- **Thread Safety**: Single consumer, multiple producer pattern
- **Telemetry Integration**: Full observability of command processing

**Command Flow:**
```
1. HTTP API receives request
2. Converts to gRPC call to VerticalHost
3. gRPC service enqueues command in buffer
4. Command buffer processes sequentially
5. Handler executes business logic
6. Response flows back through the layers
```

### 4. Multi-Tenant Architecture

Each tenant runs in an isolated **VerticalHost** process:

```bash
# Tenant-specific process startup
ACS.VerticalHost.exe --tenant TenantA --port 50051
ACS.VerticalHost.exe --tenant TenantB --port 50052
```

**Isolation Benefits:**
- Independent scaling per tenant
- Fault isolation (one tenant failure doesn't affect others)
- Tenant-specific configuration and database connections
- Resource management per tenant

## Service Layer Architecture

### Business Logic Layer (ACS.Service)

Contains all domain models, business logic, and service interfaces:

```csharp
// Core Services
IUserService           // User management operations
IGroupService          // Group and hierarchy management  
IRoleService          // Role-based access control
IPermissionEvaluationService // Permission evaluation
ISystemMetricsService  // System diagnostics and metrics
```

**Service Implementation Pattern:**
```csharp
public class UserService : IUserService
{
    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest request)
    {
        // 1. Domain validation
        // 2. Business rule enforcement  
        // 3. Database persistence via UnitOfWork
        // 4. In-memory graph updates
        // 5. Audit logging
    }
}
```

### Data Access Layer

**Entity Framework Core 8** with advanced features:
- **Connection Pooling**: Optimized database connections
- **Encryption Interceptors**: Automatic field-level encryption
- **Performance Interceptors**: Query monitoring and optimization
- **Multi-tenant Database Support**: Tenant-specific connection strings

```csharp
// Database connection configuration per tenant
services.AddDbContextPool<ApplicationDbContext>((serviceProvider, optionsBuilder) =>
{
    ACS.Service.Data.DatabaseConnectionPooling.ConfigureConnectionPooling(
        optionsBuilder, 
        connectionString, 
        tenantId, 
        builder.Configuration);
        
    optionsBuilder.ConfigureEncryptionInterceptors(serviceProvider);
}, poolSize: 128);
```

### In-Memory Entity Graph

High-performance in-memory representation of the domain model:

```csharp
public class InMemoryEntityGraph
{
    public Dictionary<int, User> Users { get; }
    public Dictionary<int, Group> Groups { get; }
    public Dictionary<int, Role> Roles { get; }
    public Dictionary<int, Permission> Permissions { get; }
    
    // Fast lookups and relationship traversal
    public int TotalEntityCount { get; }
    public TimeSpan LoadDuration { get; }
    public long MemoryUsageBytes { get; }
}
```

## Handler Auto-Registration System

The system automatically discovers and registers 67+ handlers using reflection:

```csharp
public static IServiceCollection AddHandlersAutoRegistration(this IServiceCollection services)
{
    var assembly = Assembly.GetExecutingAssembly();
    var handlerTypes = assembly.GetTypes()
        .Where(type => type.IsClass && !type.IsAbstract)
        .Where(type => type.Namespace == "ACS.VerticalHost.Handlers")
        .ToList();

    foreach (var handlerType in handlerTypes)
    {
        var interfaces = handlerType.GetInterfaces();
        foreach (var interfaceType in interfaces)
        {
            if (IsCommandHandlerInterface(interfaceType) || IsQueryHandlerInterface(interfaceType))
            {
                services.AddTransient(interfaceType, handlerType);
            }
        }
    }
}
```

## Request Flow Diagrams

### Command Processing Flow
```
┌─────────────────┐    gRPC     ┌─────────────────┐    Channel    ┌─────────────────┐
│   HTTP API      │ ────────→   │ VerticalHost    │ ───────────→  │ Command Buffer  │
│ (ACS.WebApi)    │             │ gRPC Service    │               │ (Sequential)    │
└─────────────────┘             └─────────────────┘               └─────────────────┘
                                                                           │
┌─────────────────┐    Response ┌─────────────────┐    Handler    ┌───────▼─────────┐
│   HTTP Client   │ ◄────────   │ Business Logic  │ ◄───────────  │ Command Handler │
│                 │             │ (ACS.Service)   │               │ (Auto-registered)│
└─────────────────┘             └─────────────────┘               └─────────────────┘
```

### Query Processing Flow
```
┌─────────────────┐    gRPC     ┌─────────────────┐    Direct     ┌─────────────────┐
│   HTTP API      │ ────────→   │ Query Handler   │ ───────────→  │ Business Service│
│ (Read Request)  │             │ (Fast Path)     │               │ + Entity Graph  │
└─────────────────┘             └─────────────────┘               └─────────────────┘
                                        │                                   │
┌─────────────────┐    Response         │            Cached/Fast   ┌───────▼─────────┐
│   HTTP Client   │ ◄───────────────────┘ ◄─────────────────────── │ In-Memory Data  │
│                 │                                                │ + Performance   │
└─────────────────┘                                                └─────────────────┘
```

## Performance Characteristics

### Command Buffer Metrics
- **Capacity**: 10,000 commands buffered
- **Processing**: Sequential, single-threaded for data consistency
- **Throughput**: ~1,000-5,000 commands/sec depending on complexity
- **Latency**: Sub-millisecond command enqueueing

### Database Performance
- **Connection Pooling**: 128 connections per tenant
- **Query Optimization**: Automatic query plan caching
- **Batch Processing**: Bulk operations for efficiency
- **Health Monitoring**: Real-time connection pool metrics

### Memory Usage
- **Entity Graph**: Loaded on-demand, cached in memory
- **Command Buffer**: Bounded channel prevents memory bloat
- **Service Pooling**: Scoped services for optimal memory usage

## Scaling Considerations

### Horizontal Scaling
- **Multi-Process**: Each tenant in separate VerticalHost process
- **Load Balancing**: HTTP API can route to multiple VerticalHost instances
- **Database Scaling**: Separate databases per tenant or shared with partitioning

### Vertical Scaling
- **Command Buffer Tuning**: Adjust buffer size based on load
- **Connection Pool Optimization**: Scale database connections per tenant load
- **Memory Management**: Entity graph memory usage monitoring

### Observability
- **OpenTelemetry Integration**: Distributed tracing across all layers
- **Metrics Collection**: Command processing rates, database performance, memory usage
- **Health Checks**: Command buffer status, entity graph health, database connectivity

## Deployment Architecture

### Development Environment
```
HTTP API (Port 5000) ──→ VerticalHost (Port 50051) ──→ LocalDB
```

### Production Environment
```
Load Balancer ──→ HTTP API Cluster ──→ VerticalHost Cluster ──→ SQL Server Cluster
     │                    │                     │                       │
     └─ Health Checks     └─ Circuit Breakers  └─ Connection Pooling   └─ AlwaysOn AG
```

### Container Architecture
```yaml
# docker-compose.yml structure
services:
  webapi:
    image: acs-webapi:latest
    environment:
      - VERTICAL_HOST_ENDPOINTS=verticalhost-tenant1:50051,verticalhost-tenant2:50052
  
  verticalhost-tenant1:
    image: acs-verticalhost:latest
    environment:
      - TENANT_ID=tenant1
      - GRPC_PORT=50051
      - BASE_CONNECTION_STRING=Server=sqlserver;Database=ACS_tenant1;...
```

## Security Architecture

### Authentication Flow
```
Client ──→ JWT Token ──→ HTTP API ──→ gRPC (with token) ──→ VerticalHost
   │                        │                                    │
   └─ OAuth2/OIDC          └─ JWT Validation                   └─ Service Auth
```

### Data Protection
- **Field-Level Encryption**: Sensitive data encrypted at rest
- **TLS Everywhere**: All communications encrypted in transit
- **Tenant Isolation**: Complete data separation between tenants

This architecture provides enterprise-grade scalability, performance, and maintainability while maintaining clean separation of concerns and following established architectural patterns.