namespace ACS.WebApi.Resources;

/// <summary>
/// Generic collection resource with pagination metadata
/// </summary>
public record CollectionResource<T>
{
    public ICollection<T> Items { get; init; } = new List<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    
    // HATEOAS links
    public ICollection<LinkResource> Links { get; init; } = new List<LinkResource>();
}

/// <summary>
/// Generic API response wrapper for consistent responses
/// </summary>
public record ApiResponse<T>
{
    public bool Success { get; init; } = true;
    public T? Data { get; init; }
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? RequestId { get; init; }
    
    // HATEOAS links
    public ICollection<LinkResource> Links { get; init; } = new List<LinkResource>();
}

/// <summary>
/// HATEOAS link resource for hypermedia API
/// </summary>
public record LinkResource
{
    public string Href { get; init; } = string.Empty;
    public string Rel { get; init; } = string.Empty;
    public string Method { get; init; } = "GET";
    public string? Type { get; init; }
    public string? Title { get; init; }
}

/// <summary>
/// Resource for pagination parameters
/// </summary>
public record PaginationResource
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int MaxPageSize { get; init; } = 100;
    
    // Ensure valid pagination values
    public int ValidPage => Math.Max(1, Page);
    public int ValidPageSize => Math.Min(Math.Max(1, PageSize), MaxPageSize);
}

/// <summary>
/// Resource for sorting parameters
/// </summary>
public record SortResource
{
    public string Field { get; init; } = string.Empty;
    public SortDirection Direction { get; init; } = SortDirection.Ascending;
}

public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Resource for filtering parameters
/// </summary>
public record FilterResource
{
    public string Field { get; init; } = string.Empty;
    public FilterOperator Operator { get; init; } = FilterOperator.Equals;
    public string Value { get; init; } = string.Empty;
}

public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    EndsWith,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    In,
    NotIn
}

/// <summary>
/// Resource for query parameters combining pagination, sorting, and filtering
/// </summary>
public record QueryResource
{
    public PaginationResource Pagination { get; init; } = new();
    public ICollection<SortResource> Sort { get; init; } = new List<SortResource>();
    public ICollection<FilterResource> Filters { get; init; } = new List<FilterResource>();
    public string? Search { get; init; }
}

/// <summary>
/// Resource for bulk operations
/// </summary>
public record BulkOperationResource<T>
{
    public ICollection<T> Items { get; init; } = new List<T>();
    public string Operation { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Resource for bulk operation results
/// </summary>
public record BulkOperationResultResource<T>
{
    public int TotalRequested { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public ICollection<T> SuccessfulItems { get; init; } = new List<T>();
    public ICollection<BulkOperationErrorResource> Errors { get; init; } = new List<BulkOperationErrorResource>();
}

/// <summary>
/// Resource for bulk operation errors
/// </summary>
public record BulkOperationErrorResource
{
    public int ItemIndex { get; init; }
    public string Error { get; init; } = string.Empty;
    public string? Details { get; init; }
}

/// <summary>
/// Alias for paged responses - maps to CollectionResource for consistency
/// </summary>
public record PagedResponse<T> : CollectionResource<T>
{
}

/// <summary>
/// Date range for filtering and reporting
/// </summary>
public record DateRange
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public TimeSpan Duration => EndDate - StartDate;
    public bool IsValid => EndDate >= StartDate;
}