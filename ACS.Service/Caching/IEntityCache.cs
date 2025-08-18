using ACS.Service.Domain;

namespace ACS.Service.Caching;

/// <summary>
/// Interface for entity caching operations
/// Separation of Concerns: Pure caching contract, no implementation details
/// </summary>
public interface IEntityCache
{
    // User caching operations
    Task<User?> GetUserAsync(int userId);
    Task SetUserAsync(User user);
    Task InvalidateUserAsync(int userId);
    
    // Group caching operations
    Task<Group?> GetGroupAsync(int groupId);
    Task SetGroupAsync(Group group);
    Task InvalidateGroupAsync(int groupId);
    
    // Role caching operations
    Task<Role?> GetRoleAsync(int roleId);
    Task SetRoleAsync(Role role);
    Task InvalidateRoleAsync(int roleId);
    
    // Permission caching operations
    Task<List<Permission>?> GetEntityPermissionsAsync(int entityId);
    Task SetEntityPermissionsAsync(int entityId, List<Permission> permissions);
    Task InvalidateEntityPermissionsAsync(int entityId);
    
    // Relationship caching operations
    Task<List<int>?> GetUserGroupsAsync(int userId);
    Task SetUserGroupsAsync(int userId, List<int> groupIds);
    Task InvalidateUserGroupsAsync(int userId);
    
    Task<List<int>?> GetUserRolesAsync(int userId);
    Task SetUserRolesAsync(int userId, List<int> roleIds);
    Task InvalidateUserRolesAsync(int userId);
    
    // Cache statistics and management
    Task<CacheStatistics> GetStatisticsAsync();
    Task ClearAsync();
    Task WarmupAsync();
}

public class CacheStatistics
{
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public double HitRate => TotalHits + TotalMisses > 0 ? TotalHits / (double)(TotalHits + TotalMisses) : 0;
    public long ItemCount { get; set; }
    public long MemoryUsageBytes { get; set; }
    public DateTime LastResetTime { get; set; }
    public Dictionary<string, long> HitsByType { get; set; } = new();
    public Dictionary<string, long> MissesByType { get; set; } = new();
}