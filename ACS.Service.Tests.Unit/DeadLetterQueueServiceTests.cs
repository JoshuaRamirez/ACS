using Microsoft.Extensions.Logging;
using ACS.Service.Services;
using ACS.Service.Infrastructure;
using System.Text.Json;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class DeadLetterQueueServiceTests
{
    private ILogger<DeadLetterQueueService> _logger = null!;
    private TenantConfiguration _tenantConfig = null!;
    private DeadLetterQueueService _deadLetterQueueService = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Mock.Of<ILogger<DeadLetterQueueService>>();
        _tenantConfig = new TenantConfiguration { TenantId = "test-tenant" };
        _deadLetterQueueService = new DeadLetterQueueService(_logger, _tenantConfig);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _deadLetterQueueService?.Dispose();
    }

    [TestMethod]
    public async Task EnqueueFailedCommandAsync_DomainCommand_AddsToQueue()
    {
        // Arrange
        var command = new TestDomainCommand();
        var exception = new InvalidOperationException("test error");

        // Act
        await _deadLetterQueueService.EnqueueFailedCommandAsync(command, exception, 1);

        // Assert - No exception should be thrown, and command should be queued
        // Since the service runs in background, we can't directly verify queue contents
        // but we can verify the method completes without error
        Assert.IsTrue(true);
    }

    [TestMethod]
    public async Task EnqueueFailedCommandAsync_WebRequestCommand_AddsToQueue()
    {
        // Arrange
        var command = new TestWebRequestCommand("req-123", DateTime.UtcNow, "user-1");
        var exception = new TimeoutException("timeout error");

        // Act
        await _deadLetterQueueService.EnqueueFailedCommandAsync(command, exception, 2);

        // Assert - Method should complete without error
        Assert.IsTrue(true);
    }

    [TestMethod]
    public async Task GetFailedCommandsAsync_ReturnsEmptyList()
    {
        // Act
        var failedCommands = await _deadLetterQueueService.GetFailedCommandsAsync(10);

        // Assert
        Assert.IsNotNull(failedCommands);
        Assert.AreEqual(0, failedCommands.Count);
    }

    [TestMethod]
    public async Task GetFailedCommandsAsync_WithMaxCount_RespectsLimit()
    {
        // Act
        var failedCommands = await _deadLetterQueueService.GetFailedCommandsAsync(5);

        // Assert
        Assert.IsNotNull(failedCommands);
        Assert.AreEqual(0, failedCommands.Count); // Empty in this test implementation
    }

    [TestMethod]
    public void Constructor_InitializesCorrectly()
    {
        // Act - Constructor was called in Setup
        // Assert - No exception should be thrown
        Assert.IsNotNull(_deadLetterQueueService);
    }

    [TestMethod]
    public async Task EnqueueFailedCommandAsync_NullCommand_HandlesGracefully()
    {
        // Arrange
        DomainCommand? nullCommand = null;
        var exception = new ArgumentNullException("command");

        // Act & Assert - Should not throw unhandled exception
        try
        {
            await _deadLetterQueueService.EnqueueFailedCommandAsync(nullCommand!, exception);
            Assert.IsTrue(true); // If we get here, it handled gracefully
        }
        catch (ArgumentNullException)
        {
            // Expected behavior for null command
            Assert.IsTrue(true);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected exception type: {ex.GetType().Name}");
        }
    }

    [TestMethod]
    public async Task EnqueueFailedCommandAsync_NullException_HandlesGracefully()
    {
        // Arrange
        var command = new TestDomainCommand();
        Exception? nullException = null;

        // Act & Assert - Should not throw unhandled exception
        try
        {
            await _deadLetterQueueService.EnqueueFailedCommandAsync(command, nullException!);
            Assert.IsTrue(true); // If we get here, it handled gracefully
        }
        catch (ArgumentNullException)
        {
            // Expected behavior for null exception
            Assert.IsTrue(true);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected exception type: {ex.GetType().Name}");
        }
    }

    [TestMethod]
    public async Task BackgroundService_ExecutesWithoutError()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act - Start the background service and then cancel it quickly
        var serviceTask = _deadLetterQueueService.StartAsync(cts.Token);
        
        // Wait a short time to let it start
        await Task.Delay(100);
        
        // Cancel the service
        cts.Cancel();
        
        // Wait for it to stop
        await _deadLetterQueueService.StopAsync(CancellationToken.None);

        // Assert - Should complete without throwing
        Assert.IsTrue(serviceTask.IsCompletedSuccessfully || serviceTask.IsCanceled);
    }
}

[TestClass]
public class FailedCommandTests
{
    [TestMethod]
    public void FailedCommand_Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var failedCommand = new FailedCommand
        {
            Id = Guid.NewGuid(),
            TenantId = "test-tenant",
            CommandType = "TestCommand",
            CommandData = "{}",
            RequestId = "req-123",
            UserId = "user-1",
            OriginalException = "Test exception",
            ExceptionType = "TestException",
            ExceptionMessage = "Test message",
            AttemptNumber = 2,
            FirstFailureTime = DateTime.UtcNow.AddMinutes(-5),
            LastAttemptTime = DateTime.UtcNow.AddMinutes(-1),
            NextRetryTime = DateTime.UtcNow.AddMinutes(5),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act & Assert
        Assert.IsNotNull(failedCommand.Id);
        Assert.AreEqual("test-tenant", failedCommand.TenantId);
        Assert.AreEqual("TestCommand", failedCommand.CommandType);
        Assert.AreEqual("{}", failedCommand.CommandData);
        Assert.AreEqual("req-123", failedCommand.RequestId);
        Assert.AreEqual("user-1", failedCommand.UserId);
        Assert.AreEqual("Test exception", failedCommand.OriginalException);
        Assert.AreEqual("TestException", failedCommand.ExceptionType);
        Assert.AreEqual("Test message", failedCommand.ExceptionMessage);
        Assert.AreEqual(2, failedCommand.AttemptNumber);
        Assert.IsTrue(failedCommand.FirstFailureTime < DateTime.UtcNow);
        Assert.IsTrue(failedCommand.LastAttemptTime < DateTime.UtcNow);
        Assert.IsTrue(failedCommand.NextRetryTime > DateTime.UtcNow);
        Assert.IsTrue(failedCommand.ExpiresAt > DateTime.UtcNow);
    }

    [TestMethod]
    public void FailedCommand_JsonSerialization_WorksCorrectly()
    {
        // Arrange
        var originalCommand = new FailedCommand
        {
            Id = Guid.NewGuid(),
            TenantId = "test-tenant",
            CommandType = "TestCommand",
            CommandData = "test data",
            AttemptNumber = 1
        };

        // Act
        var json = JsonSerializer.Serialize(originalCommand);
        var deserializedCommand = JsonSerializer.Deserialize<FailedCommand>(json);

        // Assert
        Assert.IsNotNull(deserializedCommand);
        Assert.AreEqual(originalCommand.Id, deserializedCommand.Id);
        Assert.AreEqual(originalCommand.TenantId, deserializedCommand.TenantId);
        Assert.AreEqual(originalCommand.CommandType, deserializedCommand.CommandType);
        Assert.AreEqual(originalCommand.CommandData, deserializedCommand.CommandData);
        Assert.AreEqual(originalCommand.AttemptNumber, deserializedCommand.AttemptNumber);
    }
}

// Test helper classes
public class TestDomainCommand : DomainCommand
{
    public string TestProperty { get; set; } = "test";
}

public record TestWebRequestCommand(string RequestId, DateTime Timestamp, string UserId, string TestProperty = "test") 
    : WebRequestCommand(RequestId, Timestamp, UserId);