using Microsoft.Extensions.Logging;
using ACS.Service.Services;
using ACS.Service.Infrastructure;
using System.Net;
using System.Net.Sockets;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class ErrorRecoveryServiceTests
{
    private ILogger<ErrorRecoveryService> _logger = null!;
    private TenantConfiguration _tenantConfig = null!;
    private ErrorRecoveryService _errorRecoveryService = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Mock.Of<ILogger<ErrorRecoveryService>>();
        _tenantConfig = new TenantConfiguration { TenantId = "test-tenant" };
        _errorRecoveryService = new ErrorRecoveryService(_logger, _tenantConfig);
    }

    [TestMethod]
    public async Task ExecuteWithRecoveryAsync_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        const string expectedResult = "success";
        var operation = new Func<CancellationToken, Task<string>>(_ => Task.FromResult(expectedResult));

        // Act
        var result = await _errorRecoveryService.ExecuteWithRecoveryAsync("test", operation);

        // Assert
        Assert.AreEqual(expectedResult, result);
    }

    [TestMethod]
    public async Task ExecuteWithRecoveryAsync_RetryableException_RetriesAndSucceeds()
    {
        // Arrange
        var attemptCount = 0;
        const string expectedResult = "success";
        
        var operation = new Func<CancellationToken, Task<string>>(_ =>
        {
            attemptCount++;
            if (attemptCount < 2)
                throw new SocketException((int)SocketError.ConnectionRefused);
            return Task.FromResult(expectedResult);
        });

        // Act
        var result = await _errorRecoveryService.ExecuteWithRecoveryAsync("test", operation, maxRetries: 2);

        // Assert
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(2, attemptCount);
    }

    [TestMethod]
    public async Task ExecuteWithRecoveryAsync_NonRetryableException_DoesNotRetry()
    {
        // Arrange
        var attemptCount = 0;
        var operation = new Func<CancellationToken, Task<string>>(_ =>
        {
            attemptCount++;
            throw new ArgumentNullException("test");
        });

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            () => _errorRecoveryService.ExecuteWithRecoveryAsync("test", operation, maxRetries: 2));
        
        Assert.AreEqual(1, attemptCount);
    }

    [TestMethod]
    public async Task ExecuteWithRecoveryAsync_MaxRetriesExceeded_ThrowsLastException()
    {
        // Arrange
        var attemptCount = 0;
        var operation = new Func<CancellationToken, Task<string>>(_ =>
        {
            attemptCount++;
            throw new SocketException((int)SocketError.ConnectionRefused);
        });

        // Act & Assert
        await Assert.ThrowsExceptionAsync<SocketException>(
            () => _errorRecoveryService.ExecuteWithRecoveryAsync("test", operation, maxRetries: 2));
        
        Assert.AreEqual(3, attemptCount); // Initial attempt + 2 retries
    }

    [TestMethod]
    public async Task ExecuteWithRecoveryAsync_WithFallback_UsesFallbackOnFailure()
    {
        // Arrange
        const string fallbackResult = "fallback";
        var operation = new Func<CancellationToken, Task<string>>(_ => 
            throw new SocketException((int)SocketError.ConnectionRefused));
        
        var fallback = new Func<Exception, Task<string>>(ex => Task.FromResult(fallbackResult));

        // Act
        var result = await _errorRecoveryService.ExecuteWithRecoveryAsync("test", operation, fallback, maxRetries: 1);

        // Assert
        Assert.AreEqual(fallbackResult, result);
    }

    [TestMethod]
    public async Task ExecuteWithRecoveryAsync_TimeoutException_CreatesTimeoutFromCancellation()
    {
        // Arrange
        var operation = new Func<CancellationToken, Task<string>>(async ct =>
        {
            // Simulate timeout by waiting longer than the operation timeout
            await Task.Delay(100, ct);
            return "success";
        });

        // Act & Assert  
        await Assert.ThrowsExceptionAsync<TimeoutException>(
            () => _errorRecoveryService.ExecuteWithRecoveryAsync("test", operation, maxRetries: 1));
    }

    [TestMethod]
    public async Task ExecuteWithRecoveryAsync_CancellationRequested_DoesNotRetry()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var attemptCount = 0;
        var operation = new Func<CancellationToken, Task<string>>(ct =>
        {
            attemptCount++;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult("success");
        });

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => _errorRecoveryService.ExecuteWithRecoveryAsync("test", operation, maxRetries: 2, cancellationToken: cts.Token));
        
        Assert.AreEqual(1, attemptCount);
    }

    [TestMethod]
    public void GetCircuitBreakerState_NewOperationType_ReturnsClosedState()
    {
        // Act
        var state = _errorRecoveryService.GetCircuitBreakerState("new-operation");

        // Assert
        Assert.AreEqual(CircuitBreakerState.CircuitState.Closed, state.State);
        Assert.AreEqual(0, state.FailureCount);
    }

    [TestMethod]
    public void ResetCircuitBreaker_ExistingOperationType_ResetsState()
    {
        // Arrange
        var state = _errorRecoveryService.GetCircuitBreakerState("test-operation");
        
        // Force failure to change state
        state.RecordFailure(new Exception("test"));

        // Act
        _errorRecoveryService.ResetCircuitBreaker("test-operation");

        // Assert
        var resetState = _errorRecoveryService.GetCircuitBreakerState("test-operation");
        Assert.AreEqual(CircuitBreakerState.CircuitState.Closed, resetState.State);
        Assert.AreEqual(0, resetState.FailureCount);
    }

    [TestMethod]
    public async Task ExecuteWithRecoveryAsync_VoidOverload_ExecutesSuccessfully()
    {
        // Arrange
        var executed = false;
        var operation = new Func<CancellationToken, Task>(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        // Act
        await _errorRecoveryService.ExecuteWithRecoveryAsync("test", operation);

        // Assert
        Assert.IsTrue(executed);
    }
}

[TestClass]
public class CircuitBreakerStateTests
{
    private ILogger _logger = null!;
    private CircuitBreakerState _circuitBreaker = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Mock.Of<ILogger>();
        var config = new CircuitBreakerConfig(3, TimeSpan.FromMinutes(1));
        _circuitBreaker = new CircuitBreakerState(config, _logger);
    }

    [TestMethod]
    public void InitialState_IsClosed()
    {
        // Assert
        Assert.AreEqual(CircuitBreakerState.CircuitState.Closed, _circuitBreaker.State);
        Assert.AreEqual(0, _circuitBreaker.FailureCount);
    }

    [TestMethod]
    public void RecordSuccess_AfterFailures_ResetsState()
    {
        // Arrange
        _circuitBreaker.RecordFailure(new Exception("test"));
        _circuitBreaker.RecordFailure(new Exception("test"));

        // Act
        _circuitBreaker.RecordSuccess();

        // Assert
        Assert.AreEqual(CircuitBreakerState.CircuitState.Closed, _circuitBreaker.State);
        Assert.AreEqual(0, _circuitBreaker.FailureCount);
    }

    [TestMethod]
    public void RecordFailure_ReachesThreshold_OpensCircuit()
    {
        // Arrange & Act
        _circuitBreaker.RecordFailure(new Exception("test"));
        _circuitBreaker.RecordFailure(new Exception("test"));
        _circuitBreaker.RecordFailure(new Exception("test")); // Should open circuit

        // Assert
        Assert.AreEqual(CircuitBreakerState.CircuitState.Open, _circuitBreaker.State);
        Assert.AreEqual(3, _circuitBreaker.FailureCount);
    }

    [TestMethod]
    public void Reset_OpensCircuit_ResetsToClosedState()
    {
        // Arrange
        _circuitBreaker.RecordFailure(new Exception("test"));
        _circuitBreaker.RecordFailure(new Exception("test"));
        _circuitBreaker.RecordFailure(new Exception("test"));

        // Act
        _circuitBreaker.Reset();

        // Assert
        Assert.AreEqual(CircuitBreakerState.CircuitState.Closed, _circuitBreaker.State);
        Assert.AreEqual(0, _circuitBreaker.FailureCount);
    }

    [TestMethod]
    public void State_AfterRecoveryWindow_TransitionsToHalfOpen()
    {
        // Arrange - Create circuit breaker with short recovery window for testing
        var config = new CircuitBreakerConfig(1, TimeSpan.FromMilliseconds(100));
        var shortWindowCircuitBreaker = new CircuitBreakerState(config, _logger);

        // Open the circuit
        shortWindowCircuitBreaker.RecordFailure(new Exception("test"));
        Assert.AreEqual(CircuitBreakerState.CircuitState.Open, shortWindowCircuitBreaker.State);

        // Act - Wait for recovery window
        Thread.Sleep(150);

        // Assert - Should transition to HalfOpen
        Assert.AreEqual(CircuitBreakerState.CircuitState.HalfOpen, shortWindowCircuitBreaker.State);
    }
}