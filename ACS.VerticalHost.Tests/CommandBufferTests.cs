using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ACS.VerticalHost.Services;
using System.Diagnostics;

namespace ACS.VerticalHost.Tests;

/// <summary>
/// Unit tests for CommandBuffer implementation
/// Tests command/query processing, performance, and error handling
/// </summary>
[TestClass]
public class CommandBufferTests
{
    private Mock<ILogger<CommandBuffer>> _mockLogger = null!;
    private Mock<IServiceProvider> _mockServiceProvider = null!;
    private Mock<IServiceScope> _mockServiceScope = null!;
    private Mock<IServiceScopeFactory> _mockServiceScopeFactory = null!;
    private CommandBuffer _commandBuffer = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        _mockLogger = new Mock<ILogger<CommandBuffer>>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

        // CreateScope() is an extension method on IServiceProvider that internally calls
        // GetService(typeof(IServiceScopeFactory)). Extension methods cannot be mocked directly.
        // Instead, we mock the IServiceScopeFactory that the extension method retrieves.
        _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);
        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(_mockServiceScopeFactory.Object);

        _commandBuffer = new CommandBuffer(_mockLogger.Object, _mockServiceProvider.Object);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        try
        {
            // First try to stop gracefully
            await _commandBuffer.StopAsync();
        }
        catch (Exception)
        {
            // Ignore errors during stop - buffer might not have been started
        }

        try
        {
            _commandBuffer?.Dispose();
        }
        catch (Exception)
        {
            // Ignore disposal exceptions
        }
    }

    #region Startup and Shutdown Tests

    [TestMethod]
    public async Task CommandBuffer_StartAsync_LogsStartupInformation()
    {
        // Act
        await _commandBuffer.StartAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting CommandBuffer processing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CommandBuffer_StartAsync_CompletesSuccessfully()
    {
        // Act
        var task = _commandBuffer.StartAsync();

        // Assert
        Assert.IsTrue(task.IsCompletedSuccessfully);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CommandBuffer started successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CommandBuffer_StopAsync_LogsShutdownInformation()
    {
        // Arrange
        await _commandBuffer.StartAsync();

        // Act
        await _commandBuffer.StopAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping CommandBuffer processing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CommandBuffer_StopAsync_LogsCompletionInformation()
    {
        // Arrange
        await _commandBuffer.StartAsync();

        // Act
        await _commandBuffer.StopAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CommandBuffer stopped successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Query Processing Tests

    [TestMethod]
    public async Task CommandBuffer_ExecuteQueryAsync_ExecutesImmediately()
    {
        // Arrange
        var query = new TestQuery { Value = "test" };
        var expectedResponse = new TestQueryResponse { Result = "test-result" };
        var mockHandler = new Mock<IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>>();

        mockHandler.Setup(x => x.HandleAsync(It.IsAny<IQuery<TestQueryResponse>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(expectedResponse);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>)))
                           .Returns(mockHandler.Object);

        // Act
        var result = await _commandBuffer.ExecuteQueryAsync(query);

        // Assert
        Assert.AreEqual(expectedResponse, result);
        mockHandler.Verify(x => x.HandleAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CommandBuffer_ExecuteQueryAsync_IncrementsQueryCounter()
    {
        // Arrange
        var query = new TestQuery { Value = "test" };
        var expectedResponse = new TestQueryResponse { Result = "test-result" };
        var mockHandler = new Mock<IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>>();

        mockHandler.Setup(x => x.HandleAsync(It.IsAny<IQuery<TestQueryResponse>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(expectedResponse);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>)))
                           .Returns(mockHandler.Object);

        // Act
        await _commandBuffer.ExecuteQueryAsync(query);
        var stats = _commandBuffer.GetStats();

        // Assert
        Assert.AreEqual(1, stats.QueriesProcessed);
        Assert.AreEqual(0, stats.CommandsProcessed);
    }

    [TestMethod]
    public async Task CommandBuffer_ExecuteQueryAsync_LogsDebugInformation()
    {
        // Arrange
        var query = new TestQuery { Value = "test" };
        var expectedResponse = new TestQueryResponse { Result = "test-result" };
        var mockHandler = new Mock<IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>>();

        mockHandler.Setup(x => x.HandleAsync(It.IsAny<IQuery<TestQueryResponse>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(expectedResponse);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>)))
                           .Returns(mockHandler.Object);

        // Act
        await _commandBuffer.ExecuteQueryAsync(query);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Query TestQuery executed in")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CommandBuffer_ExecuteQueryAsync_ThrowsWhenHandlerNotFound()
    {
        // Arrange
        var query = new TestQuery { Value = "test" };

        _mockServiceProvider.Setup(x => x.GetService(typeof(IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>)))
                           .Returns((object?)null);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _commandBuffer.ExecuteQueryAsync(query));
    }

    [TestMethod]
    public async Task CommandBuffer_ExecuteQueryAsync_PropagatesHandlerException()
    {
        // Arrange
        var query = new TestQuery { Value = "test" };
        var exception = new InvalidOperationException("Handler error");
        var mockHandler = new Mock<IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>>();

        mockHandler.Setup(x => x.HandleAsync(It.IsAny<IQuery<TestQueryResponse>>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(exception);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>)))
                           .Returns(mockHandler.Object);

        // Act & Assert
        var thrownException = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _commandBuffer.ExecuteQueryAsync(query));

        Assert.AreEqual(exception, thrownException);
    }

    #endregion

    #region Command Processing Tests

    [TestMethod]
    public async Task CommandBuffer_ExecuteCommandAsync_VoidCommand_CompletesSuccessfully()
    {
        // Arrange
        var command = new TestVoidCommand { Value = "test" };
        var mockHandler = new Mock<ICommandHandler<TestVoidCommand>>();

        mockHandler.Setup(x => x.HandleAsync(It.IsAny<TestVoidCommand>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        _mockServiceProvider.Setup(x => x.GetService(typeof(ICommandHandler<TestVoidCommand>)))
                           .Returns(mockHandler.Object);

        await _commandBuffer.StartAsync();

        // Act
        await _commandBuffer.ExecuteCommandAsync(command);

        // Assert
        // If we get here without exception, the command was processed successfully
        Assert.IsTrue(true);
    }

    [TestMethod]
    public async Task CommandBuffer_ExecuteCommandAsync_CommandWithResponse_ReturnsResult()
    {
        // Arrange
        var command = new TestCommandWithResponse { Value = "test" };
        var expectedResponse = new TestCommandResponse { Result = "test-result" };
        var mockHandler = new Mock<ICommandHandler<TestCommandWithResponse, TestCommandResponse>>();

        mockHandler.Setup(x => x.HandleAsync(It.IsAny<TestCommandWithResponse>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(expectedResponse);

        _mockServiceProvider.Setup(x => x.GetService(typeof(ICommandHandler<TestCommandWithResponse, TestCommandResponse>)))
                           .Returns(mockHandler.Object);

        await _commandBuffer.StartAsync();

        // Act
        var result = await _commandBuffer.ExecuteCommandAsync(command);

        // Assert
        Assert.AreEqual(expectedResponse, result);
    }

    #endregion

    #region Performance Statistics Tests

    [TestMethod]
    public void CommandBuffer_GetStats_InitiallyZero()
    {
        // Act
        var stats = _commandBuffer.GetStats();

        // Assert
        Assert.AreEqual(0, stats.CommandsProcessed);
        Assert.AreEqual(0, stats.QueriesProcessed);
        Assert.AreEqual(0, stats.CommandsInFlight);
        Assert.IsTrue(stats.UptimeSeconds >= 0);
        Assert.AreEqual(10000, stats.ChannelCapacity);
    }

    [TestMethod]
    public async Task CommandBuffer_GetStats_TracksUptime()
    {
        // Arrange
        var initialStats = _commandBuffer.GetStats();
        
        // Act
        await Task.Delay(100); // Small delay
        var laterStats = _commandBuffer.GetStats();

        // Assert
        Assert.IsTrue(laterStats.UptimeSeconds > initialStats.UptimeSeconds);
    }

    [TestMethod]
    public async Task CommandBuffer_GetStats_CalculatesQueriesPerSecond()
    {
        // Arrange
        var query = new TestQuery { Value = "test" };
        var expectedResponse = new TestQueryResponse { Result = "test-result" };
        var mockHandler = new Mock<IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>>();

        mockHandler.Setup(x => x.HandleAsync(It.IsAny<IQuery<TestQueryResponse>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(expectedResponse);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>)))
                           .Returns(mockHandler.Object);

        // Act
        await _commandBuffer.ExecuteQueryAsync(query);
        await Task.Delay(100); // Ensure some time passes
        var stats = _commandBuffer.GetStats();

        // Assert
        Assert.IsTrue(stats.QueriesPerSecond > 0);
        Assert.AreEqual(1, stats.QueriesProcessed);
    }

    [TestMethod]
    public void CommandBuffer_GetStats_ReturnsEmptyErrorsInitially()
    {
        // Act
        var stats = _commandBuffer.GetStats();

        // Assert
        Assert.AreEqual(0, stats.RecentErrors.Count);
    }

    #endregion

    #region Error Handling and Recovery Tests

    [TestMethod]
    public async Task CommandBuffer_ExecuteQueryAsync_LogsErrorOnException()
    {
        // Arrange
        var query = new TestQuery { Value = "test" };
        var exception = new InvalidOperationException("Handler error");
        var mockHandler = new Mock<IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>>();

        mockHandler.Setup(x => x.HandleAsync(It.IsAny<IQuery<TestQueryResponse>>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(exception);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>)))
                           .Returns(mockHandler.Object);

        // Act
        try
        {
            await _commandBuffer.ExecuteQueryAsync(query);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error executing query TestQuery")),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Thread Safety and Concurrency Tests

    [TestMethod]
    public async Task CommandBuffer_ExecuteQueryAsync_HandlesConcurrentQueries()
    {
        // Arrange
        var queries = Enumerable.Range(0, 10)
            .Select(i => new TestQuery { Value = $"test-{i}" })
            .ToArray();

        var expectedResponse = new TestQueryResponse { Result = "test-result" };
        var mockHandler = new Mock<IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>>();

        mockHandler.Setup(x => x.HandleAsync(It.IsAny<IQuery<TestQueryResponse>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(expectedResponse);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IQueryHandler<IQuery<TestQueryResponse>, TestQueryResponse>)))
                           .Returns(mockHandler.Object);

        // Act
        var tasks = queries.Select(q => _commandBuffer.ExecuteQueryAsync(q)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.AreEqual(10, results.Length);
        Assert.IsTrue(results.All(r => r == expectedResponse));
        
        var stats = _commandBuffer.GetStats();
        Assert.AreEqual(10, stats.QueriesProcessed);
    }

    #endregion

    #region Disposal Tests

    [TestMethod]
    public void CommandBuffer_Dispose_CompletesWithoutException()
    {
        // Create a fresh buffer for this test to avoid interference with TestCleanup
        var mockLogger = new Mock<ILogger<CommandBuffer>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

        mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(mockServiceScope.Object);
        mockServiceScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(mockServiceScopeFactory.Object);

        var buffer = new CommandBuffer(mockLogger.Object, mockServiceProvider.Object);

        // Act & Assert
        buffer.Dispose(); // Should not throw
        Assert.IsTrue(true);
    }

    #endregion
}

#region Test Helper Classes

public class TestQuery : IQuery<TestQueryResponse>
{
    public string Value { get; set; } = string.Empty;
}

public class TestQueryResponse
{
    public string Result { get; set; } = string.Empty;
}

public class TestVoidCommand : ICommand
{
    public string Value { get; set; } = string.Empty;
}

public class TestCommandWithResponse : ICommand<TestCommandResponse>
{
    public string Value { get; set; } = string.Empty;
}

public class TestCommandResponse
{
    public string Result { get; set; } = string.Empty;
}

#endregion