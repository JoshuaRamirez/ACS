using ACS.Infrastructure.Performance;
using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Optimized UserRepository with N+1 prevention and smart querying
/// </summary>
public class OptimizedUserRepository : OptimizedRepository<User>, IUserRepository
{
    public OptimizedUserRepository(
        ApplicationDbContext context,
        IQueryOptimizer queryOptimizer,
        ILogger<OptimizedUserRepository> logger)
        : base(context, queryOptimizer, logger)
    {
    }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(u => u.Entity)
            .Where(u => u.Email == email);

        var optimizationOptions = new QueryOptimizationOptions
        {
            EnableNoTracking = true,
            EnableAutoIncludes = true
        };

        var optimizedQuery = await _queryOptimizer.OptimizeQueryAsync(query, optimizationOptions);
        return await optimizedQuery.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> FindByRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        // Optimized query that prevents N+1 by using a single query with proper includes
        var query = _dbSet
            .Include(u => u.Entity)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Entity)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == roleName));

        var optimizationOptions = new QueryOptimizationOptions
        {
            EnableNoTracking = true,
            EnableQuerySplitting = true // Use split query for complex includes
        };

        var optimizedQuery = await _queryOptimizer.OptimizeQueryAsync(query, optimizationOptions);
        return await _queryOptimizer.ExecuteWithN1DetectionAsync(optimizedQuery, cancellationToken);
    }

    public async Task<IEnumerable<User>> FindByGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        // Optimized query with proper includes to prevent N+1
        var query = _dbSet
            .Include(u => u.Entity)
            .Include(u => u.UserGroups)
                .ThenInclude(ug => ug.Group)
                    .ThenInclude(g => g.Entity)
            .Where(u => u.UserGroups.Any(ug => ug.Group.Name == groupName));

        var optimizationOptions = new QueryOptimizationOptions
        {
            EnableNoTracking = true,
            EnableQuerySplitting = true
        };

        var optimizedQuery = await _queryOptimizer.OptimizeQueryAsync(query, optimizationOptions);
        return await _queryOptimizer.ExecuteWithN1DetectionAsync(optimizedQuery, cancellationToken);
    }

    public async Task<IEnumerable<User>> FindActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(u => u.Entity)
            .Where(u => u.IsActive);

        var optimizationOptions = new QueryOptimizationOptions
        {
            EnableNoTracking = true
        };

        var optimizedQuery = await _queryOptimizer.OptimizeQueryAsync(query, optimizationOptions);
        return await _queryOptimizer.ExecuteWithN1DetectionAsync(optimizedQuery, cancellationToken);
    }

    public async Task<IEnumerable<User>> FindUsersWithExcessiveFailedLoginsAsync(int threshold, CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(u => u.Entity)
            .Where(u => u.FailedLoginAttempts >= threshold);

        var optimizationOptions = new QueryOptimizationOptions
        {
            EnableNoTracking = true
        };

        var optimizedQuery = await _queryOptimizer.OptimizeQueryAsync(query, optimizationOptions);
        return await _queryOptimizer.ExecuteWithN1DetectionAsync(optimizedQuery, cancellationToken);
    }

    // Enhanced methods with better performance patterns

    /// <summary>
    /// Gets users with their complete security context (roles, groups, permissions) in one query
    /// </summary>
    public async Task<IEnumerable<User>> GetUsersWithSecurityContextAsync(
        Expression<Func<User, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(u => u.Entity)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Entity)
            .Include(u => u.UserGroups)
                .ThenInclude(ug => ug.Group)
                    .ThenInclude(g => g.Entity)
                        .ThenInclude(e => e.PermissionSchemes)
            .AsQueryable();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        var optimizationOptions = new QueryOptimizationOptions
        {
            EnableNoTracking = true,
            EnableQuerySplitting = true // Essential for complex includes
        };

        var optimizedQuery = await _queryOptimizer.OptimizeQueryAsync(query, optimizationOptions);
        return await _queryOptimizer.ExecuteWithN1DetectionAsync(optimizedQuery, cancellationToken);
    }

    /// <summary>
    /// Gets paginated users with optimized performance
    /// </summary>
    public async Task<OptimizedPagedResult<User>> GetUsersPagedAsync(
        string? searchTerm = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var request = new PaginationRequest
        {
            PageSize = 20,
            PageNumber = 1,
            SearchTerm = searchTerm,
            IncludeRelated = new List<string> { "Entity" }
        };

        if (isActive.HasValue)
        {
            request.Filters.Add("IsActive", isActive.Value);
        }

        var query = _dbSet.AsQueryable();

        // Apply filters
        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            var searchLower = searchTerm.ToLower();
            query = query.Where(u => 
                u.Username.ToLower().Contains(searchLower) ||
                u.Email.ToLower().Contains(searchLower) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(searchLower)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(searchLower)));
        }

        return await _queryOptimizer.PaginateOptimizedAsync(query, request, cancellationToken);
    }

    /// <summary>
    /// Gets user IDs only for lightweight operations
    /// </summary>
    public async Task<IEnumerable<int>> GetUserIdsAsync(
        Expression<Func<User, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query
            .Select(u => u.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets user summary information for lists/dropdowns
    /// </summary>
    public async Task<IEnumerable<UserSummary>> GetUserSummariesAsync(
        Expression<Func<User, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query
            .Select(u => new UserSummary
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                FullName = u.FirstName + " " + u.LastName,
                IsActive = u.IsActive
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets users by multiple IDs efficiently (prevents N+1 when loading related users)
    /// </summary>
    public async Task<Dictionary<int, User>> GetUsersByIdsAsync(
        IEnumerable<int> userIds,
        bool includeSecurityContext = false,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToList();
        if (!ids.Any()) return new Dictionary<int, User>();

        var query = _dbSet
            .Include(u => u.Entity)
            .Where(u => ids.Contains(u.Id));

        if (includeSecurityContext)
        {
            query = query
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.UserGroups)
                    .ThenInclude(ug => ug.Group);
        }

        var optimizationOptions = new QueryOptimizationOptions
        {
            EnableNoTracking = true,
            EnableQuerySplitting = includeSecurityContext
        };

        var optimizedQuery = await _queryOptimizer.OptimizeQueryAsync(query, optimizationOptions);
        var users = await _queryOptimizer.ExecuteWithN1DetectionAsync(optimizedQuery, cancellationToken);

        return users.ToDictionary(u => u.Id, u => u);
    }

    /// <summary>
    /// Bulk update user properties efficiently
    /// </summary>
    public async Task<int> BulkUpdateUserStatusAsync(
        IEnumerable<int> userIds,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToList();
        if (!ids.Any()) return 0;

        // Use bulk update for better performance
        return await BulkUpdateAsync(
            u => ids.Contains(u.Id),
            u => new User { IsActive = isActive },
            cancellationToken);
    }

    /// <summary>
    /// Gets user count by role efficiently
    /// </summary>
    public async Task<Dictionary<string, int>> GetUserCountByRoleAsync(CancellationToken cancellationToken = default)
    {
        return await _context.UserRoles
            .Include(ur => ur.Role)
            .GroupBy(ur => ur.Role.Name)
            .Select(g => new { RoleName = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToDictionaryAsync(x => x.RoleName, x => x.Count, cancellationToken);
    }

    /// <summary>
    /// Gets user count by group efficiently
    /// </summary>
    public async Task<Dictionary<string, int>> GetUserCountByGroupAsync(CancellationToken cancellationToken = default)
    {
        return await _context.UserGroups
            .Include(ug => ug.Group)
            .GroupBy(ug => ug.Group.Name)
            .Select(g => new { GroupName = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToDictionaryAsync(x => x.GroupName, x => x.Count, cancellationToken);
    }
}

/// <summary>
/// Lightweight user summary for lists and dropdowns
/// </summary>
public class UserSummary
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}