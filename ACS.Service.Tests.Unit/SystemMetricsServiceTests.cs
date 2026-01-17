using ACS.Service.Data;
using ACS.Service.Infrastructure;
using ACS.Service.Services;
using ACS.Service.Requests;
using ACS.Service.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Service.Tests.Unit;

/// <summary>
/// Unit tests for SystemMetricsService
/// Tests system metrics collection, diagnostics, and error handling
/// </summary>
[TestClass]
public class SystemMetricsServiceTests
{
    private InMemoryEntityGraph _entityGraph = null!;
    private ApplicationDbContext _dbContext = null!;
    private Mock<ILogger<SystemMetricsService>> _mockLogger = null!;
    private Mock<ILogger<InMemoryEntityGraph>> _mockEntityGraphLogger = null!;
    private SystemMetricsService _systemMetricsService = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _mockEntityGraphLogger = new Mock<ILogger<InMemoryEntityGraph>>();
        _entityGraph = new InMemoryEntityGraph(_dbContext, _mockEntityGraphLogger.Object);
        _mockLogger = new Mock<ILogger<SystemMetricsService>>();

        _systemMetricsService = new SystemMetricsService(
            _entityGraph,
            _dbContext,
            _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext?.Dispose();
    }

    #region GetSystemOverviewAsync Tests

    [TestMethod]
    public async Task SystemMetricsService_GetSystemOverviewAsync_ReturnsSuccessfulResponse()
    {
        // Arrange
        var request = new SystemOverviewRequest { TenantId = "test-tenant" };

        // Act
        var result = await _systemMetricsService.GetSystemOverviewAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual("System overview retrieved successfully", result.Message);
        Assert.IsNotNull(result.Data);
    }

    [TestMethod]
    public async Task SystemMetricsService_GetSystemOverviewAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var request = new SystemOverviewRequest { TenantId = "test-tenant" };
        
        // Add test data
        _dbContext.Users.Add(new Data.Models.User { Name = "User1", Id = 1 });
        _dbContext.Users.Add(new Data.Models.User { Name = "User2", Id = 2 });
        _dbContext.Groups.Add(new Data.Models.Group { Name = "Group1", Id = 3 });
        _dbContext.Roles.Add(new Data.Models.Role { Name = "Role1", Id = 4 });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _systemMetricsService.GetSystemOverviewAsync(request);

        // Assert
        Assert.AreEqual(2, result.Data!.UsersCount);
        Assert.AreEqual(1, result.Data.GroupsCount);
        Assert.AreEqual(1, result.Data.RolesCount);
    }

    [TestMethod]
    public async Task SystemMetricsService_GetSystemOverviewAsync_SetsHealthyStatus()
    {
        // Arrange
        var request = new SystemOverviewRequest { TenantId = "test-tenant" };

        // Act
        var result = await _systemMetricsService.GetSystemOverviewAsync(request);

        // Assert
        Assert.AreEqual("Healthy", result.Data!.Status);
    }

    [TestMethod]
    public async Task SystemMetricsService_GetSystemOverviewAsync_SetsTimestamp()
    {
        // Arrange
        var request = new SystemOverviewRequest { TenantId = "test-tenant" };
        var beforeCall = DateTime.UtcNow;

        // Act
        var result = await _systemMetricsService.GetSystemOverviewAsync(request);
        var afterCall = DateTime.UtcNow;

        // Assert
        Assert.IsTrue(result.Data!.Timestamp >= beforeCall);
        Assert.IsTrue(result.Data.Timestamp <= afterCall);
    }

    [TestMethod]
    public async Task SystemMetricsService_GetSystemOverviewAsync_LogsInformation()
    {
        // Arrange
        var request = new SystemOverviewRequest { TenantId = "test-tenant" };

        // Act
        await _systemMetricsService.GetSystemOverviewAsync(request);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting system overview")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region GetMigrationHistoryAsync Tests

    [TestMethod]
    public async Task SystemMetricsService_GetMigrationHistoryAsync_ReturnsFailureForInMemoryDb()
    {
        // Arrange
        var request = new MigrationHistoryRequest { TenantId = "test-tenant" };

        // Act
        var result = await _systemMetricsService.GetMigrationHistoryAsync(request);

        // Assert
        // In-memory database does not support GetAppliedMigrationsAsync, so the service
        // catches the exception and returns a failure response
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Failed to retrieve migration history", result.Message);
        Assert.IsNotNull(result.Errors);
        Assert.IsTrue(result.Errors.Count > 0);
    }

    [TestMethod]
    public async Task SystemMetricsService_GetMigrationHistoryAsync_LogsErrorForInMemoryDb()
    {
        // Arrange
        var request = new MigrationHistoryRequest { TenantId = "test-tenant" };

        // Act
        await _systemMetricsService.GetMigrationHistoryAsync(request);

        // Assert
        // Verify that an error was logged when the migration API fails
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting migration history")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region GetSystemDiagnosticsAsync Tests

    [TestMethod]
    public async Task SystemMetricsService_GetSystemDiagnosticsAsync_ReturnsSuccessfulResponse()
    {
        // Arrange
        var request = new SystemDiagnosticsRequest { TenantId = "test-tenant" };

        // Act
        var result = await _systemMetricsService.GetSystemDiagnosticsAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual("System diagnostics retrieved successfully", result.Message);
        Assert.IsNotNull(result.Data);
    }

    [TestMethod]
    public async Task SystemMetricsService_GetSystemDiagnosticsAsync_PopulatesSystemInfo()
    {
        // Arrange
        var request = new SystemDiagnosticsRequest { TenantId = "test-tenant" };

        // Act
        var result = await _systemMetricsService.GetSystemDiagnosticsAsync(request);

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(result.Data!.MachineName));
        Assert.IsFalse(string.IsNullOrEmpty(result.Data.ProcessId));
        Assert.IsTrue(result.Data.WorkingSetMemory > 0);
        Assert.IsFalse(string.IsNullOrEmpty(result.Data.Version));
    }

    [TestMethod]
    public async Task SystemMetricsService_GetSystemDiagnosticsAsync_SetsStartTime()
    {
        // Arrange
        var request = new SystemDiagnosticsRequest { TenantId = "test-tenant" };

        // Act
        var result = await _systemMetricsService.GetSystemDiagnosticsAsync(request);

        // Assert
        Assert.IsTrue(result.Data!.StartTime <= DateTime.UtcNow);
        Assert.IsTrue(result.Data.StartTime > DateTime.UtcNow.AddHours(-1)); // Should be recent
    }

    [TestMethod]
    public async Task SystemMetricsService_GetSystemDiagnosticsAsync_LogsInformation()
    {
        // Arrange
        var request = new SystemDiagnosticsRequest { TenantId = "test-tenant" };

        // Act
        await _systemMetricsService.GetSystemDiagnosticsAsync(request);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting system diagnostics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Performance and Logging Tests

    [TestMethod]
    public async Task SystemMetricsService_GetSystemOverviewAsync_LogsDebugWithPerformanceInfo()
    {
        // Arrange
        var request = new SystemOverviewRequest { TenantId = "test-tenant" };

        // Act
        await _systemMetricsService.GetSystemOverviewAsync(request);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("System overview retrieved in")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SystemMetricsService_GetSystemDiagnosticsAsync_LogsDebugWithPerformanceInfo()
    {
        // Arrange
        var request = new SystemDiagnosticsRequest { TenantId = "test-tenant" };

        // Act
        await _systemMetricsService.GetSystemDiagnosticsAsync(request);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("System diagnostics retrieved in")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Edge Cases and Error Scenarios

    [TestMethod]
    public async Task SystemMetricsService_GetSystemOverviewAsync_HandlesZeroCounts()
    {
        // Arrange
        var request = new SystemOverviewRequest { TenantId = "test-tenant" };

        // Act
        var result = await _systemMetricsService.GetSystemOverviewAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.Data!.UsersCount);
        Assert.AreEqual(0, result.Data.GroupsCount);
        Assert.AreEqual(0, result.Data.RolesCount);
    }

    [TestMethod]
    public void SystemMetricsService_Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new ApplicationDbContext(options);
        var entityGraphLogger = new Mock<ILogger<InMemoryEntityGraph>>();
        var entityGraph = new InMemoryEntityGraph(dbContext, entityGraphLogger.Object);
        var mockLogger = new Mock<ILogger<SystemMetricsService>>();

        // Act
        var service = new SystemMetricsService(entityGraph, dbContext, mockLogger.Object);

        // Assert
        Assert.IsNotNull(service);

        // Cleanup
        dbContext.Dispose();
    }

    #endregion
}