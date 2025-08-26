using ACS.Service.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ACS.Service.Caching;

/// <summary>
/// In-memory implementation of entity caching
/// Separation of Concerns: Caching logic isolated from business logic
/// </summary>
public class MemoryEntityCache : IEntityCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryEntityCache> _logger;
    private readonly MemoryCacheEntryOptions _defaultOptions;
    private readonly ConcurrentDictionary<string, long> _hitsByType = new();
    private readonly ConcurrentDictionary<string, long> _missesByType = new();
    private readonly ActivitySource _activitySource = new("ACS.Caching");
    
    private long _totalHits;
    private long _totalMisses;
    private readonly DateTime _startTime;
    
    // Cache key prefixes for different entity types
    private const string USER_PREFIX = "user:";
    private const string GROUP_PREFIX = "group:";
    private const string ROLE_PREFIX = "role:";
    private const string PERMISSIONS_PREFIX = "permissions:";
    private const string USER_GROUPS_PREFIX = "user_groups:";
    private const string USER_ROLES_PREFIX = "user_roles:";
    
    public MemoryEntityCache(IMemoryCache cache, ILogger<MemoryEntityCache> logger)
    {
        _cache = cache;
        _logger = logger;
        _startTime = DateTime.UtcNow;
        
        // Configure default cache options
        _defaultOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            Priority = CacheItemPriority.Normal
        };
    }
    
    // User caching
    public Task<User?> GetUserAsync(int userId)
    {
        using var activity = _activitySource.StartActivity("GetUser");
        activity?.SetTag("user.id", userId);
        
        var key = $"{USER_PREFIX}{userId}";
        if (_cache.TryGetValue<User>(key, out var user))
        {
            IncrementHit("User");
            activity?.SetTag("cache.hit", true);
            _logger.LogTrace("Cache hit for user {UserId}", userId);
            return Task.FromResult<User?>(user);
        }
        
        IncrementMiss("User");
        activity?.SetTag("cache.hit", false);
        _logger.LogTrace("Cache miss for user {UserId}", userId);
        return Task.FromResult<User?>(null);
    }
    
    public Task SetUserAsync(User user)
    {
        using var activity = _activitySource.StartActivity("SetUser");
        activity?.SetTag("user.id", user.Id);
        
        var key = $"{USER_PREFIX}{user.Id}";
        _cache.Set(key, user, _defaultOptions);
        _logger.LogTrace("Cached user {UserId}", user.Id);
        return Task.CompletedTask;
    }
    
    public Task InvalidateUserAsync(int userId)
    {
        using var activity = _activitySource.StartActivity("InvalidateUser");
        activity?.SetTag("user.id", userId);
        
        var key = $"{USER_PREFIX}{userId}";
        _cache.Remove(key);
        
        // Also invalidate related caches
        _ = InvalidateUserGroupsAsync(userId);
        _ = InvalidateUserRolesAsync(userId);
        
        _logger.LogTrace("Invalidated cache for user {UserId}", userId);
        return Task.CompletedTask;
    }
    
    // Group caching
    public Task<Group?> GetGroupAsync(int groupId)
    {
        using var activity = _activitySource.StartActivity("GetGroup");
        activity?.SetTag("group.id", groupId);
        
        var key = $"{GROUP_PREFIX}{groupId}";
        if (_cache.TryGetValue<Group>(key, out var group))
        {
            IncrementHit("Group");
            activity?.SetTag("cache.hit", true);
            return Task.FromResult<Group?>(group);
        }
        
        IncrementMiss("Group");
        activity?.SetTag("cache.hit", false);
        return Task.FromResult<Group?>(null);
    }
    
    public Task SetGroupAsync(Group group)
    {
        using var activity = _activitySource.StartActivity("SetGroup");
        activity?.SetTag("group.id", group.Id);
        
        var key = $"{GROUP_PREFIX}{group.Id}";
        _cache.Set(key, group, _defaultOptions);
        return Task.CompletedTask;
    }
    
    public Task InvalidateGroupAsync(int groupId)
    {
        using var activity = _activitySource.StartActivity("InvalidateGroup");
        activity?.SetTag("group.id", groupId);
        
        var key = $"{GROUP_PREFIX}{groupId}";
        _cache.Remove(key);
        
        // Invalidate parent and child group caches if needed
        // This would require tracking relationships, skipping for simplicity
        return Task.CompletedTask;
    }
    
    // Role caching
    public Task<Role?> GetRoleAsync(int roleId)
    {
        using var activity = _activitySource.StartActivity("GetRole");
        activity?.SetTag("role.id", roleId);
        
        var key = $"{ROLE_PREFIX}{roleId}";
        if (_cache.TryGetValue<Role>(key, out var role))
        {
            IncrementHit("Role");
            activity?.SetTag("cache.hit", true);
            return Task.FromResult<Role?>(role);
        }
        
        IncrementMiss("Role");
        activity?.SetTag("cache.hit", false);
        return Task.FromResult<Role?>(null);
    }
    
    public Task SetRoleAsync(Role role)
    {
        using var activity = _activitySource.StartActivity("SetRole");
        activity?.SetTag("role.id", role.Id);
        
        var key = $"{ROLE_PREFIX}{role.Id}";
        _cache.Set(key, role, _defaultOptions);
        return Task.CompletedTask;
    }
    
    public Task InvalidateRoleAsync(int roleId)
    {
        using var activity = _activitySource.StartActivity("InvalidateRole");
        activity?.SetTag("role.id", roleId);
        
        var key = $"{ROLE_PREFIX}{roleId}";
        _cache.Remove(key);
        return Task.CompletedTask;
    }
    
    // Permission caching
    public Task<List<Permission>?> GetEntityPermissionsAsync(int entityId)
    {
        var key = $"{PERMISSIONS_PREFIX}{entityId}";
        if (_cache.TryGetValue<List<Permission>>(key, out var permissions))
        {
            IncrementHit("Permissions");
            return Task.FromResult<List<Permission>?>(permissions);
        }
        
        IncrementMiss("Permissions");
        return Task.FromResult<List<Permission>?>(null);
    }
    
    public Task SetEntityPermissionsAsync(int entityId, List<Permission> permissions)
    {
        var key = $"{PERMISSIONS_PREFIX}{entityId}";
        // Permissions might change more frequently, use shorter expiration
        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(2),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            Priority = CacheItemPriority.Normal
        };
        _cache.Set(key, permissions, options);
        return Task.CompletedTask;
    }
    
    public Task InvalidateEntityPermissionsAsync(int entityId)
    {
        var key = $"{PERMISSIONS_PREFIX}{entityId}";
        _cache.Remove(key);
        return Task.CompletedTask;
    }
    
    // Relationship caching
    public Task<List<int>?> GetUserGroupsAsync(int userId)
    {
        var key = $"{USER_GROUPS_PREFIX}{userId}";
        if (_cache.TryGetValue<List<int>>(key, out var groupIds))
        {
            IncrementHit("UserGroups");
            return Task.FromResult<List<int>?>(groupIds);
        }
        
        IncrementMiss("UserGroups");
        return Task.FromResult<List<int>?>(null);
    }
    
    public Task SetUserGroupsAsync(int userId, List<int> groupIds)
    {
        var key = $"{USER_GROUPS_PREFIX}{userId}";
        _cache.Set(key, groupIds, _defaultOptions);
        return Task.CompletedTask;
    }
    
    public Task InvalidateUserGroupsAsync(int userId)
    {
        var key = $"{USER_GROUPS_PREFIX}{userId}";
        _cache.Remove(key);
        return Task.CompletedTask;
    }
    
    public Task<List<int>?> GetUserRolesAsync(int userId)
    {
        var key = $"{USER_ROLES_PREFIX}{userId}";
        if (_cache.TryGetValue<List<int>>(key, out var roleIds))
        {
            IncrementHit("UserRoles");
            return Task.FromResult<List<int>?>(roleIds);
        }
        
        IncrementMiss("UserRoles");
        return Task.FromResult<List<int>?>(null);
    }
    
    public Task SetUserRolesAsync(int userId, List<int> roleIds)
    {
        var key = $"{USER_ROLES_PREFIX}{userId}";
        _cache.Set(key, roleIds, _defaultOptions);
        return Task.CompletedTask;
    }
    
    public Task InvalidateUserRolesAsync(int userId)
    {
        var key = $"{USER_ROLES_PREFIX}{userId}";
        _cache.Remove(key);
        return Task.CompletedTask;
    }
    
    // Cache management
    public Task<CacheStatistics> GetStatisticsAsync()
    {
        var stats = new CacheStatistics
        {
            TotalHits = _totalHits,
            TotalMisses = _totalMisses,
            LastResetTime = _startTime,
            HitsByType = _hitsByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            MissesByType = _missesByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
        
        // Memory cache doesn't expose item count or memory usage directly
        // These would need custom tracking in production
        stats.ItemCount = -1;
        stats.MemoryUsageBytes = -1;
        
        return Task.FromResult(stats);
    }
    
    public Task ClearAsync()
    {
        using var activity = _activitySource.StartActivity("ClearCache");
        
        // MemoryCache doesn't have a clear method, would need to track keys
        // For production, consider using a wrapper that tracks all keys
        _logger.LogInformation("Cache clear requested (not fully implemented for MemoryCache)");
        
        // Reset statistics
        Interlocked.Exchange(ref _totalHits, 0);
        Interlocked.Exchange(ref _totalMisses, 0);
        _hitsByType.Clear();
        _missesByType.Clear();
        
        return Task.CompletedTask;
    }
    
    public Task WarmupAsync()
    {
        using var activity = _activitySource.StartActivity("WarmupCache");
        
        // In production, this would preload frequently accessed entities
        // For now, just log the warmup request
        _logger.LogInformation("Cache warmup requested");
        
        return Task.CompletedTask;
    }
    
    private void IncrementHit(string entityType)
    {
        Interlocked.Increment(ref _totalHits);
        _hitsByType.AddOrUpdate(entityType, 1, (_, count) => count + 1);
    }
    
    private void IncrementMiss(string entityType)
    {
        Interlocked.Increment(ref _totalMisses);
        _missesByType.AddOrUpdate(entityType, 1, (_, count) => count + 1);
    }
}