using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Domain.Validation;

/// <summary>
/// Domain invariants that must be maintained across all operations
/// These are fundamental rules that cannot be violated under any circumstances
/// </summary>
public static class DomainInvariants
{
    /// <summary>
    /// Validates all invariants for an entity
    /// </summary>
    public static IEnumerable<ValidationResult> ValidateInvariants(object entity, IDomainValidationContext context)
    {
        var results = new List<ValidationResult>();

        switch (entity)
        {
            case Entity domainEntity:
                results.AddRange(ValidateEntityInvariants(domainEntity, context));
                break;
            case User user:
                results.AddRange(ValidateUserInvariants(user, context));
                break;
            case Group group:
                results.AddRange(ValidateGroupInvariants(group, context));
                break;
            case Role role:
                results.AddRange(ValidateRoleInvariants(role, context));
                break;
            case Permission permission:
                results.AddRange(ValidatePermissionInvariants(permission, context));
                break;
            case Resource resource:
                results.AddRange(ValidateResourceInvariants(resource, context));
                break;
        }

        return results;
    }

    /// <summary>
    /// Core entity invariants that apply to all entities
    /// </summary>
    private static IEnumerable<ValidationResult> ValidateEntityInvariants(Entity entity, IDomainValidationContext context)
    {
        // INV001: Entity must have a valid ID when persisted
        if (entity.Id < 0)
            yield return new ValidationResult("Entity ID cannot be negative", new[] { nameof(Entity.Id) });

        // INV002: Entity must have a non-empty name
        if (string.IsNullOrWhiteSpace(entity.Name))
            yield return new ValidationResult("Entity name cannot be null or empty", new[] { nameof(Entity.Name) });

        // INV003: Entity name must be within reasonable length
        if (entity.Name.Length > 255)
            yield return new ValidationResult("Entity name cannot exceed 255 characters", new[] { nameof(Entity.Name) });

        // INV004: No self-referential relationships
        if (entity.Parents.Any(p => p.Id == entity.Id) || entity.Children.Any(c => c.Id == entity.Id))
            yield return new ValidationResult("Entity cannot be its own parent or child");

        // INV005: Permissions must be internally consistent
        foreach (var permission in entity.Permissions)
        {
            if (permission.Grant && permission.Deny)
                yield return new ValidationResult($"Permission for {permission.Uri} cannot both grant and deny access");
        }

        // INV006: Parent-child relationships must be bidirectional
        foreach (var child in entity.Children)
        {
            if (!child.Parents.Contains(entity))
                yield return new ValidationResult($"Child entity {child.Name} does not reference this entity as parent");
        }

        foreach (var parent in entity.Parents)
        {
            if (!parent.Children.Contains(entity))
                yield return new ValidationResult($"Parent entity {parent.Name} does not reference this entity as child");
        }
    }

    /// <summary>
    /// User-specific invariants
    /// </summary>
    private static IEnumerable<ValidationResult> ValidateUserInvariants(User user, IDomainValidationContext context)
    {
        // Apply base entity invariants
        foreach (var result in ValidateEntityInvariants(user, context))
            yield return result;

        // INV101: User must have a valid entity reference
        if (user.Entity == null)
            yield return new ValidationResult("User must have an associated Entity", new[] { nameof(User.Entity) });

        // INV102: User entity type must be "User"
        if (user.Entity?.EntityType != "User")
            yield return new ValidationResult("User entity type must be 'User'", new[] { nameof(User.Entity) });

        // Additional user-specific invariants would go here
    }

    /// <summary>
    /// Group-specific invariants
    /// </summary>
    private static IEnumerable<ValidationResult> ValidateGroupInvariants(Group group, IDomainValidationContext context)
    {
        // Apply base entity invariants
        foreach (var result in ValidateEntityInvariants(group, context))
            yield return result;

        // INV201: Group must have a valid entity reference
        if (group.Entity == null)
            yield return new ValidationResult("Group must have an associated Entity", new[] { nameof(Group.Entity) });

        // INV202: Group entity type must be "Group"
        if (group.Entity?.EntityType != "Group")
            yield return new ValidationResult("Group entity type must be 'Group'", new[] { nameof(Group.Entity) });

        // INV203: Group hierarchy must not contain cycles
        if (ContainsCycle(group, new HashSet<int>()))
            yield return new ValidationResult("Group hierarchy contains a cycle");

        // INV204: Group cannot be empty indefinitely (business rule)
        // This might be relaxed in some implementations
        if (IsGroupEmpty(group) && context.Configuration.StrictMode)
            yield return new ValidationResult("Group cannot be permanently empty");
    }

    /// <summary>
    /// Role-specific invariants
    /// </summary>
    private static IEnumerable<ValidationResult> ValidateRoleInvariants(Role role, IDomainValidationContext context)
    {
        // Apply base entity invariants  
        foreach (var result in ValidateEntityInvariants(role, context))
            yield return result;

        // INV301: Role must have a valid entity reference
        if (role.Entity == null)
            yield return new ValidationResult("Role must have an associated Entity", new[] { nameof(Role.Entity) });

        // INV302: Role entity type must be "Role"
        if (role.Entity?.EntityType != "Role")
            yield return new ValidationResult("Role entity type must be 'Role'", new[] { nameof(Role.Entity) });

        // INV303: Role permissions must be valid
        foreach (var permission in role.Permissions)
        {
            if (string.IsNullOrEmpty(permission.Uri))
                yield return new ValidationResult($"Role permission cannot have empty URI");
        }
    }

    /// <summary>
    /// Permission-specific invariants
    /// </summary>
    private static IEnumerable<ValidationResult> ValidatePermissionInvariants(Permission permission, IDomainValidationContext context)
    {
        // INV401: Permission must have a valid URI
        if (string.IsNullOrEmpty(permission.Uri))
            yield return new ValidationResult("Permission URI cannot be null or empty", new[] { nameof(Permission.Uri) });

        // INV402: Permission must have either Grant or Deny set, but not both
        if (permission.Grant && permission.Deny)
            yield return new ValidationResult("Permission cannot both grant and deny access");

        if (!permission.Grant && !permission.Deny)
            yield return new ValidationResult("Permission must either grant or deny access");

        // INV403: HTTP verb must be valid
        if (!Enum.IsDefined(typeof(HttpVerb), permission.HttpVerb))
            yield return new ValidationResult("Permission HTTP verb must be valid", new[] { nameof(Permission.HttpVerb) });

        // INV404: Scheme must be valid
        if (!Enum.IsDefined(typeof(Scheme), permission.Scheme))
            yield return new ValidationResult("Permission scheme must be valid", new[] { nameof(Permission.Scheme) });

        // INV405: URI must be well-formed (basic check)
        if (!IsValidUriPattern(permission.Uri))
            yield return new ValidationResult("Permission URI is not well-formed", new[] { nameof(Permission.Uri) });
    }

    /// <summary>
    /// Resource-specific invariants
    /// </summary>
    private static IEnumerable<ValidationResult> ValidateResourceInvariants(Resource resource, IDomainValidationContext context)
    {
        // INV501: Resource must have a valid URI
        if (string.IsNullOrEmpty(resource.Uri))
            yield return new ValidationResult("Resource URI cannot be null or empty", new[] { nameof(Resource.Uri) });

        // INV502: Resource URI must be well-formed
        if (!IsValidUriPattern(resource.Uri))
            yield return new ValidationResult("Resource URI is not well-formed", new[] { nameof(Resource.Uri) });

        // INV503: Resource type must be specified
        if (string.IsNullOrEmpty(resource.ResourceType))
            yield return new ValidationResult("Resource type cannot be null or empty", new[] { nameof(Resource.ResourceType) });

        // INV504: Resource must be active to be used
        if (!resource.IsActive && context.Configuration.StrictMode)
            yield return new ValidationResult("Resource must be active");

        // INV505: Versioned resources must have valid version
        if (resource.Version != null && string.IsNullOrEmpty(resource.Version))
            yield return new ValidationResult("Resource version cannot be empty if specified", new[] { nameof(Resource.Version) });
    }

    /// <summary>
    /// Cross-entity invariants that span multiple entities
    /// </summary>
    public static IEnumerable<ValidationResult> ValidateCrossEntityInvariants(
        IEnumerable<Entity> entities, 
        IDomainValidationContext context)
    {
        var entityList = entities.ToList();

        // INV901: No duplicate entity names within the same scope
        var duplicateNames = entityList
            .GroupBy(e => new { e.Name, Type = e.GetType().Name })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicateNames)
        {
            yield return new ValidationResult($"Duplicate {duplicate.Type} name found: {duplicate.Name}");
        }

        // INV902: Hierarchical relationships must be consistent
        foreach (var entity in entityList)
        {
            foreach (var child in entity.Children)
            {
                if (!child.Parents.Contains(entity))
                    yield return new ValidationResult($"Inconsistent parent-child relationship: {entity.Name} -> {child.Name}");
            }
        }

        // INV903: Permission inheritance must be calculable
        foreach (var entity in entityList)
        {
            if (HasCircularPermissionInheritance(entity, new HashSet<int>()))
                yield return new ValidationResult($"Circular permission inheritance detected for entity: {entity.Name}");
        }
    }

    /// <summary>
    /// Validates system-wide invariants
    /// </summary>
    public static async Task<IEnumerable<ValidationResult>> ValidateSystemInvariantsAsync(
        IDomainValidationContext context)
    {
        var results = new List<ValidationResult>();

        try
        {
            // SYSINV001: System must have at least one admin user
            var adminUsers = await context.DbContext.Users
                .Where(u => u.Entity.Parents.Any(p => p.Name == "Administrators"))
                .CountAsync();

            if (adminUsers == 0)
                results.Add(new ValidationResult("System must have at least one administrator user"));

            // SYSINV002: Default roles must exist
            var requiredRoles = new[] { "Administrator", "User", "Guest" };
            var existingRoles = await context.DbContext.Roles
                .Where(r => requiredRoles.Contains(r.Name))
                .Select(r => r.Name)
                .ToListAsync();

            var missingRoles = requiredRoles.Except(existingRoles);
            foreach (var missingRole in missingRoles)
            {
                results.Add(new ValidationResult($"Required system role missing: {missingRole}"));
            }

            // SYSINV003: System resources must be protected
            var systemResources = await context.DbContext.Resources
                .Where(r => r.Uri.StartsWith("/system/"))
                .ToListAsync();

            foreach (var resource in systemResources)
            {
                var hasProtection = await context.DbContext.PermissionSchemes
                    .AnyAsync(ps => ps.UriAccesses.Any(ua => ua.Resource.Uri == resource.Uri));

                if (!hasProtection)
                    results.Add(new ValidationResult($"System resource not protected: {resource.Uri}"));
            }
        }
        catch (Exception ex)
        {
            results.Add(new ValidationResult($"Error validating system invariants: {ex.Message}"));
        }

        return results;
    }

    #region Helper Methods

    private static bool ContainsCycle(Group group, HashSet<int> visited)
    {
        if (visited.Contains(group.Id))
            return true;

        visited.Add(group.Id);

        foreach (var parent in group.Parents.OfType<Group>())
        {
            if (ContainsCycle(parent, new HashSet<int>(visited)))
                return true;
        }

        return false;
    }

    private static bool IsGroupEmpty(Group group)
    {
        // A group is considered empty if it has no users and no child groups
        return !group.Children.Any();
    }

    private static bool IsValidUriPattern(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return false;

        try
        {
            // Basic URI validation - in production, this would be more sophisticated
            if (uri.StartsWith("/") || uri.Contains("://"))
            {
                return true; // Simplified validation
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasCircularPermissionInheritance(Entity entity, HashSet<int> visited)
    {
        if (visited.Contains(entity.Id))
            return true;

        visited.Add(entity.Id);

        foreach (var parent in entity.Parents)
        {
            if (HasCircularPermissionInheritance(parent, new HashSet<int>(visited)))
                return true;
        }

        return false;
    }

    #endregion
}

/// <summary>
/// Attribute to mark methods that must maintain domain invariants
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class)]
public class MaintainsInvariantsAttribute : Attribute
{
    public string[] InvariantIds { get; }
    public bool ValidateOnEntry { get; set; } = true;
    public bool ValidateOnExit { get; set; } = true;

    public MaintainsInvariantsAttribute(params string[] invariantIds)
    {
        InvariantIds = invariantIds ?? Array.Empty<string>();
    }
}

/// <summary>
/// Exception thrown when domain invariants are violated
/// </summary>
public class DomainInvariantViolationException : Exception
{
    public string InvariantId { get; }
    public object? Entity { get; }
    public IEnumerable<ValidationResult> ValidationResults { get; }

    public DomainInvariantViolationException(string invariantId, string message, object? entity = null) 
        : base(message)
    {
        InvariantId = invariantId;
        Entity = entity;
        ValidationResults = Enumerable.Empty<ValidationResult>();
    }

    public DomainInvariantViolationException(string invariantId, IEnumerable<ValidationResult> validationResults, object? entity = null)
        : base($"Domain invariant {invariantId} violated: {string.Join(", ", validationResults.Select(v => v.ErrorMessage))}")
    {
        InvariantId = invariantId;
        Entity = entity;
        ValidationResults = validationResults;
    }
}