using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using ACS.Service.Compliance;
using ACS.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class AuditServiceTests
{
    private ApplicationDbContext _dbContext = null!;
    private Mock<ILogger<AuditService>> _mockLogger = null!;
    private AuditService _auditService = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange - Use real in-memory database for integration-style testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<AuditService>>();
        
        // System under test
        _auditService = new AuditService(_dbContext, _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext?.Dispose();
        _mockLogger?.Reset();
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

        // Act
        await _auditService.LogAsync(action, entityType, entityId, performedBy, details);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        Assert.IsNotNull(auditLog);
        Assert.AreEqual(action, auditLog.ChangeType);
        Assert.AreEqual(entityType, auditLog.EntityType);
        Assert.AreEqual(entityId, auditLog.EntityId);
        Assert.AreEqual(performedBy, auditLog.ChangedBy);
        Assert.AreEqual(details, auditLog.ChangeDetails);
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

        // Act
        await _auditService.LogSecurityEventAsync(eventType, severity, source, details, userId);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        Assert.IsNotNull(auditLog);
        Assert.AreEqual($"SECURITY:{eventType}", auditLog.ChangeType);
        Assert.AreEqual("SecurityEvent", auditLog.EntityType);
        Assert.AreEqual(userId, auditLog.ChangedBy);
    }

    [TestMethod]
    public async Task AuditService_LogSecurityEventAsync_LogsWarning()
    {
        // Arrange
        var eventType = "SUSPICIOUS_ACTIVITY";
        var severity = "Medium";
        var source = "API";
        var details = "Multiple failed requests";

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

        // Act
        await _auditService.LogAccessAttemptAsync(resource, action, userId, success);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        Assert.IsNotNull(auditLog);
        Assert.AreEqual("ACCESS_GRANTED", auditLog.ChangeType);
        Assert.AreEqual("AccessAttempt", auditLog.EntityType);
        Assert.AreEqual(userId, auditLog.ChangedBy);
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

        // Act
        await _auditService.LogAccessAttemptAsync(resource, action, userId, success, reason);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        Assert.IsNotNull(auditLog);
        Assert.AreEqual("ACCESS_DENIED", auditLog.ChangeType);
        Assert.AreEqual("AccessAttempt", auditLog.EntityType);
        Assert.AreEqual(userId, auditLog.ChangedBy);
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

        // Act
        await _auditService.LogDataChangeAsync(tableName, operation, recordId, oldValue, newValue, changedBy);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        Assert.IsNotNull(auditLog);
        Assert.AreEqual($"DATA_{operation}", auditLog.ChangeType);
        Assert.AreEqual(tableName, auditLog.EntityType);
        Assert.AreEqual(changedBy, auditLog.ChangedBy);
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

        // Act
        await _auditService.LogSystemEventAsync(eventType, component, details, correlationId);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        Assert.IsNotNull(auditLog);
        Assert.AreEqual($"SYSTEM:{eventType}", auditLog.ChangeType);
        Assert.AreEqual("System", auditLog.EntityType);
        Assert.AreEqual("SYSTEM", auditLog.ChangedBy);
    }

    #endregion

    #region GetAuditLogsAsync Tests

    [TestMethod]
    public async Task AuditService_GetAuditLogsAsync_ReturnsAllLogs()
    {
        // Arrange
        var auditLog1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-1), ChangeDetails = "Created user" };
        var auditLog2 = new AuditLog { ChangeType = "UPDATE", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow, ChangeDetails = "Updated user" };
        
        _dbContext.AuditLogs.AddRange(auditLog1, auditLog2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _auditService.GetAuditLogsAsync();

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task AuditService_GetAuditLogsAsync_FiltersByDateRange()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-2);
        var endDate = DateTime.UtcNow.AddDays(-1);
        
        var auditLog1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-3), ChangeDetails = "Created user" };
        var auditLog2 = new AuditLog { ChangeType = "UPDATE", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow.AddDays(-1.5), ChangeDetails = "Updated user" };
        var auditLog3 = new AuditLog { ChangeType = "DELETE", EntityType = "User", EntityId = 3, ChangedBy = "user3", ChangeDate = DateTime.UtcNow, ChangeDetails = "Deleted user" };
        
        _dbContext.AuditLogs.AddRange(auditLog1, auditLog2, auditLog3);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _auditService.GetAuditLogsAsync(startDate, endDate);

        // Assert
        Assert.AreEqual(1, result.Count());
        Assert.AreEqual("UPDATE", result.First().ChangeType);
    }

    #endregion

    #region GetAuditLogsByEntityAsync Tests

    [TestMethod]
    public async Task AuditService_GetAuditLogsByEntityAsync_ReturnsFilteredLogs()
    {
        // Arrange
        var entityType = "User";
        var entityId = 1;
        
        var auditLog1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "Created user" };
        var auditLog2 = new AuditLog { ChangeType = "UPDATE", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow, ChangeDetails = "Updated user" };
        var auditLog3 = new AuditLog { ChangeType = "DELETE", EntityType = "Group", EntityId = 1, ChangedBy = "user3", ChangeDate = DateTime.UtcNow, ChangeDetails = "Deleted group" };
        
        _dbContext.AuditLogs.AddRange(auditLog1, auditLog2, auditLog3);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _auditService.GetAuditLogsByEntityAsync(entityType, entityId);

        // Assert
        Assert.AreEqual(1, result.Count());
        Assert.AreEqual("CREATE", result.First().ChangeType);
        Assert.AreEqual(entityType, result.First().EntityType);
        Assert.AreEqual(entityId, result.First().EntityId);
    }

    #endregion

    #region GetAuditLogByIdAsync Tests

    [TestMethod]
    public async Task AuditService_GetAuditLogByIdAsync_ReturnsLogWhenExists()
    {
        // Arrange
        var auditLog = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "Created user" };
        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();
        
        var auditLogId = auditLog.Id;

        // Act
        var result = await _auditService.GetAuditLogByIdAsync(auditLogId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(auditLogId, result.Id);
        Assert.AreEqual("CREATE", result.ChangeType);
    }

    [TestMethod]
    public async Task AuditService_GetAuditLogByIdAsync_ReturnsNullWhenNotExists()
    {
        // Arrange
        var auditLogId = 999;

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
        
        var auditLog1 = new AuditLog { ChangedBy = userId, ChangeType = "ACCESS_DENIED", EntityType = "AccessAttempt", EntityId = 1, ChangeDate = DateTime.UtcNow.AddMinutes(-10), ChangeDetails = "Failed attempt 1" };
        var auditLog2 = new AuditLog { ChangedBy = userId, ChangeType = "ACCESS_DENIED", EntityType = "AccessAttempt", EntityId = 2, ChangeDate = DateTime.UtcNow.AddMinutes(-15), ChangeDetails = "Failed attempt 2" };
        var auditLog3 = new AuditLog { ChangedBy = userId, ChangeType = "ACCESS_DENIED", EntityType = "AccessAttempt", EntityId = 3, ChangeDate = DateTime.UtcNow.AddMinutes(-20), ChangeDetails = "Failed attempt 3" };
        
        _dbContext.AuditLogs.AddRange(auditLog1, auditLog2, auditLog3);
        await _dbContext.SaveChangesAsync();

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
        
        var auditLog1 = new AuditLog { ChangedBy = userId, ChangeType = "ACCESS_GRANTED", EntityType = "AccessAttempt", EntityId = 1, ChangeDate = DateTime.UtcNow.AddMinutes(-10), ChangeDetails = "Successful access" };
        var auditLog2 = new AuditLog { ChangedBy = userId, ChangeType = "UPDATE", EntityType = "User", EntityId = 1, ChangeDate = DateTime.UtcNow.AddMinutes(-15), ChangeDetails = "Updated profile" };
        
        _dbContext.AuditLogs.AddRange(auditLog1, auditLog2);
        await _dbContext.SaveChangesAsync();

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
        
        var oldLog = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-40), ChangeDetails = "Old log" };
        var recentLog = new AuditLog { ChangeType = "UPDATE", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow.AddDays(-10), ChangeDetails = "Recent log" };
        
        _dbContext.AuditLogs.AddRange(oldLog, recentLog);
        await _dbContext.SaveChangesAsync();
        
        var initialCount = await _dbContext.AuditLogs.CountAsync();
        Assert.AreEqual(2, initialCount);

        // Act
        var result = await _auditService.PurgeOldAuditLogsAsync(retentionDays);

        // Assert
        Assert.AreEqual(1, result);
        var remainingCount = await _dbContext.AuditLogs.CountAsync();
        Assert.AreEqual(1, remainingCount);
        
        var remainingLog = await _dbContext.AuditLogs.FirstAsync();
        Assert.AreEqual("UPDATE", remainingLog.ChangeType);
    }

    #endregion

    #region GetAuditStatisticsAsync Tests

    [TestMethod]
    public async Task AuditService_GetAuditStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var auditLog1 = new AuditLog { ChangedBy = "user1", EntityType = "User", ChangeType = "SECURITY:LOGIN", EntityId = 1, ChangeDate = DateTime.UtcNow, ChangeDetails = "Security login" };
        var auditLog2 = new AuditLog { ChangedBy = "user2", EntityType = "Group", ChangeType = "DATA_UPDATE", EntityId = 1, ChangeDate = DateTime.UtcNow, ChangeDetails = "Data update" };
        var auditLog3 = new AuditLog { ChangedBy = "user1", EntityType = "User", ChangeType = "CREATE", EntityId = 2, ChangeDate = DateTime.UtcNow, ChangeDetails = "Create user" };
        
        _dbContext.AuditLogs.AddRange(auditLog1, auditLog2, auditLog3);
        await _dbContext.SaveChangesAsync();

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
        var auditLog = new AuditLog 
        { 
            EntityType = "User", 
            EntityId = 1, 
            ChangeType = "CREATE",
            ChangedBy = "user1",
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = "Created user"
        };
        
        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();
        
        var auditLogId = auditLog.Id;

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
        var auditLog = new AuditLog 
        { 
            EntityType = "User", 
            EntityId = 1, 
            ChangeType = "CREATE",
            ChangedBy = "user1",
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = "Created user"
        };
        
        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();
        
        var auditLogId = auditLog.Id;

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
        var auditLog = new AuditLog 
        { 
            EntityType = "User", 
            EntityId = 1, 
            ChangeType = "CREATE",
            ChangedBy = "user1",
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = "Created user"
        };
        
        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();
        
        var auditLogId = auditLog.Id;
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

        // Act
        var result = await _auditService.CreateAlertRuleAsync(ruleName, condition, action);

        // Assert
        Assert.IsTrue(result > 0);
        
        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync(al => al.ChangeType == "CREATE_ALERT_RULE");
        Assert.IsNotNull(auditLog);
        Assert.AreEqual("CREATE_ALERT_RULE", auditLog.ChangeType);
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

        // Dispose the context to simulate database error
        _dbContext.Dispose();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
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

        // Act
        var result = await _auditService.GenerateGDPRReportAsync(userId, startDate, endDate);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("GDPR", result.Framework.ToString());
        Assert.IsTrue(result.GeneratedDate <= DateTime.UtcNow);
        Assert.IsTrue(result.Items.Any());
    }

    [TestMethod]
    public async Task AuditService_GenerateSOC2ReportAsync_GeneratesReport()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _auditService.GenerateSOC2ReportAsync(startDate, endDate);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("SOC2", result.Framework.ToString());
        Assert.IsTrue(result.GeneratedDate <= DateTime.UtcNow);
        Assert.IsTrue(result.Items.Any());
    }

    #endregion

    #region Export Tests

    [TestMethod]
    public async Task AuditService_ExportAuditLogsAsync_ReturnsJsonFormat()
    {
        // Arrange
        var format = "json";
        var auditLog = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "Created user" };
        
        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();

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
        var auditLog = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "admin", ChangeDate = DateTime.UtcNow, ChangeDetails = "test" };
        
        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _auditService.ExportAuditLogsAsync(format);

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(result));
        Assert.IsTrue(result.Contains("Id,EntityType,EntityId,ChangeType"));
    }

    #endregion
}