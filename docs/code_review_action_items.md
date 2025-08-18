# Code Review Action Items - Vertical Architecture

## 🔴 Critical (Do Immediately)

### 1. Fix Thread-Safe ID Generation
**File**: `AccessControlDomainService.cs`
**Current Code**:
```csharp
var newId = _entityGraph.Users.Keys.Any() ? _entityGraph.Users.Keys.Max() + 1 : 1;
```
**Proposed Fix**:
```csharp
private int _nextUserId = 0;
private int _nextGroupId = 0;
private int _nextRoleId = 0;

private int GetNextUserId() => Interlocked.Increment(ref _nextUserId);
private int GetNextGroupId() => Interlocked.Increment(ref _nextGroupId);
private int GetNextRoleId() => Interlocked.Increment(ref _nextRoleId);
```

### 2. Add Port Range Management
**File**: `TenantProcessManager.cs`
**Proposed Fix**:
```csharp
private const int MIN_PORT = 5001;
private const int MAX_PORT = 5100;
private readonly HashSet<int> _usedPorts = new();

private int AllocatePort()
{
    lock (_lock)
    {
        for (int port = MIN_PORT; port <= MAX_PORT; port++)
        {
            if (!_usedPorts.Contains(port))
            {
                _usedPorts.Add(port);
                return port;
            }
        }
        throw new InvalidOperationException("No available ports");
    }
}

private void ReleasePort(int port)
{
    lock (_lock)
    {
        _usedPorts.Remove(port);
    }
}
```

### 3. Add Database Retry Logic
**Package**: Install `Polly` NuGet package
**Implementation**:
```csharp
private readonly IAsyncPolicy _retryPolicy = Policy
    .Handle<DbUpdateException>()
    .Or<TimeoutException>()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// Usage:
await _retryPolicy.ExecuteAsync(async () => 
    await _persistenceService.PersistCreateUserAsync(...));
```

## 🟡 Medium Priority

### 4. Fix Resource Disposal
```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    
    // Copy to avoid modification during iteration
    var tenantIds = _processes.Keys.ToList();
    
    Parallel.ForEach(tenantIds, tenantId =>
    {
        try
        {
            StopTenantProcessInternal(tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop process for {TenantId}", tenantId);
        }
    });
}
```

### 5. Optimize RefreshNormalizerCollections
```csharp
private void RefreshNormalizerCollectionsForEntity(Entity entity)
{
    // Only update collections that could be affected by this entity
    if (entity is User)
    {
        var usersList = _entityGraph.Users.Values.ToList();
        UpdateUserNormalizers(usersList);
    }
    else if (entity is Group)
    {
        var groupsList = _entityGraph.Groups.Values.ToList();
        UpdateGroupNormalizers(groupsList);
    }
    // etc...
}
```

## 🟢 Nice to Have

### 6. Add Configuration
**File**: `appsettings.json`
```json
{
  "VerticalHost": {
    "MinPort": 5001,
    "MaxPort": 5100,
    "HealthCheckTimeoutSeconds": 30,
    "MaxRetries": 3,
    "ProcessStartTimeoutSeconds": 60
  }
}
```

### 7. Add Metrics Collection
```csharp
public class ProcessMetrics
{
    public int TotalProcessesStarted { get; set; }
    public int TotalProcessesStopped { get; set; }
    public int CurrentActiveProcesses { get; set; }
    public Dictionary<string, long> CommandCounts { get; set; }
    public Dictionary<string, double> AverageResponseTimes { get; set; }
}
```

### 8. Improve Error Responses
```csharp
public class DomainError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string Details { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

## 🧪 Testing Improvements

### 9. Add Integration Tests
```csharp
[TestClass]
public class VerticalIntegrationTests
{
    [TestMethod]
    public async Task ProcessManager_HandlesProcessCrash()
    {
        // Start process
        // Kill process externally
        // Verify auto-recovery
    }
    
    [TestMethod]
    public async Task DomainService_HandlesConcurrentCommands()
    {
        // Execute multiple commands in parallel
        // Verify no ID collisions
    }
}
```

### 10. Add Performance Tests
```csharp
[TestClass]
public class PerformanceTests
{
    [TestMethod]
    public async Task MeasureGrpcOverhead()
    {
        // Compare direct vs gRPC call times
        // Assert overhead < 10ms
    }
}
```

## 📈 Monitoring & Observability

### 11. Add Health Checks
```csharp
public class VerticalHostHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Check all tenant processes
        // Return Degraded if any unhealthy
        // Return Unhealthy if > 50% unhealthy
    }
}
```

### 12. Add OpenTelemetry
```csharp
services.AddOpenTelemetryTracing(builder =>
{
    builder
        .AddSource("ACS.VerticalHost")
        .AddGrpcClientInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation();
});
```

## Implementation Priority

1. **Week 1**: Critical items 1-3
2. **Week 2**: Medium priority items 4-5
3. **Week 3**: Testing improvements 9-10
4. **Week 4**: Monitoring and remaining items

## Success Metrics

- [ ] Zero ID collision errors in 24 hours of testing
- [ ] Process recovery time < 5 seconds
- [ ] 99.9% command success rate under load
- [ ] gRPC overhead < 10ms p99
- [ ] All critical issues resolved
- [ ] 80%+ code coverage on new code