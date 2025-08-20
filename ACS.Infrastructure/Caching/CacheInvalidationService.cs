using ACS.Service.Domain;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Implementation of cache invalidation service with smart dependency tracking
/// </summary>
public class CacheInvalidationService : ICacheInvalidationService
{
    private readonly IMultiLevelCache _cache;
    private readonly ICacheStrategy _cacheStrategy;
    private readonly ILogger<CacheInvalidationService> _logger;
    private readonly ActivitySource _activitySource = new("ACS.CacheInvalidation");

    public CacheInvalidationService(
        IMultiLevelCache cache,
        ICacheStrategy cacheStrategy,
        ILogger<CacheInvalidationService> logger)
    {
        _cache = cache;
        _cacheStrategy = cacheStrategy;
        _logger = logger;
    }

    public async Task OnEntityCreatedAsync<T>(T entity, CacheType cacheType, string tenantId, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("OnEntityCreatedAsync");
        activity?.SetTag("cache.entity_type", typeof(T).Name);
        activity?.SetTag("cache.cache_type", cacheType.ToString());
        activity?.SetTag("cache.tenant_id", tenantId);

        var invalidationEvent = CreateInvalidationEvent(entity, cacheType, tenantId, "EntityCreated");
        
        // For creation, mainly invalidate list/search caches
        invalidationEvent.DependentKeys = GetListInvalidationKeys(cacheType, tenantId);
        
        await _cache.InvalidateAsync(invalidationEvent, cancellationToken);
        
        _logger.LogDebug("Processed cache invalidation for entity creation: {EntityType}", typeof(T).Name);
    }

    public async Task OnEntityUpdatedAsync<T>(T entity, CacheType cacheType, string tenantId, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("OnEntityUpdatedAsync");
        activity?.SetTag("cache.entity_type", typeof(T).Name);
        activity?.SetTag("cache.cache_type", cacheType.ToString());
        activity?.SetTag("cache.tenant_id", tenantId);

        var invalidationEvent = CreateInvalidationEvent(entity, cacheType, tenantId, "EntityUpdated");
        
        // For updates, invalidate entity cache and related dependencies
        var dependencies = _cacheStrategy.GetInvalidationKeys(cacheType, entity, GetEntityKey(entity));
        invalidationEvent.DependentKeys = dependencies.Concat(GetListInvalidationKeys(cacheType, tenantId)).ToArray();
        
        await _cache.InvalidateAsync(invalidationEvent, cancellationToken);
        
        _logger.LogDebug("Processed cache invalidation for entity update: {EntityType}", typeof(T).Name);
    }

    public async Task OnEntityDeletedAsync<T>(T entity, CacheType cacheType, string tenantId, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("OnEntityDeletedAsync");
        activity?.SetTag("cache.entity_type", typeof(T).Name);
        activity?.SetTag("cache.cache_type", cacheType.ToString());
        activity?.SetTag("cache.tenant_id", tenantId);

        var invalidationEvent = CreateInvalidationEvent(entity, cacheType, tenantId, "EntityDeleted");
        
        // For deletion, invalidate all related caches
        var dependencies = _cacheStrategy.GetInvalidationKeys(cacheType, entity, GetEntityKey(entity));
        invalidationEvent.DependentKeys = dependencies.Concat(GetListInvalidationKeys(cacheType, tenantId)).ToArray();
        
        await _cache.InvalidateAsync(invalidationEvent, cancellationToken);
        
        _logger.LogDebug("Processed cache invalidation for entity deletion: {EntityType}", typeof(T).Name);
    }

    public async Task OnRelationshipCreatedAsync(string relationshipType, object sourceEntity, object targetEntity, string tenantId, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("OnRelationshipCreatedAsync");
        activity?.SetTag("cache.relationship_type", relationshipType);
        activity?.SetTag("cache.tenant_id", tenantId);

        var keysToInvalidate = GetRelationshipInvalidationKeys(relationshipType, sourceEntity, targetEntity, tenantId);
        
        foreach (var key in keysToInvalidate)
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        
        _logger.LogDebug("Processed cache invalidation for relationship creation: {RelationshipType}", relationshipType);
    }

    public async Task OnRelationshipRemovedAsync(string relationshipType, object sourceEntity, object targetEntity, string tenantId, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("OnRelationshipRemovedAsync");
        activity?.SetTag("cache.relationship_type", relationshipType);
        activity?.SetTag("cache.tenant_id", tenantId);

        var keysToInvalidate = GetRelationshipInvalidationKeys(relationshipType, sourceEntity, targetEntity, tenantId);
        
        foreach (var key in keysToInvalidate)
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        
        _logger.LogDebug("Processed cache invalidation for relationship removal: {RelationshipType}", relationshipType);
    }

    public async Task InvalidateByPatternAsync(string pattern, string tenantId, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("InvalidateByPatternAsync");
        activity?.SetTag("cache.pattern", pattern);
        activity?.SetTag("cache.tenant_id", tenantId);

        var tenantPattern = $"{tenantId}:{pattern}";
        await _cache.RemoveByPatternAsync(tenantPattern, cancellationToken);
        
        _logger.LogDebug("Invalidated cache entries by pattern: {Pattern} for tenant: {TenantId}", pattern, tenantId);
    }

    public async Task ClearTenantCacheAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ClearTenantCacheAsync");
        activity?.SetTag("cache.tenant_id", tenantId);

        await _cache.RemoveByPatternAsync($"{tenantId}:", cancellationToken);
        
        _logger.LogInformation("Cleared all cache entries for tenant: {TenantId}", tenantId);
    }

    public async Task PublishInvalidationEventAsync(CacheInvalidationEvent invalidationEvent, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("PublishInvalidationEventAsync");
        activity?.SetTag("cache.invalidation_key", invalidationEvent.Key);
        activity?.SetTag("cache.tenant_id", invalidationEvent.TenantId);

        // In a distributed environment, this would publish to a message bus (Redis, Service Bus, etc.)
        // For now, just log the event for monitoring
        _logger.LogDebug("Published cache invalidation event: {Key} for tenant: {TenantId}", 
            invalidationEvent.Key, invalidationEvent.TenantId);
        
        // Implement message publishing for multi-server cache invalidation
        try 
        {
            // In production, this would publish to Redis Pub/Sub, Service Bus, or similar
            // For now, we simulate the publishing with local event handling
            
            var message = new 
            {
                Type = "CacheInvalidation",
                Timestamp = DateTime.UtcNow,
                Event = invalidationEvent
            };
            
            // This would be: await _messageBus.PublishAsync("cache.invalidation", message);
            _logger.LogInformation("Cache invalidation message published for key: {Key}", invalidationEvent.Key);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish cache invalidation message for key: {Key}", invalidationEvent.Key);
        }
    }

    private CacheInvalidationEvent CreateInvalidationEvent<T>(T entity, CacheType cacheType, string tenantId, string source)
    {
        var entityKey = GetEntityKey(entity);
        
        return new CacheInvalidationEvent
        {
            Key = $"{tenantId}:{cacheType.ToString().ToLowerInvariant()}:{entityKey}",
            Type = cacheType,
            TenantId = tenantId,
            Source = source,
            Timestamp = DateTime.UtcNow
        };
    }

    private string GetEntityKey<T>(T entity)
    {
        // Try to get ID property using reflection
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null)
        {
            var id = idProperty.GetValue(entity);
            return id?.ToString() ?? "unknown";
        }
        
        // Fallback to hash code
        return entity?.GetHashCode().ToString() ?? "unknown";
    }

    private string[] GetListInvalidationKeys(CacheType cacheType, string tenantId)
    {
        var baseType = cacheType.ToString().ToLowerInvariant();
        
        return new[]
        {
            $"{tenantId}:{baseType}s:all",
            $"{tenantId}:{baseType}s:search:*",
            $"{tenantId}:{baseType}s:page:*"
        };
    }

    private string[] GetRelationshipInvalidationKeys(string relationshipType, object sourceEntity, object targetEntity, string tenantId)
    {
        var sourceKey = GetEntityKey(sourceEntity);
        var targetKey = GetEntityKey(targetEntity);
        
        return relationshipType.ToLowerInvariant() switch
        {
            "usergroup" => new[]
            {
                $"{tenantId}:user_groups:{sourceKey}",
                $"{tenantId}:group_users:{targetKey}",
                $"{tenantId}:users:group:{targetKey}",
                $"{tenantId}:permissions:user:{sourceKey}"
            },
            "userrole" => new[]
            {
                $"{tenantId}:user_roles:{sourceKey}",
                $"{tenantId}:role_users:{targetKey}",
                $"{tenantId}:users:role:{targetKey}",
                $"{tenantId}:permissions:user:{sourceKey}"
            },
            "groupgroup" => new[]
            {
                $"{tenantId}:group_children:{sourceKey}",
                $"{tenantId}:group_parents:{targetKey}",
                $"{tenantId}:permissions:group:{sourceKey}",
                $"{tenantId}:permissions:group:{targetKey}"
            },
            _ => Array.Empty<string>()
        };
    }
}