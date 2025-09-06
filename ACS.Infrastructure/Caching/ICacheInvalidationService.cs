namespace ACS.Infrastructure.Caching;

/// <summary>
/// Service for handling cache invalidation events and dependencies
/// </summary>
public interface ICacheInvalidationService
{
    /// <summary>
    /// Invalidates cache entries when an entity is created
    /// </summary>
    Task OnEntityCreatedAsync<T>(T entity, CacheType cacheType, string tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Invalidates cache entries when an entity is updated
    /// </summary>
    Task OnEntityUpdatedAsync<T>(T entity, CacheType cacheType, string tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Invalidates cache entries when an entity is deleted
    /// </summary>
    Task OnEntityDeletedAsync<T>(T entity, CacheType cacheType, string tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Invalidates cache entries when a relationship is created
    /// </summary>
    Task OnRelationshipCreatedAsync(string relationshipType, object sourceEntity, object targetEntity, string tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Invalidates cache entries when a relationship is removed
    /// </summary>
    Task OnRelationshipRemovedAsync(string relationshipType, object sourceEntity, object targetEntity, string tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Manually invalidates cache entries by pattern
    /// </summary>
    Task InvalidateByPatternAsync(string pattern, string tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clears all cache entries for a tenant
    /// </summary>
    Task ClearTenantCacheAsync(string tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Publishes cache invalidation event to other processes/servers
    /// </summary>
    Task PublishInvalidationEventAsync(CacheInvalidationEvent invalidationEvent, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets cache invalidation statistics
    /// </summary>
    Task<Dictionary<string, long>> GetStatisticsAsync();
}