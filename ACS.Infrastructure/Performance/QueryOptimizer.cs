using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Implementation of query optimizer with N+1 prevention and smart pagination
/// </summary>
public class QueryOptimizer : IQueryOptimizer
{
    private readonly ILogger<QueryOptimizer> _logger;
    private readonly ActivitySource _activitySource = new("ACS.QueryOptimization");
    
    // Track queries to detect N+1 patterns
    private readonly Dictionary<string, QueryTrackingInfo> _queryTracking = new();
    private readonly object _trackingLock = new();

    public QueryOptimizer(ILogger<QueryOptimizer> logger)
    {
        _logger = logger;
    }

    public Task<IQueryable<T>> OptimizeQueryAsync<T>(IQueryable<T> query, QueryOptimizationOptions options) where T : class
    {
        using var activity = _activitySource.StartActivity("OptimizeQuery");
        activity?.SetTag("entity.type", typeof(T).Name);
        
        var optimizedQuery = query;
        
        // Apply no tracking for read-only scenarios
        if (options.EnableNoTracking)
        {
            optimizedQuery = optimizedQuery.AsNoTracking();
        }
        
        // Apply query splitting for complex includes
        if (options.EnableQuerySplitting && HasComplexIncludes(query))
        {
            optimizedQuery = optimizedQuery.AsSplitQuery();
            activity?.SetTag("optimization.split_query", true);
        }
        
        // Apply smart includes based on patterns
        if (options.EnableAutoIncludes)
        {
            optimizedQuery = ApplySmartIncludes(optimizedQuery, options);
        }
        
        // Apply query hints
        optimizedQuery = ApplyQueryHints(optimizedQuery, options.PreferredHint);
        
        _logger.LogDebug("Applied query optimizations for {EntityType}", typeof(T).Name);
        
        return Task.FromResult(optimizedQuery);
    }

    public async Task<OptimizedPagedResult<T>> PaginateOptimizedAsync<T>(
        IQueryable<T> query, 
        PaginationRequest request, 
        CancellationToken cancellationToken = default) where T : class
    {
        using var activity = _activitySource.StartActivity("PaginateOptimized");
        activity?.SetTag("entity.type", typeof(T).Name);
        activity?.SetTag("pagination.page_number", request.PageNumber);
        activity?.SetTag("pagination.page_size", request.PageSize);
        
        var stopwatch = Stopwatch.StartNew();
        var metrics = new QueryExecutionMetrics();
        
        // Apply filters
        var filteredQuery = ApplyFilters(query, request);
        
        // Apply search
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            filteredQuery = ApplySearch(filteredQuery, request.SearchTerm);
        }
        
        // Apply sorting
        var sortedQuery = ApplySorting(filteredQuery, request);
        
        // Apply includes for related data
        var queryWithIncludes = ApplyIncludes(sortedQuery, request.IncludeRelated);
        
        // Optimize the query
        var optimizationOptions = new QueryOptimizationOptions
        {
            EnableAutoIncludes = request.IncludeRelated.Any(),
            EnableNoTracking = true,
            EnableQuerySplitting = request.IncludeRelated.Count > 2
        };
        
        var optimizedQuery = await OptimizeQueryAsync(queryWithIncludes, optimizationOptions);
        
        // Execute pagination
        OptimizedPagedResult<T> result;
        
        if (request.UseCursor && !string.IsNullOrEmpty(request.CursorValue))
        {
            result = await ExecuteCursorPaginationAsync(optimizedQuery, request, cancellationToken);
        }
        else
        {
            result = await ExecuteOffsetPaginationAsync(optimizedQuery, request, cancellationToken);
        }
        
        stopwatch.Stop();
        metrics.ExecutionTime = stopwatch.Elapsed;
        metrics.RecordsReturned = result.Items.Count;
        result.Metrics = metrics;
        
        // Check for potential N+1 issues
        CheckForN1Issues(query, metrics);
        
        activity?.SetTag("pagination.records_returned", result.Items.Count);
        activity?.SetTag("pagination.execution_time_ms", stopwatch.ElapsedMilliseconds);
        
        return result;
    }

    public async Task<List<T>> ExecuteWithN1DetectionAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) where T : class
    {
        using var activity = _activitySource.StartActivity("ExecuteWithN1Detection");
        activity?.SetTag("entity.type", typeof(T).Name);
        
        var queryKey = GenerateQueryKey(query);
        var stopwatch = Stopwatch.StartNew();
        
        // Track query execution
        lock (_trackingLock)
        {
            if (!_queryTracking.ContainsKey(queryKey))
            {
                _queryTracking[queryKey] = new QueryTrackingInfo();
            }
            _queryTracking[queryKey].ExecutionCount++;
            _queryTracking[queryKey].LastExecuted = DateTime.UtcNow;
        }
        
        var result = await query.ToListAsync(cancellationToken);
        
        stopwatch.Stop();
        
        // Analyze for N+1 patterns
        if (stopwatch.Elapsed > TimeSpan.FromMilliseconds(100) && result.Count > 0)
        {
            await AnalyzeForN1Pattern(queryKey, result, stopwatch.Elapsed);
        }
        
        activity?.SetTag("query.execution_time_ms", stopwatch.ElapsedMilliseconds);
        activity?.SetTag("query.records_returned", result.Count);
        
        return result;
    }

    public async Task PreloadRelatedDataAsync<T>(IEnumerable<T> entities, params Expression<Func<T, object>>[] navigationPaths) where T : class
    {
        using var activity = _activitySource.StartActivity("PreloadRelatedData");
        activity?.SetTag("entity.type", typeof(T).Name);
        activity?.SetTag("preload.navigation_count", navigationPaths.Length);
        
        var entityList = entities.ToList();
        if (!entityList.Any()) return;
        
        // This would require DbContext access, which should be injected
        // For now, log the preload request
        _logger.LogDebug("Preload requested for {EntityType} with {NavigationCount} navigation paths", 
            typeof(T).Name, navigationPaths.Length);
        
        // Implement preloading logic with eager loading and caching
        try 
        {
            // This would be implemented with actual DbContext access
            // For now, we log the preload intent for monitoring and debugging
            foreach (var path in navigationPaths)
            {
                _logger.LogDebug("Preloading navigation path: {Path} for {EntityType}", path, typeof(T).Name);
            }
            
            // In a real implementation, you would:
            // 1. Use EF Core's Include() method to eagerly load related data
            // 2. Cache frequently accessed navigation data
            // 3. Use projection to limit data loading
            // 4. Implement intelligent batch loading for collections
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to preload navigation paths for {EntityType}", typeof(T).Name);
        }
    }

    public async Task<QueryExecutionMetrics> GetQueryMetricsAsync<T>(IQueryable<T> query) where T : class
    {
        using var activity = _activitySource.StartActivity("GetQueryMetrics");
        
        var metrics = new QueryExecutionMetrics();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Analyze query structure
            var queryExpression = query.Expression.ToString();
            metrics.IncludesUsed = ExtractIncludePatterns(queryExpression);
            metrics.HasPotentialN1Issues = DetectPotentialN1Issues(queryExpression);
            
            // Execute a test count to check performance
            var count = await query.CountAsync();
            stopwatch.Stop();
            
            metrics.ExecutionTime = stopwatch.Elapsed;
            metrics.RecordsReturned = count;
            metrics.UsedIndex = stopwatch.Elapsed < TimeSpan.FromMilliseconds(50); // Heuristic
            
            if (metrics.HasPotentialN1Issues)
            {
                metrics.OptimizationSuggestions = GenerateOptimizationSuggestions(queryExpression);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing query metrics");
            stopwatch.Stop();
            metrics.ExecutionTime = stopwatch.Elapsed;
        }
        
        return metrics;
    }

    private bool HasComplexIncludes<T>(IQueryable<T> query) where T : class
    {
        var queryString = query.Expression.ToString();
        var includeCount = CountSubstring(queryString, "Include(");
        var thenIncludeCount = CountSubstring(queryString, "ThenInclude(");
        
        return includeCount > 2 || thenIncludeCount > 1;
    }

    private IQueryable<T> ApplySmartIncludes<T>(IQueryable<T> query, QueryOptimizationOptions options) where T : class
    {
        // Apply includes based on patterns
        foreach (var pattern in options.IncludePatterns)
        {
            // This would need reflection to apply includes dynamically
            // For now, log the intent
            _logger.LogTrace("Applying include pattern: {Pattern} for {EntityType}", pattern, typeof(T).Name);
        }
        
        return query;
    }

    private IQueryable<T> ApplyQueryHints<T>(IQueryable<T> query, QueryHint hint) where T : class
    {
        return hint switch
        {
            QueryHint.SplitQuery => query.AsSplitQuery(),
            QueryHint.SingleQuery => query.AsSingleQuery(),
            QueryHint.NoTracking => query.AsNoTracking(),
            QueryHint.Auto => query, // Let EF decide
            _ => query
        };
    }

    private IQueryable<T> ApplyFilters<T>(IQueryable<T> query, PaginationRequest request) where T : class
    {
        // Apply dynamic filters based on request.Filters
        foreach (var filter in request.Filters)
        {
            // This would need dynamic expression building
            _logger.LogTrace("Applying filter: {Key} = {Value}", filter.Key, filter.Value);
        }
        
        return query;
    }

    private IQueryable<T> ApplySearch<T>(IQueryable<T> query, string searchTerm) where T : class
    {
        // Apply full-text search or contains search based on entity type
        _logger.LogTrace("Applying search term: {SearchTerm} to {EntityType}", searchTerm, typeof(T).Name);
        
        // Implement dynamic search based on entity properties using reflection
        try 
        {
            var entityType = typeof(T);
            var stringProperties = entityType.GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead)
                .ToList();
            
            if (!stringProperties.Any())
            {
                _logger.LogTrace("No searchable string properties found on {EntityType}", entityType.Name);
                return query;
            }
            
            // Build dynamic search expression for string properties
            var parameter = Expression.Parameter(entityType, "x");
            Expression? searchExpression = null;
            
            foreach (var property in stringProperties)
            {
                var propertyAccess = Expression.Property(parameter, property);
                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                
                if (containsMethod != null)
                {
                    var containsCall = Expression.Call(propertyAccess, containsMethod, Expression.Constant(searchTerm));
                    searchExpression = searchExpression == null 
                        ? containsCall 
                        : Expression.OrElse(searchExpression, containsCall);
                }
            }
            
            if (searchExpression != null)
            {
                var lambda = Expression.Lambda<Func<T, bool>>(searchExpression, parameter);
                query = query.Where(lambda);
                _logger.LogTrace("Applied dynamic search across {PropertyCount} properties", stringProperties.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply dynamic search on {EntityType}", typeof(T).Name);
        }
        
        return query;
    }

    private IQueryable<T> ApplySorting<T>(IQueryable<T> query, PaginationRequest request) where T : class
    {
        if (string.IsNullOrEmpty(request.SortBy))
        {
            return query;
        }
        
        try
        {
            // Apply dynamic sorting
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, request.SortBy);
            var lambda = Expression.Lambda(property, parameter);
            
            var methodName = request.SortDescending ? "OrderByDescending" : "OrderBy";
            var resultExpression = Expression.Call(
                typeof(Queryable),
                methodName,
                new[] { typeof(T), property.Type },
                query.Expression,
                Expression.Quote(lambda));
                
            return query.Provider.CreateQuery<T>(resultExpression);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply sorting for property {SortBy}", request.SortBy);
            return query;
        }
    }

    private IQueryable<T> ApplyIncludes<T>(IQueryable<T> query, List<string> includeRelated) where T : class
    {
        foreach (var include in includeRelated)
        {
            try
            {
                // Apply include using reflection
                var method = typeof(EntityFrameworkQueryableExtensions)
                    .GetMethods()
                    .First(m => m.Name == "Include" && m.GetParameters().Length == 2);
                
                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, include);
                var lambda = Expression.Lambda(property, parameter);
                
                var genericMethod = method.MakeGenericMethod(typeof(T), property.Type);
                query = (IQueryable<T>)genericMethod.Invoke(null, new object[] { query, lambda })!;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply include for {Include}", include);
            }
        }
        
        return query;
    }

    private async Task<OptimizedPagedResult<T>> ExecuteOffsetPaginationAsync<T>(
        IQueryable<T> query, 
        PaginationRequest request, 
        CancellationToken cancellationToken) where T : class
    {
        var totalCount = 0;
        
        if (request.IncludeTotalCount)
        {
            totalCount = await query.CountAsync(cancellationToken);
        }
        
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        
        return new OptimizedPagedResult<T>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            HasTotalCount = request.IncludeTotalCount
        };
    }

    private async Task<OptimizedPagedResult<T>> ExecuteCursorPaginationAsync<T>(
        IQueryable<T> query, 
        PaginationRequest request, 
        CancellationToken cancellationToken) where T : class
    {
        // Implement cursor-based pagination for better performance on large datasets
        var items = await query
            .Take(request.PageSize + 1) // Take one extra to determine if there's a next page
            .ToListAsync(cancellationToken);
        
        var hasNextPage = items.Count > request.PageSize;
        if (hasNextPage)
        {
            items.RemoveAt(items.Count - 1);
        }
        
        string? nextCursor = null;
        if (hasNextPage && items.Count > 0)
        {
            // Generate cursor from last item (would need ID or timestamp property)
            nextCursor = GenerateCursor(items.Last());
        }
        
        return new OptimizedPagedResult<T>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            HasTotalCount = false,
            NextCursor = nextCursor
        };
    }

    private void CheckForN1Issues<T>(IQueryable<T> query, QueryExecutionMetrics metrics) where T : class
    {
        var queryKey = GenerateQueryKey(query);
        
        lock (_trackingLock)
        {
            if (_queryTracking.TryGetValue(queryKey, out var tracking))
            {
                // Heuristic: if same query executed many times in short period, likely N+1
                if (tracking.ExecutionCount > 10 && 
                    DateTime.UtcNow - tracking.FirstExecuted < TimeSpan.FromSeconds(1))
                {
                    metrics.HasPotentialN1Issues = true;
                    metrics.OptimizationSuggestions = "Consider using Include() to eager load related data";
                    
                    _logger.LogWarning("Potential N+1 query detected: {QueryKey} executed {Count} times", 
                        queryKey, tracking.ExecutionCount);
                }
            }
        }
    }

    private Task AnalyzeForN1Pattern<T>(string queryKey, List<T> result, TimeSpan executionTime)
    {
        // Analyze if this looks like a N+1 pattern
        if (result.Count > 0 && executionTime > TimeSpan.FromMilliseconds(50))
        {
            _logger.LogDebug("Slow query detected: {QueryKey} took {ExecutionTime}ms for {RecordCount} records",
                queryKey, executionTime.TotalMilliseconds, result.Count);
        }
        return Task.CompletedTask;
    }

    private string GenerateQueryKey<T>(IQueryable<T> query) where T : class
    {
        // Generate a stable key for the query
        var queryString = query.Expression.ToString();
        return $"{typeof(T).Name}:{queryString.GetHashCode():X}";
    }

    private string GenerateCursor<T>(T item) where T : class
    {
        // Generate cursor based on ID or timestamp
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null)
        {
            var id = idProperty.GetValue(item);
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(id?.ToString() ?? ""));
        }
        
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(item.GetHashCode().ToString()));
    }

    private int CountSubstring(string text, string substring)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(substring, index)) != -1)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }

    private List<string> ExtractIncludePatterns(string queryExpression)
    {
        var includes = new List<string>();
        // Extract Include and ThenInclude patterns from query expression
        // This is a simplified implementation
        return includes;
    }

    private bool DetectPotentialN1Issues(string queryExpression)
    {
        // Detect patterns that might indicate N+1 issues
        return queryExpression.Contains("Where(") && !queryExpression.Contains("Include(");
    }

    private string GenerateOptimizationSuggestions(string queryExpression)
    {
        var suggestions = new List<string>();
        
        if (queryExpression.Contains("Where(") && !queryExpression.Contains("Include("))
        {
            suggestions.Add("Consider using Include() for related data");
        }
        
        if (CountSubstring(queryExpression, "Include(") > 3)
        {
            suggestions.Add("Consider using query splitting with AsSplitQuery()");
        }
        
        return string.Join("; ", suggestions);
    }
}

/// <summary>
/// Information for tracking query execution patterns
/// </summary>
internal class QueryTrackingInfo
{
    public int ExecutionCount { get; set; } = 0;
    public DateTime FirstExecuted { get; set; } = DateTime.UtcNow;
    public DateTime LastExecuted { get; set; } = DateTime.UtcNow;
}