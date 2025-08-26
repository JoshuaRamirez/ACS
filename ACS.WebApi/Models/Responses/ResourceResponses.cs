namespace ACS.WebApi.Models.Responses;

/// <summary>
/// Response model for resource information
/// </summary>
public class ResourceResponse
{
    /// <summary>
    /// Resource ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Resource name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URI pattern for the resource
    /// </summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// Resource description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type of resource
    /// </summary>
    public string? ResourceType { get; set; }

    /// <summary>
    /// Resource version
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Parent resource ID
    /// </summary>
    public int? ParentResourceId { get; set; }

    /// <summary>
    /// Whether the resource is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When the resource was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the resource was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Number of child resources
    /// </summary>
    public int ChildResourceCount { get; set; }

    /// <summary>
    /// Number of permissions defined for this resource
    /// </summary>
    public int PermissionCount { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Response model for paged results
/// </summary>
/// <typeparam name="T">Type of items in the response</typeparam>
public class PagedResponse<T>
{
    /// <summary>
    /// List of items for the current page
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Whether there is a next page available
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether there is a previous page available
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Pagination links
    /// </summary>
    public PaginationLinks Links { get; set; } = new();
}

/// <summary>
/// Pagination navigation links
/// </summary>
public class PaginationLinks
{
    /// <summary>
    /// Link to the first page
    /// </summary>
    public string? First { get; set; }

    /// <summary>
    /// Link to the previous page
    /// </summary>
    public string? Previous { get; set; }

    /// <summary>
    /// Link to the next page
    /// </summary>
    public string? Next { get; set; }

    /// <summary>
    /// Link to the last page
    /// </summary>
    public string? Last { get; set; }
}

/// <summary>
/// Response model for URI pattern validation
/// </summary>
public class UriPatternValidationResponse
{
    /// <summary>
    /// Whether the pattern is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// The pattern that was validated
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Suggested corrections for invalid patterns
    /// </summary>
    public List<string> SuggestedCorrections { get; set; } = new();

    /// <summary>
    /// Examples of URIs that would match this pattern
    /// </summary>
    public List<string> MatchExamples { get; set; } = new();

    /// <summary>
    /// Pattern complexity score (0-100)
    /// </summary>
    public int ComplexityScore { get; set; }

    /// <summary>
    /// Performance characteristics of the pattern
    /// </summary>
    public PatternPerformanceInfo Performance { get; set; } = new();
}

/// <summary>
/// Performance information for URI patterns
/// </summary>
public class PatternPerformanceInfo
{
    /// <summary>
    /// Estimated matching performance (Fast, Medium, Slow)
    /// </summary>
    public string MatchingPerformance { get; set; } = "Medium";

    /// <summary>
    /// Whether the pattern can be efficiently indexed
    /// </summary>
    public bool CanBeIndexed { get; set; } = true;

    /// <summary>
    /// Memory usage estimate for pattern compilation
    /// </summary>
    public string MemoryUsage { get; set; } = "Low";

    /// <summary>
    /// Recommendations for pattern optimization
    /// </summary>
    public List<string> OptimizationRecommendations { get; set; } = new();
}

/// <summary>
/// Response model for URI pattern testing
/// </summary>
public class UriPatternTestResponse
{
    /// <summary>
    /// The pattern that was tested
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Test results for each URI
    /// </summary>
    public List<UriTestResult> TestResults { get; set; } = new();

    /// <summary>
    /// Number of URIs that matched the pattern
    /// </summary>
    public int MatchCount { get; set; }

    /// <summary>
    /// Total number of URIs tested
    /// </summary>
    public int TotalTests { get; set; }

    /// <summary>
    /// Match percentage
    /// </summary>
    public double MatchPercentage => TotalTests > 0 ? (double)MatchCount / TotalTests * 100 : 0;

    /// <summary>
    /// Performance metrics for the pattern testing
    /// </summary>
    public TestPerformanceMetrics Performance { get; set; } = new();
}

/// <summary>
/// Result of testing a single URI against a pattern
/// </summary>
public class UriTestResult
{
    /// <summary>
    /// The URI that was tested
    /// </summary>
    public string TestUri { get; set; } = string.Empty;

    /// <summary>
    /// Whether the URI matched the pattern
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// Parameters extracted from the URI (for parameterized patterns)
    /// </summary>
    public Dictionary<string, string> ExtractedParameters { get; set; } = new();

    /// <summary>
    /// Match confidence score (0.0 to 1.0)
    /// </summary>
    public double MatchConfidence { get; set; }

    /// <summary>
    /// Additional match details
    /// </summary>
    public MatchDetails? Details { get; set; }
}

/// <summary>
/// Detailed match information
/// </summary>
public class MatchDetails
{
    /// <summary>
    /// Which part of the pattern matched
    /// </summary>
    public string MatchedSegment { get; set; } = string.Empty;

    /// <summary>
    /// Match type (Exact, Wildcard, Parameter, Regex)
    /// </summary>
    public string MatchType { get; set; } = string.Empty;

    /// <summary>
    /// Any warnings about the match
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Performance metrics for pattern testing
/// </summary>
public class TestPerformanceMetrics
{
    /// <summary>
    /// Total time taken for all tests
    /// </summary>
    public TimeSpan TotalTestTime { get; set; }

    /// <summary>
    /// Average time per test
    /// </summary>
    public TimeSpan AverageTestTime { get; set; }

    /// <summary>
    /// Number of tests per second
    /// </summary>
    public double TestsPerSecond { get; set; }

    /// <summary>
    /// Memory usage during testing
    /// </summary>
    public long MemoryUsage { get; set; }
}

/// <summary>
/// Response model for resource hierarchy
/// </summary>
public class ResourceHierarchyResponse
{
    /// <summary>
    /// The resource at this level of the hierarchy
    /// </summary>
    public ResourceResponse Resource { get; set; } = new();

    /// <summary>
    /// Child resources in the hierarchy
    /// </summary>
    public List<ResourceHierarchyResponse> Children { get; set; } = new();

    /// <summary>
    /// Depth in the hierarchy (0 = root)
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Whether this resource has children
    /// </summary>
    public bool HasChildren { get; set; }

    /// <summary>
    /// Path from root to this resource
    /// </summary>
    public List<string> Path { get; set; } = new();

    /// <summary>
    /// Total number of descendants
    /// </summary>
    public int DescendantCount { get; set; }
}

/// <summary>
/// Response model for URI protection status
/// </summary>
public class UriProtectionStatusResponse
{
    /// <summary>
    /// The URI that was checked
    /// </summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// Whether the URI is protected by any resources
    /// </summary>
    public bool IsProtected { get; set; }

    /// <summary>
    /// Resources that match or protect this URI
    /// </summary>
    public List<ResourceResponse> MatchingResources { get; set; } = new();

    /// <summary>
    /// Protection level (Unprotected, PartiallyProtected, FullyProtected, etc.)
    /// </summary>
    public string ProtectionLevel { get; set; } = string.Empty;

    /// <summary>
    /// Required permissions for accessing this URI
    /// </summary>
    public List<string> RequiredPermissions { get; set; } = new();

    /// <summary>
    /// Security recommendations for this URI
    /// </summary>
    public List<string> SecurityRecommendations { get; set; } = new();

    /// <summary>
    /// Risk assessment for this URI
    /// </summary>
    public UriRiskAssessment RiskAssessment { get; set; } = new();
}

/// <summary>
/// Risk assessment for a URI
/// </summary>
public class UriRiskAssessment
{
    /// <summary>
    /// Overall risk level (Low, Medium, High, Critical)
    /// </summary>
    public string RiskLevel { get; set; } = "Medium";

    /// <summary>
    /// Risk score (0-100)
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Factors contributing to the risk
    /// </summary>
    public List<string> RiskFactors { get; set; } = new();

    /// <summary>
    /// Recommended mitigation strategies
    /// </summary>
    public List<string> MitigationStrategies { get; set; } = new();
}


/// <summary>
/// Response model for resource discovery
/// </summary>
public class ResourceDiscoveryResponse
{
    /// <summary>
    /// Base path that was used for discovery
    /// </summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>
    /// Discovered resources
    /// </summary>
    public List<ResourceResponse> DiscoveredResources { get; set; } = new();

    /// <summary>
    /// Number of resources discovered
    /// </summary>
    public int DiscoveryCount { get; set; }

    /// <summary>
    /// Discovery statistics
    /// </summary>
    public DiscoveryStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Any issues encountered during discovery
    /// </summary>
    public List<string> DiscoveryIssues { get; set; } = new();
}

/// <summary>
/// Statistics from resource discovery
/// </summary>
public class DiscoveryStatistics
{
    /// <summary>
    /// Total paths scanned
    /// </summary>
    public int PathsScanned { get; set; }

    /// <summary>
    /// Resources discovered by type
    /// </summary>
    public Dictionary<string, int> ResourcesByType { get; set; } = new();

    /// <summary>
    /// Time taken for discovery
    /// </summary>
    public TimeSpan DiscoveryTime { get; set; }

    /// <summary>
    /// Maximum depth reached
    /// </summary>
    public int MaxDepthReached { get; set; }
}