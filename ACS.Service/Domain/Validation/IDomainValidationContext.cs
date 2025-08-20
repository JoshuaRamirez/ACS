using ACS.Service.Data;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Domain.Validation;

/// <summary>
/// Provides context and services for domain validation operations
/// </summary>
public interface IDomainValidationContext
{
    /// <summary>
    /// Database context for validation queries
    /// </summary>
    ApplicationDbContext DbContext { get; }
    
    /// <summary>
    /// Logger for validation operations
    /// </summary>
    ILogger Logger { get; }
    
    /// <summary>
    /// Current user context for validation
    /// </summary>
    IUserContext? UserContext { get; }
    
    /// <summary>
    /// Validation configuration and settings
    /// </summary>
    ValidationConfiguration Configuration { get; }
    
    /// <summary>
    /// Cache for expensive validation operations
    /// </summary>
    IValidationCache ValidationCache { get; }
    
    /// <summary>
    /// Service provider for dependency resolution
    /// </summary>
    IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Current validation operation context
    /// </summary>
    ValidationOperationContext OperationContext { get; }
}

/// <summary>
/// User context information for validation
/// </summary>
public interface IUserContext
{
    int UserId { get; }
    string UserName { get; }
    List<string> Roles { get; }
    Dictionary<string, object> Claims { get; }
}

/// <summary>
/// Configuration for domain validation behavior
/// </summary>
public class ValidationConfiguration
{
    /// <summary>
    /// Whether to enable strict validation mode
    /// </summary>
    public bool StrictMode { get; set; } = true;
    
    /// <summary>
    /// Maximum depth for recursive validations
    /// </summary>
    public int MaxValidationDepth { get; set; } = 10;
    
    /// <summary>
    /// Timeout for validation operations
    /// </summary>
    public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Whether to cache validation results
    /// </summary>
    public bool EnableValidationCaching { get; set; } = true;
    
    /// <summary>
    /// Cache expiration time
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Custom validation settings by entity type
    /// </summary>
    public Dictionary<string, EntityValidationSettings> EntitySettings { get; set; } = new();
}

/// <summary>
/// Entity-specific validation settings
/// </summary>
public class EntityValidationSettings
{
    public bool EnableCascadeValidation { get; set; } = true;
    public bool EnableBusinessRuleValidation { get; set; } = true;
    public bool EnableConstraintValidation { get; set; } = true;
    public List<string> SkippedValidations { get; set; } = new();
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// Context for the current validation operation
/// </summary>
public class ValidationOperationContext
{
    public string OperationType { get; set; } = string.Empty; // Create, Update, Delete
    public object? Entity { get; set; }
    public Dictionary<string, object> OperationData { get; set; } = new();
    public List<string> ValidationPath { get; set; } = new(); // Tracks nested validation calls
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public bool IsBulkOperation { get; set; }
}