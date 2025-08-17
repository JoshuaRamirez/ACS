# Phase 2: Domain Integration & Hydration for Vertical Architecture

## Overview

Phase 2 integrates the existing sophisticated domain models with the Vertical architecture infrastructure established in Phase 1. This phase implements the domain-first hydration strategy where normalizers reference the same domain object collections, eliminating duplication and ensuring perfect consistency in the ACS.VerticalHost process.

**Timeline**: 2 weeks  
**Priority**: Critical  
**Persona**: Lead Developer

## Objectives

- Implement domain-first hydration strategy for single-tenant processes
- Adapt existing normalizers to reference domain object collections within tenant process
- Complete in-memory entity graph loading with relationship building for single tenant
- Create service layer facade for domain operations within ACS.VerticalHost
- Integrate database persistence for audit and recovery per tenant process

## Implementation Tasks

### 1. Enhanced In-Memory Entity Graph

#### 1.1 Complete Entity Relationship Building
**File**: `ACS.Service/Infrastructure/InMemoryEntityGraph.cs` (Enhance Existing)

Add relationship building to the existing `BuildEntityRelationships()` method:

```csharp
private async Task BuildEntityRelationships()
{
    _logger.LogInformation("Building entity relationships from database");

    // Load user-group relationships
    await BuildUserGroupRelationships();
    
    // Load user-role relationships  
    await BuildUserRoleRelationships();
    
    // Load group-role relationships
    await BuildGroupRoleRelationships();
    
    // Load group-group hierarchical relationships
    await BuildGroupHierarchyRelationships();
    
    // Load entity permissions
    await BuildEntityPermissionRelationships();
    
    _logger.LogInformation("Entity relationships built successfully");
}

private async Task BuildUserGroupRelationships()
{
    // Load from User table where GroupId is not null
    var userGroupMappings = await _dbContext.Users
        .Where(u => u.GroupId.HasValue)
        .Select(u => new { UserId = u.Id, GroupId = u.GroupId.Value })
        .AsNoTracking()
        .ToListAsync();

    foreach (var mapping in userGroupMappings)
    {
        if (Users.TryGetValue(mapping.UserId, out var user) && 
            Groups.TryGetValue(mapping.GroupId, out var group))
        {
            // Build bidirectional relationship using domain object collections
            user.Parents.Add(group);
            group.Children.Add(user);
        }
    }
    
    _logger.LogDebug("Built {Count} user-group relationships", userGroupMappings.Count);
}

private async Task BuildUserRoleRelationships()
{
    // Load from User table where RoleId is not null
    var userRoleMappings = await _dbContext.Users
        .Where(u => u.RoleId.HasValue)
        .Select(u => new { UserId = u.Id, RoleId = u.RoleId.Value })
        .AsNoTracking()
        .ToListAsync();

    foreach (var mapping in userRoleMappings)
    {
        if (Users.TryGetValue(mapping.UserId, out var user) && 
            Roles.TryGetValue(mapping.RoleId, out var role))
        {
            user.Parents.Add(role);
            role.Children.Add(user);
        }
    }
    
    _logger.LogDebug("Built {Count} user-role relationships", userRoleMappings.Count);
}

private async Task BuildGroupRoleRelationships()
{
    // Load from Role table where GroupId is not null
    var groupRoleMappings = await _dbContext.Roles
        .Where(r => r.GroupId.HasValue)
        .Select(r => new { RoleId = r.Id, GroupId = r.GroupId.Value })
        .AsNoTracking()
        .ToListAsync();

    foreach (var mapping in groupRoleMappings)
    {
        if (Groups.TryGetValue(mapping.GroupId, out var group) && 
            Roles.TryGetValue(mapping.RoleId, out var role))
        {
            group.Children.Add(role);
            role.Parents.Add(group);
        }
    }
    
    _logger.LogDebug("Built {Count} group-role relationships", groupRoleMappings.Count);
}

private async Task BuildGroupHierarchyRelationships()
{
    // Load group hierarchy - need to add parent-child tracking to database
    // For now, implement basic hierarchy support
    // TODO: Enhance database schema to support explicit group hierarchies
    
    _logger.LogDebug("Group hierarchy relationships - implementation pending enhanced schema");
}

private async Task BuildEntityPermissionRelationships()
{
    // Load permission schemes and URI access to build entity permissions
    var entityPermissions = await _dbContext.PermissionSchemes
        .Include(ps => ps.UriAccesses)
            .ThenInclude(ua => ua.Resource)
        .Include(ps => ps.UriAccesses)
            .ThenInclude(ua => ua.VerbType)
        .Include(ps => ps.SchemeType)
        .AsNoTracking()
        .ToListAsync();

    foreach (var permissionScheme in entityPermissions)
    {
        if (!permissionScheme.EntityId.HasValue) continue;

        // Find the domain entity (could be User, Group, or Role)
        Entity? domainEntity = null;
        if (Users.TryGetValue(permissionScheme.EntityId.Value, out var user))
            domainEntity = user;
        else if (Groups.TryGetValue(permissionScheme.EntityId.Value, out var group))
            domainEntity = group;
        else if (Roles.TryGetValue(permissionScheme.EntityId.Value, out var role))
            domainEntity = role;

        if (domainEntity == null) continue;

        // Add permissions to the entity
        foreach (var uriAccess in permissionScheme.UriAccesses)
        {
            var permission = new Permission
            {
                Id = uriAccess.Id,
                Uri = uriAccess.Resource.Uri,
                HttpVerb = Enum.Parse<HttpVerb>(uriAccess.VerbType.VerbName),
                Grant = uriAccess.Grant,
                Deny = uriAccess.Deny,
                Scheme = Enum.Parse<Scheme>(permissionScheme.SchemeType.SchemeName)
            };

            domainEntity.Permissions.Add(permission);
        }
    }
    
    _logger.LogDebug("Built entity permission relationships");
}
```

#### 1.2 Enhanced Normalizer Hydration
**File**: `ACS.Service/Infrastructure/InMemoryEntityGraph.cs` (Enhance Existing)

```csharp
public void HydrateNormalizerReferences()
{
    _logger.LogInformation("Hydrating normalizer references to domain objects");

    // Convert dictionaries to lists for normalizer compatibility
    var usersList = Users.Values.ToList();
    var groupsList = Groups.Values.ToList();
    var rolesList = Roles.Values.ToList();

    // Hydrate all 13 normalizers with references to domain objects
    
    // User-Group normalizers
    AddUserToGroupNormalizer.Users = usersList;
    AddUserToGroupNormalizer.Groups = groupsList;
    
    RemoveUserFromGroupNormalizer.Users = usersList;
    RemoveUserFromGroupNormalizer.Groups = groupsList;

    // User-Role normalizers
    AssignUserToRoleNormalizer.Users = usersList;
    AssignUserToRoleNormalizer.Roles = rolesList;
    
    UnAssignUserFromRoleNormalizer.Users = usersList;
    UnAssignUserFromRoleNormalizer.Roles = rolesList;

    // Group-Role normalizers
    AddRoleToGroupNormalizer.Roles = rolesList;
    AddRoleToGroupNormalizer.Groups = groupsList;
    
    RemoveRoleFromGroupNormalizer.Roles = rolesList;
    RemoveRoleFromGroupNormalizer.Groups = groupsList;

    // Group-Group normalizers
    AddGroupToGroupNormalizer.Groups = groupsList;
    RemoveGroupFromGroupNormalizer.Groups = groupsList;

    // Permission normalizers - these need additional collections
    HydratePermissionNormalizers();

    _logger.LogInformation("Normalizer references hydrated successfully. " +
        "Users: {UserCount}, Groups: {GroupCount}, Roles: {RoleCount}", 
        usersList.Count, groupsList.Count, rolesList.Count);
}

private void HydratePermissionNormalizers()
{
    // Load additional collections needed by permission normalizers
    var entities = Users.Values.Cast<Entity>()
        .Union(Groups.Values.Cast<Entity>())
        .Union(Roles.Values.Cast<Entity>())
        .ToList();

    // Load scheme types and verb types from database for normalizers
    var schemeTypes = _dbContext.SchemeTypes.AsNoTracking().ToList();
    var verbTypes = _dbContext.VerbTypes.AsNoTracking().ToList();
    var resources = _dbContext.Resources.AsNoTracking().ToList();
    var permissionSchemes = _dbContext.PermissionSchemes.AsNoTracking().ToList();
    var uriAccesses = _dbContext.UriAccesses.AsNoTracking().ToList();

    // Hydrate permission-related normalizers
    AddPermissionToEntity.Entities = entities;
    AddPermissionToEntity.SchemeTypes = schemeTypes;
    AddPermissionToEntity.Resources = resources;
    AddPermissionToEntity.PermissionSchemes = permissionSchemes;
    AddPermissionToEntity.UriAccessList = uriAccesses;

    RemovePermissionFromEntity.Entities = entities;
    RemovePermissionFromEntity.SchemeTypes = schemeTypes;
    RemovePermissionFromEntity.Resources = resources;
    RemovePermissionFromEntity.PermissionSchemes = permissionSchemes;
    RemovePermissionFromEntity.UriAccessList = uriAccesses;

    CreateResourceNormalizer.Resources = resources;
    CreateUriAccessNormalizer.VerbTypes = verbTypes;
    CreateUriAccessNormalizer.UriAccessList = uriAccesses;
    CreatePermissionSchemeNormalizer.PermissionSchemes = permissionSchemes;
}
```

### 2. Service Layer Facade Implementation

#### 2.1 Create AccessControlDomainService
**File**: `ACS.Service/Services/AccessControlDomainService.cs` (CREATE NEW)

```csharp
public class AccessControlDomainService
{
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AccessControlDomainService> _logger;

    public AccessControlDomainService(
        InMemoryEntityGraph entityGraph,
        ApplicationDbContext dbContext,
        ILogger<AccessControlDomainService> logger)
    {
        _entityGraph = entityGraph;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ProcessCommandAsync(WebRequestCommand command)
    {
        _logger.LogDebug("Processing command {CommandType} with ID {RequestId}", 
            command.GetType().Name, command.RequestId);

        try
        {
            switch (command)
            {
                case CreateUserCommand createUser:
                    await ProcessCreateUserCommand(createUser);
                    break;

                case AddUserToGroupCommand addUserToGroup:
                    ProcessAddUserToGroupCommand(addUserToGroup);
                    break;

                case RemoveUserFromGroupCommand removeUserFromGroup:
                    ProcessRemoveUserFromGroupCommand(removeUserFromGroup);
                    break;

                case CreateGroupCommand createGroup:
                    await ProcessCreateGroupCommand(createGroup);
                    break;

                case AddGroupToGroupCommand addGroupToGroup:
                    ProcessAddGroupToGroupCommand(addGroupToGroup);
                    break;

                case CreateRoleCommand createRole:
                    await ProcessCreateRoleCommand(createRole);
                    break;

                case AssignUserToRoleCommand assignUserToRole:
                    ProcessAssignUserToRoleCommand(assignUserToRole);
                    break;

                case GrantPermissionCommand grantPermission:
                    ProcessGrantPermissionCommand(grantPermission);
                    break;

                case DenyPermissionCommand denyPermission:
                    ProcessDenyPermissionCommand(denyPermission);
                    break;

                case EvaluatePermissionCommand evaluatePermission:
                    ProcessEvaluatePermissionCommand(evaluatePermission);
                    break;

                default:
                    throw new NotSupportedException($"Command type {command.GetType().Name} is not supported");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command {CommandType} with ID {RequestId}: {Error}", 
                command.GetType().Name, command.RequestId, ex.Message);
            throw;
        }
    }

    private async Task ProcessCreateUserCommand(CreateUserCommand command)
    {
        _logger.LogDebug("Creating user: {UserName}", command.Name);

        // Create new domain user
        var user = new User
        {
            Id = GenerateNewId(), // TODO: Implement ID generation strategy
            Name = command.Name
        };

        // Add to entity graph
        _entityGraph.Users[user.Id] = user;

        // Update normalizer collections (they reference the same objects)
        RefreshNormalizerCollections();

        _logger.LogInformation("Created user {UserId}: {UserName}", user.Id, user.Name);

        // Persist to tenant-specific database for durability
        await PersistUserToDatabaseAsync(user);
    }

    private void ProcessAddUserToGroupCommand(AddUserToGroupCommand command)
    {
        _logger.LogDebug("Adding user {UserId} to group {GroupId}", 
            command.TargetUserId, command.GroupId);

        var user = _entityGraph.GetUser(command.TargetUserId);
        var group = _entityGraph.GetGroup(command.GroupId);

        // Execute domain operation - this calls the normalizer
        user.AddToGroup(group);

        _logger.LogInformation("Added user {UserId} to group {GroupId}", 
            command.TargetUserId, command.GroupId);
    }

    private void ProcessRemoveUserFromGroupCommand(RemoveUserFromGroupCommand command)
    {
        _logger.LogDebug("Removing user {UserId} from group {GroupId}", 
            command.TargetUserId, command.GroupId);

        var user = _entityGraph.GetUser(command.TargetUserId);
        var group = _entityGraph.GetGroup(command.GroupId);

        // Execute domain operation - this calls the normalizer
        user.RemoveFromGroup(group);

        _logger.LogInformation("Removed user {UserId} from group {GroupId}", 
            command.TargetUserId, command.GroupId);
    }

    private async Task ProcessCreateGroupCommand(CreateGroupCommand command)
    {
        _logger.LogDebug("Creating group: {GroupName}", command.Name);

        var group = new Group
        {
            Id = GenerateNewId(),
            Name = command.Name
        };

        // Add to entity graph
        _entityGraph.Groups[group.Id] = group;

        // If parent group specified, establish relationship
        if (command.ParentGroupId.HasValue)
        {
            var parentGroup = _entityGraph.GetGroup(command.ParentGroupId.Value);
            group.AddToGroup(parentGroup);
        }

        RefreshNormalizerCollections();

        _logger.LogInformation("Created group {GroupId}: {GroupName}", group.Id, group.Name);
        
        // Persist to tenant-specific database
        await PersistGroupToDatabaseAsync(group);
    }

    private void ProcessAddGroupToGroupCommand(AddGroupToGroupCommand command)
    {
        _logger.LogDebug("Adding group {ChildGroupId} to group {ParentGroupId}", 
            command.ChildGroupId, command.ParentGroupId);

        var childGroup = _entityGraph.GetGroup(command.ChildGroupId);
        var parentGroup = _entityGraph.GetGroup(command.ParentGroupId);

        // Execute domain operation - this calls the normalizer with cycle prevention
        childGroup.AddToGroup(parentGroup);

        _logger.LogInformation("Added group {ChildGroupId} to group {ParentGroupId}", 
            command.ChildGroupId, command.ParentGroupId);
    }

    private async Task ProcessCreateRoleCommand(CreateRoleCommand command)
    {
        _logger.LogDebug("Creating role: {RoleName}", command.Name);

        var role = new Role
        {
            Id = GenerateNewId(),
            Name = command.Name
        };

        // Add to entity graph
        _entityGraph.Roles[role.Id] = role;

        // If group specified, establish relationship
        if (command.GroupId.HasValue)
        {
            var group = _entityGraph.GetGroup(command.GroupId.Value);
            role.AddToGroup(group);
        }

        RefreshNormalizerCollections();

        _logger.LogInformation("Created role {RoleId}: {RoleName}", role.Id, role.Name);
        
        // Persist to tenant-specific database
        await PersistRoleToDatabaseAsync(role);
    }

    private void ProcessAssignUserToRoleCommand(AssignUserToRoleCommand command)
    {
        _logger.LogDebug("Assigning user {UserId} to role {RoleId}", 
            command.TargetUserId, command.RoleId);

        var user = _entityGraph.GetUser(command.TargetUserId);
        var role = _entityGraph.GetRole(command.RoleId);

        // Execute domain operation - this calls the normalizer
        user.AssignToRole(role);

        _logger.LogInformation("Assigned user {UserId} to role {RoleId}", 
            command.TargetUserId, command.RoleId);
    }

    private void ProcessGrantPermissionCommand(GrantPermissionCommand command)
    {
        _logger.LogDebug("Granting permission {Uri} {Verb} to entity {EntityId}", 
            command.Uri, command.Verb, command.EntityId);

        // Find the entity (could be User, Group, or Role)
        Entity entity = null!;
        if (_entityGraph.Users.TryGetValue(command.EntityId, out var user))
            entity = user;
        else if (_entityGraph.Groups.TryGetValue(command.EntityId, out var group))
            entity = group;
        else if (_entityGraph.Roles.TryGetValue(command.EntityId, out var role))
            entity = role;
        else
            throw new InvalidOperationException($"Entity {command.EntityId} not found");

        var permission = new Permission
        {
            Uri = command.Uri,
            HttpVerb = command.Verb,
            Grant = true,
            Deny = false,
            Scheme = command.Scheme
        };

        // Execute domain operation - this calls the normalizer
        entity.AddPermission(permission);

        _logger.LogInformation("Granted permission {Uri} {Verb} to entity {EntityId}", 
            command.Uri, command.Verb, command.EntityId);
    }

    private void ProcessDenyPermissionCommand(DenyPermissionCommand command)
    {
        _logger.LogDebug("Denying permission {Uri} {Verb} to entity {EntityId}", 
            command.Uri, command.Verb, command.EntityId);

        // Find the entity
        Entity entity = null!;
        if (_entityGraph.Users.TryGetValue(command.EntityId, out var user))
            entity = user;
        else if (_entityGraph.Groups.TryGetValue(command.EntityId, out var group))
            entity = group;
        else if (_entityGraph.Roles.TryGetValue(command.EntityId, out var role))
            entity = role;
        else
            throw new InvalidOperationException($"Entity {command.EntityId} not found");

        var permission = new Permission
        {
            Uri = command.Uri,
            HttpVerb = command.Verb,
            Grant = false,
            Deny = true,
            Scheme = command.Scheme
        };

        // Execute domain operation - this calls the normalizer
        entity.AddPermission(permission);

        _logger.LogInformation("Denied permission {Uri} {Verb} to entity {EntityId}", 
            command.Uri, command.Verb, command.EntityId);
    }

    private void ProcessEvaluatePermissionCommand(EvaluatePermissionCommand command)
    {
        _logger.LogDebug("Evaluating permission {Uri} {Verb} for user {UserId}", 
            command.Uri, command.Verb, command.TargetUserId);

        var user = _entityGraph.GetUser(command.TargetUserId);

        // Use existing domain permission evaluation logic
        var hasPermission = user.HasPermission(command.Uri, command.Verb);

        _logger.LogInformation("Permission evaluation for user {UserId} on {Uri} {Verb}: {Result}", 
            command.TargetUserId, command.Uri, command.Verb, hasPermission);

        // TODO: Store result for retrieval by waiting API client
    }

    private int GenerateNewId()
    {
        // Simple implementation - in production, use more sophisticated ID generation
        var allIds = _entityGraph.Users.Keys
            .Union(_entityGraph.Groups.Keys)
            .Union(_entityGraph.Roles.Keys);
        
        return allIds.Any() ? allIds.Max() + 1 : 1;
    }

    private void RefreshNormalizerCollections()
    {
        // Update normalizer collections to include new entities
        // This ensures normalizers always have current references
        var usersList = _entityGraph.Users.Values.ToList();
        var groupsList = _entityGraph.Groups.Values.ToList();
        var rolesList = _entityGraph.Roles.Values.ToList();

        AddUserToGroupNormalizer.Users = usersList;
        AddUserToGroupNormalizer.Groups = groupsList;
        
        RemoveUserFromGroupNormalizer.Users = usersList;
        RemoveUserFromGroupNormalizer.Groups = groupsList;

        AssignUserToRoleNormalizer.Users = usersList;
        AssignUserToRoleNormalizer.Roles = rolesList;
        
        UnAssignUserFromRoleNormalizer.Users = usersList;
        UnAssignUserFromRoleNormalizer.Roles = rolesList;

        AddRoleToGroupNormalizer.Roles = rolesList;
        AddRoleToGroupNormalizer.Groups = groupsList;
        
        RemoveRoleFromGroupNormalizer.Roles = rolesList;
        RemoveRoleFromGroupNormalizer.Groups = groupsList;

        AddGroupToGroupNormalizer.Groups = groupsList;
        RemoveGroupFromGroupNormalizer.Groups = groupsList;
    }

    // Database persistence methods for single tenant
    private async Task PersistUserToDatabaseAsync(User user)
    {
        var dbUser = new ACS.Service.Data.Models.User
        {
            Id = user.Id,
            Name = user.Name
        };
        
        _dbContext.Users.Add(dbUser);
        await _dbContext.SaveChangesAsync();
    }

    private async Task PersistGroupToDatabaseAsync(Group group)
    {
        var dbGroup = new ACS.Service.Data.Models.Group
        {
            Id = group.Id,
            Name = group.Name
        };
        
        _dbContext.Groups.Add(dbGroup);
        await _dbContext.SaveChangesAsync();
    }

    private async Task PersistRoleToDatabaseAsync(Role role)
    {
        var dbRole = new ACS.Service.Data.Models.Role
        {
            Id = role.Id,
            Name = role.Name
        };
        
        _dbContext.Roles.Add(dbRole);
        await _dbContext.SaveChangesAsync();
    }
}
```

### 3. Database Persistence Strategy

#### 3.1 Create EventPersistenceService
**File**: `ACS.Service/Services/EventPersistenceService.cs` (CREATE NEW)

```csharp
public class EventPersistenceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<EventPersistenceService> _logger;

    public EventPersistenceService(
        ApplicationDbContext dbContext,
        ILogger<EventPersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task PersistCommandAsync(WebRequestCommand command)
    {
        // Create audit log entry for the command
        var auditLog = new AuditLog
        {
            EntityType = ExtractEntityType(command),
            EntityId = ExtractEntityId(command),
            ChangeType = ExtractChangeType(command),
            ChangedBy = command.UserId,
            ChangeDate = command.Timestamp,
            ChangeDetails = SerializeCommand(command)
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();

        _logger.LogDebug("Persisted audit log for command {CommandType} with ID {RequestId}", 
            command.GetType().Name, command.RequestId);
    }

    private string ExtractEntityType(WebRequestCommand command)
    {
        return command switch
        {
            CreateUserCommand => "User",
            AddUserToGroupCommand => "User",
            RemoveUserFromGroupCommand => "User",
            CreateGroupCommand => "Group",
            AddGroupToGroupCommand => "Group",
            CreateRoleCommand => "Role",
            AssignUserToRoleCommand => "User",
            GrantPermissionCommand => "Permission",
            DenyPermissionCommand => "Permission",
            _ => "Unknown"
        };
    }

    private int ExtractEntityId(WebRequestCommand command)
    {
        return command switch
        {
            AddUserToGroupCommand addUser => addUser.TargetUserId,
            RemoveUserFromGroupCommand removeUser => removeUser.TargetUserId,
            AddGroupToGroupCommand addGroup => addGroup.ChildGroupId,
            AssignUserToRoleCommand assignUser => assignUser.TargetUserId,
            GrantPermissionCommand grantPerm => grantPerm.EntityId,
            DenyPermissionCommand denyPerm => denyPerm.EntityId,
            _ => 0 // For create commands, ID will be generated
        };
    }

    private string ExtractChangeType(WebRequestCommand command)
    {
        return command switch
        {
            CreateUserCommand => "INSERT",
            CreateGroupCommand => "INSERT",
            CreateRoleCommand => "INSERT",
            AddUserToGroupCommand => "UPDATE",
            RemoveUserFromGroupCommand => "UPDATE",
            AddGroupToGroupCommand => "UPDATE",
            AssignUserToRoleCommand => "UPDATE",
            GrantPermissionCommand => "INSERT",
            DenyPermissionCommand => "INSERT",
            _ => "UNKNOWN"
        };
    }

    private string SerializeCommand(WebRequestCommand command)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(command, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize command {CommandType}", command.GetType().Name);
            return $"{{\"error\": \"Failed to serialize\", \"commandType\": \"{command.GetType().Name}\"}}";
        }
    }
}
```

### 4. Enhanced Normalizer Adaptation

#### 4.1 Update Normalizers for Domain Object References
**Note**: The existing normalizers need slight modifications to work with domain object references instead of separate collections.

**File**: `ACS.Service/Delegates/Normalizers/AddUserToGroupNormalizer.cs` (Enhance Existing)

```csharp
// Update the normalizer to work with domain object collections
public static class AddUserToGroupNormalizer
{
    // These now reference the same objects as the domain entity graph
    public static List<User> Users { get; set; } = null!;
    public static List<Group> Groups { get; set; } = null!;

    public static void Execute(int userId, int groupId)
    {
        if (Users is null)
        {
            throw new InvalidOperationException("Users collection has not been initialized.");
        }

        if (Groups is null)
        {
            throw new InvalidOperationException("Groups collection has not been initialized.");
        }

        var user = Users.SingleOrDefault(x => x.Id == userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var group = Groups.SingleOrDefault(x => x.Id == groupId)
            ?? throw new InvalidOperationException($"Group {groupId} not found.");

        // Update the domain object collections directly
        // These ARE the same objects as in the entity graph
        if (!group.Children.Contains(user))
        {
            group.Children.Add(user);
        }
        
        if (!user.Parents.Contains(group))
        {
            user.Parents.Add(group);
        }
    }
}
```

Similar updates would be applied to all 13 normalizers to ensure they work with domain object references.

## Testing Strategy

### Unit Tests
- **AccessControlDomainService**: Test command processing logic
- **Enhanced InMemoryEntityGraph**: Test relationship building and hydration
- **EventPersistenceService**: Test audit logging functionality
- **Updated Normalizers**: Verify domain object reference behavior

### Integration Tests
- **Complete Domain Flow**: Test API request → Ring Buffer → Service → Domain → Normalizers
- **Entity Graph Loading**: Test with realistic database data
- **Relationship Integrity**: Verify bidirectional relationships maintain consistency
- **Permission Evaluation**: Test complex hierarchical permission scenarios

## Success Criteria

### Functional Requirements
- ✅ Domain objects and normalizers share the same object references
- ✅ Complete entity relationships loaded and maintained in memory
- ✅ Service layer successfully translates commands to domain operations
- ✅ All existing normalizer functionality preserved and enhanced
- ✅ Audit logging captures all domain changes

### Performance Requirements
- ✅ Entity graph loading completes in under 10 seconds for 10,000 entities
- ✅ Permission evaluation completes in under 1ms for complex hierarchies
- ✅ Memory usage per tenant remains predictable and reasonable
- ✅ Command processing maintains single-threaded performance targets

### Technical Requirements
- ✅ Zero breaking changes to existing domain model interfaces
- ✅ Normalizers work identically to previous behavior
- ✅ Comprehensive logging throughout domain operations
- ✅ Perfect consistency between domain objects and normalizer operations

## Next Phase Dependencies

Phase 2 completion enables:
- **Phase 3**: API layer can utilize complete domain service facade
- **Phase 4**: Advanced features can leverage fully hydrated entity graph
- **Phase 5**: Production features can build upon stable domain integration

## Risk Mitigation

### Memory Consistency
- Comprehensive testing of shared object references
- Validation of entity graph and normalizer synchronization
- Memory profiling under various load scenarios

### Relationship Integrity
- Testing of complex multi-level hierarchies
- Validation of bidirectional relationship maintenance
- Edge case testing for relationship operations

### Performance Validation
- Benchmarking entity graph loading with various data sizes
- Permission evaluation performance testing
- Memory usage monitoring and optimization