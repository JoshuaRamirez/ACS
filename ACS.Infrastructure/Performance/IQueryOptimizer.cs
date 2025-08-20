using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Interface for query optimization services
/// </summary>
public interface IQueryOptimizer
{
    /// <summary>
    /// Optimizes a query to prevent N+1 issues with smart includes
    /// </summary>
    Task<IQueryable<T>> OptimizeQueryAsync<T>(IQueryable<T> query, QueryOptimizationOptions options) where T : class;
    
    /// <summary>
    /// Applies optimized pagination with prefetching
    /// </summary>
    Task<OptimizedPagedResult<T>> PaginateOptimizedAsync<T>(
        IQueryable<T> query, 
        PaginationRequest request, 
        CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// Executes query with N+1 detection and warnings
    /// </summary>
    Task<List<T>> ExecuteWithN1DetectionAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// Preloads related data to prevent N+1 queries
    /// </summary>
    Task PreloadRelatedDataAsync<T>(IEnumerable<T> entities, params Expression<Func<T, object>>[] navigationPaths) where T : class;
    
    /// <summary>
    /// Gets query execution metrics
    /// </summary>
    Task<QueryExecutionMetrics> GetQueryMetricsAsync<T>(IQueryable<T> query) where T : class;
}

/// <summary>
/// Options for query optimization
/// </summary>
public class QueryOptimizationOptions
{
    public bool EnableAutoIncludes { get; set; } = true;
    public bool EnableQuerySplitting { get; set; } = true;
    public bool EnableNoTracking { get; set; } = true;
    public bool EnableProjection { get; set; } = false;
    public int MaxIncludeDepth { get; set; } = 3;
    public List<string> IncludePatterns { get; set; } = new();
    public List<string> ExcludePatterns { get; set; } = new();
    public QueryHint PreferredHint { get; set; } = QueryHint.Auto;
}

/// <summary>
/// Enhanced pagination request with optimization options
/// </summary>
public class PaginationRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = false;
    public string? SearchTerm { get; set; }
    public Dictionary<string, object> Filters { get; set; } = new();
    public bool IncludeTotalCount { get; set; } = true;
    public bool UseCursor { get; set; } = false;
    public string? CursorValue { get; set; }
    public List<string> IncludeRelated { get; set; } = new();
}

/// <summary>
/// Optimized paged result with performance metrics
/// </summary>
public class OptimizedPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasTotalCount { get; set; }
    public int TotalPages => HasTotalCount && PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => HasTotalCount ? PageNumber < TotalPages : Items.Count == PageSize;
    public string? NextCursor { get; set; }
    public string? PreviousCursor { get; set; }
    public QueryExecutionMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Query execution metrics for performance monitoring
/// </summary>
public class QueryExecutionMetrics
{
    public TimeSpan ExecutionTime { get; set; }
    public int RecordsReturned { get; set; }
    public int DatabaseRoundtrips { get; set; }
    public bool UsedIndex { get; set; }
    public List<string> IncludesUsed { get; set; } = new();
    public bool HasPotentialN1Issues { get; set; }
    public string? OptimizationSuggestions { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}

/// <summary>
/// Query hints for optimization
/// </summary>
public enum QueryHint
{
    Auto,
    ForceIndex,
    ForceScan,
    SplitQuery,
    SingleQuery,
    NoTracking,
    NoLock
}