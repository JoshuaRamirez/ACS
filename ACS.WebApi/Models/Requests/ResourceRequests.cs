using System.ComponentModel.DataAnnotations;

namespace ACS.WebApi.Models.Requests;

/// <summary>
/// Request model for querying resources
/// </summary>
public class GetResourcesRequest : PagedRequest
{
    /// <summary>
    /// Filter by resource type
    /// </summary>
    public string? ResourceType { get; set; }

    /// <summary>
    /// Filter by active status
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Filter by URI pattern (supports partial matching)
    /// </summary>
    public string? UriPattern { get; set; }

    /// <summary>
    /// Filter by version
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Filter by parent resource ID
    /// </summary>
    public int? ParentResourceId { get; set; }

    /// <summary>
    /// Sort field (Name, Uri, CreatedAt, UpdatedAt)
    /// </summary>
    public string SortBy { get; set; } = "Name";

    /// <summary>
    /// Sort direction (asc, desc)
    /// </summary>
    public string SortDirection { get; set; } = "asc";
}

/// <summary>
/// Base class for paged requests
/// </summary>
public abstract class PagedRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
    public int Page
    {
        get => _page;
        set => _page = Math.Max(1, value);
    }

    /// <summary>
    /// Number of items per page
    /// </summary>
    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, 100);
    }
}

/// <summary>
/// Request model for creating a new resource
/// </summary>
public class CreateResourceRequest
{
    /// <summary>
    /// Resource name (optional, will be extracted from URI if not provided)
    /// </summary>
    [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }

    /// <summary>
    /// URI pattern for the resource
    /// </summary>
    [Required(ErrorMessage = "URI is required")]
    [StringLength(2000, ErrorMessage = "URI cannot exceed 2000 characters")]
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// Resource description
    /// </summary>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Type of resource (API, Web, File, Database, etc.)
    /// </summary>
    [Required(ErrorMessage = "Resource type is required")]
    [StringLength(100, ErrorMessage = "Resource type cannot exceed 100 characters")]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Resource version (semantic versioning recommended)
    /// </summary>
    [RegularExpression(@"^(\d+\.)?(\d+\.)?(\*|\d+)$", ErrorMessage = "Version must follow semantic versioning pattern (e.g., 1.0.0)")]
    public string? Version { get; set; }

    /// <summary>
    /// Parent resource ID for hierarchical resources
    /// </summary>
    public int? ParentResourceId { get; set; }

    /// <summary>
    /// Whether the resource is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Additional metadata as key-value pairs
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Request model for updating an existing resource
/// </summary>
public class UpdateResourceRequest
{
    /// <summary>
    /// Resource description
    /// </summary>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Type of resource
    /// </summary>
    [StringLength(100, ErrorMessage = "Resource type cannot exceed 100 characters")]
    public string? ResourceType { get; set; }

    /// <summary>
    /// Resource version
    /// </summary>
    [RegularExpression(@"^(\d+\.)?(\d+\.)?(\*|\d+)$", ErrorMessage = "Version must follow semantic versioning pattern")]
    public string? Version { get; set; }

    /// <summary>
    /// Parent resource ID
    /// </summary>
    public int? ParentResourceId { get; set; }

    /// <summary>
    /// Whether the resource is active
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Additional metadata as key-value pairs
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Request model for discovering resources from a base path
/// </summary>
public class DiscoverResourcesRequest
{
    /// <summary>
    /// Base path to discover resources from
    /// </summary>
    [Required(ErrorMessage = "Base path is required")]
    [StringLength(2000, ErrorMessage = "Base path cannot exceed 2000 characters")]
    public string BasePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether to include inactive resources in discovery
    /// </summary>
    public bool IncludeInactive { get; set; } = false;

    /// <summary>
    /// Resource types to include in discovery
    /// </summary>
    public List<string> ResourceTypes { get; set; } = new();

    /// <summary>
    /// Maximum depth for recursive discovery
    /// </summary>
    [Range(1, 10, ErrorMessage = "Max depth must be between 1 and 10")]
    public int MaxDepth { get; set; } = 5;

    /// <summary>
    /// Patterns to exclude from discovery
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new();
}

/// <summary>
/// Request model for validating a URI pattern
/// </summary>
public class ValidateUriPatternRequest
{
    /// <summary>
    /// URI pattern to validate
    /// </summary>
    [Required(ErrorMessage = "Pattern is required")]
    [StringLength(2000, ErrorMessage = "Pattern cannot exceed 2000 characters")]
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Whether to allow wildcard characters (*)
    /// </summary>
    public bool AllowWildcards { get; set; } = true;

    /// <summary>
    /// Whether to allow parameter placeholders ({param})
    /// </summary>
    public bool AllowParameters { get; set; } = true;

    /// <summary>
    /// Allowed URI schemes (http, https, ftp, etc.)
    /// </summary>
    public List<string> AllowedSchemes { get; set; } = new() { "http", "https" };

    /// <summary>
    /// Whether to perform strict validation
    /// </summary>
    public bool StrictValidation { get; set; } = true;
}

/// <summary>
/// Request model for testing URI pattern matching
/// </summary>
public class TestUriPatternRequest
{
    /// <summary>
    /// URI pattern to test
    /// </summary>
    [Required(ErrorMessage = "Pattern is required")]
    [StringLength(2000, ErrorMessage = "Pattern cannot exceed 2000 characters")]
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// List of URIs to test against the pattern
    /// </summary>
    [Required(ErrorMessage = "Test URIs are required")]
    [MinLength(1, ErrorMessage = "At least one test URI is required")]
    public List<string> TestUris { get; set; } = new();

    /// <summary>
    /// Whether to return detailed matching information
    /// </summary>
    public bool IncludeDetails { get; set; } = true;

    /// <summary>
    /// Whether to test parameter extraction
    /// </summary>
    public bool TestParameterExtraction { get; set; } = true;
}

/// <summary>
/// Request model for bulk resource operations
/// </summary>
public class BulkResourceOperationRequest
{
    /// <summary>
    /// List of resource IDs to operate on
    /// </summary>
    [Required(ErrorMessage = "Resource IDs are required")]
    [MinLength(1, ErrorMessage = "At least one resource ID is required")]
    public List<int> ResourceIds { get; set; } = new();

    /// <summary>
    /// Operation to perform (activate, deactivate, delete, etc.)
    /// </summary>
    [Required(ErrorMessage = "Operation is required")]
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Optional parameters for the operation
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Whether to continue processing if some operations fail
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Justification for the bulk operation
    /// </summary>
    [StringLength(1000, ErrorMessage = "Justification cannot exceed 1000 characters")]
    public string? Justification { get; set; }
}

/// <summary>
/// Request model for resource permission assignment
/// </summary>
public class AssignResourcePermissionRequest
{
    /// <summary>
    /// Entity ID to assign permission to
    /// </summary>
    [Required(ErrorMessage = "Entity ID is required")]
    public int EntityId { get; set; }

    /// <summary>
    /// HTTP verbs to grant access for
    /// </summary>
    [Required(ErrorMessage = "HTTP verbs are required")]
    [MinLength(1, ErrorMessage = "At least one HTTP verb is required")]
    public List<string> HttpVerbs { get; set; } = new();

    /// <summary>
    /// Whether to grant or deny access
    /// </summary>
    public bool Grant { get; set; } = true;

    /// <summary>
    /// Permission scheme to use
    /// </summary>
    public string Scheme { get; set; } = "Allow";

    /// <summary>
    /// Expiration date for temporary permissions
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Justification for the permission assignment
    /// </summary>
    [StringLength(1000, ErrorMessage = "Justification cannot exceed 1000 characters")]
    public string? Justification { get; set; }
}

/// <summary>
/// Request model for resource access analysis
/// </summary>
public class ResourceAccessAnalysisRequest
{
    /// <summary>
    /// Resource IDs to analyze
    /// </summary>
    public List<int> ResourceIds { get; set; } = new();

    /// <summary>
    /// Analysis type (security, compliance, usage, performance)
    /// </summary>
    [Required(ErrorMessage = "Analysis type is required")]
    public string AnalysisType { get; set; } = string.Empty;

    /// <summary>
    /// Date range for the analysis
    /// </summary>
    public DateRange? DateRange { get; set; }

    /// <summary>
    /// Whether to include detailed results
    /// </summary>
    public bool IncludeDetails { get; set; } = true;

    /// <summary>
    /// Specific metrics to include in the analysis
    /// </summary>
    public List<string> Metrics { get; set; } = new();

    /// <summary>
    /// Grouping criteria for the analysis
    /// </summary>
    public string? GroupBy { get; set; }
}

/// <summary>
/// Date range model
/// </summary>
public class DateRange
{
    /// <summary>
    /// Start date for the range
    /// </summary>
    [Required(ErrorMessage = "Start date is required")]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date for the range
    /// </summary>
    [Required(ErrorMessage = "End date is required")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Validates that end date is after start date
    /// </summary>
    public bool IsValid => EndDate >= StartDate;
}