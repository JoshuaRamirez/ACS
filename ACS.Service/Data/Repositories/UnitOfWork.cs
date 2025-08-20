using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Unit of Work pattern implementation for coordinating repository operations
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;
    private readonly Dictionary<Type, object> _repositories = new();
    private bool _disposed = false;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Repository Properties

    public IUserRepository Users => GetRepository<IUserRepository>(() => new UserRepository(_context));
    public IGroupRepository Groups => GetRepository<IGroupRepository>(() => new GroupRepository(_context));
    public IRoleRepository Roles => GetRepository<IRoleRepository>(() => new RoleRepository(_context));
    public IResourceRepository Resources => GetRepository<IResourceRepository>(() => new ResourceRepository(_context));
    public IAuditLogRepository AuditLogs => GetRepository<IAuditLogRepository>(() => new AuditLogRepository(_context));
    public IRepository<Models.Entity> Entities => GetRepository<IRepository<Models.Entity>>(() => new Repository<Models.Entity>(_context));
    public IRepository<Models.PermissionScheme> PermissionSchemes => GetRepository<IRepository<Models.PermissionScheme>>(() => new Repository<Models.PermissionScheme>(_context));
    public IRepository<Models.UriAccess> UriAccesses => GetRepository<IRepository<Models.UriAccess>>(() => new Repository<Models.UriAccess>(_context));
    public IRepository<Models.VerbType> VerbTypes => GetRepository<IRepository<Models.VerbType>>(() => new Repository<Models.VerbType>(_context));
    public IRepository<Models.SchemeType> SchemeTypes => GetRepository<IRepository<Models.SchemeType>>(() => new Repository<Models.SchemeType>(_context));
    public IRepository<Models.UserGroup> UserGroups => GetRepository<IRepository<Models.UserGroup>>(() => new Repository<Models.UserGroup>(_context));
    public IRepository<Models.UserRole> UserRoles => GetRepository<IRepository<Models.UserRole>>(() => new Repository<Models.UserRole>(_context));
    public IRepository<Models.GroupRole> GroupRoles => GetRepository<IRepository<Models.GroupRole>>(() => new Repository<Models.GroupRole>(_context));
    public IRepository<Models.GroupHierarchy> GroupHierarchies => GetRepository<IRepository<Models.GroupHierarchy>>(() => new Repository<Models.GroupHierarchy>(_context));

    #endregion

    #region Repository Management

    private T GetRepository<T>(Func<T> factory) where T : class
    {
        var type = typeof(T);
        if (_repositories.ContainsKey(type))
        {
            return (T)_repositories[type];
        }

        var repository = factory();
        _repositories.Add(type, repository);
        return repository;
    }

    #endregion

    #region Transaction Management

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Handle concurrency conflicts
            foreach (var entry in ex.Entries)
            {
                if (entry.Entity != null)
                {
                    var proposedValues = entry.CurrentValues;
                    var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);

                    if (databaseValues == null)
                    {
                        // Entity was deleted by another user
                        throw new InvalidOperationException($"Entity {entry.Entity.GetType().Name} was deleted by another user.", ex);
                    }

                    // Reset original values to database values
                    entry.OriginalValues.SetValues(databaseValues);
                }
            }

            // Retry the save operation
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _transaction;
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            try
            {
                await _transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await _transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            try
            {
                await _transaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            // Already in a transaction, just execute the operation
            return await operation();
        }

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation();
            await CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await operation();
            return true;
        }, cancellationToken);
    }

    #endregion

    #region Bulk Operations

    public async Task<BulkOperationResult> ExecuteBulkOperationsAsync(IEnumerable<IBulkOperation> operations, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operationsList = operations.ToList();
        var result = new BulkOperationResult
        {
            TotalOperations = operationsList.Count
        };
        var errors = new List<string>();

        await ExecuteInTransactionAsync(async () =>
        {
            foreach (var operation in operationsList)
            {
                try
                {
                    await operation.ExecuteAsync(_context, cancellationToken);
                    result.SuccessfulOperations++;
                }
                catch (Exception ex)
                {
                    result.FailedOperations++;
                    errors.Add($"{operation.OperationType}: {ex.Message}");
                }
            }

            if (result.FailedOperations > 0)
            {
                throw new InvalidOperationException($"Bulk operation failed with {result.FailedOperations} errors.");
            }

            await SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        stopwatch.Stop();
        result.ExecutionTime = stopwatch.Elapsed;
        result.Errors = errors;

        return result;
    }

    public async Task<int> BulkSaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Disable auto-detect changes for better performance
        _context.ChangeTracker.AutoDetectChangesEnabled = false;
        
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    #endregion

    #region Caching Support

    public void ClearCache()
    {
        _repositories.Clear();
    }

    public void ClearCache<T>() where T : class
    {
        var type = typeof(T);
        if (_repositories.ContainsKey(type))
        {
            _repositories.Remove(type);
        }
    }

    #endregion

    #region Context Access

    public ApplicationDbContext GetContext()
    {
        return _context;
    }

    #endregion

    #region IDisposable Implementation

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _repositories.Clear();
            _context.Dispose();
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Base class for bulk operations
/// </summary>
public abstract class BulkOperationBase : IBulkOperation
{
    public abstract string OperationType { get; }
    public abstract Task ExecuteAsync(ApplicationDbContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Bulk insert operation
/// </summary>
public class BulkInsertOperation<T> : BulkOperationBase where T : class
{
    private readonly IEnumerable<T> _entities;

    public BulkInsertOperation(IEnumerable<T> entities)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
    }

    public override string OperationType => $"BulkInsert<{typeof(T).Name}>";

    public override async Task ExecuteAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Set<T>().AddRangeAsync(_entities, cancellationToken);
    }
}

/// <summary>
/// Bulk update operation
/// </summary>
public class BulkUpdateOperation<T> : BulkOperationBase where T : class
{
    private readonly IEnumerable<T> _entities;

    public BulkUpdateOperation(IEnumerable<T> entities)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
    }

    public override string OperationType => $"BulkUpdate<{typeof(T).Name}>";

    public override async Task ExecuteAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        context.Set<T>().UpdateRange(_entities);
        await Task.CompletedTask; // UpdateRange is synchronous
    }
}

/// <summary>
/// Bulk delete operation
/// </summary>
public class BulkDeleteOperation<T> : BulkOperationBase where T : class
{
    private readonly IEnumerable<T> _entities;

    public BulkDeleteOperation(IEnumerable<T> entities)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
    }

    public override string OperationType => $"BulkDelete<{typeof(T).Name}>";

    public override async Task ExecuteAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        context.Set<T>().RemoveRange(_entities);
        await Task.CompletedTask; // RemoveRange is synchronous
    }
}