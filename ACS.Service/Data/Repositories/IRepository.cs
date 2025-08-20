using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Generic repository interface for data access operations
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface IRepository<T> where T : class
{
    #region Query Operations

    /// <summary>
    /// Get entity by primary key
    /// </summary>
    Task<T?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get entity by primary key with include expressions
    /// </summary>
    Task<T?> GetByIdAsync<TKey>(TKey id, 
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find entities matching the predicate
    /// </summary>
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find entities with includes and ordering
    /// </summary>
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find first entity matching the predicate
    /// </summary>
    Task<T?> FindFirstAsync(Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find first entity with includes
    /// </summary>
    Task<T?> FindFirstAsync(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all entities
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all entities with includes and ordering
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync(
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count entities matching the predicate
    /// </summary>
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if any entity matches the predicate
    /// </summary>
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default);

    #endregion

    #region Paging Operations

    /// <summary>
    /// Get paged results with optional filtering, includes, and ordering
    /// </summary>
    Task<PagedResult<T>> GetPagedAsync(int pageNumber, int pageSize,
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged results with projection
    /// </summary>
    Task<PagedResult<TProjection>> GetPagedAsync<TProjection>(int pageNumber, int pageSize,
        Expression<Func<T, TProjection>> selector,
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Modification Operations

    /// <summary>
    /// Add new entity
    /// </summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add multiple entities
    /// </summary>
    Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing entity
    /// </summary>
    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update multiple entities
    /// </summary>
    Task<IEnumerable<T>> UpdateRangeAsync(IEnumerable<T> entities, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete entity
    /// </summary>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete entities by predicate
    /// </summary>
    Task DeleteAsync(Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete multiple entities
    /// </summary>
    Task DeleteRangeAsync(IEnumerable<T> entities, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete entity by primary key
    /// </summary>
    Task DeleteByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default);

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Bulk insert entities
    /// </summary>
    Task BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk update entities
    /// </summary>
    Task BulkUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk delete entities
    /// </summary>
    Task BulkDeleteAsync(Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default);

    #endregion

    #region Raw SQL Operations

    /// <summary>
    /// Execute raw SQL query
    /// </summary>
    Task<IEnumerable<T>> FromSqlAsync(string sql, params object[] parameters);

    /// <summary>
    /// Execute raw SQL command
    /// </summary>
    Task<int> ExecuteSqlAsync(string sql, params object[] parameters);

    #endregion

    #region Transaction Support

    /// <summary>
    /// Get queryable for complex operations
    /// </summary>
    IQueryable<T> Query();

    /// <summary>
    /// Get queryable with no tracking for read-only operations
    /// </summary>
    IQueryable<T> QueryAsNoTracking();

    #endregion
}

/// <summary>
/// Paged result container
/// </summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}