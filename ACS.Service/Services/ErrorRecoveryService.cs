using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.Http;
using ACS.Service.Infrastructure;

namespace ACS.Service.Services;

/// <summary>
/// Comprehensive error recovery service that handles various failure scenarios
/// across the vertical architecture with circuit breaker, timeout, and fallback patterns
/// </summary>
public class ErrorRecoveryService
{
    private static readonly ActivitySource ActivitySource = new("ACS.ErrorRecovery");
    
    private readonly ILogger<ErrorRecoveryService> _logger;
    private readonly string _tenantId;
    
    // Circuit breaker state tracking per operation type
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers = new();
    
    // Timeout configurations per operation type
    private readonly Dictionary<string, TimeSpan> _timeoutConfigurations = new()
    {
        ["database"] = TimeSpan.FromSeconds(30),
        ["grpc"] = TimeSpan.FromSeconds(10),
        ["external_api"] = TimeSpan.FromSeconds(15),
        ["file_system"] = TimeSpan.FromSeconds(5),
        ["network"] = TimeSpan.FromSeconds(20),
        ["test"] = TimeSpan.FromMilliseconds(50) // For unit testing
    };
    
    // Circuit breaker configurations per operation type
    private readonly Dictionary<string, CircuitBreakerConfig> _circuitBreakerConfigs = new()
    {
        ["database"] = new CircuitBreakerConfig(5, TimeSpan.FromMinutes(2)),
        ["grpc"] = new CircuitBreakerConfig(3, TimeSpan.FromMinutes(1)),
        ["external_api"] = new CircuitBreakerConfig(4, TimeSpan.FromMinutes(3)),
        ["file_system"] = new CircuitBreakerConfig(3, TimeSpan.FromSeconds(30)),
        ["network"] = new CircuitBreakerConfig(5, TimeSpan.FromMinutes(2))
    };

    public ErrorRecoveryService(
        ILogger<ErrorRecoveryService> logger,
        TenantConfiguration tenantConfig)
    {
        _logger = logger;
        _tenantId = tenantConfig.TenantId;
        
        _logger.LogInformation("Error Recovery Service initialized for tenant {TenantId}", _tenantId);
    }

    /// <summary>
    /// Executes an operation with comprehensive error recovery including circuit breaker,
    /// timeout, retry, and fallback mechanisms
    /// </summary>
    public async Task<T> ExecuteWithRecoveryAsync<T>(
        string operationType,
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, Task<T>>? fallback = null,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"error_recovery.{operationType}");
        activity?.SetTag("tenant.id", _tenantId);
        activity?.SetTag("operation.type", operationType);
        activity?.SetTag("max_retries", maxRetries);
        
        var circuitBreaker = GetOrCreateCircuitBreaker(operationType);
        
        // Check circuit breaker state
        if (circuitBreaker.State == CircuitBreakerState.CircuitState.Open)
        {
            activity?.SetTag("circuit_breaker.state", "open");
            _logger.LogWarning("Circuit breaker is OPEN for operation {OperationType}. Executing fallback if available.", operationType);
            
            if (fallback != null)
            {
                try
                {
                    var fallbackResult = await fallback(new InvalidOperationException("Circuit breaker is open"));
                    activity?.SetTag("fallback.executed", true);
                    return fallbackResult;
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback failed for operation {OperationType}", operationType);
                    activity?.SetStatus(ActivityStatusCode.Error, fallbackEx.Message);
                    throw;
                }
            }
            
            throw new InvalidOperationException($"Circuit breaker is open for operation {operationType} and no fallback is available");
        }

        var startTime = DateTime.UtcNow;
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            try
            {
                activity?.SetTag("attempt.number", attempt);
                
                // Apply timeout for this operation type
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (_timeoutConfigurations.TryGetValue(operationType, out var timeout))
                {
                    timeoutCts.CancelAfter(timeout);
                    activity?.SetTag("timeout.seconds", timeout.TotalSeconds);
                }
                
                var result = await operation(timeoutCts.Token);
                
                // Success - record success and close circuit breaker if it was half-open
                var duration = DateTime.UtcNow - startTime;
                activity?.SetTag("operation.successful", true);
                activity?.SetTag("operation.duration_ms", duration.TotalMilliseconds);
                activity?.SetTag("attempts.total", attempt);
                
                circuitBreaker.RecordSuccess();
                
                if (attempt > 1)
                {
                    _logger.LogInformation("Operation {OperationType} succeeded after {Attempts} attempts in {Duration}ms",
                        operationType, attempt, duration.TotalMilliseconds);
                }
                
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User cancellation - don't retry
                activity?.SetStatus(ActivityStatusCode.Error, "Operation cancelled by user");
                _logger.LogInformation("Operation {OperationType} cancelled by user", operationType);
                throw;
            }
            catch (OperationCanceledException ex)
            {
                // Timeout - this counts as a failure
                lastException = new TimeoutException($"Operation {operationType} timed out", ex);
                activity?.SetTag("timeout.occurred", true);
                _logger.LogWarning("Operation {OperationType} timed out on attempt {Attempt}", operationType, attempt);
            }
            catch (Exception ex)
            {
                lastException = ex;
                activity?.SetTag($"attempt.{attempt}.error", ex.GetType().Name);
                _logger.LogWarning(ex, "Operation {OperationType} failed on attempt {Attempt}", operationType, attempt);
            }
            
            // Record failure in circuit breaker
            circuitBreaker.RecordFailure(lastException!);
            
            // Check if we should retry
            if (attempt <= maxRetries && ShouldRetry(lastException!, operationType))
            {
                var delay = CalculateRetryDelay(attempt);
                activity?.SetTag($"retry.{attempt}.delay_ms", delay.TotalMilliseconds);
                
                _logger.LogDebug("Retrying operation {OperationType} in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                    operationType, delay.TotalMilliseconds, attempt, maxRetries);
                
                await Task.Delay(delay, cancellationToken);
            }
            else if (!ShouldRetry(lastException!, operationType))
            {
                // Don't retry non-retryable exceptions
                break;
            }
        }
        
        // All retries exhausted
        var totalDuration = DateTime.UtcNow - startTime;
        activity?.SetStatus(ActivityStatusCode.Error, lastException?.Message ?? "Unknown error");
        activity?.SetTag("operation.successful", false);
        activity?.SetTag("operation.duration_ms", totalDuration.TotalMilliseconds);
        activity?.SetTag("attempts.total", maxRetries + 1);
        
        _logger.LogError(lastException, "Operation {OperationType} failed after {Attempts} attempts in {Duration}ms",
            operationType, maxRetries + 1, totalDuration.TotalMilliseconds);
        
        // Try fallback if available
        if (fallback != null)
        {
            try
            {
                _logger.LogInformation("Executing fallback for failed operation {OperationType}", operationType);
                var fallbackResult = await fallback(lastException!);
                activity?.SetTag("fallback.executed", true);
                return fallbackResult;
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Fallback also failed for operation {OperationType}", operationType);
                activity?.SetTag("fallback.failed", true);
            }
        }
        
        throw lastException!;
    }

    /// <summary>
    /// Executes a void operation with comprehensive error recovery
    /// </summary>
    public async Task ExecuteWithRecoveryAsync(
        string operationType,
        Func<CancellationToken, Task> operation,
        Func<Exception, Task>? fallback = null,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRecoveryAsync(
            operationType,
            async ct =>
            {
                await operation(ct);
                return true; // Dummy return value
            },
            fallback == null ? null : async ex =>
            {
                await fallback(ex);
                return true; // Dummy return value
            },
            maxRetries,
            cancellationToken);
    }

    /// <summary>
    /// Gets the circuit breaker state for monitoring
    /// </summary>
    public CircuitBreakerState GetCircuitBreakerState(string operationType)
    {
        return GetOrCreateCircuitBreaker(operationType);
    }

    /// <summary>
    /// Manually resets a circuit breaker (for administrative intervention)
    /// </summary>
    public void ResetCircuitBreaker(string operationType)
    {
        var circuitBreaker = GetOrCreateCircuitBreaker(operationType);
        circuitBreaker.Reset();
        _logger.LogInformation("Circuit breaker manually reset for operation {OperationType}", operationType);
    }

    private CircuitBreakerState GetOrCreateCircuitBreaker(string operationType)
    {
        return _circuitBreakers.GetOrAdd(operationType, _ => 
            new CircuitBreakerState(
                _circuitBreakerConfigs.GetValueOrDefault(operationType, 
                    new CircuitBreakerConfig(5, TimeSpan.FromMinutes(2))),
                _logger));
    }

    private bool ShouldRetry(Exception exception, string operationType)
    {
        return exception switch
        {
            ArgumentNullException => false,       // Null arguments (more specific)
            ArgumentException => false,           // Invalid parameters (more general)
            NotSupportedException => false,       // Unsupported operations
            InvalidOperationException => false,   // Business rule violations
            TimeoutException => true,             // Network/database timeouts
            TaskCanceledException => true,        // Operation timeouts
            HttpRequestException => true,         // HTTP client errors
            SocketException => true,              // Network errors
            IOException => true,                  // File/network I/O errors
            _ => true                            // Default to retry for unknown exceptions
        };
    }

    private TimeSpan CalculateRetryDelay(int attemptNumber)
    {
        // Exponential backoff with jitter
        var baseDelay = TimeSpan.FromMilliseconds(1000); // 1 second base
        var exponentialDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1));
        var maxDelay = TimeSpan.FromSeconds(30); // Cap at 30 seconds
        
        var delay = exponentialDelay > maxDelay ? maxDelay : exponentialDelay;
        
        // Add jitter (±25% randomization)
        var jitterRange = delay.TotalMilliseconds * 0.25;
        var jitter = (Random.Shared.NextDouble() - 0.5) * 2 * jitterRange;
        
        return TimeSpan.FromMilliseconds(Math.Max(0, delay.TotalMilliseconds + jitter));
    }
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public record CircuitBreakerConfig(int FailureThreshold, TimeSpan RecoveryTimeWindow);

/// <summary>
/// Circuit breaker state management
/// </summary>
public class CircuitBreakerState
{
    private readonly CircuitBreakerConfig _config;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    
    private int _failureCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private CircuitState _state = CircuitState.Closed;
    
    public enum CircuitState { Closed, Open, HalfOpen }
    
    public CircuitState State 
    { 
        get 
        { 
            lock (_lock)
            {
                // Check if circuit should transition from Open to HalfOpen
                if (_state == CircuitState.Open && 
                    DateTime.UtcNow - _lastFailureTime > _config.RecoveryTimeWindow)
                {
                    _state = CircuitState.HalfOpen;
                    _logger.LogInformation("Circuit breaker transitioning from OPEN to HALF-OPEN");
                }
                
                return _state;
            }
        } 
    }
    
    public int FailureCount => _failureCount;
    public DateTime LastFailureTime => _lastFailureTime;
    
    public CircuitBreakerState(CircuitBreakerConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }
    
    public void RecordSuccess()
    {
        lock (_lock)
        {
            var previousState = _state;
            _failureCount = 0;
            _state = CircuitState.Closed;
            
            if (previousState != CircuitState.Closed)
            {
                _logger.LogInformation("Circuit breaker CLOSED after successful operation");
            }
        }
    }
    
    public void RecordFailure(Exception exception)
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            
            if (_failureCount >= _config.FailureThreshold && _state != CircuitState.Open)
            {
                _state = CircuitState.Open;
                _logger.LogWarning("Circuit breaker OPENED after {FailureCount} failures. Exception: {Exception}",
                    _failureCount, exception.Message);
            }
        }
    }
    
    public void Reset()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
            _lastFailureTime = DateTime.MinValue;
        }
    }
}