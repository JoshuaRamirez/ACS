using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Services;

namespace ACS.Service.Domain.Validation;

/// <summary>
/// Validates that an entity name is unique within its scope
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class UniqueEntityNameAttribute : DomainValidationAttribute
{
    public Type EntityType { get; set; } = typeof(Entity);
    public string? ScopeProperty { get; set; }
    public bool CaseInsensitive { get; set; } = true;

    public override System.ComponentModel.DataAnnotations.ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (value is not string name || string.IsNullOrEmpty(name))
            return System.ComponentModel.DataAnnotations.ValidationResult.Success;

        var entity = validationContext.ObjectInstance as Entity;
        if (entity == null)
            return new System.ComponentModel.DataAnnotations.ValidationResult("Validation can only be applied to Entity types", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());

        // Check cache first
        var cacheKey = $"unique_name_{EntityType.Name}_{name}_{ScopeProperty}";
        var cachedResult = domainContext.ValidationCache.GetAsync<string>(cacheKey).Result;
        if (cachedResult != null && bool.TryParse(cachedResult, out var isUnique))
        {
            return isUnique ? System.ComponentModel.DataAnnotations.ValidationResult.Success : 
                new System.ComponentModel.DataAnnotations.ValidationResult($"An entity with name '{name}' already exists", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());
        }

        // Query database using reflection-based approach since EntityType is runtime Type
        var setMethod = typeof(DbContext).GetMethod("Set", new Type[0])!.MakeGenericMethod(EntityType);
        var dbSet = setMethod.Invoke(domainContext.DbContext, null);
        var query = (IQueryable<object>)dbSet!;
        
        if (CaseInsensitive)
            query = query.Where(e => EF.Functions.Like(EF.Property<string>(e, "Name"), name));
        else
            query = query.Where(e => EF.Property<string>(e, "Name") == name);

        // Exclude current entity if updating
        if (entity.Id > 0)
            query = query.Where(e => EF.Property<int>(e, "Id") != entity.Id);

        // Apply scope if specified
        if (!string.IsNullOrEmpty(ScopeProperty) && validationContext.ObjectInstance != null)
        {
            var scopeValue = validationContext.ObjectType.GetProperty(ScopeProperty)?.GetValue(validationContext.ObjectInstance);
            if (scopeValue != null)
                query = query.Where(e => EF.Property<object>(e, ScopeProperty).Equals(scopeValue));
        }

        var exists = query.Any();
        
        // Cache result
        _ = domainContext.ValidationCache.SetAsync(cacheKey, (!exists).ToString(), domainContext.Configuration.CacheExpiration);

        return exists ? new System.ComponentModel.DataAnnotations.ValidationResult($"An entity with name '{name}' already exists", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>()) : System.ComponentModel.DataAnnotations.ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a hierarchy doesn't create cycles
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class NoCyclicHierarchyAttribute : DomainValidationAttribute
{
    public int MaxDepth { get; set; } = 50;

    public override System.ComponentModel.DataAnnotations.ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var entity = validationContext.ObjectInstance as Entity;
        if (entity == null || value is not Entity parentEntity)
            return System.ComponentModel.DataAnnotations.ValidationResult.Success;

        // Cannot be parent of itself
        if (entity.Id == parentEntity.Id && entity.Id > 0)
            return new System.ComponentModel.DataAnnotations.ValidationResult("Entity cannot be its own parent", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());

        // Check for cycles using breadth-first search
        var visited = new HashSet<int>();
        var queue = new Queue<Entity>();
        queue.Enqueue(parentEntity);
        visited.Add(parentEntity.Id);
        
        var depth = 0;
        while (queue.Count > 0 && depth < MaxDepth)
        {
            var current = queue.Dequeue();
            
            // If we find the original entity in the parent chain, there's a cycle
            if (current.Id == entity.Id && entity.Id > 0)
                return new System.ComponentModel.DataAnnotations.ValidationResult("Adding this parent would create a circular hierarchy", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());

            // Add current entity's parents to queue
            foreach (var parent in current.Parents)
            {
                if (!visited.Contains(parent.Id))
                {
                    visited.Add(parent.Id);
                    queue.Enqueue(parent);
                }
            }
            
            depth++;
        }

        if (depth >= MaxDepth)
            return new System.ComponentModel.DataAnnotations.ValidationResult($"Hierarchy depth exceeds maximum of {MaxDepth} levels", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());

        return System.ComponentModel.DataAnnotations.ValidationResult.Success;
    }
}

/// <summary>
/// Validates maximum number of children for an entity
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class MaxChildrenAttribute : DomainValidationAttribute
{
    public int Maximum { get; }

    public MaxChildrenAttribute(int maximum)
    {
        Maximum = maximum;
    }

    public override System.ComponentModel.DataAnnotations.ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var entity = validationContext.ObjectInstance as Entity;
        if (entity == null)
            return System.ComponentModel.DataAnnotations.ValidationResult.Success;

        if (entity.Children.Count >= Maximum)
            return new System.ComponentModel.DataAnnotations.ValidationResult($"Entity cannot have more than {Maximum} children", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());

        return System.ComponentModel.DataAnnotations.ValidationResult.Success;
    }
}

/// <summary>
/// Validates that URI patterns are well-formed
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class ValidUriPatternAttribute : DomainValidationAttribute
{
    public bool AllowWildcards { get; set; } = true;
    public bool AllowParameters { get; set; } = true;
    public string[]? AllowedSchemes { get; set; }

    private static readonly Regex UriParameterRegex = new(@"\{[a-zA-Z_][a-zA-Z0-9_]*\}", RegexOptions.Compiled);
    private static readonly Regex WildcardRegex = new(@"\*+", RegexOptions.Compiled);

    public override System.ComponentModel.DataAnnotations.ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (value is not string uri || string.IsNullOrEmpty(uri))
            return System.ComponentModel.DataAnnotations.ValidationResult.Success;

        // Check for wildcards if not allowed
        if (!AllowWildcards && WildcardRegex.IsMatch(uri))
            return new System.ComponentModel.DataAnnotations.ValidationResult("URI pattern cannot contain wildcards", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());

        // Check for parameters if not allowed
        if (!AllowParameters && UriParameterRegex.IsMatch(uri))
            return new System.ComponentModel.DataAnnotations.ValidationResult("URI pattern cannot contain parameters", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());

        // Validate URI format (excluding parameters and wildcards for validation)
        var cleanUri = uri;
        if (AllowParameters)
            cleanUri = UriParameterRegex.Replace(cleanUri, "test");
        if (AllowWildcards)
            cleanUri = WildcardRegex.Replace(cleanUri, "test");

        try
        {
            if (cleanUri.StartsWith("/"))
            {
                // Relative URI - prepend dummy scheme and host
                cleanUri = "http://localhost" + cleanUri;
            }

            var parsedUri = new Uri(cleanUri, UriKind.Absolute);
            
            // Check allowed schemes if specified
            if (AllowedSchemes != null && AllowedSchemes.Length > 0)
            {
                if (!AllowedSchemes.Contains(parsedUri.Scheme, StringComparer.OrdinalIgnoreCase))
                    return new System.ComponentModel.DataAnnotations.ValidationResult($"URI scheme must be one of: {string.Join(", ", AllowedSchemes)}", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());
            }
        }
        catch (UriFormatException ex)
        {
            return new System.ComponentModel.DataAnnotations.ValidationResult($"Invalid URI format: {ex.Message}", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());
        }

        return System.ComponentModel.DataAnnotations.ValidationResult.Success;
    }
}

/// <summary>
/// Validates that permission combinations are valid
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class ValidPermissionCombinationAttribute : DomainValidationAttribute
{
    public override System.ComponentModel.DataAnnotations.ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var permission = validationContext.ObjectInstance as Permission;
        if (permission == null)
            return System.ComponentModel.DataAnnotations.ValidationResult.Success;

        // Cannot both grant and deny the same permission
        if (permission.Grant && permission.Deny)
            return new System.ComponentModel.DataAnnotations.ValidationResult("Permission cannot both grant and deny access", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());

        // Must have either grant or deny set
        if (!permission.Grant && !permission.Deny)
            return new System.ComponentModel.DataAnnotations.ValidationResult("Permission must either grant or deny access", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());

        return System.ComponentModel.DataAnnotations.ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a user has required permissions to perform an operation
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequiresPermissionAttribute : DomainValidationAttribute
{
    public string Resource { get; }
    public HttpVerb Verb { get; }

    public RequiresPermissionAttribute(string resource, HttpVerb verb)
    {
        Resource = resource;
        Verb = verb;
    }

    public override System.ComponentModel.DataAnnotations.ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var userContext = domainContext.UserContext;
        if (userContext == null)
            return new System.ComponentModel.DataAnnotations.ValidationResult("User context required for permission validation", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());

        // Check if user has required permission
        // This would typically use IPermissionEvaluationService
        var permissionService = domainContext.ServiceProvider.GetService(typeof(IPermissionEvaluationService)) as IPermissionEvaluationService;
        if (permissionService != null)
        {
            var hasPermission = permissionService.HasPermissionAsync(userContext.UserId, Resource, Verb).Result;
            if (!hasPermission)
                return new System.ComponentModel.DataAnnotations.ValidationResult($"Insufficient permissions. Required: {Verb} on {Resource}", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());
        }

        return System.ComponentModel.DataAnnotations.ValidationResult.Success;
    }
}

/// <summary>
/// Validates business rules specific to entity relationships
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class ValidEntityRelationshipAttribute : DomainValidationAttribute
{
    public string RelationshipType { get; }
    public int? MaxRelationships { get; set; }
    public Type[]? AllowedRelatedTypes { get; set; }

    public ValidEntityRelationshipAttribute(string relationshipType)
    {
        RelationshipType = relationshipType;
    }

    public override System.ComponentModel.DataAnnotations.ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var entity = validationContext.ObjectInstance as Entity;
        if (entity == null || value is not Entity relatedEntity)
            return System.ComponentModel.DataAnnotations.ValidationResult.Success;

        // Check allowed types
        if (AllowedRelatedTypes != null && AllowedRelatedTypes.Length > 0)
        {
            var relatedType = relatedEntity.GetType();
            if (!AllowedRelatedTypes.Any(t => t.IsAssignableFrom(relatedType)))
            {
                var allowedNames = string.Join(", ", AllowedRelatedTypes.Select(t => t.Name));
                return new System.ComponentModel.DataAnnotations.ValidationResult($"Related entity must be one of: {allowedNames}", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());
            }
        }

        // Check maximum relationships
        if (MaxRelationships.HasValue)
        {
            var currentCount = RelationshipType.ToLower() switch
            {
                "parent" => entity.Parents.Count,
                "child" => entity.Children.Count,
                _ => 0
            };

            if (currentCount >= MaxRelationships.Value)
                return new System.ComponentModel.DataAnnotations.ValidationResult($"Maximum {RelationshipType} relationships ({MaxRelationships.Value}) exceeded", validationContext.MemberName != null ? new[] { validationContext.MemberName } : Array.Empty<string>());
        }

        return System.ComponentModel.DataAnnotations.ValidationResult.Success;
    }
}

