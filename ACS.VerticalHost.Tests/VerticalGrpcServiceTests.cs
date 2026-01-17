using Grpc.Core;
using Google.Protobuf;
using ACS.Core.Grpc;
using ACS.VerticalHost.Services;
using ACS.Service.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ACS.VerticalHost.Tests;

/// <summary>
/// Unit tests for VerticalGrpcService
/// Tests gRPC service operations, CQRS routing, and error handling
/// </summary>
[TestClass]
public class VerticalGrpcServiceTests
{
    private Mock<ICommandBuffer> _mockCommandBuffer = null!;
    private Mock<TenantConfiguration> _mockTenantConfig = null!;
    private Mock<ILogger<VerticalGrpcService>> _mockLogger = null!;
    private VerticalGrpcService _grpcService = null!;
    private Mock<ServerCallContext> _mockCallContext = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        _mockCommandBuffer = new Mock<ICommandBuffer>();
        _mockTenantConfig = new Mock<TenantConfiguration>();
        _mockLogger = new Mock<ILogger<VerticalGrpcService>>();
        _mockCallContext = new Mock<ServerCallContext>();

        _mockTenantConfig.SetupGet(x => x.TenantId).Returns("test-tenant");

        _grpcService = new VerticalGrpcService(
            _mockCommandBuffer.Object,
            _mockTenantConfig.Object,
            _mockLogger.Object);
    }

    #region HealthCheck Tests

    [TestMethod]
    public async Task VerticalGrpcService_HealthCheck_ReturnsHealthyResponse()
    {
        // Arrange
        var request = new HealthRequest();

        // Act
        var response = await _grpcService.HealthCheck(request, _mockCallContext.Object);

        // Assert
        Assert.IsTrue(response.Healthy);
        Assert.IsTrue(response.UptimeSeconds >= 0);
        Assert.AreEqual(1, response.ActiveConnections);
        Assert.AreEqual(0, response.CommandsProcessed);
    }

    [TestMethod]
    public async Task VerticalGrpcService_HealthCheck_LogsDebugInformation()
    {
        // Arrange
        var request = new HealthRequest();

        // Act
        await _grpcService.HealthCheck(request, _mockCallContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Health check")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task VerticalGrpcService_HealthCheck_IncrementsUptimeOnSubsequentCalls()
    {
        // Arrange
        var request = new HealthRequest();

        // Act
        var response1 = await _grpcService.HealthCheck(request, _mockCallContext.Object);
        await Task.Delay(100); // Small delay
        var response2 = await _grpcService.HealthCheck(request, _mockCallContext.Object);

        // Assert
        Assert.IsTrue(response2.UptimeSeconds >= response1.UptimeSeconds);
    }

    #endregion

    #region ExecuteCommand Tests - Success Scenarios

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_VoidCommand_ReturnsSuccessResponse()
    {
        // Arrange
        var commandType = typeof(TestVoidCommand).AssemblyQualifiedName!;
        var command = new TestVoidCommand { Value = "test" };
        var serializedCommand = SerializeCommand(command);

        var request = new CommandRequest
        {
            CommandType = commandType,
            CommandData = ByteString.CopyFrom(serializedCommand),
            CorrelationId = "test-correlation-123"
        };

        _mockCommandBuffer.Setup(x => x.ExecuteCommandAsync(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
                         .Returns(Task.CompletedTask);

        // Act
        var response = await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        Assert.IsTrue(response.Success);
        Assert.AreEqual("test-correlation-123", response.CorrelationId);
        Assert.IsTrue(string.IsNullOrEmpty(response.ErrorMessage));
    }

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_CommandWithResult_ReturnsResponseWithData()
    {
        // Arrange
        var commandType = typeof(TestCommandWithResponse).AssemblyQualifiedName!;
        var command = new TestCommandWithResponse { Value = "test" };
        var serializedCommand = SerializeCommand(command);
        var expectedResult = new TestCommandResponse { Result = "test-result" };

        var request = new CommandRequest
        {
            CommandType = commandType,
            CommandData = ByteString.CopyFrom(serializedCommand),
            CorrelationId = "test-correlation-456"
        };

        _mockCommandBuffer.Setup(x => x.ExecuteCommandAsync(It.IsAny<ICommand<TestCommandResponse>>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(expectedResult);

        // Act
        var response = await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        Assert.IsTrue(response.Success);
        Assert.AreEqual("test-correlation-456", response.CorrelationId);
        Assert.IsTrue(response.ResultData.Length > 0);
    }

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_Query_ReturnsResponseWithData()
    {
        // Arrange
        var queryType = typeof(TestQuery).AssemblyQualifiedName!;
        var query = new TestQuery { Value = "test" };
        var serializedQuery = SerializeCommand(query);
        var expectedResult = new TestQueryResponse { Result = "query-result" };

        var request = new CommandRequest
        {
            CommandType = queryType,
            CommandData = ByteString.CopyFrom(serializedQuery),
            CorrelationId = "test-correlation-789"
        };

        _mockCommandBuffer.Setup(x => x.ExecuteQueryAsync(It.IsAny<IQuery<TestQueryResponse>>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(expectedResult);

        // Act
        var response = await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        Assert.IsTrue(response.Success);
        Assert.AreEqual("test-correlation-789", response.CorrelationId);
        Assert.IsTrue(response.ResultData.Length > 0);
    }

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_LogsSuccessfulExecution()
    {
        // Arrange
        var commandType = typeof(TestVoidCommand).AssemblyQualifiedName!;
        var command = new TestVoidCommand { Value = "test" };
        var serializedCommand = SerializeCommand(command);

        var request = new CommandRequest
        {
            CommandType = commandType,
            CommandData = ByteString.CopyFrom(serializedCommand),
            CorrelationId = "test-correlation-123"
        };

        _mockCommandBuffer.Setup(x => x.ExecuteCommandAsync(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
                         .Returns(Task.CompletedTask);

        // Act
        await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("executed successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region ExecuteCommand Tests - Error Scenarios

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_UnknownCommandType_ReturnsErrorResponse()
    {
        // Arrange
        var request = new CommandRequest
        {
            CommandType = "NonExistent.Command.Type",
            CommandData = ByteString.CopyFrom(new byte[] { 1, 2, 3 }),
            CorrelationId = "error-correlation-123"
        };

        // Act
        var response = await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        Assert.IsFalse(response.Success);
        Assert.AreEqual("error-correlation-123", response.CorrelationId);
        Assert.IsTrue(response.ErrorMessage.Contains("Unknown command type"));
    }

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_DeserializationFailure_ReturnsErrorResponse()
    {
        // Arrange
        var commandType = typeof(TestVoidCommand).AssemblyQualifiedName!;
        // Use bytes that start with 0xFF (JSON marker) followed by invalid JSON
        // This will trigger JSON deserialization which will fail
        var request = new CommandRequest
        {
            CommandType = commandType,
            CommandData = ByteString.CopyFrom(new byte[] { 0xFF, (byte)'{', (byte)'{' }), // Invalid JSON data
            CorrelationId = "deserialization-error"
        };

        // Act
        var response = await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        Assert.IsFalse(response.Success);
        Assert.AreEqual("deserialization-error", response.CorrelationId);
        // The error could be "Failed to deserialize" or contain JSON-related error message
        // or contain "invalid" since JSON parsing can fail with "invalid start of value"
        Assert.IsTrue(
            response.ErrorMessage.Contains("Failed to deserialize") ||
            response.ErrorMessage.Contains("JSON") ||
            response.ErrorMessage.Contains("deserialize") ||
            response.ErrorMessage.Contains("invalid") ||
            response.ErrorMessage.Contains("does not implement"),
            $"Unexpected error message: {response.ErrorMessage}");
    }

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_CommandBufferException_ReturnsErrorResponse()
    {
        // Arrange
        var commandType = typeof(TestVoidCommand).AssemblyQualifiedName!;
        var command = new TestVoidCommand { Value = "test" };
        var serializedCommand = SerializeCommand(command);

        var request = new CommandRequest
        {
            CommandType = commandType,
            CommandData = ByteString.CopyFrom(serializedCommand),
            CorrelationId = "buffer-error"
        };

        var exception = new InvalidOperationException("Command buffer error");
        _mockCommandBuffer.Setup(x => x.ExecuteCommandAsync(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
                         .ThrowsAsync(exception);

        // Act
        var response = await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        Assert.IsFalse(response.Success);
        Assert.AreEqual("buffer-error", response.CorrelationId);
        Assert.AreEqual(exception.Message, response.ErrorMessage);
    }

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_LogsErrorOnException()
    {
        // Arrange
        var commandType = typeof(TestVoidCommand).AssemblyQualifiedName!;
        var command = new TestVoidCommand { Value = "test" };
        var serializedCommand = SerializeCommand(command);

        var request = new CommandRequest
        {
            CommandType = commandType,
            CommandData = ByteString.CopyFrom(serializedCommand),
            CorrelationId = "logging-test"
        };

        var exception = new InvalidOperationException("Test exception");
        _mockCommandBuffer.Setup(x => x.ExecuteCommandAsync(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
                         .ThrowsAsync(exception);

        // Act
        await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error executing command")),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_UnsupportedCommandInterface_ReturnsErrorResponse()
    {
        // Arrange
        var commandType = typeof(UnsupportedCommand).AssemblyQualifiedName!;
        var command = new UnsupportedCommand { Value = "test" };
        var serializedCommand = SerializeCommand(command);

        var request = new CommandRequest
        {
            CommandType = commandType,
            CommandData = ByteString.CopyFrom(serializedCommand),
            CorrelationId = "unsupported-command"
        };

        // Act
        var response = await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        Assert.IsFalse(response.Success);
        Assert.AreEqual("unsupported-command", response.CorrelationId);
        Assert.IsTrue(response.ErrorMessage.Contains("does not implement a recognized CQRS interface"));
    }

    #endregion

    #region Command Buffer Interaction Tests

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_VoidCommand_CallsCommandBufferCorrectly()
    {
        // Arrange
        var commandType = typeof(TestVoidCommand).AssemblyQualifiedName!;
        var command = new TestVoidCommand { Value = "test" };
        var serializedCommand = SerializeCommand(command);

        var request = new CommandRequest
        {
            CommandType = commandType,
            CommandData = ByteString.CopyFrom(serializedCommand),
            CorrelationId = "buffer-test"
        };

        _mockCommandBuffer.Setup(x => x.ExecuteCommandAsync(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
                         .Returns(Task.CompletedTask);

        // Act
        await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        _mockCommandBuffer.Verify(
            x => x.ExecuteCommandAsync(It.IsAny<TestVoidCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task VerticalGrpcService_ExecuteCommand_Query_CallsCommandBufferQueryMethod()
    {
        // Arrange
        var queryType = typeof(TestQuery).AssemblyQualifiedName!;
        var query = new TestQuery { Value = "test" };
        var serializedQuery = SerializeCommand(query);
        var expectedResult = new TestQueryResponse { Result = "result" };

        var request = new CommandRequest
        {
            CommandType = queryType,
            CommandData = ByteString.CopyFrom(serializedQuery),
            CorrelationId = "query-buffer-test"
        };

        _mockCommandBuffer.Setup(x => x.ExecuteQueryAsync(It.IsAny<IQuery<TestQueryResponse>>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(expectedResult);

        // Act
        await _grpcService.ExecuteCommand(request, _mockCallContext.Object);

        // Assert
        _mockCommandBuffer.Verify(
            x => x.ExecuteQueryAsync(It.IsAny<TestQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static byte[] SerializeCommand(object command)
    {
        // Simple serialization for testing - in real implementation this would use ProtoSerializer
        var json = System.Text.Json.JsonSerializer.Serialize(command);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    #endregion
}

#region Test Helper Classes

public class UnsupportedCommand
{
    public string Value { get; set; } = string.Empty;
}

#endregion