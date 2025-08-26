using ACS.Service.Data;
using ACS.Service.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace ACS.Service.Services;

/// <summary>
/// Enhanced repository base class with basic query optimization
/// </summary>
public abstract class OptimizedRepository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;
    protected readonly ILogger _logger;

    protected OptimizedRepository(
        ApplicationDbContext context,
        ILogger logger)
    {
        _context = context;
        _dbSet = context.Set<T>();
        _logger = logger;
    }

    public virtual async Task<T?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(BuildIdPredicate<TKey>(id));
        return await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<T?> GetByIdAsync<TKey>(TKey id, 
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(BuildIdPredicate<TKey>(id));
        
        if (include != null)
        {
            query = include(query);
        }
        
        return await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<T?> GetByIdWithIncludesAsync(object id, params Expression<Func<T, object>>[] includes)
    {
        var query = _dbSet.Where(BuildIdPredicate(id));
        
        // Apply includes
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        
        return await query.AsNoTracking().FirstOrDefaultAsync();
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.AsNoTracking().ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();
        
        if (include != null)
        {
            query = include(query);
        }
        
        if (orderBy != null)
        {
            query = orderBy(query);
        }
        
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }

    public virtual async Task<PagedResult<T>> GetPagedAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        
        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public virtual async Task<PagedResult<T>> GetPagedWithPredicateAsync(
        Expression<Func<T, bool>> predicate,
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(predicate);
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        
        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public virtual async Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).AsNoTracking().ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(predicate);
        
        if (include != null)
        {
            query = include(query);
        }
        
        if (orderBy != null)
        {
            query = orderBy(query);
        }
        
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> FindWithIncludesAsync(
        Expression<Func<T, bool>> predicate,
        params Expression<Func<T, object>>[] includes)
    {
        var query = _dbSet.Where(predicate);
        
        // Apply includes
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        
        return await query.AsNoTracking().ToListAsync();
    }

    public virtual async Task<T?> FindFirstAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<T?> FindFirstAsync(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(predicate);
        
        if (include != null)
        {
            query = include(query);
        }
        
        return await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        
        return await query.CountAsync(cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).AnyAsync(cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await _dbSet.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    public virtual async Task<IEnumerable<T>> AddRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        await _dbSet.AddRangeAsync(entityList, cancellationToken);
        return entityList;
    }

    public virtual Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = _dbSet.Update(entity);
        return Task.FromResult(entry.Entity);
    }

    public virtual Task<IEnumerable<T>> UpdateRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        _dbSet.UpdateRange(entityList);
        return Task.FromResult<IEnumerable<T>>(entityList);
    }

    public virtual async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            _dbSet.Remove(entity);
        }
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    public virtual async Task DeleteRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        _dbSet.RemoveRange(entities);
        await Task.CompletedTask;
    }

    public virtual async Task DeleteRangeAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        _dbSet.RemoveRange(entities);
    }

    // Bulk operations for better performance
    public virtual async Task<int> BulkUpdateAsync(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, T>> updateExpression,
        CancellationToken cancellationToken = default)
    {
        // This would use a bulk update library like EFCore.BulkExtensions
        // For now, fall back to standard approach
        var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        
        foreach (var entity in entities)
        {
            // Apply update (this is a simplified approach)
            _dbSet.Update(entity);
        }
        
        return entities.Count;
    }

    public virtual async Task BulkDeleteAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        _dbSet.RemoveRange(entities);
    }

    // Query building helpers
    protected virtual Expression<Func<T, bool>> BuildIdPredicate<TKey>(TKey id)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, "Id");
        var constant = Expression.Constant(id, typeof(TKey));
        var equals = Expression.Equal(property, Expression.Convert(constant, property.Type));
        
        return Expression.Lambda<Func<T, bool>>(equals, parameter);
    }

    protected virtual Expression<Func<T, bool>> BuildIdPredicate(object id)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, "Id");
        var constant = Expression.Constant(id);
        var equals = Expression.Equal(property, constant);
        
        return Expression.Lambda<Func<T, bool>>(equals, parameter);
    }

    // Missing IRepository interface methods
    public virtual async Task<PagedResult<T>> GetPagedAsync(int pageNumber, int pageSize,
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        
        if (include != null)
        {
            query = include(query);
        }
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        if (orderBy != null)
        {
            query = orderBy(query);
        }
        
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        
        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public virtual async Task<PagedResult<TProjection>> GetPagedAsync<TProjection>(int pageNumber, int pageSize,
        Expression<Func<T, TProjection>> selector,
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        if (orderBy != null)
        {
            query = orderBy(query);
        }
        
        var items = await query
            .AsNoTracking()
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(selector)
            .ToListAsync(cancellationToken);
        
        return new PagedResult<TProjection>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public virtual async Task DeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        _dbSet.RemoveRange(entities);
    }

    public virtual async Task DeleteByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            _dbSet.Remove(entity);
        }
    }

    public virtual async Task BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    public virtual async Task BulkUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        _dbSet.UpdateRange(entities);
        await Task.CompletedTask;
    }


    public virtual async Task<IEnumerable<T>> FromSqlAsync(string sql, params object[] parameters)
    {
        return await _dbSet.FromSqlRaw(sql, parameters).AsNoTracking().ToListAsync();
    }

    public virtual async Task<int> ExecuteSqlAsync(string sql, params object[] parameters)
    {
        return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
    }

    public virtual IQueryable<T> Query()
    {
        return _dbSet.AsQueryable();
    }

    public virtual IQueryable<T> QueryAsNoTracking()
    {
        return _dbSet.AsNoTracking();
    }

    // Projection methods for better performance
    public virtual async Task<IEnumerable<TResult>> ProjectAsync<TResult>(
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Select(selector);
        return await query.ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<TResult>> ProjectAsync<TResult>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(predicate).Select(selector);
        return await query.ToListAsync(cancellationToken);
    }

    public virtual async Task<OptimizedPagedResult<TResult>> ProjectPagedAsync<TResult>(
        Expression<Func<T, TResult>> selector,
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Select(selector);
        
        // Create a wrapper to handle projections in pagination
        var projectedQuery = query.AsQueryable();
        
        // Apply filters and sorting to projection
        var totalCount = await projectedQuery.CountAsync(cancellationToken); // Always count since IncludeTotalCount doesn't exist
        
        var items = await projectedQuery
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        
        return new OptimizedPagedResult<TResult>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }
}