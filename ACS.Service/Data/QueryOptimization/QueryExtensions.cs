using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace ACS.Service.Data.QueryOptimization;

/// <summary>
/// Extensions for query optimization and performance improvements
/// </summary>
public static class QueryExtensions
{
    #region Include Optimization

    /// <summary>
    /// Conditionally include related data based on a predicate
    /// </summary>
    public static IQueryable<T> IncludeIf<T, TProperty>(
        this IQueryable<T> query,
        bool condition,
        Expression<Func<T, TProperty>> navigationPropertyPath)
        where T : class
    {
        return condition ? query.Include(navigationPropertyPath) : query;
    }

    /// <summary>
    /// Conditionally include related data with then include based on a predicate
    /// </summary>
    public static IQueryable<T> ThenIncludeIf<T, TPreviousProperty, TProperty>(
        this IIncludableQueryable<T, TPreviousProperty> query,
        bool condition,
        Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath)
        where T : class
    {
        return condition ? query.ThenInclude(navigationPropertyPath) : query;
    }

    /// <summary>
    /// Include user with security context (roles and groups)
    /// </summary>
    public static IQueryable<Models.User> IncludeSecurityContext(this IQueryable<Models.User> query)
    {
        return query
            .Include(u => u.Entity)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Entity)
            .Include(u => u.UserGroups)
                .ThenInclude(ug => ug.Group)
                    .ThenInclude(g => g.Entity);
    }

    /// <summary>
    /// Include group with hierarchy and members
    /// </summary>
    public static IQueryable<Models.Group> IncludeHierarchyAndMembers(this IQueryable<Models.Group> query)
    {
        return query
            .Include(g => g.Entity)
            .Include(g => g.ParentGroupRelations)
                .ThenInclude(gh => gh.ParentGroup)
            .Include(g => g.ChildGroupRelations)
                .ThenInclude(gh => gh.ChildGroup)
            .Include(g => g.UserGroups)
                .ThenInclude(ug => ug.User)
            .Include(g => g.GroupRoles)
                .ThenInclude(gr => gr.Role);
    }

    /// <summary>
    /// Include role with assignments and permissions
    /// </summary>
    public static IQueryable<Models.Role> IncludeAssignmentsAndPermissions(this IQueryable<Models.Role> query)
    {
        return query
            .Include(r => r.Entity)
            .Include(r => r.UserRoles)
                .ThenInclude(ur => ur.User)
            .Include(r => r.GroupRoles)
                .ThenInclude(gr => gr.Group);
    }

    #endregion

    #region Performance Optimizations

    /// <summary>
    /// Apply no tracking for read-only queries
    /// </summary>
    public static IQueryable<T> AsNoTrackingQuery<T>(this IQueryable<T> query) where T : class
    {
        return query.AsNoTracking();
    }

    /// <summary>
    /// Apply tracking with identity resolution disabled for better performance
    /// </summary>
    public static IQueryable<T> AsNoTrackingWithIdentityResolution<T>(this IQueryable<T> query) where T : class
    {
        return query.AsNoTrackingWithIdentityResolution();
    }

    /// <summary>
    /// Apply query splitting for complex includes to avoid cartesian explosion
    /// </summary>
    public static IQueryable<T> AsSplitQuery<T>(this IQueryable<T> query) where T : class
    {
        return query.AsSplitQuery();
    }

    /// <summary>
    /// Apply single query optimization for simple includes
    /// </summary>
    public static IQueryable<T> AsSingleQuery<T>(this IQueryable<T> query) where T : class
    {
        return query.AsSingleQuery();
    }

    #endregion

    #region Filtering Extensions

    /// <summary>
    /// Filter entities by date range
    /// </summary>
    public static IQueryable<T> WhereCreatedBetween<T>(this IQueryable<T> query, DateTime? startDate, DateTime? endDate)
        where T : class
    {
        if (startDate.HasValue)
            query = query.Where(e => EF.Property<DateTime>(e, "CreatedAt") >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => EF.Property<DateTime>(e, "CreatedAt") <= endDate.Value);

        return query;
    }

    /// <summary>
    /// Filter entities by update date range
    /// </summary>
    public static IQueryable<T> WhereUpdatedBetween<T>(this IQueryable<T> query, DateTime? startDate, DateTime? endDate)
        where T : class
    {
        if (startDate.HasValue)
            query = query.Where(e => EF.Property<DateTime>(e, "UpdatedAt") >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => EF.Property<DateTime>(e, "UpdatedAt") <= endDate.Value);

        return query;
    }

    /// <summary>
    /// Filter users by active status
    /// </summary>
    public static IQueryable<Models.User> WhereActive(this IQueryable<Models.User> query, bool? isActive = true)
    {
        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        return query;
    }

    /// <summary>
    /// Filter users by failed login threshold
    /// </summary>
    public static IQueryable<Models.User> WhereFailedLoginsBelowThreshold(this IQueryable<Models.User> query, int threshold)
    {
        return query.Where(u => u.FailedLoginAttempts < threshold);
    }

    /// <summary>
    /// Filter audit logs by security events
    /// </summary>
    public static IQueryable<Models.AuditLog> WhereSecurityEvents(this IQueryable<Models.AuditLog> query)
    {
        return query.Where(al => 
            al.ChangeType.ToLower().Contains("security") ||
            al.ChangeDetails.ToLower().Contains("login") ||
            al.ChangeDetails.ToLower().Contains("failed") ||
            al.ChangeDetails.ToLower().Contains("unauthorized") ||
            al.ChangeDetails.ToLower().Contains("permission"));
    }

    #endregion

    #region Sorting Extensions

    /// <summary>
    /// Apply dynamic ordering based on property name and direction
    /// </summary>
    public static IOrderedQueryable<T> OrderByDynamic<T>(this IQueryable<T> query, string propertyName, bool ascending = true)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyName);
        var lambda = Expression.Lambda(property, parameter);

        var methodName = ascending ? "OrderBy" : "OrderByDescending";
        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            new[] { typeof(T), property.Type },
            query.Expression,
            Expression.Quote(lambda));

        return (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(resultExpression);
    }

    /// <summary>
    /// Apply secondary dynamic ordering
    /// </summary>
    public static IOrderedQueryable<T> ThenByDynamic<T>(this IOrderedQueryable<T> query, string propertyName, bool ascending = true)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyName);
        var lambda = Expression.Lambda(property, parameter);

        var methodName = ascending ? "ThenBy" : "ThenByDescending";
        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            new[] { typeof(T), property.Type },
            query.Expression,
            Expression.Quote(lambda));

        return (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(resultExpression);
    }

    #endregion

    #region Pagination Extensions

    /// <summary>
    /// Apply efficient pagination with optional total count
    /// </summary>
    public static async Task<PagedQueryResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, 
        int pageNumber, 
        int pageSize, 
        bool includeTotalCount = true,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var totalCount = includeTotalCount ? await query.CountAsync(cancellationToken) : 0;
        
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            HasTotalCount = includeTotalCount
        };
    }

    /// <summary>
    /// Apply cursor-based pagination for large datasets
    /// </summary>
    public static IQueryable<T> WhereCursor<T, TKey>(
        this IQueryable<T> query,
        Expression<Func<T, TKey>> keySelector,
        TKey? cursor,
        bool ascending = true) where TKey : IComparable<TKey>
    {
        if (cursor == null) return query;

        var parameter = keySelector.Parameters[0];
        var property = keySelector.Body;
        var constant = Expression.Constant(cursor);

        var comparison = ascending 
            ? Expression.GreaterThan(property, constant)
            : Expression.LessThan(property, constant);

        var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
        return query.Where(lambda);
    }

    #endregion

    #region Aggregation Extensions

    /// <summary>
    /// Get count with optional predicate (optimized for large tables)
    /// </summary>
    public static async Task<int> CountOptimizedAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        if (predicate != null)
            query = query.Where(predicate);

        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Check existence efficiently (stops at first match)
    /// </summary>
    public static async Task<bool> ExistsOptimizedAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await query.Where(predicate).AnyAsync(cancellationToken);
    }

    #endregion
}

/// <summary>
/// Paged query result with additional metadata
/// </summary>
public class PagedQueryResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasTotalCount { get; set; }
    public int TotalPages => HasTotalCount && PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => HasTotalCount ? PageNumber < TotalPages : Items.Count() == PageSize;
}

/// <summary>
/// Query performance metrics
/// </summary>
public class QueryPerformanceMetrics
{
    public TimeSpan ExecutionTime { get; set; }
    public int RecordsReturned { get; set; }
    public int TotalRecords { get; set; }
    public bool UsedIndex { get; set; }
    public string? ExecutionPlan { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}