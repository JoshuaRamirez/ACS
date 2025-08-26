using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

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
    public static IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> ValidateInvariants(object entity, IDomainValidationContext context)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        switch (entity)
        {
            case User user:
                results.AddRange(ValidateEntityInvariants(user, context));
                results.AddRange(ValidateUserInvariants(user, context));
                break;
            case Group group:
                results.AddRange(ValidateEntityInvariants(group, context));
                results.AddRange(ValidateGroupInvariants(group, context));
                break;
            case Role role:
                results.AddRange(ValidateEntityInvariants(role, context));
                results.AddRange(ValidateRoleInvariants(role, context));
                break;
            case Permission permission:
                results.AddRange(ValidatePermissionInvariants(permission, context));
                break;
            case Resource resource:
                results.AddRange(ValidateResourceInvariants(resource, context));
                break;
            case Entity domainEntity:
                results.AddRange(ValidateEntityInvariants(domainEntity, context));
                break;
        }

        return results;
    }

    /// <summary>
    /// Core entity invariants that apply to all entities
    /// </summary>
    private static IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> ValidateEntityInvariants(Entity entity, IDomainValidationContext context)
    {
        // INV001: Entity must have a valid ID when persisted
        if (entity.Id < 0)
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Entity ID cannot be negative", new[] { nameof(Entity.Id) }.AsEnumerable());

        // INV002: Entity must have a non-empty name
        if (string.IsNullOrWhiteSpace(entity.Name))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Entity name cannot be null or empty", new[] { nameof(Entity.Name) }.AsEnumerable());

        // INV003: Entity name must be within reasonable length
        if (entity.Name.Length > 255)
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Entity name cannot exceed 255 characters", new[] { nameof(Entity.Name) }.AsEnumerable());

        // INV004: No self-referential relationships
        if (entity.Parents.Any(p => p.Id == entity.Id) || entity.Children.Any(c => c.Id == entity.Id))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Entity cannot be its own parent or child");

        // INV005: Permissions must be internally consistent
        foreach (var permission in entity.Permissions)
        {
            if (permission.Grant && permission.Deny)
                yield return new System.ComponentModel.DataAnnotations.ValidationResult($"Permission for {permission.Uri} cannot both grant and deny access");
        }

        // INV006: Parent-child relationships must be bidirectional
        foreach (var child in entity.Children)
        {
            if (!child.Parents.Contains(entity))
                yield return new System.ComponentModel.DataAnnotations.ValidationResult($"Child entity {child.Name} does not reference this entity as parent");
        }

        foreach (var parent in entity.Parents)
        {
            if (!parent.Children.Contains(entity))
                yield return new System.ComponentModel.DataAnnotations.ValidationResult($"Parent entity {parent.Name} does not reference this entity as child");
        }
    }

    /// <summary>
    /// User-specific invariants
    /// </summary>
    private static IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> ValidateUserInvariants(User user, IDomainValidationContext context)
    {
        // Apply base entity invariants
        foreach (var result in ValidateEntityInvariants(user, context))
            yield return result;

        // INV101: User ID must be valid when persisted
        if (user.Id < 0)
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("User ID cannot be negative", new[] { nameof(User.Id) });

        // INV102: User entity type must be "User" 
        // Note: User inherits from Entity, so this validation is handled in Entity invariants

        // Additional user-specific invariants would go here
    }

    /// <summary>
    /// Group-specific invariants
    /// </summary>
    private static IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> ValidateGroupInvariants(Group group, IDomainValidationContext context)
    {
        // Apply base entity invariants
        foreach (var result in ValidateEntityInvariants(group, context))
            yield return result;

        // INV201: Group ID must be valid when persisted
        if (group.Id < 0)
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Group ID cannot be negative", new[] { nameof(Group.Id) });

        // INV202: Group entity type validation
        // Note: Group inherits from Entity, so this validation is handled in Entity invariants

        // INV203: Group hierarchy must not contain cycles
        if (ContainsCycle(group, new HashSet<int>()))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Group hierarchy contains a cycle");

        // INV204: Group cannot be empty indefinitely (business rule)
        // This might be relaxed in some implementations
        if (IsGroupEmpty(group) && context.Configuration.StrictMode)
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Group cannot be permanently empty");
    }

    /// <summary>
    /// Role-specific invariants
    /// </summary>
    private static IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> ValidateRoleInvariants(Role role, IDomainValidationContext context)
    {
        // Apply base entity invariants  
        foreach (var result in ValidateEntityInvariants(role, context))
            yield return result;

        // INV301: Role ID must be valid when persisted
        if (role.Id < 0)
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Role ID cannot be negative", new[] { nameof(Role.Id) });

        // INV302: Role entity type validation
        // Note: Role inherits from Entity, so this validation is handled in Entity invariants

        // INV303: Role permissions must be valid
        foreach (var permission in role.Permissions)
        {
            if (string.IsNullOrEmpty(permission.Uri))
                yield return new System.ComponentModel.DataAnnotations.ValidationResult($"Role permission cannot have empty URI");
        }
    }

    /// <summary>
    /// Permission-specific invariants
    /// </summary>
    private static IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> ValidatePermissionInvariants(Permission permission, IDomainValidationContext context)
    {
        // INV401: Permission must have a valid URI
        if (string.IsNullOrEmpty(permission.Uri))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Permission URI cannot be null or empty", new[] { nameof(Permission.Uri) });

        // INV402: Permission must have either Grant or Deny set, but not both
        if (permission.Grant && permission.Deny)
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Permission cannot both grant and deny access");

        if (!permission.Grant && !permission.Deny)
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Permission must either grant or deny access");

        // INV403: HTTP verb must be valid
        if (!Enum.IsDefined(typeof(HttpVerb), permission.HttpVerb))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Permission HTTP verb must be valid", new[] { nameof(Permission.HttpVerb) });

        // INV404: Scheme must be valid
        if (!Enum.IsDefined(typeof(Scheme), permission.Scheme))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Permission scheme must be valid", new[] { nameof(Permission.Scheme) });

        // INV405: URI must be well-formed (basic check)
        if (!IsValidUriPattern(permission.Uri))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Permission URI is not well-formed", new[] { nameof(Permission.Uri) });
    }

    /// <summary>
    /// Resource-specific invariants
    /// </summary>
    private static IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> ValidateResourceInvariants(Resource resource, IDomainValidationContext context)
    {
        // INV501: Resource must have a valid URI
        if (string.IsNullOrEmpty(resource.Uri))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Resource URI cannot be null or empty", new[] { nameof(Resource.Uri) });

        // INV502: Resource URI must be well-formed
        if (!IsValidUriPattern(resource.Uri))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Resource URI is not well-formed", new[] { nameof(Resource.Uri) });

        // INV503: Resource type must be specified
        if (string.IsNullOrEmpty(resource.ResourceType))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Resource type cannot be null or empty", new[] { nameof(Resource.ResourceType) });

        // INV504: Resource must be active to be used
        if (!resource.IsActive && context.Configuration.StrictMode)
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Resource must be active");

        // INV505: Versioned resources must have valid version
        if (resource.Version != null && string.IsNullOrEmpty(resource.Version))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult("Resource version cannot be empty if specified", new[] { nameof(Resource.Version) });
    }

    /// <summary>
    /// Cross-entity invariants that span multiple entities
    /// </summary>
    public static IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> ValidateCrossEntityInvariants(
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
            yield return new System.ComponentModel.DataAnnotations.ValidationResult($"Duplicate {duplicate.Type} name found: {duplicate.Name}");
        }

        // INV902: Hierarchical relationships must be consistent
        foreach (var entity in entityList)
        {
            foreach (var child in entity.Children)
            {
                if (!child.Parents.Contains(entity))
                    yield return new System.ComponentModel.DataAnnotations.ValidationResult($"Inconsistent parent-child relationship: {entity.Name} -> {child.Name}");
            }
        }

        // INV903: Permission inheritance must be calculable
        foreach (var entity in entityList)
        {
            if (HasCircularPermissionInheritance(entity, new HashSet<int>()))
                yield return new System.ComponentModel.DataAnnotations.ValidationResult($"Circular permission inheritance detected for entity: {entity.Name}");
        }
    }

    /// <summary>
    /// Validates system-wide invariants
    /// </summary>
    public static async Task<IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult>> ValidateSystemInvariantsAsync(
        IDomainValidationContext context)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        try
        {
            // SYSINV001: System must have at least one admin user
            // Note: Using UserRoles instead of domain Parents since DbContext uses data models
            var adminUsers = await context.DbContext.UserRoles
                .Where(ur => ur.Role.Name == "Administrator")
                .CountAsync();

            if (adminUsers == 0)
                results.Add(new System.ComponentModel.DataAnnotations.ValidationResult("System must have at least one administrator user"));

            // SYSINV002: Default roles must exist
            var requiredRoles = new[] { "Administrator", "User", "Guest" };
            var existingRoles = await context.DbContext.Roles
                .Where(r => requiredRoles.Contains(r.Name))
                .Select(r => r.Name)
                .ToListAsync();

            var missingRoles = requiredRoles.Except(existingRoles);
            foreach (var missingRole in missingRoles)
            {
                results.Add(new System.ComponentModel.DataAnnotations.ValidationResult($"Required system role missing: {missingRole}"));
            }

            // SYSINV003: System resources must be protected
            var systemResources = await context.DbContext.Resources
                .Where(r => r.Uri.StartsWith("/system/"))
                .ToListAsync();

            foreach (var resource in systemResources)
            {
                var hasProtection = await context.DbContext.UriAccesses
                    .AnyAsync(ua => ua.Resource.Uri == resource.Uri);

                if (!hasProtection)
                    results.Add(new System.ComponentModel.DataAnnotations.ValidationResult($"System resource not protected: {resource.Uri}"));
            }
        }
        catch (Exception ex)
        {
            results.Add(new System.ComponentModel.DataAnnotations.ValidationResult($"Error validating system invariants: {ex.Message}"));
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
        : base($"Domain invariant {invariantId} violated: {string.Join(", ", validationResults.Where(v => v != null).Select(v => v.ToString()))}")
    {
        InvariantId = invariantId;
        Entity = entity;
        ValidationResults = validationResults;
    }
}