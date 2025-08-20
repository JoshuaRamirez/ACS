using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Generic repository implementation for data access operations
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<T>();
    }

    #region Query Operations

    public virtual async Task<T?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id! }, cancellationToken);
    }

    public virtual async Task<T?> GetByIdAsync<TKey>(TKey id, 
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;
        
        if (include != null)
            query = include(query);

        return await query.FirstOrDefaultAsync(e => EF.Property<TKey>(e, "Id")!.Equals(id), cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.Where(predicate);
        
        if (include != null)
            query = include(query);
        
        if (orderBy != null)
            query = orderBy(query);

        return await query.ToListAsync(cancellationToken);
    }

    public virtual async Task<T?> FindFirstAsync(Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<T?> FindFirstAsync(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.Where(predicate);
        
        if (include != null)
            query = include(query);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;
        
        if (include != null)
            query = include(query);
        
        if (orderBy != null)
            query = orderBy(query);

        return await query.ToListAsync(cancellationToken);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, 
        CancellationToken cancellationToken = default)
    {
        if (predicate == null)
            return await _dbSet.CountAsync(cancellationToken);
        
        return await _dbSet.CountAsync(predicate, cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(predicate, cancellationToken);
    }

    #endregion

    #region Paging Operations

    public virtual async Task<PagedResult<T>> GetPagedAsync(int pageNumber, int pageSize,
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        IQueryable<T> query = _dbSet;
        
        if (predicate != null)
            query = query.Where(predicate);
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        if (include != null)
            query = include(query);
        
        if (orderBy != null)
            query = orderBy(query);
        else
            query = query.OrderBy(e => EF.Property<object>(e, "Id"));

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
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
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        IQueryable<T> query = _dbSet;
        
        if (predicate != null)
            query = query.Where(predicate);
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        if (orderBy != null)
            query = orderBy(query);
        else
            query = query.OrderBy(e => EF.Property<object>(e, "Id"));

        var items = await query
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

    #endregion

    #region Modification Operations

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, 
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        await _dbSet.AddRangeAsync(entityList, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entityList;
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task<IEnumerable<T>> UpdateRangeAsync(IEnumerable<T> entities, 
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        _dbSet.UpdateRange(entityList);
        await _context.SaveChangesAsync(cancellationToken);
        return entityList;
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        _dbSet.RemoveRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteRangeAsync(IEnumerable<T> entities, 
        CancellationToken cancellationToken = default)
    {
        _dbSet.RemoveRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    #endregion

    #region Bulk Operations

    public virtual async Task BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task BulkUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        _dbSet.UpdateRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task BulkDeleteAsync(Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        _dbSet.RemoveRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Raw SQL Operations

    public virtual async Task<IEnumerable<T>> FromSqlAsync(string sql, params object[] parameters)
    {
        return await _dbSet.FromSqlRaw(sql, parameters).ToListAsync();
    }

    public virtual async Task<int> ExecuteSqlAsync(string sql, params object[] parameters)
    {
        return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
    }

    #endregion

    #region Transaction Support

    public virtual IQueryable<T> Query()
    {
        return _dbSet;
    }

    public virtual IQueryable<T> QueryAsNoTracking()
    {
        return _dbSet.AsNoTracking();
    }

    #endregion
}