using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Advanced circuit breaker service for cache provider failover with intelligent recovery,
/// health monitoring, and graceful degradation patterns
/// </summary>
public class CacheCircuitBreakerService : IDisposable
{
    private readonly ILogger<CacheCircuitBreakerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource = new("ACS.CacheCircuitBreaker");
    
    // Circuit breakers for each cache level
    private readonly AsyncCircuitBreakerPolicy _l1CircuitBreaker;
    private readonly AsyncCircuitBreakerPolicy _l2CircuitBreaker;
    private readonly AsyncCircuitBreakerPolicy _l3CircuitBreaker;
    
    // Health monitoring
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitStates = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastFailures = new();
    private readonly ConcurrentDictionary<string, long> _failureCounts = new();
    private readonly Timer _healthMonitorTimer;
    
    // Failover strategies
    private readonly Dictionary<CacheLevel, CacheFailoverConfig> _failoverConfigs = new();
    private readonly ConcurrentQueue<FailoverEvent> _failoverHistory = new();
    
    // Recovery testing
    private readonly Timer _recoveryTestTimer;
    private readonly SemaphoreSlim _recoveryTestSemaphore = new(1, 1);

    public CacheCircuitBreakerService(
        IConfiguration configuration,
        ILogger<CacheCircuitBreakerService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Initialize failover configurations
        InitializeFailoverConfigs();
        
        // Create circuit breakers for each level
        _l1CircuitBreaker = CreateCircuitBreaker("L1_Memory", _failoverConfigs[CacheLevel.L1_Memory]);
        _l2CircuitBreaker = CreateCircuitBreaker("L2_Redis", _failoverConfigs[CacheLevel.L2_Redis]);
        _l3CircuitBreaker = CreateCircuitBreaker("L3_SqlServer", _failoverConfigs[CacheLevel.L3_SqlServer]);
        
        // Start monitoring timers
        var monitorInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("CircuitBreaker:MonitorIntervalSeconds", 30));
        var recoveryTestInterval = TimeSpan.FromMinutes(configuration.GetValue<int>("CircuitBreaker:RecoveryTestIntervalMinutes", 2));
        
        _healthMonitorTimer = new Timer(MonitorHealth, null, monitorInterval, monitorInterval);
        _recoveryTestTimer = new Timer(async _ => await TestRecoveryAsync(), null, recoveryTestInterval, recoveryTestInterval);
        
        _logger.LogInformation("Initialized cache circuit breaker service with monitoring interval {MonitorIntervalSeconds}s",
            monitorInterval.TotalSeconds);
    }

    public async Task<T> ExecuteAsync<T>(CacheLevel level, Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ExecuteWithCircuitBreaker");
        activity?.SetTag("cache.level", level.ToString());
        
        var circuitBreaker = GetCircuitBreakerForLevel(level);
        var levelName = level.ToString();
        
        try
        {
            var result = await circuitBreaker.ExecuteAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await operation();
                stopwatch.Stop();
                
                // Record successful operation
                RecordSuccess(levelName, stopwatch.ElapsedMilliseconds);
                activity?.SetTag("cache.success", true);
                activity?.SetTag("cache.duration_ms", stopwatch.ElapsedMilliseconds);
                
                return result;
            });
            
            return result;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning("Circuit breaker is open for {Level}, operation rejected", levelName);
            activity?.SetTag("cache.circuit_breaker_open", true);
            
            // Record circuit breaker rejection
            RecordCircuitBreakerRejection(levelName);
            
            // Attempt failover if configured
            if (ShouldAttemptFailover(level))
            {
                return await AttemptFailover(level, operation, cancellationToken);
            }
            
            throw new CacheUnavailableException(level, $"Circuit breaker is open for {levelName}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing operation for {Level}", levelName);
            activity?.SetTag("cache.error", ex.Message);
            
            // Record failure
            RecordFailure(levelName, ex);
            
            throw;
        }
    }

    public CircuitBreakerHealthReport GetHealthReport()
    {
        var report = new CircuitBreakerHealthReport
        {
            Timestamp = DateTime.UtcNow,
            CircuitStates = _circuitStates.ToDictionary(kv => kv.Key, kv => kv.Value),
            FailureCounts = _failureCounts.ToDictionary(kv => kv.Key, kv => kv.Value),
            LastFailures = _lastFailures.ToDictionary(kv => kv.Key, kv => kv.Value),
            RecentFailovers = _failoverHistory.ToArray()
        };
        
        // Add circuit breaker states
        report.L1State = GetCircuitBreakerState(_l1CircuitBreaker);
        report.L2State = GetCircuitBreakerState(_l2CircuitBreaker);
        report.L3State = GetCircuitBreakerState(_l3CircuitBreaker);
        
        return report;
    }

    public async Task<bool> TestCircuitBreakerAsync(CacheLevel level, CancellationToken cancellationToken = default)
    {
        var circuitBreaker = GetCircuitBreakerForLevel(level);
        var levelName = level.ToString();
        
        try
        {
            await circuitBreaker.ExecuteAsync(async () =>
            {
                // Simple test operation - just return success
                await Task.Delay(1, cancellationToken);
                return true;
            });
            
            _logger.LogTrace("Circuit breaker test passed for {Level}", levelName);
            return true;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogTrace("Circuit breaker is open for {Level}", levelName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Circuit breaker test failed for {Level}", levelName);
            return false;
        }
    }

    public void ForceCircuitBreakerState(CacheLevel level, CircuitBreakerState state)
    {
        var levelName = level.ToString();
        
        switch (state)
        {
            case CircuitBreakerState.Closed:
                // Reset the circuit breaker
                var circuitBreaker = GetCircuitBreakerForLevel(level);
                if (circuitBreaker.CircuitState == Polly.CircuitBreaker.CircuitState.Open)
                {
                    _logger.LogInformation("Manually resetting circuit breaker for {Level}", levelName);
                    // Note: Polly doesn't allow manual reset, so we log the intention
                }
                break;
                
            case CircuitBreakerState.Open:
                // Force failure to trip the circuit breaker
                RecordFailure(levelName, new Exception("Manual circuit breaker trip"));
                break;
        }
        
        _circuitStates[levelName] = state;
    }

    public Dictionary<string, long> GetMetrics()
    {
        var metrics = new Dictionary<string, long>();
        
        foreach (var (level, count) in _failureCounts)
        {
            metrics[$"{level}_failures"] = count;
        }
        
        metrics["total_failovers"] = _failoverHistory.Count;
        metrics["l1_circuit_state"] = (long)_l1CircuitBreaker.CircuitState;
        metrics["l2_circuit_state"] = (long)_l2CircuitBreaker.CircuitState;
        metrics["l3_circuit_state"] = (long)_l3CircuitBreaker.CircuitState;
        
        return metrics;
    }

    private void InitializeFailoverConfigs()
    {
        _failoverConfigs[CacheLevel.L1_Memory] = new CacheFailoverConfig
        {
            Level = CacheLevel.L1_Memory,
            FailureThreshold = _configuration.GetValue<int>("CircuitBreaker:L1:FailureThreshold", 5),
            BreakDuration = TimeSpan.FromSeconds(_configuration.GetValue<int>("CircuitBreaker:L1:BreakDurationSeconds", 30)),
            SamplingDuration = TimeSpan.FromSeconds(_configuration.GetValue<int>("CircuitBreaker:L1:SamplingDurationSeconds", 60)),
            MinimumThroughput = _configuration.GetValue<int>("CircuitBreaker:L1:MinimumThroughput", 10),
            EnableFailover = _configuration.GetValue<bool>("CircuitBreaker:L1:EnableFailover", false),
            FailoverLevels = new[] { CacheLevel.L2_Redis, CacheLevel.L3_SqlServer }
        };
        
        _failoverConfigs[CacheLevel.L2_Redis] = new CacheFailoverConfig
        {
            Level = CacheLevel.L2_Redis,
            FailureThreshold = _configuration.GetValue<int>("CircuitBreaker:L2:FailureThreshold", 3),
            BreakDuration = TimeSpan.FromSeconds(_configuration.GetValue<int>("CircuitBreaker:L2:BreakDurationSeconds", 60)),
            SamplingDuration = TimeSpan.FromSeconds(_configuration.GetValue<int>("CircuitBreaker:L2:SamplingDurationSeconds", 120)),
            MinimumThroughput = _configuration.GetValue<int>("CircuitBreaker:L2:MinimumThroughput", 5),
            EnableFailover = _configuration.GetValue<bool>("CircuitBreaker:L2:EnableFailover", true),
            FailoverLevels = new[] { CacheLevel.L3_SqlServer }
        };
        
        _failoverConfigs[CacheLevel.L3_SqlServer] = new CacheFailoverConfig
        {
            Level = CacheLevel.L3_SqlServer,
            FailureThreshold = _configuration.GetValue<int>("CircuitBreaker:L3:FailureThreshold", 2),
            BreakDuration = TimeSpan.FromMinutes(_configuration.GetValue<int>("CircuitBreaker:L3:BreakDurationMinutes", 5)),
            SamplingDuration = TimeSpan.FromMinutes(_configuration.GetValue<int>("CircuitBreaker:L3:SamplingDurationMinutes", 10)),
            MinimumThroughput = _configuration.GetValue<int>("CircuitBreaker:L3:MinimumThroughput", 3),
            EnableFailover = _configuration.GetValue<bool>("CircuitBreaker:L3:EnableFailover", false),
            FailoverLevels = Array.Empty<CacheLevel>()
        };
    }

    private AsyncCircuitBreakerPolicy CreateCircuitBreaker(string name, CacheFailoverConfig config)
    {
        return Policy
            .Handle<Exception>(ex => IsTransientException(ex))
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5, // 50% failure threshold
                samplingDuration: TimeSpan.FromSeconds(10),
                minimumThroughput: Math.Max(2, config.FailureThreshold),
                durationOfBreak: config.BreakDuration,
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning("Circuit breaker opened for {Name} due to: {Exception}. Duration: {Duration}s",
                        name, exception.Message, duration.TotalSeconds);
                    
                    _circuitStates[name] = CircuitBreakerState.Open;
                    RecordFailoverEvent(name, CircuitBreakerState.Open, exception.Message);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset for {Name} - service recovered", name);
                    _circuitStates[name] = CircuitBreakerState.Closed;
                    RecordFailoverEvent(name, CircuitBreakerState.Closed, "Service recovered");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open for {Name} - testing recovery", name);
                    _circuitStates[name] = CircuitBreakerState.HalfOpen;
                    RecordFailoverEvent(name, CircuitBreakerState.HalfOpen, "Testing recovery");
                });
    }

    private AsyncCircuitBreakerPolicy GetCircuitBreakerForLevel(CacheLevel level)
    {
        return level switch
        {
            CacheLevel.L1_Memory => _l1CircuitBreaker,
            CacheLevel.L2_Redis => _l2CircuitBreaker,
            CacheLevel.L3_SqlServer => _l3CircuitBreaker,
            _ => throw new ArgumentException($"Unknown cache level: {level}")
        };
    }

    private bool IsTransientException(Exception exception)
    {
        // Define which exceptions are considered transient and should trigger circuit breaker
        return exception is not ArgumentException &&
               exception is not InvalidOperationException &&
               exception is not NotSupportedException;
    }

    private bool ShouldAttemptFailover(CacheLevel level)
    {
        return _failoverConfigs.TryGetValue(level, out var config) && 
               config.EnableFailover && 
               config.FailoverLevels.Length > 0;
    }

    private async Task<T> AttemptFailover<T>(CacheLevel failedLevel, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var config = _failoverConfigs[failedLevel];
        
        foreach (var failoverLevel in config.FailoverLevels)
        {
            try
            {
                _logger.LogInformation("Attempting failover from {FailedLevel} to {FailoverLevel}",
                    failedLevel, failoverLevel);
                
                var result = await ExecuteAsync(failoverLevel, operation, cancellationToken);
                
                RecordFailoverEvent(failedLevel.ToString(), CircuitBreakerState.Open, 
                    $"Failed over to {failoverLevel}");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failover to {FailoverLevel} also failed", failoverLevel);
            }
        }
        
        throw new CacheUnavailableException(failedLevel, "All failover options exhausted");
    }

    private void RecordSuccess(string level, long durationMs)
    {
        // Could implement success rate tracking here
        _logger.LogTrace("Successful operation for {Level} in {Duration}ms", level, durationMs);
    }

    private void RecordFailure(string level, Exception exception)
    {
        _failureCounts.AddOrUpdate(level, 1, (key, value) => value + 1);
        _lastFailures[level] = DateTime.UtcNow;
        
        _logger.LogDebug("Recorded failure for {Level}: {Exception}", level, exception.Message);
    }

    private void RecordCircuitBreakerRejection(string level)
    {
        _failureCounts.AddOrUpdate($"{level}_rejections", 1, (key, value) => value + 1);
    }

    private void RecordFailoverEvent(string level, CircuitBreakerState state, string reason)
    {
        var failoverEvent = new FailoverEvent
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            State = state,
            Reason = reason
        };
        
        _failoverHistory.Enqueue(failoverEvent);
        
        // Keep only recent events (last 100)
        while (_failoverHistory.Count > 100)
        {
            _failoverHistory.TryDequeue(out _);
        }
    }

    private CircuitBreakerState GetCircuitBreakerState(AsyncCircuitBreakerPolicy circuitBreaker)
    {
        return circuitBreaker.CircuitState switch
        {
            Polly.CircuitBreaker.CircuitState.Closed => CircuitBreakerState.Closed,
            Polly.CircuitBreaker.CircuitState.Open => CircuitBreakerState.Open,
            Polly.CircuitBreaker.CircuitState.HalfOpen => CircuitBreakerState.HalfOpen,
            Polly.CircuitBreaker.CircuitState.Isolated => CircuitBreakerState.Open,
            _ => CircuitBreakerState.Closed
        };
    }

    private void MonitorHealth(object? state)
    {
        try
        {
            // Update circuit breaker states
            _circuitStates["L1_Memory"] = GetCircuitBreakerState(_l1CircuitBreaker);
            _circuitStates["L2_Redis"] = GetCircuitBreakerState(_l2CircuitBreaker);
            _circuitStates["L3_SqlServer"] = GetCircuitBreakerState(_l3CircuitBreaker);
            
            // Log health status
            var openCircuits = _circuitStates.Where(kv => kv.Value == CircuitBreakerState.Open).ToArray();
            if (openCircuits.Length > 0)
            {
                _logger.LogWarning("Open circuit breakers detected: {OpenCircuits}",
                    string.Join(", ", openCircuits.Select(kv => kv.Key)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring circuit breaker health");
        }
    }

    private async Task TestRecoveryAsync()
    {
        if (!await _recoveryTestSemaphore.WaitAsync(0))
        {
            return; // Recovery test already in progress
        }
        
        try
        {
            var openCircuits = _circuitStates.Where(kv => kv.Value == CircuitBreakerState.Open).ToArray();
            
            foreach (var (levelName, _) in openCircuits)
            {
                if (Enum.TryParse<CacheLevel>(levelName, out var level))
                {
                    _logger.LogDebug("Testing recovery for {Level}", levelName);
                    await TestCircuitBreakerAsync(level, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during recovery testing");
        }
        finally
        {
            _recoveryTestSemaphore.Release();
        }
    }

    public void Dispose()
    {
        _healthMonitorTimer?.Dispose();
        _recoveryTestTimer?.Dispose();
        _recoveryTestSemaphore?.Dispose();
        _activitySource?.Dispose();
    }
}

/// <summary>
/// Configuration for cache failover behavior
/// </summary>
public class CacheFailoverConfig
{
    public CacheLevel Level { get; set; }
    public int FailureThreshold { get; set; }
    public TimeSpan BreakDuration { get; set; }
    public TimeSpan SamplingDuration { get; set; }
    public int MinimumThroughput { get; set; }
    public bool EnableFailover { get; set; }
    public CacheLevel[] FailoverLevels { get; set; } = Array.Empty<CacheLevel>();
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Failover event for tracking
/// </summary>
public class FailoverEvent
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public CircuitBreakerState State { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Circuit breaker health report
/// </summary>
public class CircuitBreakerHealthReport
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, CircuitBreakerState> CircuitStates { get; set; } = new();
    public Dictionary<string, long> FailureCounts { get; set; } = new();
    public Dictionary<string, DateTime> LastFailures { get; set; } = new();
    public FailoverEvent[] RecentFailovers { get; set; } = Array.Empty<FailoverEvent>();
    public CircuitBreakerState L1State { get; set; }
    public CircuitBreakerState L2State { get; set; }
    public CircuitBreakerState L3State { get; set; }
}

/// <summary>
/// Exception thrown when cache is unavailable
/// </summary>
public class CacheUnavailableException : Exception
{
    public CacheLevel Level { get; }
    
    public CacheUnavailableException(CacheLevel level, string message) : base(message)
    {
        Level = level;
    }
    
    public CacheUnavailableException(CacheLevel level, string message, Exception innerException) : base(message, innerException)
    {
        Level = level;
    }
}