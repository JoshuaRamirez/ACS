using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class AuditServiceTests
{
    private Mock<ApplicationDbContext> _mockDbContext = null!;
    private Mock<ILogger<AuditService>> _mockLogger = null!;
    private AuditService _auditService = null!;
    private Mock<DbSet<AuditLog>> _mockAuditLogDbSet = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _mockDbContext = new Mock<ApplicationDbContext>(options);
        _mockLogger = new Mock<ILogger<AuditService>>();
        
        _mockAuditLogDbSet = new Mock<DbSet<AuditLog>>();
        
        _mockDbContext.Setup(x => x.AuditLogs).Returns(_mockAuditLogDbSet.Object);
        
        _auditService = new AuditService(
            _mockDbContext.Object,
            _mockLogger.Object);
    }

    #region LogAsync Tests

    [TestMethod]
    public async Task AuditService_LogAsync_CreatesAuditLog()
    {
        // Arrange
        var action = "CREATE";
        var entityType = "User";
        var entityId = 1;
        var performedBy = "TestUser";
        var details = "Created new user";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        // Act
        await _auditService.LogAsync(action, entityType, entityId, performedBy, details);

        // Assert
        _mockAuditLogDbSet.Verify(x => x.Add(It.Is<AuditLog>(al => 
            al.ChangeType == action &&
            al.EntityType == entityType &&
            al.EntityId == entityId &&
            al.ChangedBy == performedBy &&
            al.ChangeDetails == details)), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task AuditService_LogAsync_LogsInformation()
    {
        // Arrange
        var action = "CREATE";
        var entityType = "User";
        var entityId = 1;
        var performedBy = "TestUser";
        var details = "Created new user";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _auditService.LogAsync(action, entityType, entityId, performedBy, details);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Audit log created")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region LogSecurityEventAsync Tests

    [TestMethod]
    public async Task AuditService_LogSecurityEventAsync_LogsSecurityEvent()
    {
        // Arrange
        var eventType = "UNAUTHORIZED_ACCESS";
        var severity = "High";
        var source = "Authentication";
        var details = "Failed login attempt";
        var userId = "user123";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _auditService.LogSecurityEventAsync(eventType, severity, source, details, userId);

        // Assert
        _mockAuditLogDbSet.Verify(x => x.Add(It.Is<AuditLog>(al => 
            al.ChangeType == $"SECURITY:{eventType}" &&
            al.EntityType == "SecurityEvent" &&
            al.ChangedBy == userId)), Times.Once);
    }

    [TestMethod]
    public async Task AuditService_LogSecurityEventAsync_LogsWarning()
    {
        // Arrange
        var eventType = "SUSPICIOUS_ACTIVITY";
        var severity = "Medium";
        var source = "API";
        var details = "Multiple failed requests";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _auditService.LogSecurityEventAsync(eventType, severity, source, details);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Security event logged")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region LogAccessAttemptAsync Tests

    [TestMethod]
    public async Task AuditService_LogAccessAttemptAsync_LogsSuccessfulAccess()
    {
        // Arrange
        var resource = "/api/users";
        var action = "GET";
        var userId = "user123";
        var success = true;

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _auditService.LogAccessAttemptAsync(resource, action, userId, success);

        // Assert
        _mockAuditLogDbSet.Verify(x => x.Add(It.Is<AuditLog>(al => 
            al.ChangeType == "ACCESS_GRANTED" &&
            al.EntityType == "AccessAttempt" &&
            al.ChangedBy == userId)), Times.Once);
    }

    [TestMethod]
    public async Task AuditService_LogAccessAttemptAsync_LogsFailedAccess()
    {
        // Arrange
        var resource = "/api/admin";
        var action = "GET";
        var userId = "user123";
        var success = false;
        var reason = "Insufficient permissions";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _auditService.LogAccessAttemptAsync(resource, action, userId, success, reason);

        // Assert
        _mockAuditLogDbSet.Verify(x => x.Add(It.Is<AuditLog>(al => 
            al.ChangeType == "ACCESS_DENIED" &&
            al.EntityType == "AccessAttempt" &&
            al.ChangedBy == userId)), Times.Once);
    }

    #endregion

    #region LogDataChangeAsync Tests

    [TestMethod]
    public async Task AuditService_LogDataChangeAsync_LogsDataChange()
    {
        // Arrange
        var tableName = "Users";
        var operation = "UPDATE";
        var recordId = "123";
        var oldValue = "John Doe";
        var newValue = "Jane Doe";
        var changedBy = "admin";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _auditService.LogDataChangeAsync(tableName, operation, recordId, oldValue, newValue, changedBy);

        // Assert
        _mockAuditLogDbSet.Verify(x => x.Add(It.Is<AuditLog>(al => 
            al.ChangeType == $"DATA_{operation}" &&
            al.EntityType == tableName &&
            al.ChangedBy == changedBy)), Times.Once);
    }

    #endregion

    #region LogSystemEventAsync Tests

    [TestMethod]
    public async Task AuditService_LogSystemEventAsync_LogsSystemEvent()
    {
        // Arrange
        var eventType = "STARTUP";
        var component = "API";
        var details = "Application started successfully";
        var correlationId = Guid.NewGuid().ToString();

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _auditService.LogSystemEventAsync(eventType, component, details, correlationId);

        // Assert
        _mockAuditLogDbSet.Verify(x => x.Add(It.Is<AuditLog>(al => 
            al.ChangeType == $"SYSTEM:{eventType}" &&
            al.EntityType == "System" &&
            al.ChangedBy == "SYSTEM")), Times.Once);
    }

    #endregion

    #region GetAuditLogsAsync Tests

    [TestMethod]
    public async Task AuditService_GetAuditLogsAsync_ReturnsAllLogs()
    {
        // Arrange
        var auditLogs = new List<AuditLog>
        {
            new() { Id = 1, ChangeType = "CREATE", EntityType = "User", ChangeDate = DateTime.UtcNow.AddDays(-1) },
            new() { Id = 2, ChangeType = "UPDATE", EntityType = "User", ChangeDate = DateTime.UtcNow }
        };

        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _auditService.GetAuditLogsAsync();

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task AuditService_GetAuditLogsAsync_FiltersBy DateRange()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-2);
        var endDate = DateTime.UtcNow.AddDays(-1);
        var auditLogs = new List<AuditLog>
        {
            new() { Id = 1, ChangeType = "CREATE", EntityType = "User", ChangeDate = DateTime.UtcNow.AddDays(-3) },
            new() { Id = 2, ChangeType = "UPDATE", EntityType = "User", ChangeDate = DateTime.UtcNow.AddDays(-1.5) },
            new() { Id = 3, ChangeType = "DELETE", EntityType = "User", ChangeDate = DateTime.UtcNow }
        };

        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _auditService.GetAuditLogsAsync(startDate, endDate);

        // Assert
        Assert.AreEqual(1, result.Count());
        Assert.AreEqual(2, result.First().Id);
    }

    #endregion

    #region GetAuditLogsByEntityAsync Tests

    [TestMethod]
    public async Task AuditService_GetAuditLogsByEntityAsync_ReturnsFilteredLogs()
    {
        // Arrange
        var entityType = "User";
        var entityId = 1;
        var auditLogs = new List<AuditLog>
        {
            new() { Id = 1, ChangeType = "CREATE", EntityType = "User", EntityId = 1 },
            new() { Id = 2, ChangeType = "UPDATE", EntityType = "User", EntityId = 2 },
            new() { Id = 3, ChangeType = "DELETE", EntityType = "Group", EntityId = 1 }
        };

        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _auditService.GetAuditLogsByEntityAsync(entityType, entityId);

        // Assert
        Assert.AreEqual(1, result.Count());
        Assert.AreEqual(1, result.First().Id);
    }

    #endregion

    #region GetAuditLogByIdAsync Tests

    [TestMethod]
    public async Task AuditService_GetAuditLogByIdAsync_ReturnsLogWhenExists()
    {
        // Arrange
        var auditLogId = 1;
        var auditLog = new AuditLog { Id = auditLogId, ChangeType = "CREATE" };

        _mockAuditLogDbSet.Setup(x => x.FindAsync(auditLogId))
            .ReturnsAsync(auditLog);

        // Act
        var result = await _auditService.GetAuditLogByIdAsync(auditLogId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(auditLogId, result.Id);
    }

    [TestMethod]
    public async Task AuditService_GetAuditLogByIdAsync_ReturnsNullWhenNotExists()
    {
        // Arrange
        var auditLogId = 999;

        _mockAuditLogDbSet.Setup(x => x.FindAsync(auditLogId))
            .ReturnsAsync((AuditLog?)null);

        // Act
        var result = await _auditService.GetAuditLogByIdAsync(auditLogId);

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region HasSuspiciousActivityAsync Tests

    [TestMethod]
    public async Task AuditService_HasSuspiciousActivityAsync_ReturnsTrueForMultipleFailedAttempts()
    {
        // Arrange
        var userId = "user123";
        var timeWindowMinutes = 30;
        var startTime = DateTime.UtcNow.AddMinutes(-timeWindowMinutes);
        
        var auditLogs = new List<AuditLog>
        {
            new() { Id = 1, ChangedBy = userId, ChangeType = "ACCESS_DENIED", ChangeDate = DateTime.UtcNow.AddMinutes(-10) },
            new() { Id = 2, ChangedBy = userId, ChangeType = "ACCESS_DENIED", ChangeDate = DateTime.UtcNow.AddMinutes(-15) },
            new() { Id = 3, ChangedBy = userId, ChangeType = "ACCESS_DENIED", ChangeDate = DateTime.UtcNow.AddMinutes(-20) }
        };

        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _auditService.HasSuspiciousActivityAsync(userId, timeWindowMinutes);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task AuditService_HasSuspiciousActivityAsync_ReturnsFalseForNormalActivity()
    {
        // Arrange
        var userId = "user123";
        var timeWindowMinutes = 30;
        
        var auditLogs = new List<AuditLog>
        {
            new() { Id = 1, ChangedBy = userId, ChangeType = "ACCESS_GRANTED", ChangeDate = DateTime.UtcNow.AddMinutes(-10) },
            new() { Id = 2, ChangedBy = userId, ChangeType = "UPDATE", ChangeDate = DateTime.UtcNow.AddMinutes(-15) }
        };

        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _auditService.HasSuspiciousActivityAsync(userId, timeWindowMinutes);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region PurgeOldAuditLogsAsync Tests

    [TestMethod]
    public async Task AuditService_PurgeOldAuditLogsAsync_DeletesOldLogs()
    {
        // Arrange
        var retentionDays = 30;
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        
        var oldLog = new AuditLog { Id = 1, ChangeDate = DateTime.UtcNow.AddDays(-40) };
        var recentLog = new AuditLog { Id = 2, ChangeDate = DateTime.UtcNow.AddDays(-10) };
        var auditLogs = new List<AuditLog> { oldLog, recentLog };

        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _auditService.PurgeOldAuditLogsAsync(retentionDays);

        // Assert
        Assert.AreEqual(1, result);
        _mockAuditLogDbSet.Verify(x => x.RemoveRange(It.Is<IEnumerable<AuditLog>>(logs => logs.Count() == 1)), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    #endregion

    #region GetAuditStatisticsAsync Tests

    [TestMethod]
    public async Task AuditService_GetAuditStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var auditLogs = new List<AuditLog>
        {
            new() { Id = 1, ChangedBy = "user1", EntityType = "User", ChangeType = "SECURITY:LOGIN" },
            new() { Id = 2, ChangedBy = "user2", EntityType = "Group", ChangeType = "DATA_UPDATE" },
            new() { Id = 3, ChangedBy = "user1", EntityType = "User", ChangeType = "CREATE" }
        };

        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _auditService.GetAuditStatisticsAsync();

        // Assert
        Assert.AreEqual(3, result["TotalLogs"]);
        Assert.AreEqual(2, result["UniqueUsers"]);
        Assert.AreEqual(2, result["UniqueEntities"]);
        Assert.AreEqual(1, result["SecurityEvents"]);
        Assert.AreEqual(1, result["DataChanges"]);
    }

    #endregion

    #region CalculateAuditHashAsync Tests

    [TestMethod]
    public async Task AuditService_CalculateAuditHashAsync_ReturnsHashForExistingLog()
    {
        // Arrange
        var auditLogId = 1;
        var auditLog = new AuditLog 
        { 
            Id = auditLogId, 
            EntityType = "User", 
            EntityId = 1, 
            ChangeType = "CREATE",
            ChangedBy = "user1",
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = "Created user"
        };

        _mockAuditLogDbSet.Setup(x => x.FindAsync(auditLogId))
            .ReturnsAsync(auditLog);

        // Act
        var result = await _auditService.CalculateAuditHashAsync(auditLogId);

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(result));
    }

    [TestMethod]
    public async Task AuditService_CalculateAuditHashAsync_ReturnsEmptyStringForNonExistentLog()
    {
        // Arrange
        var auditLogId = 999;

        _mockAuditLogDbSet.Setup(x => x.FindAsync(auditLogId))
            .ReturnsAsync((AuditLog?)null);

        // Act
        var result = await _auditService.CalculateAuditHashAsync(auditLogId);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    #endregion

    #region ValidateAuditHashAsync Tests

    [TestMethod]
    public async Task AuditService_ValidateAuditHashAsync_ReturnsTrueForMatchingHash()
    {
        // Arrange
        var auditLogId = 1;
        var auditLog = new AuditLog 
        { 
            Id = auditLogId, 
            EntityType = "User", 
            EntityId = 1, 
            ChangeType = "CREATE",
            ChangedBy = "user1",
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = "Created user"
        };

        _mockAuditLogDbSet.Setup(x => x.FindAsync(auditLogId))
            .ReturnsAsync(auditLog);

        // Calculate expected hash
        var expectedHash = await _auditService.CalculateAuditHashAsync(auditLogId);

        // Act
        var result = await _auditService.ValidateAuditHashAsync(auditLogId, expectedHash);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task AuditService_ValidateAuditHashAsync_ReturnsFalseForMismatchedHash()
    {
        // Arrange
        var auditLogId = 1;
        var auditLog = new AuditLog 
        { 
            Id = auditLogId, 
            EntityType = "User", 
            EntityId = 1, 
            ChangeType = "CREATE",
            ChangedBy = "user1",
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = "Created user"
        };

        _mockAuditLogDbSet.Setup(x => x.FindAsync(auditLogId))
            .ReturnsAsync(auditLog);

        var wrongHash = "wrong_hash";

        // Act
        var result = await _auditService.ValidateAuditHashAsync(auditLogId, wrongHash);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region EnableRealTimeMonitoringAsync Tests

    [TestMethod]
    public async Task AuditService_EnableRealTimeMonitoringAsync_EnablesMonitoring()
    {
        // Arrange
        var userId = "user123";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _auditService.EnableRealTimeMonitoringAsync(userId);

        // Assert
        Assert.IsTrue(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Enabled real-time monitoring")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region DisableRealTimeMonitoringAsync Tests

    [TestMethod]
    public async Task AuditService_DisableRealTimeMonitoringAsync_DisablesMonitoring()
    {
        // Arrange
        var userId = "user123";

        // First enable monitoring
        await _auditService.EnableRealTimeMonitoringAsync(userId);

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _auditService.DisableRealTimeMonitoringAsync(userId);

        // Assert
        Assert.IsTrue(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disabled real-time monitoring")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task AuditService_DisableRealTimeMonitoringAsync_ReturnsFalseWhenNotEnabled()
    {
        // Arrange
        var userId = "user123";

        // Act (without enabling first)
        var result = await _auditService.DisableRealTimeMonitoringAsync(userId);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region IsUserMonitoredAsync Tests

    [TestMethod]
    public async Task AuditService_IsUserMonitoredAsync_ReturnsTrueForMonitoredUser()
    {
        // Arrange
        var userId = "user123";

        // Enable monitoring first
        await _auditService.EnableRealTimeMonitoringAsync(userId);

        // Act
        var result = await _auditService.IsUserMonitoredAsync(userId);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task AuditService_IsUserMonitoredAsync_ReturnsFalseForNonMonitoredUser()
    {
        // Arrange
        var userId = "user123";

        // Act (without enabling monitoring)
        var result = await _auditService.IsUserMonitoredAsync(userId);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region CreateAlertRuleAsync Tests

    [TestMethod]
    public async Task AuditService_CreateAlertRuleAsync_CreatesNewRule()
    {
        // Arrange
        var ruleName = "High Security Alert";
        var condition = "severity == 'Critical'";
        var action = "NOTIFY:security-team";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _auditService.CreateAlertRuleAsync(ruleName, condition, action);

        // Assert
        Assert.IsTrue(result > 0);
        _mockAuditLogDbSet.Verify(x => x.Add(It.Is<AuditLog>(al => 
            al.ChangeType == "CREATE_ALERT_RULE")), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task AuditService_LogAsync_HandlesExceptionGracefully()
    {
        // Arrange
        var action = "CREATE";
        var entityType = "User";
        var entityId = 1;
        var performedBy = "TestUser";
        var details = "Created new user";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _auditService.LogAsync(action, entityType, entityId, performedBy, details));
    }

    #endregion

    #region Compliance Report Tests

    [TestMethod]
    public async Task AuditService_GenerateGDPRReportAsync_GeneratesReport()
    {
        // Arrange
        var userId = "user123";
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        var auditLogs = new List<AuditLog>();
        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _auditService.GenerateGDPRReportAsync(userId, startDate, endDate);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("GDPR", result.ReportType);
        Assert.IsTrue(result.GeneratedAt <= DateTime.UtcNow);
        Assert.IsTrue(result.Items.Any());
    }

    [TestMethod]
    public async Task AuditService_GenerateSOC2ReportAsync_GeneratesReport()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        var auditLogs = new List<AuditLog>();
        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _auditService.GenerateSOC2ReportAsync(startDate, endDate);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("SOC2", result.ReportType);
        Assert.IsTrue(result.GeneratedAt <= DateTime.UtcNow);
        Assert.IsTrue(result.Items.Any());
    }

    #endregion

    #region Export Tests

    [TestMethod]
    public async Task AuditService_ExportAuditLogsAsync_ReturnsJsonFormat()
    {
        // Arrange
        var format = "json";
        var auditLogs = new List<AuditLog>
        {
            new() { Id = 1, ChangeType = "CREATE", EntityType = "User" }
        };

        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _auditService.ExportAuditLogsAsync(format);

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(result));
        Assert.IsTrue(result.Contains("CREATE"));
    }

    [TestMethod]
    public async Task AuditService_ExportAuditLogsAsync_ReturnsCsvFormat()
    {
        // Arrange
        var format = "csv";
        var auditLogs = new List<AuditLog>
        {
            new() { Id = 1, ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "admin", ChangeDate = DateTime.UtcNow, ChangeDetails = "test" }
        };

        var mockQueryable = auditLogs.AsQueryable();
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockAuditLogDbSet.As<IQueryable<AuditLog>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _auditService.ExportAuditLogsAsync(format);

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(result));
        Assert.IsTrue(result.Contains("Id,EntityType,EntityId,ChangeType"));
    }

    #endregion
}