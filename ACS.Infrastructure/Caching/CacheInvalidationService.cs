using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Production-ready cache invalidation service with cross-cache consistency,
/// batch processing, and reliable delivery patterns
/// </summary>
public class CacheInvalidationService : ICacheInvalidationService, IHostedService
{
    private readonly IThreeLevelCache _cache;
    private readonly ILogger<CacheInvalidationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource = new("ACS.CacheInvalidation");
    
    // Channel for processing invalidation events
    private readonly Channel<CacheInvalidationEvent> _invalidationChannel;
    private readonly ChannelReader<CacheInvalidationEvent> _reader;
    private readonly ChannelWriter<CacheInvalidationEvent> _writer;
    
    // Background processing
    private Task? _processingTask;
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    
    // Batch processing configuration
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;
    private readonly int _maxConcurrency;
    
    // Retry and reliability patterns
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _retryDelay;
    private readonly ConcurrentQueue<CacheInvalidationEvent> _deadLetterQueue = new();
    
    // Statistics and monitoring
    private readonly ConcurrentDictionary<string, long> _metrics = new();
    private readonly Timer _metricsTimer;
    
    // Dependency tracking for cascade invalidation
    private readonly ConcurrentDictionary<string, HashSet<string>> _dependencyGraph = new();
    
    public CacheInvalidationService(
        IThreeLevelCache cache,
        IConfiguration configuration,
        ILogger<CacheInvalidationService> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
        
        // Configure batch processing
        _batchSize = configuration.GetValue<int>("CacheInvalidation:BatchSize", 100);
        _batchTimeout = TimeSpan.FromMilliseconds(configuration.GetValue<int>("CacheInvalidation:BatchTimeoutMs", 1000));
        _maxConcurrency = configuration.GetValue<int>("CacheInvalidation:MaxConcurrency", Environment.ProcessorCount);
        
        // Configure retry behavior
        _maxRetryAttempts = configuration.GetValue<int>("CacheInvalidation:MaxRetryAttempts", 3);
        _retryDelay = TimeSpan.FromMilliseconds(configuration.GetValue<int>("CacheInvalidation:RetryDelayMs", 500));
        
        // Create bounded channel for backpressure control
        var channelOptions = new BoundedChannelOptions(configuration.GetValue<int>("CacheInvalidation:ChannelCapacity", 10000))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        
        _invalidationChannel = Channel.CreateBounded<CacheInvalidationEvent>(channelOptions);
        _reader = _invalidationChannel.Reader;
        _writer = _invalidationChannel.Writer;
        
        // Start metrics collection
        _metricsTimer = new Timer(LogMetrics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        
        _logger.LogInformation("Initialized cache invalidation service with batch size {BatchSize} and timeout {BatchTimeoutMs}ms",
            _batchSize, _batchTimeout.TotalMilliseconds);
    }

    public async Task InvalidateAsync(string key, CacheType cacheType, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var invalidationEvent = new CacheInvalidationEvent
        {
            Key = key,
            Type = cacheType,
            TenantId = tenantId ?? string.Empty,
            Timestamp = DateTime.UtcNow,
            Source = "Direct",
            DependentKeys = GetDependentKeys(key, cacheType)
        };

        await InvalidateAsync(invalidationEvent, cancellationToken);
    }

    public async Task InvalidateAsync(CacheInvalidationEvent invalidationEvent, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("InvalidateAsync");
        activity?.SetTag("cache.invalidation_key", invalidationEvent.Key);
        activity?.SetTag("cache.invalidation_type", invalidationEvent.Type.ToString());
        activity?.SetTag("cache.tenant_id", invalidationEvent.TenantId);

        try
        {
            // Add to processing channel
            await _writer.WriteAsync(invalidationEvent, cancellationToken).ConfigureAwait(false);
            
            IncrementMetric("invalidation_queued");
            _logger.LogTrace("Queued cache invalidation for key {Key}", invalidationEvent.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing cache invalidation for key {Key}", invalidationEvent.Key);
            activity?.SetTag("cache.error", ex.Message);
            IncrementMetric("invalidation_queue_error");
            
            // Try to add to dead letter queue
            _deadLetterQueue.Enqueue(invalidationEvent);
            IncrementMetric("dead_letter_queued");
        }
    }

    public async Task InvalidateManyAsync(IEnumerable<CacheInvalidationEvent> invalidationEvents, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("InvalidateManyAsync");
        var eventList = invalidationEvents.ToList();
        activity?.SetTag("cache.invalidation_count", eventList.Count);

        try
        {
            var tasks = eventList.Select(async evt =>
            {
                try
                {
                    await _writer.WriteAsync(evt, cancellationToken).ConfigureAwait(false);
                    IncrementMetric("invalidation_queued");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error queuing cache invalidation for key {Key}", evt.Key);
                    _deadLetterQueue.Enqueue(evt);
                    IncrementMetric("dead_letter_queued");
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            _logger.LogTrace("Queued {Count} cache invalidations", eventList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing multiple cache invalidations");
            activity?.SetTag("cache.error", ex.Message);
            IncrementMetric("invalidation_batch_error");
        }
    }

    public async Task InvalidateByPatternAsync(string pattern, CacheType cacheType, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("InvalidateByPatternAsync");
        activity?.SetTag("cache.pattern", pattern);
        activity?.SetTag("cache.cache_type", cacheType.ToString());
        activity?.SetTag("cache.tenant_id", tenantId);

        try
        {
            // Create a pattern-based invalidation event
            var invalidationEvent = new CacheInvalidationEvent
            {
                Key = pattern,
                Type = cacheType,
                TenantId = tenantId ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Source = "Pattern",
                DependentKeys = new[] { pattern + "*" } // Mark as pattern
            };

            await InvalidateAsync(invalidationEvent, cancellationToken);
            _logger.LogTrace("Queued pattern cache invalidation for pattern {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache by pattern {Pattern}", pattern);
            activity?.SetTag("cache.error", ex.Message);
            IncrementMetric("pattern_invalidation_error");
        }
    }

    public void RegisterDependency(string key, string dependentKey)
    {
        _dependencyGraph.AddOrUpdate(key,
            new HashSet<string> { dependentKey },
            (_, existing) =>
            {
                existing.Add(dependentKey);
                return existing;
            });
            
        _logger.LogTrace("Registered cache dependency: {Key} -> {DependentKey}", key, dependentKey);
        IncrementMetric("dependency_registered");
    }

    public void UnregisterDependency(string key, string dependentKey)
    {
        if (_dependencyGraph.TryGetValue(key, out var dependencies))
        {
            dependencies.Remove(dependentKey);
            if (dependencies.Count == 0)
            {
                _dependencyGraph.TryRemove(key, out _);
            }
        }
        
        _logger.LogTrace("Unregistered cache dependency: {Key} -> {DependentKey}", key, dependentKey);
        IncrementMetric("dependency_unregistered");
    }

    public Task<Dictionary<string, long>> GetStatisticsAsync()
    {
        var stats = _metrics.ToDictionary(kv => kv.Key, kv => kv.Value);
        
        // Add queue statistics
        stats["queue_count"] = _reader.CanCount ? _reader.Count : -1;
        stats["dead_letter_count"] = _deadLetterQueue.Count;
        stats["dependency_graph_size"] = _dependencyGraph.Count;
        
        return Task.FromResult(stats);
    }

    public async Task ProcessDeadLetterQueueAsync(CancellationToken cancellationToken = default)
    {
        var processed = 0;
        var maxProcess = Math.Min(100, _deadLetterQueue.Count); // Process up to 100 at a time
        
        for (int i = 0; i < maxProcess && _deadLetterQueue.TryDequeue(out var invalidationEvent); i++)
        {
            try
            {
                await _writer.WriteAsync(invalidationEvent, cancellationToken).ConfigureAwait(false);
                processed++;
                IncrementMetric("dead_letter_recovered");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to requeue dead letter cache invalidation for key {Key}", invalidationEvent.Key);
                // Put it back in the dead letter queue
                _deadLetterQueue.Enqueue(invalidationEvent);
                break;
            }
        }
        
        if (processed > 0)
        {
            _logger.LogInformation("Processed {Count} dead letter cache invalidations", processed);
        }
    }

    // IHostedService implementation
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _processingTask = ProcessInvalidationEventsAsync(_shutdownTokenSource.Token);
        _logger.LogInformation("Started cache invalidation service");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping cache invalidation service...");
        
        // Signal shutdown
        _writer.Complete();
        _shutdownTokenSource.Cancel();
        
        // Wait for processing to complete
        if (_processingTask != null)
        {
            await _processingTask;
        }
        
        _logger.LogInformation("Cache invalidation service stopped");
    }

    private async Task ProcessInvalidationEventsAsync(CancellationToken cancellationToken)
    {
        await foreach (var batch in ReadBatchesAsync(cancellationToken))
        {
            if (batch.Count == 0) continue;
            
            using var activity = _activitySource.StartActivity("ProcessBatch");
            activity?.SetTag("cache.batch_size", batch.Count);
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Process batch with controlled concurrency
                var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
                
                var tasks = batch.Select(async invalidationEvent =>
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await ProcessSingleInvalidationAsync(invalidationEvent, cancellationToken);
                        IncrementMetric("invalidation_processed");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                
                await Task.WhenAll(tasks).ConfigureAwait(false);
                
                stopwatch.Stop();
                activity?.SetTag("cache.processing_time_ms", stopwatch.ElapsedMilliseconds);
                
                IncrementMetric("batch_processed");
                RecordBatchProcessingTime(stopwatch.ElapsedMilliseconds);
                
                _logger.LogTrace("Processed batch of {Count} cache invalidations in {ElapsedMs}ms",
                    batch.Count, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cache invalidation batch");
                activity?.SetTag("cache.error", ex.Message);
                IncrementMetric("batch_error");
                
                // Requeue failed items
                foreach (var item in batch)
                {
                    _deadLetterQueue.Enqueue(item);
                    IncrementMetric("dead_letter_queued");
                }
            }
        }
    }

    private async IAsyncEnumerable<List<CacheInvalidationEvent>> ReadBatchesAsync(CancellationToken cancellationToken)
    {
        var batch = new List<CacheInvalidationEvent>();
        var batchTimer = Stopwatch.StartNew();
        
        await foreach (var item in _reader.ReadAllAsync(cancellationToken))
        {
            batch.Add(item);
            
            // Yield batch if we hit size limit or timeout
            if (batch.Count >= _batchSize || batchTimer.Elapsed >= _batchTimeout)
            {
                if (batch.Count > 0)
                {
                    yield return new List<CacheInvalidationEvent>(batch);
                    batch.Clear();
                }
                batchTimer.Restart();
            }
        }
        
        // Yield any remaining items
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private async Task ProcessSingleInvalidationAsync(CacheInvalidationEvent invalidationEvent, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        
        while (retryCount <= _maxRetryAttempts)
        {
            try
            {
                using var activity = _activitySource.StartActivity("ProcessSingleInvalidation");
                activity?.SetTag("cache.key", invalidationEvent.Key);
                activity?.SetTag("cache.retry_count", retryCount);
                
                // Expand dependencies using the dependency graph
                var expandedEvent = ExpandDependencies(invalidationEvent);
                
                // Perform the actual cache invalidation
                await _cache.InvalidateAsync(expandedEvent, cancellationToken).ConfigureAwait(false);
                
                _logger.LogTrace("Successfully invalidated cache for key {Key} (attempt {Attempt})",
                    invalidationEvent.Key, retryCount + 1);
                
                if (retryCount > 0)
                {
                    IncrementMetric($"retry_success_{retryCount}");
                }
                
                return; // Success
            }
            catch (Exception ex)
            {
                retryCount++;
                IncrementMetric($"retry_attempt_{retryCount}");
                
                if (retryCount > _maxRetryAttempts)
                {
                    _logger.LogError(ex, "Failed to invalidate cache for key {Key} after {MaxAttempts} attempts",
                        invalidationEvent.Key, _maxRetryAttempts);
                    IncrementMetric("invalidation_failed");
                    
                    // Add to dead letter queue for manual processing
                    _deadLetterQueue.Enqueue(invalidationEvent);
                    IncrementMetric("dead_letter_queued");
                    return;
                }
                
                _logger.LogDebug(ex, "Cache invalidation failed for key {Key}, attempt {Attempt}/{MaxAttempts}",
                    invalidationEvent.Key, retryCount, _maxRetryAttempts);
                
                // Exponential backoff
                var delay = TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * Math.Pow(2, retryCount - 1));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private CacheInvalidationEvent ExpandDependencies(CacheInvalidationEvent originalEvent)
    {
        var allDependentKeys = new HashSet<string>(originalEvent.DependentKeys);
        
        // Add dependencies from the dependency graph
        foreach (var dependentKey in originalEvent.DependentKeys)
        {
            if (_dependencyGraph.TryGetValue(dependentKey, out var transitiveDependencies))
            {
                foreach (var transitiveDep in transitiveDependencies)
                {
                    allDependentKeys.Add(transitiveDep);
                }
            }
        }
        
        // Add dependencies for the primary key
        if (_dependencyGraph.TryGetValue(originalEvent.Key, out var primaryDependencies))
        {
            foreach (var primaryDep in primaryDependencies)
            {
                allDependentKeys.Add(primaryDep);
            }
        }
        
        return new CacheInvalidationEvent
        {
            Key = originalEvent.Key,
            Type = originalEvent.Type,
            TenantId = originalEvent.TenantId,
            Timestamp = originalEvent.Timestamp,
            Source = originalEvent.Source,
            DependentKeys = allDependentKeys.ToArray()
        };
    }

    private string[] GetDependentKeys(string key, CacheType cacheType)
    {
        var dependencies = new List<string>();
        
        // Add type-specific dependency patterns
        switch (cacheType)
        {
            case CacheType.User:
                dependencies.Add($"user_groups:{ExtractIdFromKey(key)}");
                dependencies.Add($"user_roles:{ExtractIdFromKey(key)}");
                dependencies.Add($"permission_eval:{ExtractIdFromKey(key)}*");
                break;
                
            case CacheType.Group:
                dependencies.Add($"user_groups*"); // All user-group relationships
                dependencies.Add($"permission_eval*"); // All permission evaluations
                break;
                
            case CacheType.Role:
                dependencies.Add($"user_roles*"); // All user-role relationships
                dependencies.Add($"permission_eval*"); // All permission evaluations
                break;
                
            case CacheType.Permission:
                dependencies.Add($"permission_eval*"); // All permission evaluations
                break;
        }
        
        return dependencies.ToArray();
    }

    private string ExtractIdFromKey(string key)
    {
        var parts = key.Split(':');
        return parts.Length > 1 ? parts[1] : key;
    }

    private void IncrementMetric(string metric, long increment = 1)
    {
        _metrics.AddOrUpdate(metric, increment, (key, value) => value + increment);
    }

    private void RecordBatchProcessingTime(long milliseconds)
    {
        IncrementMetric("batch_total_time_ms", milliseconds);
        IncrementMetric("batch_count");
    }

    private void LogMetrics(object? state)
    {
        try
        {
            if (_metrics.IsEmpty) return;
            
            var metrics = _metrics.ToDictionary(kv => kv.Key, kv => kv.Value);
            _logger.LogDebug("Cache invalidation metrics: {@Metrics}", metrics);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error logging cache invalidation metrics");
        }
    }


    // Additional interface methods required by ICacheInvalidationService

    public async Task OnEntityCreatedAsync<T>(T entity, CacheType cacheType, string tenantId, CancellationToken cancellationToken = default)
    {
        await InvalidateAsync($"{tenantId}:{cacheType}:entity_created", cacheType, tenantId, cancellationToken);
        _logger.LogDebug("Processed entity creation cache invalidation for {EntityType} in tenant {TenantId}", typeof(T).Name, tenantId);
    }

    public async Task OnEntityUpdatedAsync<T>(T entity, CacheType cacheType, string tenantId, CancellationToken cancellationToken = default)
    {
        var entityId = ExtractEntityId(entity);
        await InvalidateAsync($"{tenantId}:{cacheType}:{entityId}", cacheType, tenantId, cancellationToken);
        await InvalidateAsync($"{tenantId}:{cacheType}:entity_updated", cacheType, tenantId, cancellationToken);
        _logger.LogDebug("Processed entity update cache invalidation for {EntityType}:{EntityId} in tenant {TenantId}", typeof(T).Name, entityId, tenantId);
    }

    public async Task OnEntityDeletedAsync<T>(T entity, CacheType cacheType, string tenantId, CancellationToken cancellationToken = default)
    {
        var entityId = ExtractEntityId(entity);
        await InvalidateAsync($"{tenantId}:{cacheType}:{entityId}", cacheType, tenantId, cancellationToken);
        await InvalidateAsync($"{tenantId}:{cacheType}:entity_deleted", cacheType, tenantId, cancellationToken);
        _logger.LogDebug("Processed entity deletion cache invalidation for {EntityType}:{EntityId} in tenant {TenantId}", typeof(T).Name, entityId, tenantId);
    }

    public async Task OnRelationshipCreatedAsync(string relationshipType, object sourceEntity, object targetEntity, string tenantId, CancellationToken cancellationToken = default)
    {
        var sourceId = ExtractEntityId(sourceEntity);
        var targetId = ExtractEntityId(targetEntity);
        
        await InvalidateAsync($"{tenantId}:relationship:{relationshipType}:{sourceId}:{targetId}", CacheType.Permission, tenantId, cancellationToken).ConfigureAwait(false);
        await InvalidateAsync($"{tenantId}:relationships:created", CacheType.Permission, tenantId, cancellationToken);
        
        _logger.LogDebug("Processed relationship creation cache invalidation for {RelationshipType} between {SourceId} and {TargetId} in tenant {TenantId}", 
            relationshipType, sourceId, targetId, tenantId);
    }

    public async Task OnRelationshipRemovedAsync(string relationshipType, object sourceEntity, object targetEntity, string tenantId, CancellationToken cancellationToken = default)
    {
        var sourceId = ExtractEntityId(sourceEntity);
        var targetId = ExtractEntityId(targetEntity);
        
        await InvalidateAsync($"{tenantId}:relationship:{relationshipType}:{sourceId}:{targetId}", CacheType.Permission, tenantId, cancellationToken).ConfigureAwait(false);
        await InvalidateAsync($"{tenantId}:relationships:removed", CacheType.Permission, tenantId, cancellationToken);
        
        _logger.LogDebug("Processed relationship removal cache invalidation for {RelationshipType} between {SourceId} and {TargetId} in tenant {TenantId}", 
            relationshipType, sourceId, targetId, tenantId);
    }

    public async Task InvalidateByPatternAsync(string pattern, string tenantId, CancellationToken cancellationToken = default)
    {
        await InvalidateByPatternAsync(pattern, CacheType.All, tenantId, cancellationToken);
    }

    public async Task ClearTenantCacheAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing all cache for tenant {TenantId}", tenantId);
        await InvalidateByPatternAsync($"{tenantId}:*", CacheType.All, tenantId, cancellationToken);
    }

    public async Task PublishInvalidationEventAsync(CacheInvalidationEvent invalidationEvent, CancellationToken cancellationToken = default)
    {
        await InvalidateAsync(invalidationEvent, cancellationToken);
        _logger.LogDebug("Published and processed cache invalidation event for key {Key} in tenant {TenantId}", 
            invalidationEvent.Key, invalidationEvent.TenantId);
    }

    private string ExtractEntityId(object entity)
    {
        if (entity == null) return "unknown";

        var idProperty = entity.GetType().GetProperty("Id");
        if (idProperty != null)
        {
            var id = idProperty.GetValue(entity);
            return id?.ToString() ?? "unknown";
        }

        return entity.GetHashCode().ToString();
    }

    public void Dispose()
    {
        _metricsTimer?.Dispose();
        _shutdownTokenSource?.Dispose();
        _activitySource?.Dispose();
    }
}
