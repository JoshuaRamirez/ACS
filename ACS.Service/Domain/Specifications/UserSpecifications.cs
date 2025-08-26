using System.Linq.Expressions;

namespace ACS.Service.Domain.Specifications;

/// <summary>
/// Specification for users in a specific group
/// </summary>
public class UserInGroupSpecification : Specification<User>
{
    private readonly int _groupId;

    public UserInGroupSpecification(int groupId)
    {
        _groupId = groupId;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Parents.Any(p => p.Id == _groupId && p is Group);
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.GroupMemberships.Any(g => g.Id == _groupId);
    }
}

/// <summary>
/// Specification for users in any of the specified groups
/// </summary>
public class UserInGroupsSpecification : Specification<User>
{
    private readonly HashSet<int> _groupIds;

    public UserInGroupsSpecification(params int[] groupIds)
    {
        _groupIds = new HashSet<int>(groupIds ?? throw new ArgumentNullException(nameof(groupIds)));
    }

    public UserInGroupsSpecification(IEnumerable<int> groupIds)
    {
        _groupIds = new HashSet<int>(groupIds ?? throw new ArgumentNullException(nameof(groupIds)));
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Parents.Any(p => _groupIds.Contains(p.Id) && p is Group);
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.GroupMemberships.Any(g => _groupIds.Contains(g.Id));
    }
}

/// <summary>
/// Specification for users with a specific role
/// </summary>
public class UserWithRoleSpecification : Specification<User>
{
    private readonly int _roleId;

    public UserWithRoleSpecification(int roleId)
    {
        _roleId = roleId;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Parents.Any(p => p.Id == _roleId && p is Role);
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.RoleMemberships.Any(r => r.Id == _roleId);
    }
}

/// <summary>
/// Specification for users with any of the specified roles
/// </summary>
public class UserWithRolesSpecification : Specification<User>
{
    private readonly HashSet<int> _roleIds;

    public UserWithRolesSpecification(params int[] roleIds)
    {
        _roleIds = new HashSet<int>(roleIds ?? throw new ArgumentNullException(nameof(roleIds)));
    }

    public UserWithRolesSpecification(IEnumerable<int> roleIds)
    {
        _roleIds = new HashSet<int>(roleIds ?? throw new ArgumentNullException(nameof(roleIds)));
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Parents.Any(p => _roleIds.Contains(p.Id) && p is Role);
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.RoleMemberships.Any(r => _roleIds.Contains(r.Id));
    }
}

/// <summary>
/// Specification for users with a specific role name
/// </summary>
public class UserWithRoleNameSpecification : Specification<User>
{
    private readonly string _roleName;
    private readonly bool _exactMatch;

    public UserWithRoleNameSpecification(string roleName, bool exactMatch = true)
    {
        _roleName = roleName ?? throw new ArgumentNullException(nameof(roleName));
        _exactMatch = exactMatch;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        if (_exactMatch)
        {
            return u => u.Parents.Any(p => p.Name == _roleName && p is Role);
        }
        else
        {
            return u => u.Parents.Any(p => p.Name.Contains(_roleName) && p is Role);
        }
    }

    public override bool IsSatisfiedBy(User entity)
    {
        if (_exactMatch)
        {
            return entity.RoleMemberships.Any(r => r.Name.Equals(_roleName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            return entity.RoleMemberships.Any(r => r.Name.Contains(_roleName, StringComparison.OrdinalIgnoreCase));
        }
    }
}

/// <summary>
/// Specification for users without any groups
/// </summary>
public class UserWithoutGroupsSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => !u.Parents.Any(p => p is Group);
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return !entity.GroupMemberships.Any();
    }
}

/// <summary>
/// Specification for users without any roles
/// </summary>
public class UserWithoutRolesSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => !u.Parents.Any(p => p is Role);
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return !entity.RoleMemberships.Any();
    }
}

/// <summary>
/// Specification for users with administrative roles
/// </summary>
public class AdminUserSpecification : Specification<User>
{
    private static readonly string[] AdminRoleNames = 
    {
        "Administrator", "Admin", "SuperAdmin", "SystemAdmin", "SecurityAdmin"
    };

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Parents.Any(p => AdminRoleNames.Contains(p.Name) && p is Role);
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.RoleMemberships.Any(r => 
            AdminRoleNames.Contains(r.Name, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Specification for users with specific permissions
/// </summary>
public class UserWithPermissionSpecification : Specification<User>
{
    private readonly string _uri;
    private readonly HttpVerb _httpVerb;

    public UserWithPermissionSpecification(string uri, HttpVerb httpVerb)
    {
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        _httpVerb = httpVerb;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Permissions.Any(p => p.Uri == _uri && p.HttpVerb == _httpVerb && p.Grant && !p.Deny) ||
                   u.Parents.Any(parent => parent.Permissions.Any(p => p.Uri == _uri && p.HttpVerb == _httpVerb && p.Grant && !p.Deny));
    }

    public override bool IsSatisfiedBy(User entity)
    {
        // Check direct permissions
        var hasDirectPermission = entity.Permissions.Any(p => 
            p.Uri.Equals(_uri, StringComparison.OrdinalIgnoreCase) && 
            p.HttpVerb == _httpVerb && 
            p.Grant && 
            !p.Deny);

        if (hasDirectPermission)
            return true;

        // Check inherited permissions from roles and groups
        var inheritedPermissions = entity.Parents.SelectMany(parent => parent.Permissions);
        return inheritedPermissions.Any(p => 
            p.Uri.Equals(_uri, StringComparison.OrdinalIgnoreCase) && 
            p.HttpVerb == _httpVerb && 
            p.Grant && 
            !p.Deny);
    }
}

/// <summary>
/// Specification for users with access to high-risk resources
/// </summary>
public class UserWithHighRiskAccessSpecification : Specification<User>
{
    private static readonly string[] HighRiskUriPatterns = 
    {
        "/admin", "/system", "/config", "/security", "/delete"
    };

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Permissions.Any(p => HighRiskUriPatterns.Any(pattern => p.Uri.Contains(pattern)) && p.Grant && !p.Deny) ||
                   u.Parents.Any(parent => parent.Permissions.Any(p => HighRiskUriPatterns.Any(pattern => p.Uri.Contains(pattern)) && p.Grant && !p.Deny));
    }

    public override bool IsSatisfiedBy(User entity)
    {
        var allPermissions = entity.Permissions.Concat(entity.Parents.SelectMany(p => p.Permissions));
        
        return allPermissions.Any(p => 
            HighRiskUriPatterns.Any(pattern => p.Uri.Contains(pattern, StringComparison.OrdinalIgnoreCase)) &&
            p.Grant && 
            !p.Deny);
    }
}

/// <summary>
/// Specification for users with minimum number of roles
/// </summary>
public class UserWithMinimumRolesSpecification : Specification<User>
{
    private readonly int _minimumRoles;

    public UserWithMinimumRolesSpecification(int minimumRoles)
    {
        _minimumRoles = minimumRoles;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Parents.Count(p => p is Role) >= _minimumRoles;
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.RoleMemberships.Count >= _minimumRoles;
    }
}

/// <summary>
/// Specification for users with maximum number of roles
/// </summary>
public class UserWithMaximumRolesSpecification : Specification<User>
{
    private readonly int _maximumRoles;

    public UserWithMaximumRolesSpecification(int maximumRoles)
    {
        _maximumRoles = maximumRoles;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Parents.Count(p => p is Role) <= _maximumRoles;
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.RoleMemberships.Count <= _maximumRoles;
    }
}

/// <summary>
/// Specification for users with minimum number of groups
/// </summary>
public class UserWithMinimumGroupsSpecification : Specification<User>
{
    private readonly int _minimumGroups;

    public UserWithMinimumGroupsSpecification(int minimumGroups)
    {
        _minimumGroups = minimumGroups;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Parents.Count(p => p is Group) >= _minimumGroups;
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.GroupMemberships.Count >= _minimumGroups;
    }
}

/// <summary>
/// Specification for users with maximum number of groups
/// </summary>
public class UserWithMaximumGroupsSpecification : Specification<User>
{
    private readonly int _maximumGroups;

    public UserWithMaximumGroupsSpecification(int maximumGroups)
    {
        _maximumGroups = maximumGroups;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Parents.Count(p => p is Group) <= _maximumGroups;
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.GroupMemberships.Count <= _maximumGroups;
    }
}

/// <summary>
/// Specification for orphaned users (users without groups or roles)
/// </summary>
public class OrphanedUserSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => !u.Parents.Any();
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return !entity.GroupMemberships.Any() && !entity.RoleMemberships.Any();
    }
}

/// <summary>
/// Specification for users that need permission review
/// </summary>
public class UserNeedsPermissionReviewSpecification : Specification<User>
{
    private readonly int _maxRoles;
    private readonly int _maxPermissions;

    public UserNeedsPermissionReviewSpecification(int maxRoles = 5, int maxPermissions = 50)
    {
        _maxRoles = maxRoles;
        _maxPermissions = maxPermissions;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Parents.Count(p => p is Role) > _maxRoles ||
                   u.Permissions.Count() > _maxPermissions ||
                   u.Parents.Any(parent => parent.Permissions.Any(p => p.Uri.Contains("/admin") || p.Uri.Contains("/system")));
    }

    public override bool IsSatisfiedBy(User entity)
    {
        // Too many roles
        if (entity.RoleMemberships.Count > _maxRoles)
            return true;

        // Too many direct permissions
        if (entity.Permissions.Count > _maxPermissions)
            return true;

        // Has high-risk permissions
        var allPermissions = entity.Permissions.Concat(entity.Parents.SelectMany(p => p.Permissions));
        return allPermissions.Any(p => 
            p.Uri.Contains("/admin", StringComparison.OrdinalIgnoreCase) || 
            p.Uri.Contains("/system", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Builder for creating complex user specifications
/// </summary>
public class UserSpecificationBuilder
{
    private ISpecification<User>? _specification;

    public UserSpecificationBuilder()
    {
        _specification = new TrueSpecification<User>();
    }

    public UserSpecificationBuilder InGroup(int groupId)
    {
        var groupSpec = new UserInGroupSpecification(groupId);
        _specification = _specification?.And(groupSpec) ?? groupSpec;
        return this;
    }

    public UserSpecificationBuilder InGroups(params int[] groupIds)
    {
        var groupsSpec = new UserInGroupsSpecification(groupIds);
        _specification = _specification?.And(groupsSpec) ?? groupsSpec;
        return this;
    }

    public UserSpecificationBuilder WithRole(int roleId)
    {
        var roleSpec = new UserWithRoleSpecification(roleId);
        _specification = _specification?.And(roleSpec) ?? roleSpec;
        return this;
    }

    public UserSpecificationBuilder WithRoles(params int[] roleIds)
    {
        var rolesSpec = new UserWithRolesSpecification(roleIds);
        _specification = _specification?.And(rolesSpec) ?? rolesSpec;
        return this;
    }

    public UserSpecificationBuilder WithRoleName(string roleName, bool exactMatch = true)
    {
        var roleNameSpec = new UserWithRoleNameSpecification(roleName, exactMatch);
        _specification = _specification?.And(roleNameSpec) ?? roleNameSpec;
        return this;
    }

    public UserSpecificationBuilder WithoutGroups()
    {
        var noGroupsSpec = new UserWithoutGroupsSpecification();
        _specification = _specification?.And(noGroupsSpec) ?? noGroupsSpec;
        return this;
    }

    public UserSpecificationBuilder WithoutRoles()
    {
        var noRolesSpec = new UserWithoutRolesSpecification();
        _specification = _specification?.And(noRolesSpec) ?? noRolesSpec;
        return this;
    }

    public UserSpecificationBuilder ThatAreAdmins()
    {
        var adminSpec = new AdminUserSpecification();
        _specification = _specification?.And(adminSpec) ?? adminSpec;
        return this;
    }

    public UserSpecificationBuilder WithPermission(string uri, HttpVerb httpVerb)
    {
        var permSpec = new UserWithPermissionSpecification(uri, httpVerb);
        _specification = _specification?.And(permSpec) ?? permSpec;
        return this;
    }

    public UserSpecificationBuilder WithHighRiskAccess()
    {
        var highRiskSpec = new UserWithHighRiskAccessSpecification();
        _specification = _specification?.And(highRiskSpec) ?? highRiskSpec;
        return this;
    }

    public UserSpecificationBuilder WithMinimumRoles(int count)
    {
        var minRolesSpec = new UserWithMinimumRolesSpecification(count);
        _specification = _specification?.And(minRolesSpec) ?? minRolesSpec;
        return this;
    }

    public UserSpecificationBuilder WithMaximumRoles(int count)
    {
        var maxRolesSpec = new UserWithMaximumRolesSpecification(count);
        _specification = _specification?.And(maxRolesSpec) ?? maxRolesSpec;
        return this;
    }

    public UserSpecificationBuilder WithMinimumGroups(int count)
    {
        var minGroupsSpec = new UserWithMinimumGroupsSpecification(count);
        _specification = _specification?.And(minGroupsSpec) ?? minGroupsSpec;
        return this;
    }

    public UserSpecificationBuilder WithMaximumGroups(int count)
    {
        var maxGroupsSpec = new UserWithMaximumGroupsSpecification(count);
        _specification = _specification?.And(maxGroupsSpec) ?? maxGroupsSpec;
        return this;
    }

    public UserSpecificationBuilder ThatAreOrphaned()
    {
        var orphanedSpec = new OrphanedUserSpecification();
        _specification = _specification?.And(orphanedSpec) ?? orphanedSpec;
        return this;
    }

    public UserSpecificationBuilder ThatNeedReview(int maxRoles = 5, int maxPermissions = 50)
    {
        var reviewSpec = new UserNeedsPermissionReviewSpecification(maxRoles, maxPermissions);
        _specification = _specification?.And(reviewSpec) ?? reviewSpec;
        return this;
    }

    public UserSpecificationBuilder And(ISpecification<User> otherSpec)
    {
        _specification = _specification?.And(otherSpec) ?? otherSpec;
        return this;
    }

    public UserSpecificationBuilder Or(ISpecification<User> otherSpec)
    {
        _specification = _specification?.Or(otherSpec) ?? otherSpec;
        return this;
    }

    public ISpecification<User> Build()
    {
        return _specification ?? new TrueSpecification<User>();
    }

    public static implicit operator Specification<User>(UserSpecificationBuilder builder)
    {
        return (Specification<User>)builder.Build();
    }
}

/// <summary>
/// Extensions for user specifications
/// </summary>
public static class UserSpecificationExtensions
{
    /// <summary>
    /// Creates a specification builder for users
    /// </summary>
    public static UserSpecificationBuilder Specify(this IQueryable<User> query)
    {
        return new UserSpecificationBuilder();
    }

    /// <summary>
    /// Finds users with conflicting role assignments
    /// </summary>
    public static ISpecification<User> WithConflictingRoles(params string[] conflictingRoleNames)
    {
        return new UserSpecificationBuilder()
            .Build();
    }

    /// <summary>
    /// Finds users that violate segregation of duties
    /// </summary>
    public static ISpecification<User> ViolatingSegregationOfDuties()
    {
        // Example: Users with both "Approver" and "Requester" roles
        var approverSpec = new UserWithRoleNameSpecification("Approver");
        var requesterSpec = new UserWithRoleNameSpecification("Requester");
        return approverSpec.And(requesterSpec);
    }

    /// <summary>
    /// Finds users with excessive permissions
    /// </summary>
    public static ISpecification<User> WithExcessivePermissions(int maxRoles = 3, int maxDirectPermissions = 10)
    {
        var tooManyRolesSpec = new UserWithMinimumRolesSpecification(maxRoles + 1);
        var tooManyPermissionsSpec = new EntityWithMinimumPermissionsSpecification(maxDirectPermissions + 1);
        
        var tooManyDirectPermissionsSpec = new EntityWithMinimumPermissionsSpecification(maxDirectPermissions + 1);
        var userWithTooManyPermissions = new UserEntitySpecification();
        
        return tooManyRolesSpec.Or(new UserWithMinimumDirectPermissionsSpecification(maxDirectPermissions + 1));
    }
}

/// <summary>
/// Specification for users with minimum direct permissions
/// </summary>
public class UserWithMinimumDirectPermissionsSpecification : Specification<User>
{
    private readonly int _minimumPermissions;

    public UserWithMinimumDirectPermissionsSpecification(int minimumPermissions)
    {
        _minimumPermissions = minimumPermissions;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return u => u.Permissions.Count >= _minimumPermissions;
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.Permissions.Count >= _minimumPermissions;
    }
}