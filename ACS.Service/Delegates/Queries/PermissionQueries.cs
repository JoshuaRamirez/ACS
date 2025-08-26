using ACS.Service.Domain;
using ACS.Service.Infrastructure;

namespace ACS.Service.Delegates.Queries;

/// <summary>
/// Query to retrieve a single permission by ID
/// </summary>
public class GetPermissionByIdQuery : Query<Permission?>
{
    public int PermissionId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (PermissionId <= 0)
            throw new ArgumentException("Permission ID must be greater than zero", nameof(PermissionId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override Permission? ExecuteQuery()
    {
        EntityGraph.Permissions.TryGetValue(PermissionId, out var permission);
        return permission;
    }
}

/// <summary>
/// Query to retrieve multiple permissions with filtering, sorting, and pagination
/// </summary>
public class GetPermissionsQuery : Query<ICollection<Permission>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Resource { get; set; }
    public string? Action { get; set; }
    public string? Scope { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (Page <= 0)
            throw new ArgumentException("Page must be greater than zero", nameof(Page));
        
        if (PageSize <= 0 || PageSize > 1000)
            throw new ArgumentException("Page size must be between 1 and 1000", nameof(PageSize));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<Permission> ExecuteQuery()
    {
        var allPermissions = EntityGraph.Permissions.Values.AsQueryable();

        // Apply resource filter
        if (!string.IsNullOrEmpty(Resource))
        {
            allPermissions = allPermissions.Where(p => p.Resource.Equals(Resource, StringComparison.OrdinalIgnoreCase));
        }

        // Apply action filter
        if (!string.IsNullOrEmpty(Action))
        {
            allPermissions = allPermissions.Where(p => p.Action.Equals(Action, StringComparison.OrdinalIgnoreCase));
        }

        // Apply scope filter
        if (!string.IsNullOrEmpty(Scope))
        {
            allPermissions = allPermissions.Where(p => p.Scope != null && p.Scope.Equals(Scope, StringComparison.OrdinalIgnoreCase));
        }

        // Apply search filter (searches across resource, action, and scope)
        if (!string.IsNullOrEmpty(Search))
        {
            allPermissions = allPermissions.Where(p => 
                p.Resource.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                p.Action.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                (p.Scope != null && p.Scope.Contains(Search, StringComparison.OrdinalIgnoreCase)));
        }

        // Apply pagination
        return allPermissions
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}

/// <summary>
/// Query to get total count of permissions with optional filtering
/// </summary>
public class GetPermissionsCountQuery : Query<int>
{
    public string? Search { get; set; }
    public string? Resource { get; set; }
    public string? Action { get; set; }
    public string? Scope { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override int ExecuteQuery()
    {
        var allPermissions = EntityGraph.Permissions.Values.AsQueryable();

        // Apply same filters as GetPermissionsQuery
        if (!string.IsNullOrEmpty(Resource))
        {
            allPermissions = allPermissions.Where(p => p.Resource.Equals(Resource, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(Action))
        {
            allPermissions = allPermissions.Where(p => p.Action.Equals(Action, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(Scope))
        {
            allPermissions = allPermissions.Where(p => p.Scope != null && p.Scope.Equals(Scope, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(Search))
        {
            allPermissions = allPermissions.Where(p => 
                p.Resource.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                p.Action.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                (p.Scope != null && p.Scope.Contains(Search, StringComparison.OrdinalIgnoreCase)));
        }

        return allPermissions.Count();
    }
}

/// <summary>
/// Query to get permissions for a specific entity (user, group, or role)
/// </summary>
public class GetEntityPermissionsQuery : Query<ICollection<Permission>>
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty; // "User", "Group", "Role"
    public bool IncludeInheritedPermissions { get; set; } = true;
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (EntityId <= 0)
            throw new ArgumentException("Entity ID must be greater than zero", nameof(EntityId));
        
        if (string.IsNullOrWhiteSpace(EntityType))
            throw new ArgumentException("Entity type cannot be null or empty", nameof(EntityType));
        
        if (!new[] { "User", "Group", "Role" }.Contains(EntityType, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Entity type must be User, Group, or Role", nameof(EntityType));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<Permission> ExecuteQuery()
    {
        var permissions = new HashSet<Permission>();

        switch (EntityType.ToLower())
        {
            case "user":
                permissions.UnionWith(GetUserPermissions());
                break;
            case "group":
                permissions.UnionWith(GetGroupPermissions());
                break;
            case "role":
                permissions.UnionWith(GetRolePermissions());
                break;
        }

        return permissions.ToList();
    }

    private ICollection<Permission> GetUserPermissions()
    {
        if (!EntityGraph.Users.TryGetValue(EntityId, out var user))
        {
            return new List<Permission>();
        }

        var permissions = new HashSet<Permission>();

        // Users don't have direct permissions in the current model
        // All permissions come through roles and groups

        if (IncludeInheritedPermissions)
        {
            // Add permissions from roles using composition
            var userRolesQuery = new GetUserRolesQuery
            {
                UserId = EntityId,
                IncludeInheritedRoles = true,
                EntityGraph = EntityGraph
            };

            var userRoles = userRolesQuery.Execute();
            foreach (var role in userRoles)
            {
                var rolePermissionsQuery = new GetEntityPermissionsQuery
                {
                    EntityId = role.Id,
                    EntityType = "Role",
                    IncludeInheritedPermissions = false, // Avoid infinite recursion
                    EntityGraph = EntityGraph
                };

                var rolePermissions = rolePermissionsQuery.Execute();
                permissions.UnionWith(rolePermissions);
            }

            // Add permissions from groups using composition
            var userGroupsQuery = new GetUserGroupsQuery
            {
                UserId = EntityId,
                IncludeNestedGroups = true,
                EntityGraph = EntityGraph
            };

            var userGroups = userGroupsQuery.Execute();
            foreach (var group in userGroups)
            {
                var groupPermissionsQuery = new GetEntityPermissionsQuery
                {
                    EntityId = group.Id,
                    EntityType = "Group",
                    IncludeInheritedPermissions = false, // Avoid infinite recursion
                    EntityGraph = EntityGraph
                };

                var groupPermissions = groupPermissionsQuery.Execute();
                permissions.UnionWith(groupPermissions);
            }
        }

        return permissions.ToList();
    }

    private ICollection<Permission> GetGroupPermissions()
    {
        if (!EntityGraph.Groups.TryGetValue(EntityId, out var group))
        {
            return new List<Permission>();
        }

        var permissions = new HashSet<Permission>();

        // Add direct permissions
        foreach (var permission in group.Permissions)
        {
            permissions.Add(permission);
        }

        if (IncludeInheritedPermissions)
        {
            // Add permissions from parent groups using composition
            var parentGroupsQuery = new GetParentGroupsQuery
            {
                ChildGroupId = EntityId,
                IncludeNestedParents = true,
                EntityGraph = EntityGraph
            };

            var parentGroups = parentGroupsQuery.Execute();
            foreach (var parentGroup in parentGroups)
            {
                var parentPermissionsQuery = new GetEntityPermissionsQuery
                {
                    EntityId = parentGroup.Id,
                    EntityType = "Group",
                    IncludeInheritedPermissions = false, // Avoid infinite recursion
                    EntityGraph = EntityGraph
                };

                var parentPermissions = parentPermissionsQuery.Execute();
                permissions.UnionWith(parentPermissions);
            }
        }

        return permissions.ToList();
    }

    private ICollection<Permission> GetRolePermissions()
    {
        if (!EntityGraph.Roles.TryGetValue(EntityId, out var role))
        {
            return new List<Permission>();
        }

        var permissions = new HashSet<Permission>();

        // Add direct permissions
        foreach (var permission in role.Permissions)
        {
            permissions.Add(permission);
        }

        // Roles don't inherit permissions from other roles in this model
        return permissions.ToList();
    }
}

/// <summary>
/// Query to check if an entity has a specific permission
/// </summary>
public class CheckEntityPermissionQuery : Query<bool>
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (EntityId <= 0)
            throw new ArgumentException("Entity ID must be greater than zero", nameof(EntityId));
        
        if (string.IsNullOrWhiteSpace(EntityType))
            throw new ArgumentException("Entity type cannot be null or empty", nameof(EntityType));
        
        if (string.IsNullOrWhiteSpace(Resource))
            throw new ArgumentException("Resource cannot be null or empty", nameof(Resource));
        
        if (string.IsNullOrWhiteSpace(Action))
            throw new ArgumentException("Action cannot be null or empty", nameof(Action));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override bool ExecuteQuery()
    {
        // Use composition to get all entity permissions
        var entityPermissionsQuery = new GetEntityPermissionsQuery
        {
            EntityId = EntityId,
            EntityType = EntityType,
            IncludeInheritedPermissions = true,
            EntityGraph = EntityGraph
        };

        var permissions = entityPermissionsQuery.Execute();

        // Check if any permission matches the requested resource, action, and scope
        return permissions.Any(p => 
            p.Resource.Equals(Resource, StringComparison.OrdinalIgnoreCase) &&
            p.Action.Equals(Action, StringComparison.OrdinalIgnoreCase) &&
            (Scope == null || p.Scope == null || p.Scope.Equals(Scope, StringComparison.OrdinalIgnoreCase)));
    }
}

/// <summary>
/// Query to find permissions by resource and action pattern
/// </summary>
public class FindPermissionsByPatternQuery : Query<ICollection<Permission>>
{
    public string ResourcePattern { get; set; } = string.Empty;
    public string ActionPattern { get; set; } = string.Empty;
    public bool ExactMatch { get; set; } = false;
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (string.IsNullOrWhiteSpace(ResourcePattern))
            throw new ArgumentException("Resource pattern cannot be null or empty", nameof(ResourcePattern));
        
        if (string.IsNullOrWhiteSpace(ActionPattern))
            throw new ArgumentException("Action pattern cannot be null or empty", nameof(ActionPattern));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<Permission> ExecuteQuery()
    {
        var allPermissions = EntityGraph.Permissions.Values;

        if (ExactMatch)
        {
            return allPermissions.Where(p => 
                p.Resource.Equals(ResourcePattern, StringComparison.OrdinalIgnoreCase) &&
                p.Action.Equals(ActionPattern, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else
        {
            return allPermissions.Where(p => 
                p.Resource.Contains(ResourcePattern, StringComparison.OrdinalIgnoreCase) &&
                p.Action.Contains(ActionPattern, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}

/// <summary>
/// Composite query to get permissions with their usage statistics
/// </summary>
public class GetPermissionsWithUsageQuery : Query<ICollection<(Permission Permission, int UserCount, int GroupCount, int RoleCount)>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (Page <= 0)
            throw new ArgumentException("Page must be greater than zero", nameof(Page));
        
        if (PageSize <= 0 || PageSize > 1000)
            throw new ArgumentException("Page size must be between 1 and 1000", nameof(PageSize));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<(Permission Permission, int UserCount, int GroupCount, int RoleCount)> ExecuteQuery()
    {
        // First get the permissions using composition
        var permissionsQuery = new GetPermissionsQuery
        {
            Page = Page,
            PageSize = PageSize,
            Search = Search,
            EntityGraph = EntityGraph
        };

        var permissions = permissionsQuery.Execute();
        var result = new List<(Permission Permission, int UserCount, int GroupCount, int RoleCount)>();

        foreach (var permission in permissions)
        {
            // Count users with this permission (through roles and groups)
            var userCount = 0; // Users don't have direct permissions in current model

            // Count groups with this permission
            var groupCount = EntityGraph.Groups.Values.Count(g =>
                g.Permissions.Contains(permission));

            // Count roles with this permission
            var roleCount = EntityGraph.Roles.Values.Count(r =>
                r.Permissions.Contains(permission));

            result.Add((permission, userCount, groupCount, roleCount));
        }

        return result;
    }
}