# ACS Caching Infrastructure

## Overview

This document describes the comprehensive caching infrastructure implemented for the ACS (Access Control System) application. The implementation provides a production-ready, multi-level caching system with resilience patterns, performance monitoring, and intelligent cache management.

## Architecture

### Three-Level Cache Architecture

The caching system implements a sophisticated three-level hierarchy:

1. **L1 Cache (Memory)**: In-memory cache using `Microsoft.Extensions.Caching.Memory`
   - Fastest access times (microseconds)
   - Limited by application memory
   - Automatically evicted under memory pressure

2. **L2 Cache (Redis)**: Distributed Redis cache
   - Millisecond access times
   - Scalable across multiple application instances
   - Persistent storage with configurable expiration

3. **L3 Cache (SQL Server)**: SQL Server distributed cache fallback
   - Slower but highly reliable
   - Uses existing SQL Server infrastructure
   - Automatic cleanup of expired entries

### Key Components

#### Core Services

- **`RedisCache`**: Production-ready Redis implementation with connection pooling, compression, and circuit breakers
- **`SqlServerCache`**: SQL Server cache implementation with automatic table creation and cleanup
- **`ThreeLevelCache`**: Orchestrates all three cache levels with intelligent promotion/demotion
- **`CacheAsideService`**: Implements cache-aside pattern with cache stampede prevention

#### Advanced Features

- **`CacheInvalidationService`**: Cross-cache consistency with batch processing and dead letter queues
- **`CacheWarmupService`**: Intelligent cache warming with predictive patterns and access analysis
- **`CacheCircuitBreakerService`**: Circuit breaker patterns for graceful degradation
- **`CachePerformanceMonitor`**: Comprehensive monitoring, health checks, and SLA tracking

## Features

### Resilience & Reliability

- **Circuit Breakers**: Automatic failover when cache providers are unavailable
- **Retry Policies**: Exponential backoff for transient failures
- **Health Monitoring**: Continuous health checks with alerting
- **Graceful Degradation**: Application continues to function even with cache failures

### Performance Optimization

- **Connection Pooling**: Efficient connection management for Redis
- **Compression**: Automatic compression for large cache values
- **Batch Processing**: Efficient batch operations for cache invalidation
- **Intelligent Promotion**: Smart promotion of frequently accessed data to faster cache levels

### Monitoring & Observability

- **Performance Metrics**: Detailed metrics for hit rates, response times, and error rates
- **Health Checks**: ASP.NET Core health check integration
- **Distributed Tracing**: OpenTelemetry integration for request tracing
- **SLA Monitoring**: Track and alert on SLA violations

### Cache Management

- **Smart Warming**: Predictive cache warming based on access patterns
- **Automatic Cleanup**: Background cleanup of expired entries
- **Pattern-based Invalidation**: Efficient invalidation using key patterns
- **Dependency Tracking**: Automatic invalidation of dependent cache entries

## Configuration

### Basic Configuration

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ACS_Development;Trusted_Connection=true"
  },
  "Caching": {
    "Strategy": "Auto",
    "EnableL2Fallback": true,
    "EnableL3Fallback": true
  }
}
```

### Advanced Configuration

See `CachingConfiguration.json` for comprehensive configuration options including:
- Circuit breaker thresholds
- Retry policies
- Performance monitoring settings
- Cache warming strategies

## Usage

### Service Registration

The caching services are automatically registered through the `AddAcsCaching` extension method:

```csharp
services.AddAcsCaching(configuration);
```

### Basic Cache Operations

```csharp
public class UserService
{
    private readonly ICacheAsideService _cache;
    
    public UserService(ICacheAsideService cache)
    {
        _cache = cache;
    }
    
    public async Task<User> GetUserAsync(int userId)
    {
        var key = $"user:{userId}";
        return await _cache.GetOrSetAsync(key, 
            () => LoadUserFromDatabase(userId),
            CacheType.User);
    }
}
```

### Cache Invalidation

```csharp
// Invalidate specific key
await _cacheInvalidation.InvalidateAsync("user:123", CacheType.User);

// Invalidate by pattern
await _cacheInvalidation.InvalidateByPatternAsync("user:*", CacheType.User);
```

### Health Monitoring

```csharp
// Get cache health report
var healthReport = await _performanceMonitor.CheckHealthAsync(context);

// Get performance statistics  
var stats = await _threeLevelCache.GetThreeLevelStatisticsAsync();
```

## Implementation Details

### Cache Key Strategies

The system uses hierarchical cache keys for efficient pattern-based operations:

- Users: `user:{userId}`
- Groups: `group:{groupId}`
- Roles: `role:{roleId}`
- Permissions: `permission:{resourceId}:{action}`
- User Groups: `user_groups:{userId}`
- User Roles: `user_roles:{userId}`

### Serialization

Cache values are serialized using `System.Text.Json` with:
- Reference handling for circular dependencies
- Null value ignoring for efficiency
- Camel case property naming

### Compression

Large cache values (>1KB by default) are automatically compressed using GZip:
- Reduces network bandwidth
- Improves storage efficiency
- Transparent to application code

### Circuit Breaker Configuration

Each cache level has configurable circuit breakers:
- **L1 (Memory)**: 5 failures in 60 seconds → 30 second break
- **L2 (Redis)**: 3 failures in 120 seconds → 60 second break  
- **L3 (SQL Server)**: 2 failures in 10 minutes → 5 minute break

## Performance Characteristics

### Typical Response Times
- **L1 Cache**: < 1ms
- **L2 Cache**: 2-5ms (local Redis), 10-20ms (remote Redis)
- **L3 Cache**: 20-50ms (depending on query complexity)

### Throughput
- **L1 Cache**: 100K+ ops/sec
- **L2 Cache**: 10K-50K ops/sec
- **L3 Cache**: 1K-5K ops/sec

### SLA Targets
- **Availability**: 99.9%
- **Cache Hit Rate**: >85%
- **Response Time**: <100ms (P95)

## Deployment Considerations

### Redis Deployment
- Use Redis Cluster for high availability
- Configure appropriate memory limits and eviction policies
- Monitor Redis memory usage and performance

### SQL Server Cache
- Ensure proper indexing on cache table
- Configure automatic cleanup job
- Monitor table size and performance impact

### Application Deployment
- Configure appropriate cache sizes for your workload
- Tune circuit breaker thresholds based on your infrastructure
- Monitor cache performance and adjust strategies as needed

## Troubleshooting

### Common Issues

1. **High Cache Miss Rate**
   - Check cache expiration settings
   - Verify cache warming is working
   - Review access patterns

2. **Circuit Breaker Triggering**
   - Check Redis/SQL Server connectivity
   - Review error logs for root cause
   - Adjust circuit breaker thresholds if needed

3. **Memory Issues**
   - Monitor L1 cache size
   - Adjust eviction policies
   - Consider reducing cache sizes

### Monitoring and Alerting

Key metrics to monitor:
- Cache hit rates by level
- Response times by level
- Error rates and circuit breaker states
- Memory usage and capacity
- Cache invalidation queue depths

## Future Enhancements

- Machine learning-based predictive caching
- Advanced cache partitioning strategies
- Integration with CDN for static content caching
- Distributed cache warming coordination
- Advanced cache analytics and reporting

## Files Overview

| File | Purpose |
|------|---------|
| `RedisCache.cs` | Production Redis cache implementation |
| `SqlServerCache.cs` | SQL Server distributed cache |
| `ThreeLevelCache.cs` | Three-level cache orchestrator |
| `CacheAsideService.cs` | Cache-aside pattern implementation |
| `CacheInvalidationService.cs` | Cross-cache invalidation |
| `CacheWarmupService.cs` | Intelligent cache warming |
| `CacheCircuitBreakerService.cs` | Circuit breaker management |
| `CachePerformanceMonitor.cs` | Performance monitoring and health checks |
| `ICacheStrategy.cs` | Cache strategy interfaces |
| `DefaultCacheStrategy.cs` | Default cache configuration strategy |

This caching infrastructure provides enterprise-grade performance, reliability, and observability for the ACS application while maintaining simplicity for developers.