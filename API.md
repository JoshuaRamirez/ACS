# ACS Service Layer API Documentation

## Overview

The ACS Service Layer (`ACS.Service`) provides a comprehensive business logic API that powers the vertical slice architecture. This layer contains all domain models, business services, and data access abstractions used by the command and query handlers in the VerticalHost.

## Core Service Interfaces

### IUserService

Manages all user-related operations including creation, updates, role assignments, and queries.

```csharp
namespace ACS.Service.Services;

public interface IUserService
{
    // User CRUD Operations
    Task<CreateUserResponse> CreateAsync(CreateUserRequest request);
    Task<UpdateUserResponse> UpdateAsync(UpdateUserRequest request);
    Task<DeleteUserResponse> DeleteAsync(DeleteUserRequest request);
    Task<GetUserResponse> GetByIdAsync(GetUserRequest request);
    Task<GetUsersResponse> GetAllAsync(GetUsersRequest request);
    
    // User-Group Relationships
    Task AddUserToGroupAsync(int userId, int groupId, string addedBy);
    Task RemoveUserFromGroupAsync(int userId, int groupId, string removedBy);
    
    // User Authentication
    Task<AuthenticateUserResponse> AuthenticateAsync(AuthenticateUserRequest request);
    Task<ValidateUserResponse> ValidateAsync(ValidateUserRequest request);
}
```

**Request/Response Examples:**

```csharp
// Create User Request
public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateUserResponse
{
    public User? User { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

// Get User Request with Options
public class GetUserRequest
{
    public int UserId { get; set; }
    public bool IncludeRoles { get; set; }
    public bool IncludeGroups { get; set; }
    public bool IncludePermissions { get; set; }
}
```

### IGroupService

Handles hierarchical group management with cycle prevention and relationship management.

```csharp
public interface IGroupService
{
    // Group CRUD Operations
    Task<CreateGroupResponse> CreateAsync(CreateGroupRequest request);
    Task<UpdateGroupResponse> UpdateAsync(UpdateGroupRequest request);
    Task<DeleteGroupResponse> DeleteAsync(DeleteGroupRequest request);
    Task<GetGroupResponse> GetByIdAsync(GetGroupRequest request);
    Task<GetGroupsResponse> GetAllAsync(GetGroupsRequest request);
    
    // Hierarchy Management
    Task AddGroupToParentAsync(int childGroupId, int parentGroupId, string addedBy);
    Task RemoveGroupFromParentAsync(int childGroupId, int parentGroupId, string removedBy);
    Task<List<Group>> GetGroupHierarchyAsync(int groupId, bool includeChildren = true);
    
    // User-Group Operations
    Task AddUserToGroupAsync(int userId, int groupId, string addedBy);
    Task RemoveUserFromGroupAsync(int userId, int groupId, string removedBy);
    Task<List<Group>> GetGroupsByUserAsync(int userId);
    Task<List<User>> GetUsersByGroupAsync(int groupId, bool includeSubgroups = false);
    
    // Role-Group Operations
    Task AssignRoleToGroupAsync(int roleId, int groupId, string assignedBy);
    Task RemoveRoleFromGroupAsync(int roleId, int groupId, string removedBy);
}
```

**Hierarchy Example:**
```csharp
// Get complete group hierarchy
var request = new GetGroupRequest { GroupId = 1 };
var response = await _groupService.GetByIdAsync(request);

// Navigate hierarchy
var parentGroups = response.Group?.ParentGroups;
var childGroups = response.Group?.ChildGroups;
var allUsers = await _groupService.GetUsersByGroupAsync(1, includeSubgroups: true);
```

### IRoleService

Manages role-based access control with permission assignments.

```csharp
public interface IRoleService
{
    // Role CRUD Operations
    Task<CreateRoleResponse> CreateAsync(CreateRoleRequest request);
    Task<UpdateRoleResponse> UpdateAsync(UpdateRoleRequest request);
    Task<DeleteRoleResponse> DeleteAsync(DeleteRoleRequest request);
    Task<GetRoleResponse> GetByIdAsync(GetRoleRequest request);
    Task<GetRolesResponse> GetAllAsync(GetRolesRequest request);
    
    // Permission Management
    Task AssignPermissionToRoleAsync(int roleId, int permissionId, string assignedBy);
    Task RemovePermissionFromRoleAsync(int roleId, int permissionId, string removedBy);
    Task<List<Permission>> GetRolePermissionsAsync(int roleId);
    
    // Role Assignment
    Task AssignRoleToUserAsync(int roleId, int userId, string assignedBy);
    Task RemoveRoleFromUserAsync(int roleId, int userId, string removedBy);
    Task<List<Role>> GetUserRolesAsync(int userId);
}
```

### ISystemMetricsService

Provides system diagnostics, health monitoring, and performance metrics.

```csharp
public interface ISystemMetricsService
{
    /// <summary>
    /// Gets comprehensive system overview including entity counts and health status
    /// </summary>
    Task<SystemOverviewResponse> GetSystemOverviewAsync(SystemOverviewRequest request);
    
    /// <summary>
    /// Gets database migration history and current schema version
    /// </summary>
    Task<MigrationHistoryResponse> GetMigrationHistoryAsync(MigrationHistoryRequest request);
    
    /// <summary>
    /// Gets detailed system diagnostics including performance counters
    /// </summary>
    Task<SystemDiagnosticsResponse> GetSystemDiagnosticsAsync(SystemDiagnosticsRequest request);
}
```

**System Overview Response:**
```csharp
public class SystemOverviewResponse
{
    public int TotalUsers { get; set; }
    public int TotalGroups { get; set; }
    public int TotalRoles { get; set; }
    public int TotalPermissions { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public string SystemVersion { get; set; } = string.Empty;
    public string DatabaseVersion { get; set; } = string.Empty;
    public TimeSpan SystemUptime { get; set; }
    public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
}
```

### IPermissionEvaluationService

High-performance permission evaluation with caching and optimization.

```csharp
public interface IPermissionEvaluationService
{
    // Permission Evaluation
    Task<bool> HasPermissionAsync(int userId, string permission, string? resource = null);
    Task<List<string>> GetUserPermissionsAsync(int userId, string? resource = null);
    Task<PermissionEvaluationResult> EvaluatePermissionAsync(PermissionEvaluationRequest request);
    
    // Bulk Operations
    Task<Dictionary<string, bool>> EvaluateMultiplePermissionsAsync(
        int userId, 
        List<string> permissions, 
        string? resource = null);
    
    // Cache Management
    Task InvalidateUserPermissionsAsync(int userId);
    Task RefreshPermissionCacheAsync();
}
```

**Permission Evaluation Example:**
```csharp
// Simple permission check
bool canEdit = await _permissionService.HasPermissionAsync(userId: 123, permission: "edit_users");

// Resource-specific permission
bool canViewReport = await _permissionService.HasPermissionAsync(
    userId: 123, 
    permission: "view_reports", 
    resource: "financial_reports");

// Bulk permission evaluation for UI
var permissions = new List<string> { "create_users", "edit_users", "delete_users" };
var results = await _permissionService.EvaluateMultiplePermissionsAsync(123, permissions);

// Build UI based on permissions
if (results["create_users"]) ShowCreateButton();
if (results["edit_users"]) ShowEditButton();
if (results["delete_users"]) ShowDeleteButton();
```

## Business Service Implementation Pattern

### Standard Service Structure

```csharp
public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ILogger<UserService> _logger;
    private readonly IAuditService _auditService;

    public UserService(
        IUnitOfWork unitOfWork,
        InMemoryEntityGraph entityGraph,
        ILogger<UserService> logger,
        IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _entityGraph = entityGraph;
        _logger = logger;
        _auditService = auditService;
    }

    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest request)
    {
        try
        {
            // 1. Input validation
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("User name is required", nameof(request.Name));

            // 2. Business rule validation
            var existingUser = await _unitOfWork.Users.GetByEmailAsync(request.Email);
            if (existingUser != null)
                throw new InvalidOperationException($"User with email {request.Email} already exists");

            // 3. Create domain entity
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // 4. Persist to database
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CompleteAsync();

            // 5. Update in-memory graph
            _entityGraph.AddUser(user);

            // 6. Audit logging
            await _auditService.LogAsync(new AuditEvent
            {
                Action = "CreateUser",
                EntityType = "User",
                EntityId = user.Id,
                UserId = request.CreatedBy,
                Details = $"Created user: {user.Name}"
            });

            _logger.LogInformation("User {UserId} created successfully by {CreatedBy}", 
                user.Id, request.CreatedBy);

            return new CreateUserResponse { User = user, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {UserName} by {CreatedBy}", 
                request.Name, request.CreatedBy);
            
            return new CreateUserResponse 
            { 
                Success = false, 
                ErrorMessage = ex.Message 
            };
        }
    }
}
```

## Caching Strategies

### In-Memory Entity Graph

The system maintains a high-performance in-memory representation of the domain model:

```csharp
public class InMemoryEntityGraph
{
    // Core Entity Collections
    public ConcurrentDictionary<int, User> Users { get; private set; }
    public ConcurrentDictionary<int, Group> Groups { get; private set; }
    public ConcurrentDictionary<int, Role> Roles { get; private set; }
    public ConcurrentDictionary<int, Permission> Permissions { get; private set; }

    // Relationship Indexes for Fast Lookups
    private readonly ConcurrentDictionary<int, HashSet<int>> _userGroups;
    private readonly ConcurrentDictionary<int, HashSet<int>> _userRoles;
    private readonly ConcurrentDictionary<int, HashSet<int>> _groupRoles;
    private readonly ConcurrentDictionary<int, HashSet<int>> _rolePermissions;

    // Performance Metrics
    public int TotalEntityCount => Users.Count + Groups.Count + Roles.Count + Permissions.Count;
    public DateTime LastLoadTime { get; private set; }
    public TimeSpan LoadDuration { get; private set; }
    public long MemoryUsageBytes { get; private set; }

    // Fast Relationship Queries
    public List<Group> GetUserGroups(int userId) => 
        _userGroups.GetValueOrDefault(userId, new HashSet<int>())
                  .Select(groupId => Groups.GetValueOrDefault(groupId))
                  .Where(group => group != null)
                  .ToList()!;

    public List<Permission> GetUserPermissions(int userId)
    {
        var userRoles = GetUserRoles(userId);
        var groupRoles = GetUserGroups(userId).SelectMany(g => GetGroupRoles(g.Id));
        var allRoles = userRoles.Concat(groupRoles).Distinct();
        
        return allRoles.SelectMany(role => GetRolePermissions(role.Id))
                      .Distinct()
                      .ToList();
    }
}
```

### Cache Usage Patterns

```csharp
// Service layer leverages entity graph for fast reads
public class UserService : IUserService
{
    public async Task<GetUserResponse> GetByIdAsync(GetUserRequest request)
    {
        // 1. Try in-memory graph first (sub-millisecond lookup)
        if (_entityGraph.Users.TryGetValue(request.UserId, out var cachedUser))
        {
            _logger.LogDebug("User {UserId} found in entity graph", request.UserId);
            return new GetUserResponse { User = cachedUser, Success = true };
        }

        // 2. Fallback to database if not in memory
        _logger.LogDebug("User {UserId} not in entity graph, querying database", request.UserId);
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId);
        
        if (user != null)
        {
            // 3. Update entity graph for future requests
            _entityGraph.AddUser(user);
        }

        return new GetUserResponse { User = user, Success = user != null };
    }
}
```

## Database Connection Pooling

### Advanced Connection Pool Configuration

```csharp
// Located: ACS.Service.Data.DatabaseConnectionPooling.cs
public static class DatabaseConnectionPooling
{
    public static void ConfigureConnectionPooling(
        DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        string tenantId,
        IConfiguration configuration)
    {
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            // Retry Policy
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: configuration.GetValue<int>("Database:MaxRetryCount", 5),
                maxRetryDelay: TimeSpan.FromSeconds(configuration.GetValue<int>("Database:MaxRetryDelaySeconds", 30)),
                errorNumbersToAdd: null);

            // Command Timeout
            sqlOptions.CommandTimeout(configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30));

            // Batch Processing Optimization
            sqlOptions.MinBatchSize(configuration.GetValue<int>("Database:MinBatchSize", 2));
            sqlOptions.MaxBatchSize(configuration.GetValue<int>("Database:MaxBatchSize", 100));
        });

        // Configure performance interceptors
        optionsBuilder.AddInterceptors(new DatabasePerformanceInterceptor(tenantId));
        
        // Enable sensitive data logging only in development
        if (configuration.GetValue<bool>("Logging:EnableSensitiveDataLogging"))
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }
    }
}
```

### Pool Size Configuration

```csharp
// In VerticalHost Program.cs
services.AddDbContextPool<ApplicationDbContext>((serviceProvider, optionsBuilder) =>
{
    DatabaseConnectionPooling.ConfigureConnectionPooling(
        optionsBuilder, 
        connectionString, 
        tenantId, 
        builder.Configuration);
        
    optionsBuilder.ConfigureEncryptionInterceptors(serviceProvider);
}, 
poolSize: builder.Configuration.GetValue<int>("Database:DbContextPoolSize", 128));
```

**Connection Pool Metrics:**
- **Default Pool Size**: 128 connections per tenant
- **Connection Lifetime**: Managed by Entity Framework
- **Retry Policy**: 5 retries with exponential backoff
- **Command Timeout**: 30 seconds default
- **Batch Optimization**: 2-100 commands per batch

## Health Check Endpoints

### Service Health Monitoring

```csharp
// Command Buffer Health Check
public class CommandBufferHealthCheck : IHealthCheck
{
    private readonly ICommandBuffer _commandBuffer;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            var stats = _commandBuffer.GetStats();
            
            var data = new Dictionary<string, object>
            {
                ["uptime_seconds"] = stats.UptimeSeconds,
                ["commands_processed"] = stats.CommandsProcessed,
                ["queries_processed"] = stats.QueriesProcessed,
                ["commands_in_flight"] = stats.CommandsInFlight,
                ["commands_per_second"] = stats.CommandsPerSecond,
                ["queries_per_second"] = stats.QueriesPerSecond,
                ["channel_usage"] = stats.ChannelUsage,
                ["channel_capacity"] = stats.ChannelCapacity,
                ["recent_errors"] = stats.RecentErrors.Count
            };
            
            var isHealthy = stats.CommandsInFlight < stats.ChannelCapacity * 0.9;
            
            return isHealthy 
                ? HealthCheckResult.Healthy("Command buffer operating normally", data)
                : HealthCheckResult.Degraded("Command buffer under high load", null, data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Command buffer health check failed", ex);
        }
    }
}
```

### Entity Graph Health Check

```csharp
public class InMemoryEntityGraphHealthCheck : IHealthCheck
{
    private readonly InMemoryEntityGraph _entityGraph;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                ["total_entities"] = _entityGraph.TotalEntityCount,
                ["users"] = _entityGraph.Users.Count,
                ["groups"] = _entityGraph.Groups.Count,
                ["roles"] = _entityGraph.Roles.Count,
                ["permissions"] = _entityGraph.Permissions.Count,
                ["last_load_time"] = _entityGraph.LastLoadTime,
                ["load_duration_ms"] = _entityGraph.LoadDuration.TotalMilliseconds,
                ["memory_usage_mb"] = _entityGraph.MemoryUsageBytes / 1024.0 / 1024.0
            };
            
            var isHealthy = _entityGraph.TotalEntityCount > 0;
            
            return isHealthy 
                ? HealthCheckResult.Healthy("Entity graph loaded and operational", data)
                : HealthCheckResult.Unhealthy("Entity graph not loaded", null, data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Entity graph health check failed", ex);
        }
    }
}
```

### Health Endpoint Usage

```bash
# Check overall system health
curl http://localhost:5000/health

# Response example:
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "command_buffer": {
      "status": "Healthy",
      "description": "Command buffer operating normally",
      "data": {
        "uptime_seconds": 3600,
        "commands_processed": 15420,
        "commands_per_second": 4.28,
        "commands_in_flight": 3,
        "channel_usage": 0.03,
        "channel_capacity": 10000
      }
    },
    "entity_graph": {
      "status": "Healthy", 
      "description": "Entity graph loaded and operational",
      "data": {
        "total_entities": 50000,
        "users": 25000,
        "groups": 5000,
        "roles": 500,
        "permissions": 19500,
        "memory_usage_mb": 145.2
      }
    }
  }
}
```

## Performance Patterns

### Batch Processing

```csharp
public class BatchUserService : IUserService
{
    public async Task<BulkCreateUsersResponse> CreateBulkAsync(BulkCreateUsersRequest request)
    {
        var users = new List<User>();
        var results = new List<CreateUserResult>();

        // Process in batches to avoid memory issues
        const int batchSize = 100;
        for (int i = 0; i < request.Users.Count; i += batchSize)
        {
            var batch = request.Users.Skip(i).Take(batchSize);
            
            foreach (var userRequest in batch)
            {
                try
                {
                    var user = new User
                    {
                        Name = userRequest.Name,
                        Email = userRequest.Email,
                        CreatedBy = request.CreatedBy,
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    users.Add(user);
                    results.Add(new CreateUserResult { Success = true, User = user });
                }
                catch (Exception ex)
                {
                    results.Add(new CreateUserResult 
                    { 
                        Success = false, 
                        ErrorMessage = ex.Message,
                        UserRequest = userRequest
                    });
                }
            }

            // Bulk insert batch
            await _unitOfWork.Users.AddRangeAsync(users);
            await _unitOfWork.CompleteAsync();

            // Update entity graph in batch
            _entityGraph.AddUsers(users);
            
            users.Clear(); // Clear for next batch
        }

        return new BulkCreateUsersResponse { Results = results };
    }
}
```

### Optimized Query Patterns

```csharp
// Efficient pagination with total count
public async Task<GetUsersResponse> GetAllAsync(GetUsersRequest request)
{
    var query = _unitOfWork.Users.Query()
        .Where(u => u.IsActive || request.IncludeInactive);

    // Apply search filter
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
        query = query.Where(u => u.Name.Contains(request.Search) || 
                                u.Email.Contains(request.Search));
    }

    // Get total count before pagination
    var totalCount = await query.CountAsync();

    // Apply pagination
    var users = await query
        .OrderBy(u => u.Name)
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .ToListAsync();

    return new GetUsersResponse
    {
        Users = users,
        TotalCount = totalCount,
        Page = request.Page,
        PageSize = request.PageSize,
        TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
    };
}
```

This service layer API provides comprehensive business logic capabilities with enterprise-grade performance, caching, and monitoring features.