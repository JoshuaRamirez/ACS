# Domain Command Handlers

This document provides a comprehensive overview of the domain command handlers implemented in `AccessControlDomainService` and their corresponding infrastructure commands.

## Overview

The ACS system implements a comprehensive CRUD and relationship management system through domain commands that are processed by the `AccessControlDomainService`. Each command goes through the following pipeline:

1. **Infrastructure Command** → **Domain Command Translation** → **Domain Command Processing** → **Database Persistence** → **Audit Logging**

## Command Categories

### User Management Commands

#### CREATE Operations
- **CreateUserCommand**: Creates a new user with optional group assignment
  - **Telemetry**: `domain.create_user` span with user name, ID, and group association
  - **Infrastructure**: `Infrastructure.CreateUserCommand`
  - **Database**: User record creation via `TenantDatabasePersistenceService`
  - **Audit**: User creation event logging

#### READ Operations
- **GetUserCommand**: Retrieves a single user by ID
  - **Infrastructure**: `Infrastructure.GetUserCommand`
  - **Telemetry**: Tracked in main command processing span
  
- **GetUsersCommand**: Retrieves paginated list of users
  - **Infrastructure**: `Infrastructure.GetUsersCommand`
  - **Pagination**: Page and PageSize parameters

#### UPDATE Operations
- **UpdateUserCommand**: Updates user name
  - **Infrastructure**: `Infrastructure.UpdateUserCommand`
  - **Database**: User record update with retry policy
  - **Audit**: User modification event logging

#### DELETE Operations
- **DeleteUserCommand**: Removes user from system
  - **Infrastructure**: `Infrastructure.DeleteUserCommand`
  - **Business Logic**: Removes all relationships and permissions
  - **Database**: Cascade deletion with relationship cleanup

### Group Management Commands

#### CREATE Operations
- **CreateGroupCommand**: Creates a new group with optional parent group
  - **Infrastructure**: `Infrastructure.CreateGroupCommand`
  - **Hierarchy**: Supports parent-child group relationships
  - **Validation**: Prevents circular references

#### READ Operations
- **GetGroupCommand**: Retrieves a single group by ID
- **GetGroupsCommand**: Retrieves paginated list of groups

#### UPDATE Operations
- **UpdateGroupCommand**: Updates group name
- **AddGroupToGroupCommand**: Establishes parent-child group relationship
- **RemoveGroupFromGroupCommand**: Removes parent-child group relationship

#### DELETE Operations
- **DeleteGroupCommand**: Removes group from system

### Role Management Commands

#### CREATE Operations
- **CreateRoleCommand**: Creates a new role with optional group assignment
  - **Infrastructure**: `Infrastructure.CreateRoleCommand`
  - **Assignment**: Can be created within a specific group

#### READ Operations
- **GetRoleCommand**: Retrieves a single role by ID
- **GetRolesCommand**: Retrieves paginated list of roles

#### UPDATE Operations
- **UpdateRoleCommand**: Updates role name
- **AddRoleToGroupCommand**: Assigns role to a group
- **RemoveRoleFromGroupCommand**: Removes role from a group

#### DELETE Operations
- **DeleteRoleCommand**: Removes role from system

### User-Group Relationship Commands

- **AddUserToGroupCommand**: Adds user to a group
  - **Infrastructure**: `Infrastructure.AddUserToGroupCommand`
  - **Normalizer**: `AddUserToGroupNormalizer` for database sync
  - **Persistence**: `PersistAddUserToGroupAsync`
  - **Audit**: User-group association logging

- **RemoveUserFromGroupCommand**: Removes user from a group
  - **Infrastructure**: `Infrastructure.RemoveUserFromGroupCommand`
  - **Normalizer**: `RemoveUserFromGroupNormalizer`
  - **Persistence**: `PersistRemoveUserFromGroupAsync`

### User-Role Relationship Commands

- **AssignUserToRoleCommand**: Assigns user to a role
  - **Infrastructure**: `Infrastructure.AssignUserToRoleCommand`
  - **Normalizer**: `AssignUserToRoleNormalizer`
  - **Persistence**: `PersistAssignUserToRoleAsync`

- **UnAssignUserFromRoleCommand**: Removes user from a role
  - **Infrastructure**: `Infrastructure.UnAssignUserFromRoleCommand`
  - **Normalizer**: `UnAssignUserFromRoleNormalizer`
  - **Persistence**: `PersistUnAssignUserFromRoleAsync`

### Permission Management Commands

- **AddPermissionToEntityCommand**: Grants or denies permission to an entity
  - **Infrastructure**: `Infrastructure.GrantPermissionCommand` / `Infrastructure.DenyPermissionCommand`
  - **Permission Types**: Grant (allow) or Deny (block) permissions
  - **Schemes**: Different permission schemes (Explicit, Inherited, etc.)

- **RemovePermissionFromEntityCommand**: Removes permission from an entity
  - **Infrastructure**: `Infrastructure.RemovePermissionCommand`
  - **Cleanup**: Removes specific URI/HttpVerb combinations

- **CheckPermissionCommand**: Evaluates permission for an entity
  - **Infrastructure**: `Infrastructure.CheckPermissionCommand` / `Infrastructure.EvaluatePermissionCommand`
  - **Evaluation**: Traverses entity hierarchy for permission resolution
  - **Performance**: Optimized for fast permission checks

- **GetEntityPermissionsCommand**: Retrieves all permissions for an entity
  - **Infrastructure**: `Infrastructure.GetEntityPermissionsCommand`
  - **Pagination**: Supports large permission sets
  - **Entity Types**: Works with Users, Groups, and Roles

## Command Processing Pipeline

### 1. Command Translation
The `CommandTranslationService` translates infrastructure commands to domain commands:
```csharp
Infrastructure.CreateUserCommand → Services.CreateUserCommand
Infrastructure.AddUserToGroupCommand → Services.AddUserToGroupCommand
```

### 2. Domain Command Processing
The `AccessControlDomainService` processes commands through a single-threaded channel:
- **Thread Safety**: Single-threaded processing ensures consistency
- **Retry Policy**: Database operations use exponential backoff retry
- **Telemetry**: Comprehensive OpenTelemetry instrumentation

### 3. Database Persistence
Each command triggers appropriate database operations:
- **Entity Creation**: New records in database tables
- **Relationship Management**: Junction table updates
- **Audit Logging**: Event persistence for compliance

### 4. Normalizer Execution
Domain-first approach with normalizers for database synchronization:
- **AddUserToGroupNormalizer**: Syncs user-group relationships
- **AssignUserToRoleNormalizer**: Syncs user-role assignments
- **Permission Normalizers**: Handle permission inheritance

## Telemetry and Observability

### Activity Sources
- **ACS.DomainService**: Main domain service operations
- **domain.command.{CommandType}**: Individual command processing
- **domain.create_user**: User creation operations

### Key Telemetry Tags
- **command.type**: Type of domain command being processed
- **command.duration_ms**: Processing time in milliseconds
- **command.successful**: Boolean indicating success/failure
- **command.slow**: Flag for commands taking >1000ms
- **tenant.id**: Tenant identifier for multi-tenant correlation
- **user.id**, **group.id**, **role.id**: Entity identifiers
- **error.type**, **error.message**: Exception details

### Performance Monitoring
- **Slow Command Detection**: Commands >1000ms are flagged
- **Error Rate Tracking**: Failed command monitoring
- **Throughput Metrics**: Commands processed per tenant
- **Database Operation Timing**: Persistence operation performance

## Error Handling and Resilience

### Retry Policy
Database operations use Polly retry policy:
- **Retries**: Up to 3 attempts with exponential backoff
- **Exceptions**: Handles `DbUpdateException`, `TimeoutException`, `InvalidOperationException`
- **Backoff**: 2^attempt seconds delay

### Transaction Management
- **Atomic Operations**: Each command processed atomically
- **Rollback Support**: Failed operations don't affect entity graph
- **Consistency**: Entity graph and database kept in sync

### Error Propagation
- **Command Completion**: Uses `TaskCompletionSource` for async completion
- **Exception Handling**: Proper error propagation to callers
- **Logging**: Comprehensive error logging with context

## Thread Safety and Concurrency

### Single-Threaded Processing
- **Channel-Based**: Bounded channel for command queuing
- **Thread Safety**: Single reader ensures no race conditions
- **ID Generation**: Thread-safe atomic increment for entity IDs

### Performance Optimizations
- **Normalizer Refresh**: Optimized to refresh only changed collections
- **Memory Management**: Efficient collection updates
- **Resource Cleanup**: Proper disposal of resources

## Configuration and Deployment

### Environment Variables
- **TENANT_ID**: Used for telemetry correlation
- **BASE_CONNECTION_STRING**: Database connection template

### Dependency Injection
All services registered in DI container:
- **InMemoryEntityGraph**: Singleton for entity management
- **TenantDatabasePersistenceService**: Database operations
- **EventPersistenceService**: Audit logging
- **AccessControlDomainService**: Main command processor

## Usage Examples

### Creating a User with Group Assignment
```csharp
var command = new Infrastructure.CreateUserCommand(
    requestId: Guid.NewGuid().ToString(),
    timestamp: DateTime.UtcNow,
    userId: "admin",
    name: "John Doe"
);
```

### Adding User to Group
```csharp
var command = new Infrastructure.AddUserToGroupCommand(
    requestId: Guid.NewGuid().ToString(),
    timestamp: DateTime.UtcNow,
    userId: "admin",
    targetUserId: 123,
    groupId: 456
);
```

### Permission Evaluation
```csharp
var command = new Infrastructure.CheckPermissionCommand(
    requestId: Guid.NewGuid().ToString(),
    timestamp: DateTime.UtcNow,
    userId: "admin",
    entityId: 123,
    uri: "/api/users",
    httpVerb: "GET"
);
```

## Best Practices

### Command Design
- **Immutable Commands**: Use record types for command definitions
- **Validation**: Validate command parameters before processing
- **Correlation**: Include request IDs for tracing

### Performance
- **Pagination**: Use for large result sets
- **Caching**: Entity graph provides in-memory caching
- **Batch Operations**: Consider for bulk updates

### Security
- **Authorization**: Commands include requesting user context
- **Audit Trail**: All operations logged for compliance
- **Input Validation**: Sanitize and validate all inputs

### Monitoring
- **Telemetry**: Enable OpenTelemetry for observability
- **Alerts**: Set up alerts for error rates and slow commands
- **Dashboards**: Monitor command throughput and performance