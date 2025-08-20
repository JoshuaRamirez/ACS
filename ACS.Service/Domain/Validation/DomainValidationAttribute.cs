using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Domain.Validation;

/// <summary>
/// Base class for domain-specific validation attributes
/// </summary>
public abstract class DomainValidationAttribute : ValidationAttribute
{
    /// <summary>
    /// Priority for validation execution order (higher values execute first)
    /// </summary>
    public int Priority { get; set; } = 0;
    
    /// <summary>
    /// Whether this validation should be skipped during bulk operations
    /// </summary>
    public bool SkipInBulkOperations { get; set; } = false;
    
    /// <summary>
    /// Validation context type (Entity, Property, Operation)
    /// </summary>
    public ValidationContextType ContextType { get; set; } = ValidationContextType.Property;
    
    /// <summary>
    /// Additional context data needed for validation
    /// </summary>
    public Dictionary<string, object> ValidationContext { get; set; } = new();
    
    /// <summary>
    /// Validates the value with domain context
    /// </summary>
    public abstract ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext);
    
    public override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Default implementation delegates to domain validation
        var domainContext = validationContext.GetService<IDomainValidationContext>();
        if (domainContext == null)
        {
            throw new InvalidOperationException("IDomainValidationContext service not available");
        }
        
        return ValidateInDomain(value, validationContext, domainContext);
    }
}

public enum ValidationContextType
{
    Property,
    Entity, 
    Operation
}