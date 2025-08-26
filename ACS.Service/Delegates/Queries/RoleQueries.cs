using ACS.Service.Domain;
using ACS.Service.Infrastructure;

namespace ACS.Service.Delegates.Queries;

/// <summary>
/// Query to retrieve a single role by ID
/// </summary>
public class GetRoleByIdQuery : Query<Role?>
{
    public int RoleId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (RoleId <= 0)
            throw new ArgumentException("Role ID must be greater than zero", nameof(RoleId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override Role? ExecuteQuery()
    {
        EntityGraph.Roles.TryGetValue(RoleId, out var role);
        return role;
    }
}

/// <summary>
/// Query to retrieve multiple roles with filtering, sorting, and pagination
/// </summary>
public class GetRolesQuery : Query<ICollection<Role>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public bool? IsCriticalRole { get; set; }
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

    protected override ICollection<Role> ExecuteQuery()
    {
        var allRoles = EntityGraph.Roles.Values.AsQueryable();

        // Apply critical role filter
        if (IsCriticalRole.HasValue)
        {
            var criticalRoles = new[] { "Administrator", "Admin", "SystemAdmin", "Root" };
            if (IsCriticalRole.Value)
            {
                allRoles = allRoles.Where(r => criticalRoles.Contains(r.Name, StringComparer.OrdinalIgnoreCase));
            }
            else
            {
                allRoles = allRoles.Where(r => !criticalRoles.Contains(r.Name, StringComparer.OrdinalIgnoreCase));
            }
        }

        // Apply search filter
        if (!string.IsNullOrEmpty(Search))
        {
            allRoles = allRoles.Where(r => r.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
        }

        // Apply sorting
        if (!string.IsNullOrEmpty(SortBy))
        {
            allRoles = SortBy.ToLower() switch
            {
                "name" => SortDescending ? allRoles.OrderByDescending(r => r.Name) : allRoles.OrderBy(r => r.Name),
                "id" => SortDescending ? allRoles.OrderByDescending(r => r.Id) : allRoles.OrderBy(r => r.Id),
                _ => allRoles.OrderBy(r => r.Id)
            };
        }

        // Apply pagination
        return allRoles
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}

/// <summary>
/// Query to get total count of roles with optional filtering
/// </summary>
public class GetRolesCountQuery : Query<int>
{
    public string? Search { get; set; }
    public bool? IsCriticalRole { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override int ExecuteQuery()
    {
        var allRoles = EntityGraph.Roles.Values.AsQueryable();

        // Apply critical role filter
        if (IsCriticalRole.HasValue)
        {
            var criticalRoles = new[] { "Administrator", "Admin", "SystemAdmin", "Root" };
            if (IsCriticalRole.Value)
            {
                allRoles = allRoles.Where(r => criticalRoles.Contains(r.Name, StringComparer.OrdinalIgnoreCase));
            }
            else
            {
                allRoles = allRoles.Where(r => !criticalRoles.Contains(r.Name, StringComparer.OrdinalIgnoreCase));
            }
        }

        // Apply search filter
        if (!string.IsNullOrEmpty(Search))
        {
            allRoles = allRoles.Where(r => r.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
        }

        return allRoles.Count();
    }
}

/// <summary>
/// Query to get roles assigned to a specific user
/// </summary>
public class GetUserRolesQuery : Query<ICollection<Role>>
{
    public int UserId { get; set; }
    public bool IncludeInheritedRoles { get; set; } = true;
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (UserId <= 0)
            throw new ArgumentException("User ID must be greater than zero", nameof(UserId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<Role> ExecuteQuery()
    {
        if (!EntityGraph.Users.TryGetValue(UserId, out var user))
        {
            return new List<Role>();
        }

        var roles = new HashSet<Role>();

        // Add directly assigned roles
        foreach (var role in user.RoleMemberships)
        {
            roles.Add(role);
        }

        if (IncludeInheritedRoles)
        {
            // Add roles inherited through group membership using composition
            var userGroupsQuery = new GetUserGroupsQuery
            {
                UserId = UserId,
                IncludeNestedGroups = true,
                EntityGraph = EntityGraph
            };

            var userGroups = userGroupsQuery.Execute();
            
            foreach (var group in userGroups)
            {
                foreach (var role in group.Children.OfType<Role>())
                {
                    roles.Add(role);
                }
            }
        }

        return roles.ToList();
    }
}

/// <summary>
/// Query to get users assigned to a specific role
/// </summary>
public class GetRoleUsersQuery : Query<ICollection<User>>
{
    public int RoleId { get; set; }
    public bool IncludeInheritedUsers { get; set; } = true;
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (RoleId <= 0)
            throw new ArgumentException("Role ID must be greater than zero", nameof(RoleId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<User> ExecuteQuery()
    {
        if (!EntityGraph.Roles.TryGetValue(RoleId, out var role))
        {
            return new List<User>();
        }

        var users = new HashSet<User>();

        // Add directly assigned users
        foreach (var user in role.Children.OfType<User>())
        {
            users.Add(user);
        }

        if (IncludeInheritedUsers)
        {
            // Add users inherited through groups that have this role
            var roleGroupsQuery = new GetRoleGroupsQuery
            {
                RoleId = RoleId,
                EntityGraph = EntityGraph
            };

            var roleGroups = roleGroupsQuery.Execute();
            
            foreach (var group in roleGroups)
            {
                // Get all users in this group (including nested)
                var groupUsersQuery = new GetGroupUsersQuery
                {
                    GroupId = group.Id,
                    IncludeNestedUsers = true,
                    EntityGraph = EntityGraph
                };

                var groupUsers = groupUsersQuery.Execute();
                foreach (var user in groupUsers)
                {
                    users.Add(user);
                }
            }
        }

        return users.ToList();
    }
}

/// <summary>
/// Query to get groups that have a specific role assigned
/// </summary>
public class GetRoleGroupsQuery : Query<ICollection<Group>>
{
    public int RoleId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (RoleId <= 0)
            throw new ArgumentException("Role ID must be greater than zero", nameof(RoleId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<Group> ExecuteQuery()
    {
        if (!EntityGraph.Roles.TryGetValue(RoleId, out var role))
        {
            return new List<Group>();
        }

        return role.Parents.OfType<Group>().ToList();
    }
}

/// <summary>
/// Query to check if a role is considered critical/administrative
/// </summary>
public class IsCriticalRoleQuery : Query<bool>
{
    public int RoleId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    private static readonly string[] CriticalRoleNames = { "Administrator", "Admin", "SystemAdmin", "Root" };

    protected override void Validate()
    {
        if (RoleId <= 0)
            throw new ArgumentException("Role ID must be greater than zero", nameof(RoleId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override bool ExecuteQuery()
    {
        if (!EntityGraph.Roles.TryGetValue(RoleId, out var role))
        {
            return false;
        }

        return CriticalRoleNames.Contains(role.Name, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Query to find roles by name pattern
/// </summary>
public class FindRolesByNameQuery : Query<ICollection<Role>>
{
    public string NamePattern { get; set; } = string.Empty;
    public bool ExactMatch { get; set; } = false;
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (string.IsNullOrWhiteSpace(NamePattern))
            throw new ArgumentException("Name pattern cannot be null or empty", nameof(NamePattern));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<Role> ExecuteQuery()
    {
        var allRoles = EntityGraph.Roles.Values;

        if (ExactMatch)
        {
            return allRoles.Where(r => r.Name.Equals(NamePattern, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else
        {
            return allRoles.Where(r => r.Name.Contains(NamePattern, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}

/// <summary>
/// Composite query to get roles with their assignment statistics
/// </summary>
public class GetRolesWithStatsQuery : Query<ICollection<(Role Role, int UserCount, int GroupCount, bool IsCritical)>>
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

    protected override ICollection<(Role Role, int UserCount, int GroupCount, bool IsCritical)> ExecuteQuery()
    {
        // First get the roles using composition
        var rolesQuery = new GetRolesQuery
        {
            Page = Page,
            PageSize = PageSize,
            Search = Search,
            EntityGraph = EntityGraph
        };

        var roles = rolesQuery.Execute();
        var result = new List<(Role Role, int UserCount, int GroupCount, bool IsCritical)>();

        foreach (var role in roles)
        {
            // Compose user count query
            var usersQuery = new GetRoleUsersQuery
            {
                RoleId = role.Id,
                IncludeInheritedUsers = true,
                EntityGraph = EntityGraph
            };
            var userCount = usersQuery.Execute().Count;

            // Compose group count query
            var groupsQuery = new GetRoleGroupsQuery
            {
                RoleId = role.Id,
                EntityGraph = EntityGraph
            };
            var groupCount = groupsQuery.Execute().Count;

            // Compose critical role check
            var criticalQuery = new IsCriticalRoleQuery
            {
                RoleId = role.Id,
                EntityGraph = EntityGraph
            };
            var isCritical = criticalQuery.Execute();

            result.Add((role, userCount, groupCount, isCritical));
        }

        return result;
    }
}