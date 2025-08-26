# Query Pattern Implementation

This folder contains a comprehensive Query pattern implementation that provides a strong, composable approach to data access operations.

## Pattern Components

### 1. Core Interface and Base Class
- **`IQuery<T>`**: Interface defining the contract for all query operations
- **`Query<T>`**: Abstract base class providing polymorphic inheritance with validation and execution patterns

### 2. Query Structure
```csharp
public abstract class Query<T> : IQuery<T>
{
    public T Execute()
    {
        Validate();      // Validates parameters
        return ExecuteQuery(); // Performs actual query
    }
    
    protected abstract void Validate();
    protected abstract T ExecuteQuery();
}
```

### 3. Concrete Query Classes

#### User Queries
- **`GetUserByIdQuery`**: Retrieve single user by ID
- **`GetUsersQuery`**: Retrieve multiple users with filtering, sorting, pagination
- **`GetUsersCountQuery`**: Get total count with filtering
- **`UserExistsQuery`**: Check if user exists
- **`FindUsersByNameQuery`**: Find users by name pattern
- **`GetUsersWithCountQuery`**: Composite query for users + count

#### Group Queries
- **`GetGroupByIdQuery`**: Retrieve single group
- **`GetGroupsQuery`**: Retrieve multiple groups with filtering
- **`GetChildGroupsQuery`**: Get child groups (with recursive option)
- **`GetParentGroupsQuery`**: Get parent groups (with recursive option)
- **`CheckGroupCycleQuery`**: Check for circular references
- **`GetGroupUsersQuery`**: Get users in a group
- **`GetUserGroupsQuery`**: Get groups a user belongs to
- **`GetGroupsWithHierarchyQuery`**: Composite query with hierarchy info

#### Role Queries
- **`GetRoleByIdQuery`**: Retrieve single role
- **`GetRolesQuery`**: Retrieve multiple roles with filtering
- **`GetUserRolesQuery`**: Get roles for a user (with inheritance)
- **`GetRoleUsersQuery`**: Get users with a role (with inheritance)
- **`IsCriticalRoleQuery`**: Check if role is critical/administrative
- **`GetRolesWithStatsQuery`**: Composite query with assignment statistics

#### Permission Queries
- **`GetPermissionByIdQuery`**: Retrieve single permission
- **`GetPermissionsQuery`**: Retrieve multiple permissions with filtering
- **`GetEntityPermissionsQuery`**: Get permissions for any entity (User/Group/Role)
- **`CheckEntityPermissionQuery`**: Check if entity has specific permission
- **`FindPermissionsByPatternQuery`**: Find permissions by pattern
- **`GetPermissionsWithUsageQuery`**: Composite query with usage statistics

## Key Features

### 1. Strong Validation
Every query validates its parameters before execution:
```csharp
protected override void Validate()
{
    if (UserId <= 0)
        throw new ArgumentException("User ID must be greater than zero", nameof(UserId));
    
    if (EntityGraph == null)
        throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
}
```

### 2. Query Composition
Queries can compose other queries within their execution:
```csharp
protected override (ICollection<User> Users, int TotalCount) ExecuteQuery()
{
    // Compose count query
    var countQuery = new GetUsersCountQuery
    {
        Search = Search,
        EntityGraph = EntityGraph
    };
    var totalCount = countQuery.Execute();

    // Compose users query  
    var usersQuery = new GetUsersQuery
    {
        Page = Page,
        PageSize = PageSize,
        Search = Search,
        EntityGraph = EntityGraph
    };
    var users = usersQuery.Execute();

    return (users, totalCount);
}
```

### 3. Recursive Operations
Complex hierarchical operations are supported:
```csharp
// Get all nested child groups recursively
var childGroupsQuery = new GetChildGroupsQuery
{
    ParentGroupId = groupId,
    IncludeNestedChildren = true,
    EntityGraph = EntityGraph
};
```

### 4. Cross-Entity Queries
Queries can work across entity boundaries:
```csharp
// Get all permissions for a user, including inherited from groups and roles
var entityPermissionsQuery = new GetEntityPermissionsQuery
{
    EntityId = userId,
    EntityType = "User",
    IncludeInheritedPermissions = true,
    EntityGraph = EntityGraph
};
```

## Usage in Services

Services use Query objects instead of direct data access:

```csharp
public async Task<UserResponse> GetByIdAsync(GetUserRequest request)
{
    // Use Query object for data access
    var getUserQuery = new GetUserByIdQuery
    {
        UserId = request.UserId,
        EntityGraph = _entityGraph
    };

    var user = getUserQuery.Execute();
    
    return new UserResponse
    {
        User = user,
        Success = true,
        Message = user != null ? "User found" : "User not found"
    };
}
```

## Benefits

1. **Consistency**: All queries follow the same pattern
2. **Validation**: Parameter validation is enforced at the query level
3. **Composability**: Queries can be composed within other queries
4. **Testability**: Each query can be tested in isolation
5. **Reusability**: Queries can be reused across different services
6. **Type Safety**: Strong typing with generics
7. **Single Responsibility**: Each query has a focused purpose
8. **Maintainability**: Changes to query logic are isolated

## Query Composition Examples

### Simple Composition
```csharp
// Check if user exists before performing operation
var userExistsQuery = new UserExistsQuery { UserId = id, EntityGraph = _entityGraph };
if (!userExistsQuery.Execute())
{
    throw new InvalidOperationException("User not found");
}
```

### Complex Composition
```csharp
// Get user's effective permissions from all sources
var userRolesQuery = new GetUserRolesQuery 
{ 
    UserId = userId, 
    IncludeInheritedRoles = true,
    EntityGraph = _entityGraph 
};

foreach (var role in userRolesQuery.Execute())
{
    var rolePermissionsQuery = new GetEntityPermissionsQuery
    {
        EntityId = role.Id,
        EntityType = "Role",
        EntityGraph = _entityGraph
    };
    
    // Combine permissions...
}
```

This Query pattern provides a robust foundation for all data access needs while maintaining clean separation of concerns and enabling powerful composition scenarios.