using ACS.Service.Domain;

namespace ACS.Service.Requests;

/// <summary>
/// Service request for retrieving a single resource
/// </summary>
public record GetResourceRequest
{
    public int ResourceId { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
    public bool IncludePermissions { get; init; }
    public bool IncludeUsage { get; init; }
}

/// <summary>
/// Service request for retrieving multiple resources with pagination
/// </summary>
public record GetResourcesRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public string? ResourceType { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
    public string? UriPatternFilter { get; init; }
    public List<string>? HttpVerbFilter { get; init; }
    public bool? ActiveOnly { get; init; } = true;
    public bool IncludePermissions { get; init; }
}

/// <summary>
/// Service request for creating a new resource
/// </summary>
public record CreateResourceRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string UriPattern { get; init; } = string.Empty;
    public int? ParentId { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for updating an existing resource
/// </summary>
public record UpdateResourceRequest
{
    public int ResourceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string UriPattern { get; init; } = string.Empty;
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for deleting a resource
/// </summary>
public record DeleteResourceRequest
{
    public int ResourceId { get; init; }
    public string DeletedBy { get; init; } = string.Empty;
    public bool ForceDelete { get; init; }
}

/// <summary>
/// Service request for validating URI patterns
/// </summary>
public record ValidateUriPatternRequest
{
    public string Pattern { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for testing URI patterns
/// </summary>
public record TestUriPatternRequest
{
    public string Pattern { get; init; } = string.Empty;
    public string TestUri { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for checking URI protection status
/// </summary>
public record CheckUriProtectionRequest
{
    public string Uri { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for discovering resources automatically
/// </summary>
public record DiscoverResourcesRequest
{
    public string BaseUri { get; init; } = string.Empty;
    public bool DeepScan { get; init; } = false;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for paged results
/// </summary>
public record PagedRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
}
