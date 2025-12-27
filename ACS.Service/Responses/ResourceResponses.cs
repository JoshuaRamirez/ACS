using ACS.Service.Domain;

namespace ACS.Service.Responses;

/// <summary>
/// Service response for a single resource operation
/// </summary>
public record ResourceResponse
{
    public Entity? Resource { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for multiple resources with pagination
/// </summary>
public record ResourcesResponse
{
    public ICollection<Entity> Resources { get; init; } = new List<Entity>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for resource hierarchy operations
/// </summary>
public record ResourceHierarchyResponse
{
    public Entity? Root { get; init; }
    public ICollection<Entity> Hierarchy { get; init; } = new List<Entity>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for URI pattern validation
/// </summary>
public record UriPatternValidationResponse
{
    public bool IsValid { get; init; }
    public string Pattern { get; init; } = string.Empty;
    public ICollection<string> ValidationErrors { get; init; } = new List<string>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for URI pattern testing
/// </summary>
public record UriPatternTestResponse
{
    public bool Matches { get; init; }
    public string Pattern { get; init; } = string.Empty;
    public string TestUri { get; init; } = string.Empty;
    public ICollection<string> MatchGroups { get; init; } = new List<string>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for URI protection status
/// </summary>
public record UriProtectionStatusResponse
{
    public string Uri { get; init; } = string.Empty;
    public bool IsProtected { get; init; }
    public ICollection<Entity> MatchingResources { get; init; } = new List<Entity>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for resource creation operation
/// </summary>
public record CreateResourceResponse
{
    public Resource? Resource { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for resource update operation
/// </summary>
public record UpdateResourceResponse
{
    public Resource? Resource { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for single resource retrieval operation
/// </summary>
public record GetResourceResponse
{
    public Resource? Resource { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for multiple resources retrieval operation with pagination
/// </summary>
public record GetResourcesResponse
{
    public ICollection<Resource> Resources { get; init; } = new List<Resource>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for resource deletion operation
/// </summary>
public record DeleteResourceResponse
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}