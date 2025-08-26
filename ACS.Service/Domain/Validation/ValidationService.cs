using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ACS.Service.Data;
using ACS.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ACS.Service.Domain.Validation;

/// <summary>
/// Service for coordinating domain validation operations
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates an entity with all applicable domain rules
    /// </summary>
    Task<System.ComponentModel.DataAnnotations.ValidationResult> ValidateEntityAsync<T>(T entity, string operationType = "Update") where T : class;
    
    /// <summary>
    /// Validates multiple entities in bulk
    /// </summary>
    Task<Dictionary<T, ValidationResult>> ValidateEntitiesBulkAsync<T>(IEnumerable<T> entities, string operationType = "Update") where T : class;
    
    /// <summary>
    /// Validates only business rules for an entity
    /// </summary>
    Task<ValidationResult> ValidateBusinessRulesAsync<T>(T entity, IDictionary<string, object>? context = null) where T : class;
    
    /// <summary>
    /// Validates domain invariants for an entity
    /// </summary>
    Task<ValidationResult> ValidateInvariantsAsync<T>(T entity) where T : class;
    
    /// <summary>
    /// Validates system-wide invariants
    /// </summary>
    Task<ValidationResult> ValidateSystemInvariantsAsync();
    
    /// <summary>
    /// Validates a specific property with domain context
    /// </summary>
    Task<ValidationResult> ValidatePropertyAsync<T>(T entity, string propertyName, object? value) where T : class;
    
    /// <summary>
    /// Checks if an operation is allowed based on domain rules
    /// </summary>
    Task<bool> IsOperationAllowedAsync<T>(T entity, string operationType, IDictionary<string, object>? context = null) where T : class;
    
    /// <summary>
    /// Gets validation configuration for an entity type
    /// </summary>
    EntityValidationSettings GetEntityValidationSettings<T>() where T : class;
    
    /// <summary>
    /// Updates validation configuration
    /// </summary>
    Task UpdateValidationConfigurationAsync(ValidationConfiguration configuration);
}

public class ValidationService : IValidationService
{
    private readonly IDomainValidationContext _domainContext;
    private readonly ILogger<ValidationService> _logger;
    private readonly IMemoryCache _cache;
    private readonly ValidationConfiguration _configuration;

    public ValidationService(
        IDomainValidationContext domainContext,
        ILogger<ValidationService> logger,
        IMemoryCache cache,
        ValidationConfiguration configuration)
    {
        _domainContext = domainContext;
        _logger = logger;
        _cache = cache;
        _configuration = configuration;
    }

    public async Task<System.ComponentModel.DataAnnotations.ValidationResult> ValidateEntityAsync<T>(T entity, string operationType = "Update") where T : class
    {
        if (entity == null)
            return System.ComponentModel.DataAnnotations.ValidationResult.Success!;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Update operation context
            _domainContext.OperationContext.OperationType = operationType;
            _domainContext.OperationContext.Entity = entity;
            _domainContext.OperationContext.StartTime = DateTime.UtcNow;

            var validationContext = new ValidationContext(entity);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            // 1. Validate data annotations and basic attributes
            var dataAnnotationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            if (!Validator.TryValidateObject(entity, validationContext, dataAnnotationResults, true))
            {
                results.AddRange(dataAnnotationResults);
            }

            // 2. Validate domain-specific attributes
            var domainResults = await ValidateDomainAttributesAsync(entity, validationContext);
            results.AddRange(domainResults);

            // 3. Validate business rules
            var businessRuleResults = await ValidateBusinessRulesAsync(entity);
            if (!businessRuleResults.IsValid)
            {
                results.AddRange(businessRuleResults.AllErrors);
            }

            // 4. Validate domain invariants
            var invariantResults = await ValidateInvariantsAsync(entity);
            if (!invariantResults.IsValid)
            {
                results.AddRange(invariantResults.AllErrors);
            }

            // 5. Log validation performance
            stopwatch.Stop();
            _logger.LogDebug("Entity validation for {EntityType} took {ElapsedMs}ms", 
                typeof(T).Name, stopwatch.ElapsedMilliseconds);

            return results.Any() ? new System.ComponentModel.DataAnnotations.ValidationResult("Validation failed") : System.ComponentModel.DataAnnotations.ValidationResult.Success!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating entity of type {EntityType}", typeof(T).Name);
            return new System.ComponentModel.DataAnnotations.ValidationResult($"Validation error: {ex.Message}", new string[0]);
        }
    }

    public async Task<Dictionary<T, ValidationResult>> ValidateEntitiesBulkAsync<T>(
        IEnumerable<T> entities, 
        string operationType = "Update") where T : class
    {
        var results = new Dictionary<T, ValidationResult>();
        var entitiesList = entities.ToList();

        _logger.LogInformation("Starting bulk validation for {Count} entities of type {EntityType}", 
            entitiesList.Count, typeof(T).Name);

        // Mark as bulk operation
        var originalBulkFlag = _domainContext.OperationContext.IsBulkOperation;
        _domainContext.OperationContext.IsBulkOperation = true;

        try
        {
            // Validate entities in parallel for better performance
            var tasks = entitiesList.Select(async entity =>
            {
                var result = await ValidateEntityAsync(entity, operationType);
                return new { Entity = entity, Result = result };
            });

            var validationResults = await Task.WhenAll(tasks);

            foreach (var item in validationResults)
            {
                // Convert System.ComponentModel.DataAnnotations.ValidationResult to domain ValidationResult
                var domainValidationResult = item.Result.ErrorMessage != null 
                    ? new ValidationResult(new[] { item.Result })
                    : ValidationResult.Success;
                results[item.Entity] = domainValidationResult;
            }

            // Validate cross-entity invariants
            var crossEntityResults = DomainInvariants.ValidateCrossEntityInvariants(
                entitiesList.OfType<Entity>(), _domainContext);

            if (crossEntityResults.Any())
            {
                // Apply cross-entity validation errors to all entities
                var crossEntityError = new ValidationResult(crossEntityResults);
                foreach (var entity in entitiesList)
                {
                    if (results[entity].IsValid)
                        results[entity] = crossEntityError;
                    else
                        results[entity] = new ValidationResult(
                            results[entity].AllErrors.Concat(crossEntityResults));
                }
            }

            return results;
        }
        finally
        {
            _domainContext.OperationContext.IsBulkOperation = originalBulkFlag;
        }
    }

    public Task<ValidationResult> ValidateBusinessRulesAsync<T>(T entity, IDictionary<string, object>? context = null) where T : class
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var validationContext = new ValidationContext(entity);

        // Add context data
        if (context != null)
        {
            foreach (var item in context)
            {
                _domainContext.OperationContext.OperationData[item.Key] = item.Value;
            }
        }

        // Get all business rule attributes
        var businessRuleAttributes = GetBusinessRuleAttributes(typeof(T));

        foreach (var attribute in businessRuleAttributes.OrderByDescending(a => a.Priority))
        {
            // Skip if not applicable to bulk operations
            if (_domainContext.OperationContext.IsBulkOperation && attribute.SkipInBulkOperations)
                continue;

            try
            {
                var validationResult = attribute.ValidateInDomain(entity, validationContext, _domainContext);
                if (validationResult != null && validationResult != System.ComponentModel.DataAnnotations.ValidationResult.Success)
                {
                    results.Add(validationResult);
                    
                    // Stop on first critical error
                    if (attribute is BusinessRuleValidationAttribute businessRule && 
                        businessRule.Severity == RuleSeverity.Critical)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating business rule {RuleType} for {EntityType}", 
                    attribute.GetType().Name, typeof(T).Name);
                results.Add(new System.ComponentModel.DataAnnotations.ValidationResult($"Business rule validation error: {ex.Message}"));
            }
        }

        return Task.FromResult(results.Any() ? new ValidationResult(results) : ValidationResult.Success!);
    }

    public Task<ValidationResult> ValidateInvariantsAsync<T>(T entity) where T : class
    {
        try
        {
            var invariantResults = DomainInvariants.ValidateInvariants(entity, _domainContext);
            return Task.FromResult(invariantResults.Any() ? new ValidationResult(invariantResults) : ValidationResult.Success!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating invariants for {EntityType}", typeof(T).Name);
            return Task.FromResult(new ValidationResult(new[] { new System.ComponentModel.DataAnnotations.ValidationResult($"Invariant validation error: {ex.Message}") }));
        }
    }

    public async Task<ValidationResult> ValidateSystemInvariantsAsync()
    {
        try
        {
            var systemInvariantResults = await DomainInvariants.ValidateSystemInvariantsAsync(_domainContext);
            return systemInvariantResults.Any() ? new ValidationResult(systemInvariantResults) : ValidationResult.Success!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating system invariants");
            return new ValidationResult(new System.ComponentModel.DataAnnotations.ValidationResult($"System invariant validation error: {ex.Message}"));
        }
    }

    public Task<ValidationResult> ValidatePropertyAsync<T>(T entity, string propertyName, object? value) where T : class
    {
        var validationContext = new ValidationContext(entity) { MemberName = propertyName };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        // Validate property with data annotations
        var propertyInfo = typeof(T).GetProperty(propertyName);
        if (propertyInfo != null)
        {
            var attributes = propertyInfo.GetCustomAttributes<ValidationAttribute>();
            
            foreach (var attribute in attributes)
            {
                var result = attribute.GetValidationResult(value, validationContext);
                if (result != null && result != System.ComponentModel.DataAnnotations.ValidationResult.Success)
                {
                    results.Add(result);
                }
            }

            // Validate with domain attributes
            var domainAttributes = propertyInfo.GetCustomAttributes<DomainValidationAttribute>();
            foreach (var attribute in domainAttributes)
            {
                var result = attribute.ValidateInDomain(value, validationContext, _domainContext);
                if (result != null && result != System.ComponentModel.DataAnnotations.ValidationResult.Success)
                {
                    results.Add(result);
                }
            }
        }

        return Task.FromResult(results.Any() ? new ValidationResult(results) : ValidationResult.Success!);
    }

    public async Task<bool> IsOperationAllowedAsync<T>(T entity, string operationType, IDictionary<string, object>? context = null) where T : class
    {
        // Check permissions first
        if (_domainContext.UserContext != null)
        {
            var resourceName = $"/{typeof(T).Name.ToLower()}";
            var httpVerb = operationType.ToLower() switch
            {
                "create" => HttpVerb.POST,
                "read" => HttpVerb.GET,
                "update" => HttpVerb.PUT,
                "delete" => HttpVerb.DELETE,
                _ => HttpVerb.GET
            };

            var permissionService = _domainContext.ServiceProvider.GetService(typeof(IPermissionEvaluationService)) as IPermissionEvaluationService;
            if (permissionService != null)
            {
                var hasPermission = await permissionService.HasPermissionAsync(
                    _domainContext.UserContext.UserId, resourceName, httpVerb);
                
                if (!hasPermission)
                    return false;
            }
        }

        // Check business rules
        var businessValidation = await ValidateBusinessRulesAsync(entity, context);
        return businessValidation.IsValid;
    }

    public EntityValidationSettings GetEntityValidationSettings<T>() where T : class
    {
        var entityType = typeof(T).Name;
        return _configuration.EntitySettings.TryGetValue(entityType, out var settings) 
            ? settings 
            : new EntityValidationSettings();
    }

    public Task UpdateValidationConfigurationAsync(ValidationConfiguration configuration)
    {
        // In a real implementation, this would persist the configuration
        // For now, we'll just update the in-memory configuration
        foreach (var setting in configuration.EntitySettings)
        {
            _configuration.EntitySettings[setting.Key] = setting.Value;
        }
        
        _configuration.StrictMode = configuration.StrictMode;
        _configuration.MaxValidationDepth = configuration.MaxValidationDepth;
        _configuration.ValidationTimeout = configuration.ValidationTimeout;
        _configuration.EnableValidationCaching = configuration.EnableValidationCaching;
        _configuration.CacheExpiration = configuration.CacheExpiration;

        _logger.LogInformation("Validation configuration updated");
        return Task.CompletedTask;
    }

    private Task<IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult>> ValidateDomainAttributesAsync<T>(T entity, ValidationContext validationContext) where T : class
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var domainAttributes = GetDomainValidationAttributes(typeof(T));

        foreach (var attribute in domainAttributes.OrderByDescending(a => a.Priority))
        {
            if (_domainContext.OperationContext.IsBulkOperation && attribute.SkipInBulkOperations)
                continue;

            try
            {
                var validationResult = attribute.ValidateInDomain(entity, validationContext, _domainContext);
                if (validationResult != null && validationResult != System.ComponentModel.DataAnnotations.ValidationResult.Success)
                {
                    results.Add(validationResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating domain attribute {AttributeType} for {EntityType}", 
                    attribute.GetType().Name, typeof(T).Name);
                results.Add(new System.ComponentModel.DataAnnotations.ValidationResult($"Domain validation error: {ex.Message}"));
            }
        }

        return Task.FromResult<IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult>>(results);
    }

    private IEnumerable<DomainValidationAttribute> GetDomainValidationAttributes(Type type)
    {
        var cacheKey = $"domain_attributes_{type.FullName}";
        
        if (_cache.TryGetValue(cacheKey, out IEnumerable<DomainValidationAttribute>? cached))
            return cached!;

        var attributes = new List<DomainValidationAttribute>();
        
        // Get class-level attributes
        attributes.AddRange(type.GetCustomAttributes<DomainValidationAttribute>());
        
        // Get property-level attributes
        foreach (var property in type.GetProperties())
        {
            attributes.AddRange(property.GetCustomAttributes<DomainValidationAttribute>());
        }

        _cache.Set(cacheKey, attributes, TimeSpan.FromHours(1));
        return attributes;
    }

    private IEnumerable<BusinessRuleValidationAttribute> GetBusinessRuleAttributes(Type type)
    {
        var cacheKey = $"business_rule_attributes_{type.FullName}";
        
        if (_cache.TryGetValue(cacheKey, out IEnumerable<BusinessRuleValidationAttribute>? cached))
            return cached!;

        var attributes = new List<BusinessRuleValidationAttribute>();
        
        // Get class-level attributes
        attributes.AddRange(type.GetCustomAttributes<BusinessRuleValidationAttribute>());
        
        // Get property-level attributes  
        foreach (var property in type.GetProperties())
        {
            attributes.AddRange(property.GetCustomAttributes<BusinessRuleValidationAttribute>());
        }

        _cache.Set(cacheKey, attributes, TimeSpan.FromHours(1));
        return attributes;
    }
}

/// <summary>
/// Composite validation result that can contain multiple validation errors
/// </summary>
public class ValidationResult
{
    public bool IsValid => !AllErrors.Any();
    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> AllErrors { get; }

    public ValidationResult(IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> errors)
    {
        AllErrors = errors.ToList();
    }

    public ValidationResult(System.ComponentModel.DataAnnotations.ValidationResult singleError)
    {
        AllErrors = new[] { singleError };
    }

    public static ValidationResult Success { get; } = new ValidationResult(Enumerable.Empty<System.ComponentModel.DataAnnotations.ValidationResult>());

    public string GetErrorMessage() => string.Join("; ", AllErrors.Select(e => e.ErrorMessage));

    public override string ToString() => IsValid ? "Valid" : GetErrorMessage();
}
