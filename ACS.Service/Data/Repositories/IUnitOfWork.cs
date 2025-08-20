using Microsoft.EntityFrameworkCore.Storage;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Unit of Work pattern interface for coordinating repository operations
/// </summary>
public interface IUnitOfWork : IDisposable
{
    #region Repository Properties

    IUserRepository Users { get; }
    IGroupRepository Groups { get; }
    IRoleRepository Roles { get; }
    IResourceRepository Resources { get; }
    IAuditLogRepository AuditLogs { get; }
    IRepository<Models.Entity> Entities { get; }
    IRepository<Models.PermissionScheme> PermissionSchemes { get; }
    IRepository<Models.UriAccess> UriAccesses { get; }
    IRepository<Models.VerbType> VerbTypes { get; }
    IRepository<Models.SchemeType> SchemeTypes { get; }
    IRepository<Models.UserGroup> UserGroups { get; }
    IRepository<Models.UserRole> UserRoles { get; }
    IRepository<Models.GroupRole> GroupRoles { get; }
    IRepository<Models.GroupHierarchy> GroupHierarchies { get; }

    #endregion

    #region Transaction Management

    /// <summary>
    /// Save all pending changes to the database
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begin a database transaction
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute an operation within a transaction scope
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute an operation within a transaction scope (void)
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Execute multiple operations efficiently in a single transaction
    /// </summary>
    Task<BulkOperationResult> ExecuteBulkOperationsAsync(IEnumerable<IBulkOperation> operations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk save changes with optimized performance
    /// </summary>
    Task<int> BulkSaveChangesAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Caching Support

    /// <summary>
    /// Clear all repository caches
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Clear cache for specific repository type
    /// </summary>
    void ClearCache<T>() where T : class;

    #endregion

    #region Context Access

    /// <summary>
    /// Get the underlying database context (use sparingly)
    /// </summary>
    ApplicationDbContext GetContext();

    #endregion
}

/// <summary>
/// Bulk operation interface for unit of work
/// </summary>
public interface IBulkOperation
{
    string OperationType { get; }
    Task ExecuteAsync(ApplicationDbContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of bulk operations
/// </summary>
public class BulkOperationResult
{
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public IEnumerable<string> Errors { get; set; } = new List<string>();
    public TimeSpan ExecutionTime { get; set; }
    public bool IsSuccess => FailedOperations == 0;
}