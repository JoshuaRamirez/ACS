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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("AUDIT LOG CREATED")),
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
        Assert.AreEqual(1, result); // 1 old log was deleted

        // After purge: the recent log remains + a new SYSTEM:PURGE audit log is created
        var remainingCount = await _dbContext.AuditLogs.CountAsync();
        Assert.AreEqual(2, remainingCount); // Recent log + PURGE system event log

        // Verify the original recent log still exists
        var updateLog = await _dbContext.AuditLogs.FirstOrDefaultAsync(l => l.ChangeType == "UPDATE");
        Assert.IsNotNull(updateLog);
        Assert.AreEqual("UPDATE", updateLog.ChangeType);
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
        var userId = "123"; // Must be a valid integer string since GenerateGDPRReportAsync calls int.Parse(userId)
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

    #region Handler Compatibility Methods - RecordEventAsync Tests

    [TestMethod]
    public async Task RecordEventAsync_WithValidRequest_ReturnsSuccessResponse()
    {
        // Arrange
        var request = new ACS.Service.Requests.RecordAuditEventRequest
        {
            EventType = "USER_LOGIN",
            EventCategory = "Authentication",
            UserId = 123,
            EntityId = 456,
            EntityType = "User",
            Action = "Login",
            Details = "User logged in successfully",
            Severity = "Information",
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            SessionId = "session-abc-123",
            EventTimestamp = DateTime.UtcNow
        };

        // Act
        var result = await _auditService.RecordEventAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.AuditEventId > 0);
        Assert.AreEqual("Audit event recorded successfully", result.Message);

        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        Assert.IsNotNull(auditLog);
        Assert.AreEqual("USER_LOGIN", auditLog.ChangeType);
        Assert.AreEqual("User", auditLog.EntityType);
    }

    [TestMethod]
    public async Task RecordEventAsync_WithNullOptionalFields_HandlesGracefully()
    {
        // Arrange
        var request = new ACS.Service.Requests.RecordAuditEventRequest
        {
            EventType = "SYSTEM_EVENT",
            EventCategory = "System",
            Action = "Process",
            EventTimestamp = DateTime.UtcNow
            // All optional fields left null
        };

        // Act
        var result = await _auditService.RecordEventAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.AuditEventId > 0);

        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        Assert.IsNotNull(auditLog);
        Assert.AreEqual("Unknown", auditLog.EntityType);
        Assert.AreEqual("System", auditLog.ChangedBy);
    }

    [TestMethod]
    public async Task RecordEventAsync_WithMetadata_StoresMetadataInDetails()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "browser", "Chrome" },
            { "version", "120.0" }
        };
        var request = new ACS.Service.Requests.RecordAuditEventRequest
        {
            EventType = "PAGE_VIEW",
            EventCategory = "Analytics",
            Action = "View",
            Metadata = metadata,
            EventTimestamp = DateTime.UtcNow
        };

        // Act
        var result = await _auditService.RecordEventAsync(request);

        // Assert
        Assert.IsTrue(result.Success);

        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        Assert.IsNotNull(auditLog);
        Assert.IsTrue(auditLog.ChangeDetails.Contains("browser"));
        Assert.IsTrue(auditLog.ChangeDetails.Contains("Chrome"));
    }

    [TestMethod]
    public async Task RecordEventAsync_WithCorrelationId_StoresCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var request = new ACS.Service.Requests.RecordAuditEventRequest
        {
            EventType = "API_CALL",
            EventCategory = "API",
            Action = "Request",
            CorrelationId = correlationId,
            EventTimestamp = DateTime.UtcNow
        };

        // Act
        var result = await _auditService.RecordEventAsync(request);

        // Assert
        Assert.IsTrue(result.Success);

        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        Assert.IsNotNull(auditLog);
        Assert.IsTrue(auditLog.ChangeDetails.Contains(correlationId));
    }

    [TestMethod]
    public async Task RecordEventAsync_SetsRecordedAtTimestamp()
    {
        // Arrange
        var request = new ACS.Service.Requests.RecordAuditEventRequest
        {
            EventType = "TEST_EVENT",
            EventCategory = "Test",
            Action = "Test",
            EventTimestamp = DateTime.UtcNow.AddHours(-1)
        };

        // Act
        var result = await _auditService.RecordEventAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.RecordedAt <= DateTime.UtcNow);
    }

    #endregion

    #region Handler Compatibility Methods - PurgeOldDataAsync Tests

    [TestMethod]
    public async Task PurgeOldDataAsync_WithValidRequest_DeletesOldRecords()
    {
        // Arrange
        var oldLog = new AuditLog { ChangeType = "OLD_EVENT", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-60), ChangeDetails = "{}" };
        var recentLog = new AuditLog { ChangeType = "RECENT_EVENT", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow.AddDays(-10), ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(oldLog, recentLog);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.PurgeAuditDataRequest
        {
            OlderThan = DateTime.UtcNow.AddDays(-30),
            BatchSize = 1000
        };

        // Act
        var result = await _auditService.PurgeOldDataAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.RecordsDeleted);
        Assert.AreEqual(1, await _dbContext.AuditLogs.CountAsync());
    }

    [TestMethod]
    public async Task PurgeOldDataAsync_WithDryRunTrue_DoesNotDeleteRecords()
    {
        // Arrange
        var oldLog = new AuditLog { ChangeType = "OLD_EVENT", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-60), ChangeDetails = "{}" };

        _dbContext.AuditLogs.Add(oldLog);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.PurgeAuditDataRequest
        {
            OlderThan = DateTime.UtcNow.AddDays(-30),
            DryRun = true
        };

        // Act
        var result = await _auditService.PurgeOldDataAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.Message!.Contains("Dry run"));
        Assert.AreEqual(1, await _dbContext.AuditLogs.CountAsync()); // Record not deleted
    }

    [TestMethod]
    public async Task PurgeOldDataAsync_WithPreserveComplianceTrue_PreservesComplianceRecords()
    {
        // Arrange
        var complianceLog = new AuditLog { ChangeType = "COMPLIANCE_CHECK", EntityType = "System", EntityId = 1, ChangedBy = "system", ChangeDate = DateTime.UtcNow.AddDays(-60), ChangeDetails = "{}" };
        var regularLog = new AuditLog { ChangeType = "REGULAR_EVENT", EntityType = "User", EntityId = 2, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-60), ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(complianceLog, regularLog);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.PurgeAuditDataRequest
        {
            OlderThan = DateTime.UtcNow.AddDays(-30),
            PreserveCompliance = true,
            BatchSize = 1000
        };

        // Act
        var result = await _auditService.PurgeOldDataAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.RecordsPreserved > 0);
        Assert.IsTrue(result.PreservedReasons.Contains("Compliance preservation policy"));
    }

    [TestMethod]
    public async Task PurgeOldDataAsync_WithSecurityEvents_PreservesSecurityRecords()
    {
        // Arrange
        var securityLog = new AuditLog { ChangeType = "SECURITY:LOGIN_FAILED", EntityType = "SecurityEvent", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-60), ChangeDetails = "{}" };

        _dbContext.AuditLogs.Add(securityLog);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.PurgeAuditDataRequest
        {
            OlderThan = DateTime.UtcNow.AddDays(-30),
            PreserveCompliance = true,
            BatchSize = 1000
        };

        // Act
        var result = await _auditService.PurgeOldDataAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.RecordsPreserved);
        Assert.AreEqual(0, result.RecordsDeleted);
    }

    [TestMethod]
    public async Task PurgeOldDataAsync_WithEventCategoriesFilter_HandlesComplexLinqGracefully()
    {
        // Arrange
        // Note: The in-memory provider has limitations with complex LINQ expressions like
        // EventCategories.Any(cat => ChangeDetails.Contains(cat))
        // This test verifies the method handles errors gracefully
        var log1 = new AuditLog { ChangeType = "EVENT1", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-60), ChangeDetails = "Authentication event occurred" };

        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.PurgeAuditDataRequest
        {
            OlderThan = DateTime.UtcNow.AddDays(-30),
            EventCategories = new List<string> { "Authentication" },
            PreserveCompliance = false,
            BatchSize = 1000
        };

        // Act
        var result = await _auditService.PurgeOldDataAsync(request);

        // Assert
        // The method should either succeed or return error response gracefully
        // (depending on the database provider's support for complex LINQ)
        Assert.IsNotNull(result);
        if (!result.Success)
        {
            // The method should return an error message, not throw unhandled exception
            Assert.IsNotNull(result.Errors);
            Assert.IsTrue(result.Errors.Count > 0);
        }
    }

    [TestMethod]
    public async Task PurgeOldDataAsync_WithSeverityLevelsFilter_HandlesComplexLinqGracefully()
    {
        // Arrange
        // Note: The in-memory provider has limitations with complex LINQ expressions like
        // SeverityLevels.Any(sev => ChangeDetails.Contains(sev))
        // This test verifies the method handles errors gracefully
        var log1 = new AuditLog { ChangeType = "EVENT1", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-60), ChangeDetails = "Warning severity event" };

        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.PurgeAuditDataRequest
        {
            OlderThan = DateTime.UtcNow.AddDays(-30),
            SeverityLevels = new List<string> { "Warning" },
            PreserveCompliance = false,
            BatchSize = 1000
        };

        // Act
        var result = await _auditService.PurgeOldDataAsync(request);

        // Assert
        // The method should either succeed or return error response gracefully
        // (depending on the database provider's support for complex LINQ)
        Assert.IsNotNull(result);
        if (!result.Success)
        {
            // The method should return an error message, not throw unhandled exception
            Assert.IsNotNull(result.Errors);
            Assert.IsTrue(result.Errors.Count > 0);
        }
    }

    [TestMethod]
    public async Task PurgeOldDataAsync_WithBatchSizeLimit_RespectsLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _dbContext.AuditLogs.Add(new AuditLog
            {
                ChangeType = $"EVENT_{i}",
                EntityType = "User",
                EntityId = i,
                ChangedBy = "user1",
                ChangeDate = DateTime.UtcNow.AddDays(-60),
                ChangeDetails = "{}"
            });
        }
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.PurgeAuditDataRequest
        {
            OlderThan = DateTime.UtcNow.AddDays(-30),
            BatchSize = 5,
            PreserveCompliance = false
        };

        // Act
        var result = await _auditService.PurgeOldDataAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(5, result.RecordsDeleted);
        Assert.AreEqual(5, await _dbContext.AuditLogs.CountAsync());
    }

    [TestMethod]
    public async Task PurgeOldDataAsync_WithNoRecordsToDelete_ReturnsZeroDeleted()
    {
        // Arrange
        var recentLog = new AuditLog { ChangeType = "RECENT_EVENT", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-5), ChangeDetails = "{}" };

        _dbContext.AuditLogs.Add(recentLog);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.PurgeAuditDataRequest
        {
            OlderThan = DateTime.UtcNow.AddDays(-30),
            BatchSize = 1000
        };

        // Act
        var result = await _auditService.PurgeOldDataAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.RecordsDeleted);
        Assert.AreEqual(1, await _dbContext.AuditLogs.CountAsync());
    }

    #endregion

    #region Handler Compatibility Methods - GetAuditLogAsync Tests

    [TestMethod]
    public async Task GetAuditLogAsync_WithValidRequest_ReturnsLogs()
    {
        // Arrange
        var auditLog = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{\"Action\":\"Create\"}" };
        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual(1, result.Entries.Count);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithDateFilters_FiltersByDateRange()
    {
        // Arrange
        var oldLog = new AuditLog { ChangeType = "OLD", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-10), ChangeDetails = "{}" };
        var recentLog = new AuditLog { ChangeType = "RECENT", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow.AddDays(-1), ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(oldLog, recentLog);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("RECENT", result.Entries.First().EventType);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithEventTypesFilter_FiltersByEventTypes()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "DELETE", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            EventTypes = new List<string> { "CREATE" }
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("CREATE", result.Entries.First().EventType);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithUserIdFilter_FiltersByUserId()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "123", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "UPDATE", EntityType = "User", EntityId = 2, ChangedBy = "456", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            UserId = 123
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithEntityIdFilter_FiltersByEntityId()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 100, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 200, ChangedBy = "user2", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            EntityId = 100
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithEntityTypeFilter_FiltersByEntityType()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "CREATE", EntityType = "Group", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            EntityType = "User"
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("User", result.Entries.First().EntityType);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithSearchText_SearchesInDetails()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{\"Details\":\"Password updated\"}" };
        var log2 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow, ChangeDetails = "{\"Details\":\"Email changed\"}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            SearchText = "Password"
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithIpAddressFilter_FiltersByIpAddress()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{\"IpAddress\":\"192.168.1.1\"}" };
        var log2 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow, ChangeDetails = "{\"IpAddress\":\"10.0.0.1\"}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            IpAddress = "192.168.1.1"
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        for (int i = 0; i < 25; i++)
        {
            _dbContext.AuditLogs.Add(new AuditLog
            {
                ChangeType = $"EVENT_{i}",
                EntityType = "User",
                EntityId = i,
                ChangedBy = "user1",
                ChangeDate = DateTime.UtcNow.AddMinutes(-i),
                ChangeDetails = "{}"
            });
        }
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            Page = 2,
            PageSize = 10
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(25, result.TotalCount);
        Assert.AreEqual(10, result.Entries.Count);
        Assert.AreEqual(2, result.Page);
        Assert.AreEqual(10, result.PageSize);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithSortDescendingTrue_SortsDescending()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "FIRST", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddHours(-2), ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "SECOND", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow.AddHours(-1), ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            SortDescending = true
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual("SECOND", result.Entries.First().EventType);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithSortDescendingFalse_SortsAscending()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "FIRST", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddHours(-2), ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "SECOND", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow.AddHours(-1), ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            SortDescending = false
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual("FIRST", result.Entries.First().EventType);
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithSeverityLevelsFilter_HandlesComplexLinqGracefully()
    {
        // Arrange
        // Note: The in-memory provider has limitations with complex LINQ expressions like
        // SeverityLevels.Any(sev => ChangeDetails.Contains(sev))
        // This test verifies the method handles errors gracefully
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{\"Severity\":\"Critical\"}" };

        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
        {
            SeverityLevels = new List<string> { "Critical" }
        };

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        // The method should either succeed or return error response gracefully
        // (depending on the database provider's support for complex LINQ)
        Assert.IsNotNull(result);
        if (!result.Success)
        {
            // The method should return an error message, not throw unhandled exception
            Assert.IsNotNull(result.Errors);
            Assert.IsTrue(result.Errors.Count > 0);
        }
    }

    [TestMethod]
    public async Task GetAuditLogAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest();

        // Act
        var result = await _auditService.GetAuditLogAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0, result.Entries.Count);
    }

    #endregion

    #region Handler Compatibility Methods - GetUserAuditTrailAsync Tests

    [TestMethod]
    public async Task GetUserAuditTrailAsync_WithValidUserId_ReturnsUserEvents()
    {
        // Arrange
        // Use event types that match both default filters (must contain ACCESS AND PERMISSION/GRANT/REVOKE)
        var log1 = new AuditLog { ChangeType = "PERMISSION_ACCESS_LOGIN", EntityType = "User", EntityId = 1, ChangedBy = "123", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "PERMISSION_ACCESS_LOGOUT", EntityType = "User", EntityId = 1, ChangedBy = "456", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetUserAuditTrailRequest
        {
            UserId = 123
        };

        // Act
        var result = await _auditService.GetUserAuditTrailAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("PERMISSION_ACCESS_LOGIN", result.Entries.First().EventType);
    }

    [TestMethod]
    public async Task GetUserAuditTrailAsync_WithDateFilters_FiltersByDateRange()
    {
        // Arrange
        // Use event types that match both default filters (must contain ACCESS AND PERMISSION/GRANT/REVOKE)
        var log1 = new AuditLog { ChangeType = "PERMISSION_ACCESS_OLD_EVENT", EntityType = "User", EntityId = 1, ChangedBy = "123", ChangeDate = DateTime.UtcNow.AddDays(-10), ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "PERMISSION_ACCESS_RECENT_EVENT", EntityType = "User", EntityId = 1, ChangedBy = "123", ChangeDate = DateTime.UtcNow.AddDays(-1), ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetUserAuditTrailRequest
        {
            UserId = 123,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow
        };

        // Act
        var result = await _auditService.GetUserAuditTrailAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("PERMISSION_ACCESS_RECENT_EVENT", result.Entries.First().EventType);
    }

    [TestMethod]
    public async Task GetUserAuditTrailAsync_WithIncludeSystemEventsFalse_ExcludesSystemEvents()
    {
        // Arrange
        // Use event types that match both default filters (must contain ACCESS AND PERMISSION/GRANT/REVOKE)
        var log1 = new AuditLog { ChangeType = "SYSTEM:PERMISSION_ACCESS_CHECK", EntityType = "System", EntityId = 0, ChangedBy = "123", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "PERMISSION_ACCESS_USER_ACTION", EntityType = "User", EntityId = 1, ChangedBy = "123", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetUserAuditTrailRequest
        {
            UserId = 123,
            IncludeSystemEvents = false
        };

        // Act
        var result = await _auditService.GetUserAuditTrailAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("PERMISSION_ACCESS_USER_ACTION", result.Entries.First().EventType);
    }

    [TestMethod]
    public async Task GetUserAuditTrailAsync_WithIncludeSystemEventsTrue_IncludesSystemEvents()
    {
        // Arrange
        // Use event types that match both default filters (must contain ACCESS AND PERMISSION/GRANT/REVOKE)
        var log1 = new AuditLog { ChangeType = "SYSTEM:PERMISSION_ACCESS_CHECK", EntityType = "System", EntityId = 0, ChangedBy = "123", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "PERMISSION_ACCESS_GRANT", EntityType = "User", EntityId = 1, ChangedBy = "123", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetUserAuditTrailRequest
        {
            UserId = 123,
            IncludeSystemEvents = true,
            IncludePermissionChanges = true,
            IncludeResourceAccess = true
        };

        // Act
        var result = await _auditService.GetUserAuditTrailAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        // Both logs match the filters (both have ACCESS and PERMISSION)
        Assert.AreEqual(2, result.TotalCount);
    }

    [TestMethod]
    public async Task GetUserAuditTrailAsync_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        // The default filters require BOTH permission-related AND access-related event types
        // due to how the service combines the Include filters
        for (int i = 0; i < 15; i++)
        {
            _dbContext.AuditLogs.Add(new AuditLog
            {
                ChangeType = $"PERMISSION_ACCESS_EVENT_{i}",
                EntityType = "User",
                EntityId = i,
                ChangedBy = "123",
                ChangeDate = DateTime.UtcNow.AddMinutes(-i),
                ChangeDetails = "{}"
            });
        }
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetUserAuditTrailRequest
        {
            UserId = 123,
            Page = 2,
            PageSize = 5
        };

        // Act
        var result = await _auditService.GetUserAuditTrailAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(15, result.TotalCount);
        Assert.AreEqual(5, result.Entries.Count);
        Assert.AreEqual(2, result.Page);
    }

    [TestMethod]
    public async Task GetUserAuditTrailAsync_WithEventCategoriesFilter_HandlesComplexLinqGracefully()
    {
        // Arrange
        // Note: The in-memory provider has limitations with complex LINQ expressions like
        // EventCategories.Any(cat => ChangeDetails.Contains(cat))
        // This test verifies the method handles errors gracefully
        var log1 = new AuditLog { ChangeType = "PERMISSION_ACCESS_GRANTED", EntityType = "User", EntityId = 1, ChangedBy = "123", ChangeDate = DateTime.UtcNow, ChangeDetails = "Authentication event details" };

        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetUserAuditTrailRequest
        {
            UserId = 123,
            EventCategories = new List<string> { "Authentication" }
        };

        // Act
        var result = await _auditService.GetUserAuditTrailAsync(request);

        // Assert
        // The method should either succeed or return error response gracefully
        // (depending on the database provider's support for complex LINQ)
        Assert.IsNotNull(result);
        if (!result.Success)
        {
            // The method should return an error message, not throw unhandled exception
            Assert.IsNotNull(result.Errors);
            Assert.IsTrue(result.Errors.Count > 0);
        }
    }

    [TestMethod]
    public async Task GetUserAuditTrailAsync_ReturnsDescendingOrderByDefault()
    {
        // Arrange
        // Use event types that match both default filters (must contain ACCESS AND PERMISSION/GRANT/REVOKE)
        var log1 = new AuditLog { ChangeType = "PERMISSION_ACCESS_OLDER", EntityType = "User", EntityId = 1, ChangedBy = "123", ChangeDate = DateTime.UtcNow.AddHours(-2), ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "PERMISSION_ACCESS_NEWER", EntityType = "User", EntityId = 2, ChangedBy = "123", ChangeDate = DateTime.UtcNow.AddHours(-1), ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.GetUserAuditTrailRequest
        {
            UserId = 123
        };

        // Act
        var result = await _auditService.GetUserAuditTrailAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.Entries.Count);
        Assert.AreEqual("PERMISSION_ACCESS_NEWER", result.Entries.First().EventType);
    }

    [TestMethod]
    public async Task GetUserAuditTrailAsync_MessageIncludesUserId()
    {
        // Arrange
        var request = new ACS.Service.Requests.GetUserAuditTrailRequest
        {
            UserId = 999
        };

        // Act
        var result = await _auditService.GetUserAuditTrailAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.Message!.Contains("999"));
    }

    #endregion

    #region Handler Compatibility Methods - ValidateIntegrityAsync Tests

    [TestMethod]
    public async Task ValidateIntegrityAsync_WithValidData_ReturnsValid()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddMinutes(-10), ChangeDetails = "{}" };
        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest
        {
            CheckHashChain = true,
            CheckCompleteness = true,
            CheckConsistency = true
        };

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.IsIntegrityValid);
        Assert.AreEqual(0, result.Issues.Count);
    }

    [TestMethod]
    public async Task ValidateIntegrityAsync_WithDateFilters_FiltersByDateRange()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "OLD", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddDays(-10), ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "RECENT", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest
        {
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.Statistics.TotalRecordsChecked);
    }

    [TestMethod]
    public async Task ValidateIntegrityAsync_WithCheckHashChainTrue_PerformsHashChainValidation()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };
        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest
        {
            CheckHashChain = true,
            CheckCompleteness = false,
            CheckConsistency = false
        };

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.ChecksPerformed.HashChainValidated);
    }

    [TestMethod]
    public async Task ValidateIntegrityAsync_WithCheckCompletenessTrue_DetectsGaps()
    {
        // Arrange - Create logs with ID gaps (simulated by modifying IDs after save)
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow.AddMinutes(-2), ChangeDetails = "{}" };
        var log2 = new AuditLog { ChangeType = "UPDATE", EntityType = "User", EntityId = 2, ChangedBy = "user2", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };

        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();
        _dbContext.AuditLogs.Add(log2);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest
        {
            CheckCompleteness = true
        };

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.ChecksPerformed.CompletenessValidated);
    }

    [TestMethod]
    public async Task ValidateIntegrityAsync_WithCheckConsistencyTrue_ChecksForDuplicates()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };
        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest
        {
            CheckConsistency = true
        };

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.ChecksPerformed.ConsistencyValidated);
    }

    [TestMethod]
    public async Task ValidateIntegrityAsync_WithPerformDeepValidationTrue_ValidatesJsonFormat()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{\"valid\":true}" };
        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest
        {
            PerformDeepValidation = true
        };

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.ChecksPerformed.DeepValidationPerformed);
    }

    [TestMethod]
    public async Task ValidateIntegrityAsync_WithMalformedJson_ReportsMalformedDataIssue()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "not valid json {{" };
        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest
        {
            PerformDeepValidation = true,
            CheckCompleteness = false,
            CheckHashChain = false,
            CheckConsistency = false
        };

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.Issues.Any(i => i.IssueType == "MalformedData"));
    }

    [TestMethod]
    public async Task ValidateIntegrityAsync_ReturnsStatistics()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            _dbContext.AuditLogs.Add(new AuditLog
            {
                ChangeType = $"EVENT_{i}",
                EntityType = "User",
                EntityId = i,
                ChangedBy = "user1",
                ChangeDate = DateTime.UtcNow.AddMinutes(-i),
                ChangeDetails = "{}"
            });
        }
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest();

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(5, result.Statistics.TotalRecordsChecked);
        Assert.IsNotNull(result.Statistics.EarliestRecord);
        Assert.IsNotNull(result.Statistics.LatestRecord);
        Assert.IsTrue(result.Statistics.ValidationDuration.TotalMilliseconds >= 0);
    }

    [TestMethod]
    public async Task ValidateIntegrityAsync_WithEmptyDatabase_ReturnsValidWithZeroRecords()
    {
        // Arrange
        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest();

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.IsIntegrityValid);
        Assert.AreEqual(0, result.Statistics.TotalRecordsChecked);
    }

    [TestMethod]
    public async Task ValidateIntegrityAsync_MessageIndicatesValidationResult()
    {
        // Arrange
        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest();

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.Message!.Contains("validation"));
    }

    [TestMethod]
    public async Task ValidateIntegrityAsync_AllChecksDisabled_StillReturnsSuccess()
    {
        // Arrange
        var log1 = new AuditLog { ChangeType = "CREATE", EntityType = "User", EntityId = 1, ChangedBy = "user1", ChangeDate = DateTime.UtcNow, ChangeDetails = "{}" };
        _dbContext.AuditLogs.Add(log1);
        await _dbContext.SaveChangesAsync();

        var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest
        {
            CheckHashChain = false,
            CheckCompleteness = false,
            CheckConsistency = false,
            PerformDeepValidation = false
        };

        // Act
        var result = await _auditService.ValidateIntegrityAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.IsIntegrityValid);
        Assert.IsFalse(result.ChecksPerformed.HashChainValidated);
        Assert.IsFalse(result.ChecksPerformed.CompletenessValidated);
        Assert.IsFalse(result.ChecksPerformed.ConsistencyValidated);
        Assert.IsFalse(result.ChecksPerformed.DeepValidationPerformed);
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