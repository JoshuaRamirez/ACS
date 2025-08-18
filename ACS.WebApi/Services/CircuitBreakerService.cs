using System.Collections.Concurrent;

namespace ACS.WebApi.Services;

/// <summary>
/// Circuit breaker pattern implementation for gRPC calls to tenant processes
/// </summary>
public class CircuitBreakerService
{
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers = new();
    private readonly ILogger<CircuitBreakerService> _logger;

    public CircuitBreakerService(ILogger<CircuitBreakerService> logger)
    {
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(string tenantId, Func<Task<T>> operation, CircuitBreakerOptions? options = null)
    {
        options ??= CircuitBreakerOptions.Default;
        var breaker = _circuitBreakers.GetOrAdd(tenantId, _ => new CircuitBreakerState(options));

        if (breaker.State == CircuitState.Open)
        {
            if (DateTime.UtcNow < breaker.NextRetryTime)
            {
                _logger.LogWarning("Circuit breaker is OPEN for tenant {TenantId}. Failing fast.", tenantId);
                throw new CircuitBreakerOpenException($"Circuit breaker is open for tenant {tenantId}");
            }
            
            // Transition to half-open state
            breaker.State = CircuitState.HalfOpen;
            _logger.LogInformation("Circuit breaker transitioning to HALF-OPEN for tenant {TenantId}", tenantId);
        }

        try
        {
            var result = await operation();
            
            // Success - reset the circuit breaker
            if (breaker.State == CircuitState.HalfOpen)
            {
                _logger.LogInformation("Circuit breaker closing for tenant {TenantId} after successful operation", tenantId);
                breaker.Reset();
            }
            else
            {
                breaker.RecordSuccess();
            }
            
            return result;
        }
        catch (Exception ex)
        {
            breaker.RecordFailure();
            
            if (breaker.ShouldTrip())
            {
                breaker.Trip();
                _logger.LogWarning(ex, "Circuit breaker OPENED for tenant {TenantId} after {FailureCount} failures", 
                    tenantId, breaker.FailureCount);
            }
            
            throw;
        }
    }

    public CircuitState GetState(string tenantId)
    {
        return _circuitBreakers.TryGetValue(tenantId, out var breaker) 
            ? breaker.State 
            : CircuitState.Closed;
    }

    public void ForceOpen(string tenantId)
    {
        var breaker = _circuitBreakers.GetOrAdd(tenantId, _ => new CircuitBreakerState(CircuitBreakerOptions.Default));
        breaker.Trip();
        _logger.LogWarning("Circuit breaker FORCED OPEN for tenant {TenantId}", tenantId);
    }

    public void ForceClose(string tenantId)
    {
        var breaker = _circuitBreakers.GetOrAdd(tenantId, _ => new CircuitBreakerState(CircuitBreakerOptions.Default));
        breaker.Reset();
        _logger.LogInformation("Circuit breaker FORCED CLOSED for tenant {TenantId}", tenantId);
    }
}

public class CircuitBreakerState
{
    private readonly CircuitBreakerOptions _options;
    private readonly object _lock = new();

    public CircuitBreakerState(CircuitBreakerOptions options)
    {
        _options = options;
        State = CircuitState.Closed;
        FailureCount = 0;
        NextRetryTime = DateTime.MinValue;
    }

    public CircuitState State { get; set; }
    public int FailureCount { get; private set; }
    public DateTime NextRetryTime { get; private set; }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            FailureCount = 0;
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            FailureCount++;
        }
    }

    public bool ShouldTrip()
    {
        lock (_lock)
        {
            return State == CircuitState.Closed && FailureCount >= _options.FailureThreshold;
        }
    }

    public void Trip()
    {
        lock (_lock)
        {
            State = CircuitState.Open;
            NextRetryTime = DateTime.UtcNow.Add(_options.OpenTimeout);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            State = CircuitState.Closed;
            FailureCount = 0;
            NextRetryTime = DateTime.MinValue;
        }
    }
}

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public static CircuitBreakerOptions Default => new();
}

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
    public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException) { }
}