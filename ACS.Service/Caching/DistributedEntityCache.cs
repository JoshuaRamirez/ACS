using ACS.Service.Domain;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace ACS.Service.Caching;

/// <summary>
/// Distributed cache implementation for entity caching across tenant processes
/// Separation of Concerns: Distributed caching logic isolated from business logic
/// </summary>
public class DistributedEntityCache : IEntityCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedEntityCache> _logger;
    private readonly DistributedCacheEntryOptions _defaultOptions;
    private readonly ConcurrentDictionary<string, long> _hitsByType = new();
    private readonly ConcurrentDictionary<string, long> _missesByType = new();
    private readonly ActivitySource _activitySource = new("ACS.DistributedCaching");
    
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
    
    // Tenant ID for multi-tenant scenarios
    private readonly string _tenantId;
    
    public DistributedEntityCache(IDistributedCache cache, ILogger<DistributedEntityCache> logger)
    {
        _cache = cache;
        _logger = logger;
        _startTime = DateTime.UtcNow;
        _tenantId = Environment.GetEnvironmentVariable("TENANT_ID") ?? "default";
        
        // Configure default cache options
        _defaultOptions = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
    }
    
    // User caching
    public async Task<User?> GetUserAsync(int userId)
    {
        using var activity = _activitySource.StartActivity("GetUser");
        activity?.SetTag("user.id", userId);
        activity?.SetTag("tenant.id", _tenantId);
        
        var key = GetTenantKey($"{USER_PREFIX}{userId}");
        var cachedData = await _cache.GetAsync(key);
        
        if (cachedData != null)
        {
            IncrementHit("User");
            activity?.SetTag("cache.hit", true);
            _logger.LogTrace("Cache hit for user {UserId} in tenant {TenantId}", userId, _tenantId);
            return DeserializeEntity<User>(cachedData);
        }
        
        IncrementMiss("User");
        activity?.SetTag("cache.hit", false);
        _logger.LogTrace("Cache miss for user {UserId} in tenant {TenantId}", userId, _tenantId);
        return null;
    }
    
    public async Task SetUserAsync(User user)
    {
        using var activity = _activitySource.StartActivity("SetUser");
        activity?.SetTag("user.id", user.Id);
        activity?.SetTag("tenant.id", _tenantId);
        
        var key = GetTenantKey($"{USER_PREFIX}{user.Id}");
        var serializedData = SerializeEntity(user);
        await _cache.SetAsync(key, serializedData, _defaultOptions);
        _logger.LogTrace("Cached user {UserId} for tenant {TenantId}", user.Id, _tenantId);
    }
    
    public async Task InvalidateUserAsync(int userId)
    {
        using var activity = _activitySource.StartActivity("InvalidateUser");
        activity?.SetTag("user.id", userId);
        activity?.SetTag("tenant.id", _tenantId);
        
        var key = GetTenantKey($"{USER_PREFIX}{userId}");
        await _cache.RemoveAsync(key);
        
        // Also invalidate related caches
        await InvalidateUserGroupsAsync(userId);
        await InvalidateUserRolesAsync(userId);
        
        _logger.LogTrace("Invalidated cache for user {UserId} in tenant {TenantId}", userId, _tenantId);
    }
    
    // Group caching
    public async Task<Group?> GetGroupAsync(int groupId)
    {
        using var activity = _activitySource.StartActivity("GetGroup");
        activity?.SetTag("group.id", groupId);
        activity?.SetTag("tenant.id", _tenantId);
        
        var key = GetTenantKey($"{GROUP_PREFIX}{groupId}");
        var cachedData = await _cache.GetAsync(key);
        
        if (cachedData != null)
        {
            IncrementHit("Group");
            activity?.SetTag("cache.hit", true);
            return DeserializeEntity<Group>(cachedData);
        }
        
        IncrementMiss("Group");
        activity?.SetTag("cache.hit", false);
        return null;
    }
    
    public async Task SetGroupAsync(Group group)
    {
        using var activity = _activitySource.StartActivity("SetGroup");
        activity?.SetTag("group.id", group.Id);
        activity?.SetTag("tenant.id", _tenantId);
        
        var key = GetTenantKey($"{GROUP_PREFIX}{group.Id}");
        var serializedData = SerializeEntity(group);
        await _cache.SetAsync(key, serializedData, _defaultOptions);
    }
    
    public async Task InvalidateGroupAsync(int groupId)
    {
        using var activity = _activitySource.StartActivity("InvalidateGroup");
        activity?.SetTag("group.id", groupId);
        activity?.SetTag("tenant.id", _tenantId);
        
        var key = GetTenantKey($"{GROUP_PREFIX}{groupId}");
        await _cache.RemoveAsync(key);
    }
    
    // Role caching
    public async Task<Role?> GetRoleAsync(int roleId)
    {
        using var activity = _activitySource.StartActivity("GetRole");
        activity?.SetTag("role.id", roleId);
        activity?.SetTag("tenant.id", _tenantId);
        
        var key = GetTenantKey($"{ROLE_PREFIX}{roleId}");
        var cachedData = await _cache.GetAsync(key);
        
        if (cachedData != null)
        {
            IncrementHit("Role");
            activity?.SetTag("cache.hit", true);
            return DeserializeEntity<Role>(cachedData);
        }
        
        IncrementMiss("Role");
        activity?.SetTag("cache.hit", false);
        return null;
    }
    
    public async Task SetRoleAsync(Role role)
    {
        using var activity = _activitySource.StartActivity("SetRole");
        activity?.SetTag("role.id", role.Id);
        activity?.SetTag("tenant.id", _tenantId);
        
        var key = GetTenantKey($"{ROLE_PREFIX}{role.Id}");
        var serializedData = SerializeEntity(role);
        await _cache.SetAsync(key, serializedData, _defaultOptions);
    }
    
    public async Task InvalidateRoleAsync(int roleId)
    {
        using var activity = _activitySource.StartActivity("InvalidateRole");
        activity?.SetTag("role.id", roleId);
        activity?.SetTag("tenant.id", _tenantId);
        
        var key = GetTenantKey($"{ROLE_PREFIX}{roleId}");
        await _cache.RemoveAsync(key);
    }
    
    // Permission caching
    public async Task<List<Permission>?> GetEntityPermissionsAsync(int entityId)
    {
        var key = GetTenantKey($"{PERMISSIONS_PREFIX}{entityId}");
        var cachedData = await _cache.GetAsync(key);
        
        if (cachedData != null)
        {
            IncrementHit("Permissions");
            return DeserializePermissions(cachedData);
        }
        
        IncrementMiss("Permissions");
        return null;
    }
    
    public async Task SetEntityPermissionsAsync(int entityId, List<Permission> permissions)
    {
        var key = GetTenantKey($"{PERMISSIONS_PREFIX}{entityId}");
        var serializedData = SerializePermissions(permissions);
        
        // Permissions might change more frequently, use shorter expiration
        var options = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(2),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };
        
        await _cache.SetAsync(key, serializedData, options);
    }
    
    public async Task InvalidateEntityPermissionsAsync(int entityId)
    {
        var key = GetTenantKey($"{PERMISSIONS_PREFIX}{entityId}");
        await _cache.RemoveAsync(key);
    }
    
    // Relationship caching
    public async Task<List<int>?> GetUserGroupsAsync(int userId)
    {
        var key = GetTenantKey($"{USER_GROUPS_PREFIX}{userId}");
        var cachedData = await _cache.GetAsync(key);
        
        if (cachedData != null)
        {
            IncrementHit("UserGroups");
            return DeserializeIdList(cachedData);
        }
        
        IncrementMiss("UserGroups");
        return null;
    }
    
    public async Task SetUserGroupsAsync(int userId, List<int> groupIds)
    {
        var key = GetTenantKey($"{USER_GROUPS_PREFIX}{userId}");
        var serializedData = SerializeIdList(groupIds);
        await _cache.SetAsync(key, serializedData, _defaultOptions);
    }
    
    public async Task InvalidateUserGroupsAsync(int userId)
    {
        var key = GetTenantKey($"{USER_GROUPS_PREFIX}{userId}");
        await _cache.RemoveAsync(key);
    }
    
    public async Task<List<int>?> GetUserRolesAsync(int userId)
    {
        var key = GetTenantKey($"{USER_ROLES_PREFIX}{userId}");
        var cachedData = await _cache.GetAsync(key);
        
        if (cachedData != null)
        {
            IncrementHit("UserRoles");
            return DeserializeIdList(cachedData);
        }
        
        IncrementMiss("UserRoles");
        return null;
    }
    
    public async Task SetUserRolesAsync(int userId, List<int> roleIds)
    {
        var key = GetTenantKey($"{USER_ROLES_PREFIX}{userId}");
        var serializedData = SerializeIdList(roleIds);
        await _cache.SetAsync(key, serializedData, _defaultOptions);
    }
    
    public async Task InvalidateUserRolesAsync(int userId)
    {
        var key = GetTenantKey($"{USER_ROLES_PREFIX}{userId}");
        await _cache.RemoveAsync(key);
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
        
        // Distributed cache doesn't expose item count or memory usage directly
        stats.ItemCount = -1;
        stats.MemoryUsageBytes = -1;
        
        return Task.FromResult(stats);
    }
    
    public Task ClearAsync()
    {
        using var activity = _activitySource.StartActivity("ClearCache");
        activity?.SetTag("tenant.id", _tenantId);
        
        // Note: Most distributed caches don't support clearing all keys efficiently
        // This would need custom implementation based on the specific cache provider
        _logger.LogInformation("Cache clear requested for tenant {TenantId} (limited support)", _tenantId);
        
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
        activity?.SetTag("tenant.id", _tenantId);
        
        // In production, this would preload frequently accessed entities
        // For distributed cache, this is less critical as multiple processes share the cache
        _logger.LogInformation("Cache warmup requested for tenant {TenantId}", _tenantId);
        return Task.CompletedTask;
    }
    
    // Helper methods
    private string GetTenantKey(string key)
    {
        return $"{_tenantId}:{key}";
    }
    
    private byte[] SerializeEntity<T>(T entity) where T : Entity
    {
        // Simple JSON serialization for entities
        // In production, consider using more efficient serialization like protobuf
        var json = JsonSerializer.Serialize(entity, new JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        });
        return System.Text.Encoding.UTF8.GetBytes(json);
    }
    
    private T? DeserializeEntity<T>(byte[] data) where T : Entity
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize cached entity");
            return null;
        }
    }
    
    private byte[] SerializePermissions(List<Permission> permissions)
    {
        var json = JsonSerializer.Serialize(permissions);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }
    
    private List<Permission>? DeserializePermissions(byte[] data)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<List<Permission>>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize cached permissions");
            return null;
        }
    }
    
    private byte[] SerializeIdList(List<int> ids)
    {
        var json = JsonSerializer.Serialize(ids);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }
    
    private List<int>? DeserializeIdList(byte[] data)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<List<int>>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize cached ID list");
            return null;
        }
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