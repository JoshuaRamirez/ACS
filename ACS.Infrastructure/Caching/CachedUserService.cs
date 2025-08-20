using ACS.Infrastructure.Services;
using ACS.Service.Domain;
using ACS.Service.Services;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Cached decorator for IUserService using cache-aside pattern
/// </summary>
public class CachedUserService : IUserService
{
    private readonly IUserService _userService;
    private readonly ICacheAsideService _cacheService;
    private readonly ITenantContextService _tenantContext;
    private readonly ILogger<CachedUserService> _logger;

    public CachedUserService(
        IUserService userService,
        ICacheAsideService cacheService,
        ITenantContextService tenantContext,
        ILogger<CachedUserService> logger)
    {
        _userService = userService;
        _cacheService = cacheService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        var cacheKey = $"{tenantId}:user:{id}";
        
        return await _cacheService.GetOrSetAsync(
            cacheKey,
            () => _userService.GetByIdAsync(id, cancellationToken),
            CacheType.User,
            cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        var cacheKey = $"{tenantId}:user:username:{username.ToLowerInvariant()}";
        
        return await _cacheService.GetOrSetAsync(
            cacheKey,
            () => _userService.GetByUsernameAsync(username, cancellationToken),
            CacheType.User,
            cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        var cacheKey = $"{tenantId}:user:email:{email.ToLowerInvariant()}";
        
        return await _cacheService.GetOrSetAsync(
            cacheKey,
            () => _userService.GetByEmailAsync(email, cancellationToken),
            CacheType.User,
            cancellationToken);
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        var cacheKey = $"{tenantId}:users:all";
        
        return await _cacheService.GetOrSetAsync(
            cacheKey,
            () => _userService.GetAllAsync(cancellationToken),
            CacheType.User,
            cancellationToken) ?? Enumerable.Empty<User>();
    }

    public async Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        var idList = ids.ToList();
        
        var keyFactoryPairs = idList.ToDictionary(
            id => $"{tenantId}:user:{id}",
            id => (Func<Task<User?>>)(() => _userService.GetByIdAsync(id, cancellationToken))
        );
        
        var results = await _cacheService.GetOrSetManyAsync(keyFactoryPairs, CacheType.User, cancellationToken);
        return results.Values.Where(u => u != null).Cast<User>();
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        
        var invalidationEvent = new CacheInvalidationEvent
        {
            Key = $"{tenantId}:user:{user.Id}",
            Type = CacheType.User,
            TenantId = tenantId,
            Source = nameof(CachedUserService),
            DependentKeys = new[]
            {
                $"{tenantId}:users:all",
                $"{tenantId}:user:username:{user.Username?.ToLowerInvariant()}",
                $"{tenantId}:user:email:{user.Email?.ToLowerInvariant()}"
            }
        };
        
        return await _cacheService.ExecuteWithInvalidationAsync(
            () => _userService.CreateAsync(user, cancellationToken),
            invalidationEvent,
            cancellationToken);
    }

    public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        
        var invalidationEvent = new CacheInvalidationEvent
        {
            Key = $"{tenantId}:user:{user.Id}",
            Type = CacheType.User,
            TenantId = tenantId,
            Source = nameof(CachedUserService),
            DependentKeys = new[]
            {
                $"{tenantId}:users:all",
                $"{tenantId}:user:username:{user.Username?.ToLowerInvariant()}",
                $"{tenantId}:user:email:{user.Email?.ToLowerInvariant()}",
                $"{tenantId}:user_groups:{user.Id}",
                $"{tenantId}:user_roles:{user.Id}",
                $"{tenantId}:permissions:user:{user.Id}"
            }
        };
        
        return await _cacheService.ExecuteWithInvalidationAsync(
            () => _userService.UpdateAsync(user, cancellationToken),
            invalidationEvent,
            cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        
        // Get user first to get username/email for cache invalidation
        var user = await GetByIdAsync(id, cancellationToken);
        
        var invalidationEvent = new CacheInvalidationEvent
        {
            Key = $"{tenantId}:user:{id}",
            Type = CacheType.User,
            TenantId = tenantId,
            Source = nameof(CachedUserService),
            DependentKeys = new[]
            {
                $"{tenantId}:users:all",
                $"{tenantId}:user:username:{user?.Username?.ToLowerInvariant()}",
                $"{tenantId}:user:email:{user?.Email?.ToLowerInvariant()}",
                $"{tenantId}:user_groups:{id}",
                $"{tenantId}:user_roles:{id}",
                $"{tenantId}:permissions:user:{id}"
            }.Where(k => !string.IsNullOrEmpty(k)).ToArray()
        };
        
        await _cacheService.ExecuteWithInvalidationAsync(
            () => _userService.DeleteAsync(id, cancellationToken),
            invalidationEvent,
            cancellationToken);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        // For existence checks, we can leverage the cached GetById
        var user = await GetByIdAsync(id, cancellationToken);
        return user != null;
    }

    public async Task<bool> IsUsernameUniqueAsync(string username, int? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        // Username uniqueness should always be checked against the database
        return await _userService.IsUsernameUniqueAsync(username, excludeUserId, cancellationToken);
    }

    public async Task<bool> IsEmailUniqueAsync(string email, int? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        // Email uniqueness should always be checked against the database  
        return await _userService.IsEmailUniqueAsync(email, excludeUserId, cancellationToken);
    }

    public async Task<IEnumerable<User>> SearchAsync(string searchTerm, int pageSize = 50, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        var cacheKey = $"{tenantId}:users:search:{searchTerm.ToLowerInvariant()}:{pageSize}:{pageNumber}";
        
        return await _cacheService.GetOrSetAsync(
            cacheKey,
            () => _userService.SearchAsync(searchTerm, pageSize, pageNumber, cancellationToken),
            CacheType.User,
            cancellationToken) ?? Enumerable.Empty<User>();
    }

    public async Task<IEnumerable<User>> GetUsersInGroupAsync(int groupId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        var cacheKey = $"{tenantId}:users:group:{groupId}";
        
        return await _cacheService.GetOrSetAsync(
            cacheKey,
            () => _userService.GetUsersInGroupAsync(groupId, cancellationToken),
            CacheType.UserGroups,
            cancellationToken) ?? Enumerable.Empty<User>();
    }

    public async Task<IEnumerable<User>> GetUsersWithRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId() ?? "default";
        var cacheKey = $"{tenantId}:users:role:{roleId}";
        
        return await _cacheService.GetOrSetAsync(
            cacheKey,
            () => _userService.GetUsersWithRoleAsync(roleId, cancellationToken),
            CacheType.UserRoles,
            cancellationToken) ?? Enumerable.Empty<User>();
    }
}