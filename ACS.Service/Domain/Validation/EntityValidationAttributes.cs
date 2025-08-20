using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Domain.Validation;

/// <summary>
/// Validates that an entity name is unique within its scope
/// </summary>
public class UniqueEntityNameAttribute : DomainValidationAttribute
{
    public Type EntityType { get; set; } = typeof(Entity);
    public string? ScopeProperty { get; set; }
    public bool CaseInsensitive { get; set; } = true;

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (value is not string name || string.IsNullOrEmpty(name))
            return ValidationResult.Success;

        var entity = validationContext.ObjectInstance as Entity;
        if (entity == null)
            return new ValidationResult("Validation can only be applied to Entity types");

        // Check cache first
        var cacheKey = $"unique_name_{EntityType.Name}_{name}_{ScopeProperty}";
        var cachedResult = domainContext.ValidationCache.GetAsync<bool?>(cacheKey).Result;
        if (cachedResult.HasValue)
        {
            return cachedResult.Value ? ValidationResult.Success : 
                new ValidationResult($"An entity with name '{name}' already exists");
        }

        // Query database
        var query = domainContext.DbContext.Set(EntityType).AsQueryable();
        
        if (CaseInsensitive)
            query = query.Where(e => EF.Property<string>(e, "Name").ToLower() == name.ToLower());
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
        _ = domainContext.ValidationCache.SetAsync(cacheKey, !exists, domainContext.Configuration.CacheExpiration);

        return exists ? new ValidationResult($"An entity with name '{name}' already exists") : ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a hierarchy doesn't create cycles
/// </summary>
public class NoCyclicHierarchyAttribute : DomainValidationAttribute
{
    public int MaxDepth { get; set; } = 50;

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var entity = validationContext.ObjectInstance as Entity;
        if (entity == null || value is not Entity parentEntity)
            return ValidationResult.Success;

        // Cannot be parent of itself
        if (entity.Id == parentEntity.Id && entity.Id > 0)
            return new ValidationResult("Entity cannot be its own parent");

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
                return new ValidationResult("Adding this parent would create a circular hierarchy");

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
            return new ValidationResult($"Hierarchy depth exceeds maximum of {MaxDepth} levels");

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates temporal permissions have proper time constraints
/// </summary>
public class TemporalPermissionBusinessRuleAttribute : DomainValidationAttribute
{
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromDays(365);
    public TimeSpan MinDuration { get; set; } = TimeSpan.FromMinutes(5);

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not TemporaryPermission tempPerm)
            return ValidationResult.Success;

        var duration = tempPerm.ExpiresAt - tempPerm.GrantedAt;

        if (duration < MinDuration)
            return new ValidationResult($"Permission duration must be at least {MinDuration.TotalMinutes} minutes");

        if (duration > MaxDuration)
            return new ValidationResult($"Permission duration cannot exceed {MaxDuration.TotalDays} days");

        if (tempPerm.ExpiresAt <= DateTime.UtcNow)
            return new ValidationResult("Permission cannot expire in the past");

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates maximum number of children for an entity
/// </summary>
public class MaxChildrenAttribute : DomainValidationAttribute
{
    public int Maximum { get; }

    public MaxChildrenAttribute(int maximum)
    {
        Maximum = maximum;
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var entity = validationContext.ObjectInstance as Entity;
        if (entity == null)
            return ValidationResult.Success;

        if (entity.Children.Count >= Maximum)
            return new ValidationResult($"Entity cannot have more than {Maximum} children");

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates temporal permissions have proper time constraints
/// </summary>
public class TemporalPermissionBusinessRuleAttribute : DomainValidationAttribute
{
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromDays(365);
    public TimeSpan MinDuration { get; set; } = TimeSpan.FromMinutes(5);

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not TemporaryPermission tempPerm)
            return ValidationResult.Success;

        var duration = tempPerm.ExpiresAt - tempPerm.GrantedAt;

        if (duration < MinDuration)
            return new ValidationResult($"Permission duration must be at least {MinDuration.TotalMinutes} minutes");

        if (duration > MaxDuration)
            return new ValidationResult($"Permission duration cannot exceed {MaxDuration.TotalDays} days");

        if (tempPerm.ExpiresAt <= DateTime.UtcNow)
            return new ValidationResult("Permission cannot expire in the past");

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that URI patterns are well-formed
/// </summary>
public class ValidUriPatternAttribute : DomainValidationAttribute
{
    public bool AllowWildcards { get; set; } = true;
    public bool AllowParameters { get; set; } = true;
    public string[]? AllowedSchemes { get; set; }

    private static readonly Regex UriParameterRegex = new(@"\{[a-zA-Z_][a-zA-Z0-9_]*\}", RegexOptions.Compiled);
    private static readonly Regex WildcardRegex = new(@"\*+", RegexOptions.Compiled);

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (value is not string uri || string.IsNullOrEmpty(uri))
            return ValidationResult.Success;

        // Check for wildcards if not allowed
        if (!AllowWildcards && WildcardRegex.IsMatch(uri))
            return new ValidationResult("URI pattern cannot contain wildcards");

        // Check for parameters if not allowed
        if (!AllowParameters && UriParameterRegex.IsMatch(uri))
            return new ValidationResult("URI pattern cannot contain parameters");

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
                    return new ValidationResult($"URI scheme must be one of: {string.Join(", ", AllowedSchemes)}");
            }
        }
        catch (UriFormatException ex)
        {
            return new ValidationResult($"Invalid URI format: {ex.Message}");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates temporal permissions have proper time constraints
/// </summary>
public class TemporalPermissionBusinessRuleAttribute : DomainValidationAttribute
{
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromDays(365);
    public TimeSpan MinDuration { get; set; } = TimeSpan.FromMinutes(5);

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not TemporaryPermission tempPerm)
            return ValidationResult.Success;

        var duration = tempPerm.ExpiresAt - tempPerm.GrantedAt;

        if (duration < MinDuration)
            return new ValidationResult($"Permission duration must be at least {MinDuration.TotalMinutes} minutes");

        if (duration > MaxDuration)
            return new ValidationResult($"Permission duration cannot exceed {MaxDuration.TotalDays} days");

        if (tempPerm.ExpiresAt <= DateTime.UtcNow)
            return new ValidationResult("Permission cannot expire in the past");

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that permission combinations are valid
/// </summary>
public class ValidPermissionCombinationAttribute : DomainValidationAttribute
{
    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var permission = validationContext.ObjectInstance as Permission;
        if (permission == null)
            return ValidationResult.Success;

        // Cannot both grant and deny the same permission
        if (permission.Grant && permission.Deny)
            return new ValidationResult("Permission cannot both grant and deny access");

        // Must have either grant or deny set
        if (!permission.Grant && !permission.Deny)
            return new ValidationResult("Permission must either grant or deny access");

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates temporal permissions have proper time constraints
/// </summary>
public class TemporalPermissionBusinessRuleAttribute : DomainValidationAttribute
{
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromDays(365);
    public TimeSpan MinDuration { get; set; } = TimeSpan.FromMinutes(5);

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not TemporaryPermission tempPerm)
            return ValidationResult.Success;

        var duration = tempPerm.ExpiresAt - tempPerm.GrantedAt;

        if (duration < MinDuration)
            return new ValidationResult($"Permission duration must be at least {MinDuration.TotalMinutes} minutes");

        if (duration > MaxDuration)
            return new ValidationResult($"Permission duration cannot exceed {MaxDuration.TotalDays} days");

        if (tempPerm.ExpiresAt <= DateTime.UtcNow)
            return new ValidationResult("Permission cannot expire in the past");

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a user has required permissions to perform an operation
/// </summary>
public class RequiresPermissionAttribute : DomainValidationAttribute
{
    public string Resource { get; }
    public HttpVerb Verb { get; }

    public RequiresPermissionAttribute(string resource, HttpVerb verb)
    {
        Resource = resource;
        Verb = verb;
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var userContext = domainContext.UserContext;
        if (userContext == null)
            return new ValidationResult("User context required for permission validation");

        // Check if user has required permission
        // This would typically use IPermissionEvaluationService
        var permissionService = domainContext.ServiceProvider.GetService<IPermissionEvaluationService>();
        if (permissionService != null)
        {
            var hasPermission = permissionService.HasPermissionAsync(userContext.UserId, Resource, Verb).Result;
            if (!hasPermission)
                return new ValidationResult($"Insufficient permissions. Required: {Verb} on {Resource}");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates temporal permissions have proper time constraints
/// </summary>
public class TemporalPermissionBusinessRuleAttribute : DomainValidationAttribute
{
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromDays(365);
    public TimeSpan MinDuration { get; set; } = TimeSpan.FromMinutes(5);

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not TemporaryPermission tempPerm)
            return ValidationResult.Success;

        var duration = tempPerm.ExpiresAt - tempPerm.GrantedAt;

        if (duration < MinDuration)
            return new ValidationResult($"Permission duration must be at least {MinDuration.TotalMinutes} minutes");

        if (duration > MaxDuration)
            return new ValidationResult($"Permission duration cannot exceed {MaxDuration.TotalDays} days");

        if (tempPerm.ExpiresAt <= DateTime.UtcNow)
            return new ValidationResult("Permission cannot expire in the past");

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates business rules specific to entity relationships
/// </summary>
public class ValidEntityRelationshipAttribute : DomainValidationAttribute
{
    public string RelationshipType { get; }
    public int? MaxRelationships { get; set; }
    public Type[]? AllowedRelatedTypes { get; set; }

    public ValidEntityRelationshipAttribute(string relationshipType)
    {
        RelationshipType = relationshipType;
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var entity = validationContext.ObjectInstance as Entity;
        if (entity == null || value is not Entity relatedEntity)
            return ValidationResult.Success;

        // Check allowed types
        if (AllowedRelatedTypes != null && AllowedRelatedTypes.Length > 0)
        {
            var relatedType = relatedEntity.GetType();
            if (!AllowedRelatedTypes.Any(t => t.IsAssignableFrom(relatedType)))
            {
                var allowedNames = string.Join(", ", AllowedRelatedTypes.Select(t => t.Name));
                return new ValidationResult($"Related entity must be one of: {allowedNames}");
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
                return new ValidationResult($"Maximum {RelationshipType} relationships ({MaxRelationships.Value}) exceeded");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates temporal permissions have proper time constraints
/// </summary>
public class TemporalPermissionBusinessRuleAttribute : DomainValidationAttribute
{
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromDays(365);
    public TimeSpan MinDuration { get; set; } = TimeSpan.FromMinutes(5);

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not TemporaryPermission tempPerm)
            return ValidationResult.Success;

        var duration = tempPerm.ExpiresAt - tempPerm.GrantedAt;

        if (duration < MinDuration)
            return new ValidationResult($"Permission duration must be at least {MinDuration.TotalMinutes} minutes");

        if (duration > MaxDuration)
            return new ValidationResult($"Permission duration cannot exceed {MaxDuration.TotalDays} days");

        if (tempPerm.ExpiresAt <= DateTime.UtcNow)
            return new ValidationResult("Permission cannot expire in the past");

        return ValidationResult.Success;
    }
}