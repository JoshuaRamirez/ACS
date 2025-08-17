# Operational Normalization Pattern

## Overview

The Operational Normalization Pattern is an architectural approach that separates **simple domain operations** from **complex multi-object coordination**. Unlike data normalization in databases or command/query separation in CQRS, this pattern normalizes *operations* by extracting the complexity of coordinating multiple objects into dedicated normalizers.

## Problem Statement

In complex domain models with rich object graphs, simple business operations often require:

1. **Multi-object updates** across different aggregates
2. **Bidirectional relationship management** between domain and persistence models
3. **Cascading operations** that affect multiple entities
4. **Resource creation** and lifecycle management
5. **Referential integrity** maintenance across object boundaries

Traditional approaches either:
- **Pollute domain models** with persistence and coordination logic
- **Create anemic domain models** that push all logic into services
- **Tightly couple** business logic with infrastructure concerns

## Solution: Operational Normalization

### Core Principle
> **Domain Models contain business logic and simple operations. Normalizers contain coordination logic and complex multi-object operations.**

### Pattern Structure

```
Domain Operation (Simple)  →  Normalizer (Complex)
     ↓                            ↓
1. Business validation      1. Multi-object coordination
2. In-memory updates       2. Persistence mapping
3. Single responsibility   3. Referential integrity
4. Call normalizer         4. Resource lifecycle
```

## Implementation in ACS

### Domain Model Layer
Domain models focus on business logic and maintain rich object graphs:

```csharp
// Entity.cs - Simple domain operation
public void AddPermission(Permission permission)
{
    Permissions.Add(permission);                    // Simple: Add to collection
    AddPermissionToEntity.Execute(permission, Id);  // Complex: Delegate to normalizer
}

// Group.cs - Business logic with validation
public void AddGroup(Group group)
{
    if (group == this || ContainsGroup(group, this))  // Business validation
    {
        throw new InvalidOperationException("Cannot create cyclical hierarchy.");
    }
    AddChild(group);                                 // Simple: In-memory operation
    AddGroupToGroupNormalizer.Execute(group.Id, this.Id);  // Complex: Coordination
}
```

### Normalizer Layer
Normalizers handle complex coordination across multiple objects:

```csharp
// AddPermissionToEntity.cs - Multi-object coordination
public static void Execute(Permission permission, int entityId)
{
    // Coordinate across 5+ different collections
    var schemeType = SchemeTypes.Single(x => x.SchemeName == permission.Scheme.ToString());
    var entity = Entities.Single(x => x.Id == entityId);
    
    // Handle resource creation if missing
    var resource = Resources.SingleOrDefault(x => x.Uri == permission.Uri);
    if (resource == null)
    {
        resource = CreateResourceNormalizer.Execute(permission);  // Cascade to other normalizers
    }
    
    // Manage bidirectional relationships
    var permissionScheme = PermissionSchemes.SingleOrDefault(x => x.EntityId == entityId);
    if (permissionScheme == null)
    {
        permissionScheme = CreatePermissionSchemeNormalizer.Execute(entityId);
        entity.EntityPermissions.Add(permissionScheme);  // Update both sides
    }
    
    // Ensure referential integrity
    CreateUriAccessNormalizer.Execute(permissionScheme, resource, permission);
}
```

## Key Characteristics

### 1. **Granular Responsibility**
Each normalizer handles exactly one complex operation:
- `AddUserToGroupNormalizer` - User-Group association
- `CreateResourceNormalizer` - Resource lifecycle
- `AddPermissionToEntity` - Permission coordination

### 2. **Composition over Inheritance**
Normalizers can call other normalizers to handle sub-operations:
```csharp
// AddPermissionToEntity calls other normalizers
resource = CreateResourceNormalizer.Execute(permission);
permissionScheme = CreatePermissionSchemeNormalizer.Execute(entityId);
CreateUriAccessNormalizer.Execute(permissionScheme, resource, permission);
```

### 3. **Static Collections for Coordination**
Normalizers operate on static collections representing the current state:
```csharp
public static List<Group> Groups { get; set; }
public static List<User> Users { get; set; }
public static List<PermissionScheme> PermissionSchemes { get; set; }
```

### 4. **Fail-Fast Validation**
Normalizers include defensive validation:
```csharp
var user = Users.SingleOrDefault(x => x.Id == userId)
    ?? throw new InvalidOperationException($"User {userId} not found.");
```

## Benefits

### 1. **Clean Domain Models**
- Domain models focus purely on business logic
- No persistence or coordination concerns
- Rich behavior without infrastructure pollution

### 2. **Testable Separation**
- Unit test domain logic without database
- Test normalizers with mock collections
- Independent evolution of concerns

### 3. **Complex Operation Management**
- Multi-object operations are explicit and reusable
- Consistent handling of cascading updates
- Centralized referential integrity logic

### 4. **Performance Optimization**
- Domain operations work on in-memory collections
- Normalizers can batch database operations
- Efficient object graph traversal

## Comparison to Other Patterns

| Pattern | Focus | Complexity Location | Use Case |
|---------|-------|-------------------|----------|
| **Repository** | Data access | Service layer | Simple CRUD |
| **CQRS** | Read/Write separation | Command/Query handlers | Different read/write models |
| **Domain Services** | Cross-aggregate operations | Domain services | Complex business rules |
| **Operational Normalization** | Simple/Complex operation separation | Dedicated normalizers | Multi-object coordination |

## When to Use

### ✅ **Good Fit:**
- Rich domain models with complex relationships
- Operations spanning multiple aggregates
- Need for referential integrity across objects
- Complex object graph coordination

### ❌ **Poor Fit:**
- Simple CRUD applications
- Anemic domain models
- Single-aggregate operations
- Read-heavy applications

## Evolution and Extensions

### Potential Enhancements:
1. **Event-Driven Normalizers**: Emit domain events after normalization
2. **Transaction Boundaries**: Wrap normalizers in explicit transactions
3. **Async Normalizers**: Handle time-consuming coordination asynchronously
4. **Command Pattern Integration**: Commands invoke appropriate normalizers
5. **Audit Trail**: Normalizers automatically track changes for audit logs

## Conclusion

The Operational Normalization Pattern provides a clean separation between business logic and operational complexity. By normalizing *operations* rather than data, it allows domain models to remain focused on business rules while ensuring complex multi-object coordination is handled consistently and efficiently.

This pattern is particularly valuable in access control systems, workflow engines, and other domains where operations frequently span multiple related objects and require careful coordination to maintain consistency.