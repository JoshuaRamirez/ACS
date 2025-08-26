using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Domain.Specifications;

/// <summary>
/// Complex queries for permission analysis and reporting
/// </summary>
public static class ComplexPermissionQueries
{
    /// <summary>
    /// Finds users who have access to sensitive resources but lack proper justification
    /// </summary>
    public static class SensitiveAccessAnalysis
    {
        public static ISpecification<User> UsersWithUnjustifiedSensitiveAccess()
        {
            var sensitiveResourceSpec = new UriPatternPermissionSpecification("/admin/*")
                .Or(new UriPatternPermissionSpecification("/system/*"))
                .Or(new UriPatternPermissionSpecification("/config/*"))
                .Or(new UriPatternPermissionSpecification("/security/*"));

            var effectivePermissionSpec = new EffectivePermissionSpecification();
            var combinedSpec = sensitiveResourceSpec.And(effectivePermissionSpec);

            var entityWithPermSpec = new EntityWithPermissionSpecification(combinedSpec);
            var userSpec = new UserEntitySpecification();
            return entityWithPermSpec.And(userSpec).Cast<User>();
        }

        public static ISpecification<User> UsersWithDeletePermissions()
        {
            var deletePermissionSpec = new HttpVerbPermissionSpecification(HttpVerb.DELETE)
                .And(new EffectivePermissionSpecification());

            var entityWithPermSpec = new EntityWithPermissionSpecification(deletePermissionSpec);
            var userSpec = new UserEntitySpecification();
            return entityWithPermSpec.And(userSpec).Cast<User>();
        }

        public static ISpecification<User> UsersWithWildcardPermissions()
        {
            var wildcardSpec = new WildcardPermissionSpecification()
                .And(new EffectivePermissionSpecification());

            var entityWithPermSpec = new EntityWithPermissionSpecification(wildcardSpec);
            var userSpec = new UserEntitySpecification();
            return entityWithPermSpec.And(userSpec).Cast<User>();
        }
    }

    /// <summary>
    /// Compliance-related queries for audit and reporting
    /// </summary>
    public static class ComplianceQueries
    {
        public static ISpecification<User> UsersViolatingLeastPrivilege(int maxRoles = 3, int maxPermissions = 20)
        {
            return new UserSpecificationBuilder()
                .WithMinimumRoles(maxRoles + 1)
                .Or(new EntityWithMinimumPermissionsSpecification(maxPermissions + 1)
                    .And(new UserEntitySpecification()).Cast<User>())
                .Build();
        }

        public static ISpecification<User> UsersWithSegregationOfDutiesViolation()
        {
            // Example: Users with both financial approval and processing permissions
            var approvalPermissionSpec = new UriPatternPermissionSpecification("/finance/approve/*")
                .And(new EffectivePermissionSpecification());

            var processingPermissionSpec = new UriPatternPermissionSpecification("/finance/process/*")
                .And(new EffectivePermissionSpecification());

            var hasApprovalSpec = new EntityWithPermissionSpecification(approvalPermissionSpec);
            var hasProcessingSpec = new EntityWithPermissionSpecification(processingPermissionSpec);

            var combinedSpec = hasApprovalSpec.And(hasProcessingSpec);
            var userSpec = new UserEntitySpecification();
            return combinedSpec.And(userSpec).Cast<User>();
        }

        public static ISpecification<User> AdminUsersWithoutMFA()
        {
            // This would require additional user properties for MFA status
            return new AdminUserSpecification()
                .And(new UserSpecificationBuilder().Build()); // Placeholder for MFA check
        }

        public static ISpecification<Permission> PermissionsRequiringApproval()
        {
            return new HighRiskPermissionSpecification()
                .Or(new AdministrativePermissionSpecification())
                .Or(new UriPatternPermissionSpecification("/finance/*"))
                .Or(new HttpVerbPermissionSpecification(HttpVerb.DELETE));
        }
    }

    /// <summary>
    /// Security analysis queries
    /// </summary>
    public static class SecurityAnalysis
    {
        public static ISpecification<User> OverprivilegedUsers()
        {
            return new UserSpecificationBuilder()
                .WithHighRiskAccess()
                .WithMinimumRoles(5)
                .Build();
        }

        public static ISpecification<User> UsersWithConflictingRoles()
        {
            // Example: Users with both audit and system admin roles
            var auditRoleSpec = new UserWithRoleNameSpecification("Auditor");
            var sysAdminRoleSpec = new UserWithRoleNameSpecification("SystemAdmin");
            
            return auditRoleSpec.And(sysAdminRoleSpec);
        }

        public static ISpecification<Permission> SuspiciousPermissions()
        {
            // Permissions that might indicate privilege escalation
            var wildcardAdminSpec = new UriPatternPermissionSpecification("/admin/*")
                .And(new WildcardPermissionSpecification());

            var allVerbsSpec = new UriPatternPermissionSpecification("*")
                .And(new EffectivePermissionSpecification());

            return wildcardAdminSpec.Or(allVerbsSpec);
        }

        public static ISpecification<Entity> EntitiesWithCircularHierarchies()
        {
            return new CircularHierarchySpecification();
        }

        private static bool HasCircularHierarchy(Entity entity, HashSet<int>? visited = null)
        {
            visited ??= new HashSet<int>();
            
            if (visited.Contains(entity.Id))
                return true;
                
            visited.Add(entity.Id);
            
            return entity.Parents.Any(parent => HasCircularHierarchy(parent, new HashSet<int>(visited)));
        }
    }

    /// <summary>
    /// Permission effectiveness queries
    /// </summary>
    public static class PermissionEffectiveness
    {
        public static ISpecification<Permission> RedundantPermissions()
        {
            return new RedundantPermissionSpecification();
        }

        public static ISpecification<Permission> IneffectivePermissions()
        {
            return new DenyPermissionSpecification()
                .Or(new InactivePermissionSpecification());
        }

        public static ISpecification<User> UsersWithRedundantRoleAssignments()
        {
            // Users assigned to roles that provide duplicate permissions
            // This would require complex analysis of role permissions
            return new UserSpecificationBuilder()
                .WithMinimumRoles(2)
                .Build();
        }

        public static ISpecification<Entity> UnusedEntities()
        {
            // Entities without permissions and without children
            return new EntityWithoutPermissionsSpecification()
                .And(new LeafEntitySpecification());
        }
    }

    /// <summary>
    /// Resource access pattern analysis
    /// </summary>
    public static class AccessPatternAnalysis
    {
        public static ISpecification<User> UsersAccessingMultipleSensitiveSystems()
        {
            var systemPatterns = new[]
            {
                "/finance/*", "/hr/*", "/legal/*", "/security/*", "/admin/*"
            };

            var combinedSpec = systemPatterns
                .Select(pattern => new UriPatternPermissionSpecification(pattern)
                    .And(new EffectivePermissionSpecification()))
                .Cast<ISpecification<Permission>>()
                .Aggregate((spec1, spec2) => spec1.Or(spec2));

            var entityWithPermSpec = new EntityWithPermissionSpecification(combinedSpec);
            var userSpec = new UserEntitySpecification();
            return entityWithPermSpec.And(userSpec).Cast<User>();
        }

        public static ISpecification<Permission> CrossSystemPermissions()
        {
            // Permissions that span multiple business domains
            var financialSpec = new UriPatternPermissionSpecification("/finance/*");
            var hrSpec = new UriPatternPermissionSpecification("/hr/*");
            var legalSpec = new UriPatternPermissionSpecification("/legal/*");

            return financialSpec.Or(hrSpec).Or(legalSpec);
        }

        public static ISpecification<User> ExternalSystemAccessUsers()
        {
            // Users with permissions to external APIs or integrations
            var externalApiSpec = new UriPatternPermissionSpecification("/external/*")
                .Or(new UriPatternPermissionSpecification("/api/external/*"))
                .Or(new UriPatternPermissionSpecification("/integration/*"));

            var permissionSpec = externalApiSpec.And(new EffectivePermissionSpecification());
            var entityWithPermSpec = new EntityWithPermissionSpecification(permissionSpec);
            var userSpec = new UserEntitySpecification();
            return entityWithPermSpec.And(userSpec).Cast<User>();
        }
    }

    /// <summary>
    /// Hierarchical permission queries
    /// </summary>
    public static class HierarchicalQueries
    {
        public static ISpecification<User> UsersInheritingFromMultipleSources()
        {
            return new UserSpecificationBuilder()
                .WithMinimumRoles(2)
                .WithMinimumGroups(1)
                .Build();
        }

        public static ISpecification<Group> GroupsWithComplexHierarchies()
        {
            var groupWithPermissionsSpec = new EntityWithMinimumPermissionsSpecification(1);
            var hasChildrenSpec = new EntityWithChildrenSpecification();
            var groupSpec = new GroupEntitySpecification();
            return groupWithPermissionsSpec.And(hasChildrenSpec).And(groupSpec).Cast<Group>();
        }

        public static ISpecification<Role> RolesWithInheritedPermissions()
        {
            // Roles that are also assigned to groups (inheriting group permissions)
            return new RoleWithInheritedPermissionsSpecification();
        }

        public static ISpecification<Entity> DeepHierarchyEntities(int minDepth = 3)
        {
            return new EntityAtDepthSpecification(minDepth)
                .Or(new EntityAtDepthSpecification(minDepth + 1))
                .Or(new EntityAtDepthSpecification(minDepth + 2));
        }
    }

    /// <summary>
    /// Data protection and privacy queries
    /// </summary>
    public static class DataProtectionQueries
    {
        public static ISpecification<User> UsersWithPersonalDataAccess()
        {
            var personalDataPatterns = new[]
            {
                "/users/personal/*", "/employees/data/*", "/customers/pii/*", 
                "/medical/*", "/financial/personal/*"
            };

            var personalDataSpec = personalDataPatterns
                .Select(pattern => new UriPatternPermissionSpecification(pattern))
                .Cast<ISpecification<Permission>>()
                .Aggregate((spec1, spec2) => spec1.Or(spec2));

            var permissionSpec = personalDataSpec.And(new EffectivePermissionSpecification());
            var entityWithPermSpec = new EntityWithPermissionSpecification(permissionSpec);
            var userSpec = new UserEntitySpecification();
            return entityWithPermSpec.And(userSpec).Cast<User>();
        }

        public static ISpecification<Permission> DataExportPermissions()
        {
            var exportPatterns = new[]
            {
                "/export/*", "/download/*", "/backup/*", "/extract/*"
            };

            return exportPatterns
                .Select(pattern => new UriPatternPermissionSpecification(pattern))
                .Cast<ISpecification<Permission>>()
                .Aggregate((spec1, spec2) => spec1.Or(spec2))
                .And(new EffectivePermissionSpecification());
        }

        public static ISpecification<User> UsersWithBulkDataAccess()
        {
            var bulkOperationSpec = new UriPatternPermissionSpecification("/bulk/*")
                .Or(new UriPatternPermissionSpecification("/batch/*"))
                .Or(new UriPatternPermissionSpecification("/mass/*"));

            var permissionSpec = bulkOperationSpec.And(new EffectivePermissionSpecification());
            var entityWithPermSpec = new EntityWithPermissionSpecification(permissionSpec);
            var userSpec = new UserEntitySpecification();
            return entityWithPermSpec.And(userSpec).Cast<User>();
        }
    }

    /// <summary>
    /// Query builder for complex permission analysis
    /// </summary>
    public class ComplexPermissionQueryBuilder
    {
        private readonly List<ISpecification<Permission>> _permissionSpecs = new();
        private readonly List<ISpecification<User>> _userSpecs = new();
        private readonly List<ISpecification<Entity>> _entitySpecs = new();

        public ComplexPermissionQueryBuilder WithSensitiveResources()
        {
            _permissionSpecs.Add(new HighRiskPermissionSpecification());
            return this;
        }

        public ComplexPermissionQueryBuilder WithAdministrativeAccess()
        {
            _permissionSpecs.Add(new AdministrativePermissionSpecification());
            return this;
        }

        public ComplexPermissionQueryBuilder WithWritePermissions()
        {
            _permissionSpecs.Add(new WritePermissionSpecification());
            return this;
        }

        public ComplexPermissionQueryBuilder ForUsers()
        {
            _entitySpecs.Add(new UserEntitySpecification());
            return this;
        }

        public ComplexPermissionQueryBuilder ForAdminUsers()
        {
            _userSpecs.Add(new AdminUserSpecification());
            return this;
        }

        public ComplexPermissionQueryBuilder WithExcessiveRoles(int maxRoles = 3)
        {
            _userSpecs.Add(new UserWithMinimumRolesSpecification(maxRoles + 1));
            return this;
        }

        public ISpecification<User> BuildUserQuery()
        {
            ISpecification<User> spec = new TrueSpecification<User>();

            foreach (var userSpec in _userSpecs)
            {
                spec = spec.And(userSpec);
            }

            foreach (var permSpec in _permissionSpecs)
            {
                var entityWithPermSpec = new EntityWithPermissionSpecification(permSpec);
                var userWithPermSpec = entityWithPermSpec.And(new UserEntitySpecification()).Cast<User>();
                spec = spec.And(userWithPermSpec);
            }

            return spec;
        }

        public ISpecification<Permission> BuildPermissionQuery()
        {
            if (!_permissionSpecs.Any())
                return new TrueSpecification<Permission>();

            var spec = _permissionSpecs[0];
            for (int i = 1; i < _permissionSpecs.Count; i++)
            {
                spec = spec.And(_permissionSpecs[i]);
            }

            return spec;
        }
    }
}

/// <summary>
/// Extension methods for executing complex queries
/// </summary>
public static class ComplexQueryExtensions
{
    /// <summary>
    /// Executes a security analysis query
    /// </summary>
    public static async Task<SecurityAnalysisResult> ExecuteSecurityAnalysisAsync<T>(
        this IQueryable<T> query, 
        ISpecification<T> specification) where T : class
    {
        var results = await query.Where(specification.ToExpression()).ToListAsync();
        
        return new SecurityAnalysisResult
        {
            EntityType = typeof(T).Name,
            TotalEntities = await query.CountAsync(),
            MatchingEntities = results.Count,
            RiskLevel = DetermineRiskLevel<T>(results.Count),
            Entities = results.Cast<object>().ToList(),
            AnalysisDate = DateTime.UtcNow,
            QueryDescription = specification.GetType().Name
        };
    }

    private static string DetermineRiskLevel<T>(int matchingCount)
    {
        return matchingCount switch
        {
            0 => "Low",
            <= 5 => "Medium",
            <= 20 => "High",
            _ => "Critical"
        };
    }
}

/// <summary>
/// Result of security analysis queries
/// </summary>
public class UserEntitySpecification : TypedEntitySpecification<User>
{
    // Inherits default implementation from TypedEntitySpecification<User>
    // which checks if entity is User and returns true for GetTypedExpression
}

public class GroupEntitySpecification : TypedEntitySpecification<Group>
{
    // Inherits default implementation from TypedEntitySpecification<Group>
    // which checks if entity is Group and returns true for GetTypedExpression
}

public class RoleWithInheritedPermissionsSpecification : Specification<Role>
{
    public override Expression<Func<Role, bool>> ToExpression()
    {
        return r => r.Parents.Any(p => p is Group);
    }

    public override bool IsSatisfiedBy(Role entity)
    {
        return entity.Parents.Any(p => p is Group);
    }
}

public class SecurityAnalysisResult
{
    public string EntityType { get; set; } = string.Empty;
    public int TotalEntities { get; set; }
    public int MatchingEntities { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public List<object> Entities { get; set; } = new();
    public DateTime AnalysisDate { get; set; }
    public string QueryDescription { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}

public class CircularHierarchySpecification : Specification<Entity>
{
    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => false; // Placeholder - would need complex cycle detection
    }
    
    public override bool IsSatisfiedBy(Entity entity)
    {
        return HasCircularHierarchy(entity);
    }
    
    private static bool HasCircularHierarchy(Entity entity, HashSet<int>? visited = null)
    {
        visited ??= new HashSet<int>();
        
        if (visited.Contains(entity.Id))
            return true;
            
        visited.Add(entity.Id);
        
        return entity.Parents.Any(parent => HasCircularHierarchy(parent, new HashSet<int>(visited)));
    }
}

public class RedundantPermissionSpecification : Specification<Permission>
{
    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => p.Grant && p.Deny;
    }
}

public class InactivePermissionSpecification : Specification<Permission>
{
    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => !p.Grant && !p.Deny;
    }
}


public static class SpecificationCastExtensions
{
    public static ISpecification<TTarget> Cast<TTarget>(this ISpecification<Entity> entitySpec) where TTarget : Entity
    {
        return new CastedSpecification<TTarget>(entitySpec);
    }
}

public class CastedSpecification<T> : Specification<T> where T : Entity
{
    private readonly ISpecification<Entity> _entitySpec;
    
    public CastedSpecification(ISpecification<Entity> entitySpec)
    {
        _entitySpec = entitySpec;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var entityExpression = _entitySpec.ToExpression();
        var parameter = Expression.Parameter(typeof(T), entityExpression.Parameters[0].Name);
        var body = new ReplaceExpressionVisitor(entityExpression.Parameters[0], parameter).Visit(entityExpression.Body);
        return Expression.Lambda<Func<T, bool>>(body!, parameter);
    }
    
    public override bool IsSatisfiedBy(T entity)
    {
        return _entitySpec.IsSatisfiedBy(entity);
    }
}