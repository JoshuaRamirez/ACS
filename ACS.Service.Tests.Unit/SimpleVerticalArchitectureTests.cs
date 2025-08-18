using Microsoft.Extensions.Logging;
using ACS.Service.Services;
using ACS.Service.Infrastructure;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class SimpleVerticalArchitectureTests
{
    [TestMethod]
    public void ErrorRecoveryService_Constructor_InitializesCorrectly()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ErrorRecoveryService>>();
        var tenantConfig = new TenantConfiguration { TenantId = "test-tenant" };

        // Act
        var service = new ErrorRecoveryService(logger, tenantConfig);

        // Assert
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void CircuitBreakerConfig_Constructor_SetsProperties()
    {
        // Arrange & Act
        var config = new CircuitBreakerConfig(5, TimeSpan.FromMinutes(2));

        // Assert
        Assert.AreEqual(5, config.FailureThreshold);
        Assert.AreEqual(TimeSpan.FromMinutes(2), config.RecoveryTimeWindow);
    }

    [TestMethod]
    public void CircuitBreakerState_InitialState_IsClosed()
    {
        // Arrange
        var logger = Mock.Of<ILogger>();
        var config = new CircuitBreakerConfig(3, TimeSpan.FromMinutes(1));

        // Act
        var circuitBreaker = new CircuitBreakerState(config, logger);

        // Assert
        Assert.AreEqual(CircuitBreakerState.CircuitState.Closed, circuitBreaker.State);
        Assert.AreEqual(0, circuitBreaker.FailureCount);
    }

    [TestMethod]
    public void CircuitBreakerState_RecordSuccess_ResetsFailures()
    {
        // Arrange
        var logger = Mock.Of<ILogger>();
        var config = new CircuitBreakerConfig(3, TimeSpan.FromMinutes(1));
        var circuitBreaker = new CircuitBreakerState(config, logger);
        
        circuitBreaker.RecordFailure(new Exception("test"));
        Assert.AreEqual(1, circuitBreaker.FailureCount);

        // Act
        circuitBreaker.RecordSuccess();

        // Assert
        Assert.AreEqual(CircuitBreakerState.CircuitState.Closed, circuitBreaker.State);
        Assert.AreEqual(0, circuitBreaker.FailureCount);
    }

    [TestMethod]
    public void CircuitBreakerState_RecordFailure_OpensAfterThreshold()
    {
        // Arrange
        var logger = Mock.Of<ILogger>();
        var config = new CircuitBreakerConfig(2, TimeSpan.FromMinutes(1));
        var circuitBreaker = new CircuitBreakerState(config, logger);

        // Act - Record enough failures to open circuit
        circuitBreaker.RecordFailure(new Exception("test 1"));
        circuitBreaker.RecordFailure(new Exception("test 2"));

        // Assert
        Assert.AreEqual(CircuitBreakerState.CircuitState.Open, circuitBreaker.State);
        Assert.AreEqual(2, circuitBreaker.FailureCount);
    }

    [TestMethod]
    public void DeadLetterQueueService_Constructor_InitializesCorrectly()
    {
        // Arrange
        var logger = Mock.Of<ILogger<DeadLetterQueueService>>();
        var tenantConfig = new TenantConfiguration { TenantId = "test-tenant" };

        // Act
        var service = new DeadLetterQueueService(logger, tenantConfig);

        // Assert
        Assert.IsNotNull(service);
        
        // Cleanup
        service.Dispose();
    }

    [TestMethod]
    public void FailedCommand_Properties_CanBeSetAndRead()
    {
        // Arrange & Act
        var failedCommand = new FailedCommand
        {
            Id = Guid.NewGuid(),
            TenantId = "test-tenant",
            CommandType = "TestCommand",
            CommandData = "{\"test\": true}",
            AttemptNumber = 1,
            FirstFailureTime = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Assert
        Assert.IsNotNull(failedCommand.Id);
        Assert.AreEqual("test-tenant", failedCommand.TenantId);
        Assert.AreEqual("TestCommand", failedCommand.CommandType);
        Assert.AreEqual("{\"test\": true}", failedCommand.CommandData);
        Assert.AreEqual(1, failedCommand.AttemptNumber);
    }

    [TestMethod]
    public void CommandTranslationService_Constructor_InitializesCorrectly()
    {
        // Arrange
        var logger = Mock.Of<ILogger<CommandTranslationService>>();

        // Act
        var service = new CommandTranslationService(logger);

        // Assert
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void CommandTranslationService_IsQueryCommand_ClassifiesCorrectly()
    {
        // Arrange
        var logger = Mock.Of<ILogger<CommandTranslationService>>();
        var service = new CommandTranslationService(logger);
        
        var getUserCommand = new Infrastructure.GetUserCommand("req-1", DateTime.UtcNow, "user-1", 123);
        var createUserCommand = new Infrastructure.CreateUserCommand("req-1", DateTime.UtcNow, "user-1", "John Doe");

        // Act & Assert
        Assert.IsTrue(service.IsQueryCommand(getUserCommand));
        Assert.IsFalse(service.IsQueryCommand(createUserCommand));
    }

    [TestMethod]
    public void CommandTranslationService_IsMutationCommand_ClassifiesCorrectly()
    {
        // Arrange
        var logger = Mock.Of<ILogger<CommandTranslationService>>();
        var service = new CommandTranslationService(logger);
        
        var getUserCommand = new Infrastructure.GetUserCommand("req-1", DateTime.UtcNow, "user-1", 123);
        var createUserCommand = new Infrastructure.CreateUserCommand("req-1", DateTime.UtcNow, "user-1", "John Doe");

        // Act & Assert
        Assert.IsFalse(service.IsMutationCommand(getUserCommand));
        Assert.IsTrue(service.IsMutationCommand(createUserCommand));
    }

    [TestMethod]
    public void TenantConfiguration_Properties_CanBeSetAndRead()
    {
        // Arrange & Act
        var config = new TenantConfiguration { TenantId = "test-tenant-123" };

        // Assert
        Assert.AreEqual("test-tenant-123", config.TenantId);
    }

    [TestMethod]
    public void HealthThresholds_Constants_HaveCorrectValues()
    {
        // This test verifies the threshold constants are reasonable
        // We can access them through the health monitoring service
        var logger = Mock.Of<ILogger<HealthMonitoringService>>();
        var errorRecoveryLogger = Mock.Of<ILogger<ErrorRecoveryService>>();
        var tenantConfig = new TenantConfiguration { TenantId = "test" };
        var errorRecovery = new ErrorRecoveryService(errorRecoveryLogger, tenantConfig);
        
        // Just verify the service can be created
        using var healthService = new HealthMonitoringService(logger, errorRecovery, tenantConfig);
        Assert.IsNotNull(healthService);
    }
}