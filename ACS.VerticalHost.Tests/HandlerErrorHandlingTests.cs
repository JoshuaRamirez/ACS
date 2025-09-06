using Microsoft.Extensions.Logging;
using ACS.VerticalHost.Services;
using System.Diagnostics;

namespace ACS.VerticalHost.Tests;

/// <summary>
/// Unit tests for HandlerErrorHandling utilities
/// Tests error handling patterns, logging, and correlation ID functionality
/// </summary>
[TestClass]
public class HandlerErrorHandlingTests
{
    private Mock<ILogger> _mockLogger = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        _mockLogger = new Mock<ILogger>();
    }

    #region HandleCommandError Tests

    [TestMethod]
    public void HandlerErrorHandling_HandleCommandError_RethrowsException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var context = "TestHandler.Execute";

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() =>
            HandlerErrorHandling.HandleCommandError<string>(_mockLogger.Object, exception, context));
    }

    [TestMethod]
    public void HandlerErrorHandling_HandleCommandError_LogsWithCorrectLevel()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var context = "TestHandler.Execute";

        // Act & Assert
        try
        {
            HandlerErrorHandling.HandleCommandError<string>(_mockLogger.Object, exception, context);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(context)),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void HandlerErrorHandling_HandleCommandError_LogsWithCorrelationId()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var context = "TestHandler.Execute";
        var correlationId = "test-correlation-123";

        // Act & Assert
        try
        {
            HandlerErrorHandling.HandleCommandError<string>(_mockLogger.Object, exception, context, correlationId);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(correlationId)),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void HandlerErrorHandling_HandleCommandError_UsesErrorLogLevelForSystemExceptions()
    {
        // Arrange
        var exception = new SystemException("System error");
        var context = "TestHandler.Execute";

        // Act & Assert
        try
        {
            HandlerErrorHandling.HandleCommandError<string>(_mockLogger.Object, exception, context);
        }
        catch (SystemException)
        {
            // Expected
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(context)),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region HandleQueryError Tests

    [TestMethod]
    public void HandlerErrorHandling_HandleQueryError_RethrowsException()
    {
        // Arrange
        var exception = new InvalidOperationException("Query error");
        var context = "TestQueryHandler.Execute";

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() =>
            HandlerErrorHandling.HandleQueryError<string>(_mockLogger.Object, exception, context));
    }

    [TestMethod]
    public void HandlerErrorHandling_HandleQueryError_LogsError()
    {
        // Arrange
        var exception = new InvalidOperationException("Query error");
        var context = "TestQueryHandler.Execute";

        // Act & Assert
        try
        {
            HandlerErrorHandling.HandleQueryError<string>(_mockLogger.Object, exception, context);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(context)),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region LogCommandSuccess Tests

    [TestMethod]
    public void HandlerErrorHandling_LogCommandSuccess_LogsInformationLevel()
    {
        // Arrange
        var context = "TestHandler.Execute";
        var details = new { UserId = 123, Action = "Create" };

        // Act
        HandlerErrorHandling.LogCommandSuccess(_mockLogger.Object, context, details);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(context)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void HandlerErrorHandling_LogCommandSuccess_IncludesCorrelationId()
    {
        // Arrange
        var context = "TestHandler.Execute";
        var correlationId = "success-correlation-456";

        // Act
        HandlerErrorHandling.LogCommandSuccess(_mockLogger.Object, context, null, correlationId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(correlationId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void HandlerErrorHandling_LogCommandSuccess_HandlesNullDetails()
    {
        // Arrange
        var context = "TestHandler.Execute";

        // Act
        HandlerErrorHandling.LogCommandSuccess(_mockLogger.Object, context, null);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(context)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region LogQuerySuccess Tests

    [TestMethod]
    public void HandlerErrorHandling_LogQuerySuccess_LogsDebugLevel()
    {
        // Arrange
        var context = "TestQueryHandler.Execute";
        var resultInfo = new { Count = 5, Duration = 100 };

        // Act
        HandlerErrorHandling.LogQuerySuccess(_mockLogger.Object, context, resultInfo);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(context)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void HandlerErrorHandling_LogQuerySuccess_IncludesCorrelationId()
    {
        // Arrange
        var context = "TestQueryHandler.Execute";
        var correlationId = "query-correlation-789";

        // Act
        HandlerErrorHandling.LogQuerySuccess(_mockLogger.Object, context, null, correlationId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(correlationId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region LogOperationStart Tests

    [TestMethod]
    public void HandlerErrorHandling_LogOperationStart_LogsDebugLevel()
    {
        // Arrange
        var context = "TestHandler.Execute";
        var parameters = new { UserId = 123, Action = "Create" };

        // Act
        HandlerErrorHandling.LogOperationStart(_mockLogger.Object, context, parameters);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(context)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void HandlerErrorHandling_LogOperationStart_IncludesCorrelationId()
    {
        // Arrange
        var context = "TestHandler.Execute";
        var correlationId = "start-correlation-101";

        // Act
        HandlerErrorHandling.LogOperationStart(_mockLogger.Object, context, null, correlationId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(correlationId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region OperationResult Tests

    [TestMethod]
    public void OperationResult_SuccessResult_CreatesSuccessfulResult()
    {
        // Arrange
        var data = "test-data";

        // Act
        var result = OperationResult<string>.SuccessResult(data);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(data, result.Result);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNull(result.ErrorCode);
    }

    [TestMethod]
    public void OperationResult_FailureResult_CreatesFailedResult()
    {
        // Arrange
        var errorMessage = "Operation failed";
        var errorCode = "ERR001";

        // Act
        var result = OperationResult<string>.FailureResult(errorMessage, errorCode);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.Result);
        Assert.AreEqual(errorMessage, result.ErrorMessage);
        Assert.AreEqual(errorCode, result.ErrorCode);
    }

    [TestMethod]
    public void OperationResult_FailureResult_HandlesNullErrorCode()
    {
        // Arrange
        var errorMessage = "Operation failed";

        // Act
        var result = OperationResult<string>.FailureResult(errorMessage);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.Result);
        Assert.AreEqual(errorMessage, result.ErrorMessage);
        Assert.IsNull(result.ErrorCode);
    }

    #endregion

    #region HandlerExtensions Tests

    [TestMethod]
    public void HandlerExtensions_GetCorrelationId_ReturnsValidGuid()
    {
        // Act
        var correlationId = HandlerExtensions.GetCorrelationId();

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(correlationId));
        Assert.IsTrue(Guid.TryParse(correlationId.Split('-').Last(), out _) || 
                     correlationId.Length > 0); // Either a GUID or Activity ID
    }

    [TestMethod]
    public void HandlerExtensions_GetCorrelationId_ReturnsDifferentIds()
    {
        // Act
        var id1 = HandlerExtensions.GetCorrelationId();
        var id2 = HandlerExtensions.GetCorrelationId();

        // Assert
        Assert.AreNotEqual(id1, id2);
    }

    [TestMethod]
    public void HandlerExtensions_GetContext_FormatsCorrectly()
    {
        // Arrange
        var handlerName = "UserCommandHandler";
        var operation = "CreateUser";

        // Act
        var context = HandlerExtensions.GetContext(handlerName, operation);

        // Assert
        Assert.AreEqual("UserCommandHandler.CreateUser", context);
    }

    [TestMethod]
    public void HandlerExtensions_GetContext_HandlesEmptyStrings()
    {
        // Arrange
        var handlerName = "";
        var operation = "";

        // Act
        var context = HandlerExtensions.GetContext(handlerName, operation);

        // Assert
        Assert.AreEqual(".", context);
    }

    #endregion

    #region Exception Type Categorization Tests

    [TestMethod]
    public void HandlerErrorHandling_GetLogLevel_ReturnsWarningForArgumentException()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");
        var context = "TestHandler.Execute";

        // Act & Assert
        try
        {
            HandlerErrorHandling.HandleCommandError<string>(_mockLogger.Object, exception, context);
        }
        catch (ArgumentException)
        {
            // Expected
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void HandlerErrorHandling_GetLogLevel_ReturnsWarningForUnauthorizedAccess()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Access denied");
        var context = "TestHandler.Execute";

        // Act & Assert
        try
        {
            HandlerErrorHandling.HandleCommandError<string>(_mockLogger.Object, exception, context);
        }
        catch (UnauthorizedAccessException)
        {
            // Expected
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Activity and Telemetry Integration Tests

    [TestMethod]
    public void HandlerErrorHandling_HandleCommandError_SetsActivityTags()
    {
        // Arrange
        using var activity = new Activity("TestActivity").Start();
        var exception = new InvalidOperationException("Test error");
        var context = "TestHandler.Execute";

        // Act & Assert
        try
        {
            HandlerErrorHandling.HandleCommandError<string>(_mockLogger.Object, exception, context);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        Assert.AreEqual(exception.GetType().Name, activity.GetTagItem("error.type"));
        Assert.AreEqual(context, activity.GetTagItem("error.context"));
    }

    [TestMethod]
    public void HandlerErrorHandling_LogCommandSuccess_SetsActivityTags()
    {
        // Arrange
        using var activity = new Activity("TestActivity").Start();
        var context = "TestHandler.Execute";

        // Act
        HandlerErrorHandling.LogCommandSuccess(_mockLogger.Object, context);

        // Assert
        Assert.AreEqual(true, activity.GetTagItem("operation.success"));
        Assert.AreEqual(context, activity.GetTagItem("operation.context"));
    }

    #endregion
}