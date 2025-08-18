using Microsoft.Extensions.Logging;
using ACS.Service.Services;
using ACS.Service.Infrastructure;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class HealthMonitoringServiceTests
{
    private ILogger<HealthMonitoringService> _logger = null!;
    private ErrorRecoveryService _errorRecoveryService = null!;
    private TenantConfiguration _tenantConfig = null!;
    private HealthMonitoringService _healthMonitoringService = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Mock.Of<ILogger<HealthMonitoringService>>();
        var errorRecoveryLogger = Mock.Of<ILogger<ErrorRecoveryService>>();
        _tenantConfig = new TenantConfiguration { TenantId = "test-tenant" };
        _errorRecoveryService = new ErrorRecoveryService(errorRecoveryLogger, _tenantConfig);
        _healthMonitoringService = new HealthMonitoringService(_logger, _errorRecoveryService, _tenantConfig);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _healthMonitoringService?.Dispose();
    }

    [TestMethod]
    public void RecordSuccess_NewOperationType_UpdatesMetrics()
    {
        // Arrange
        var operationType = "test-operation";
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        _healthMonitoringService.RecordSuccess(operationType, duration);

        // Assert
        var status = _healthMonitoringService.GetHealthStatus(operationType);
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Unknown, status); // Need more samples for status
    }

    [TestMethod]
    public void RecordFailure_NewOperationType_UpdatesMetrics()
    {
        // Arrange
        var operationType = "test-operation";
        var duration = TimeSpan.FromMilliseconds(200);
        var exception = new InvalidOperationException("test error");

        // Act
        _healthMonitoringService.RecordFailure(operationType, exception, duration);

        // Assert
        var status = _healthMonitoringService.GetHealthStatus(operationType);
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Unknown, status); // Need more samples for status
    }

    [TestMethod]
    public void GetHealthStatus_UnknownOperationType_ReturnsUnknown()
    {
        // Act
        var status = _healthMonitoringService.GetHealthStatus("non-existent-operation");

        // Assert
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Unknown, status);
    }

    [TestMethod]
    public void GetOverallHealthStatus_NoMetrics_ReturnsUnknown()
    {
        // Act
        var status = _healthMonitoringService.GetOverallHealthStatus();

        // Assert
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Unknown, status);
    }

    [TestMethod]
    public void GetOverallHealthStatus_WithHealthyOperations_ReturnsHealthy()
    {
        // Arrange - Record enough successful operations to establish health status
        var operationType = "test-operation";
        
        for (int i = 0; i < 15; i++) // More than minimum sample size
        {
            _healthMonitoringService.RecordSuccess(operationType, TimeSpan.FromMilliseconds(100));
        }

        // Act
        var status = _healthMonitoringService.GetOverallHealthStatus();

        // Assert
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Healthy, status);
    }

    [TestMethod]
    public void GetOverallHealthStatus_WithWarningOperations_ReturnsWarning()
    {
        // Arrange - Record operations with warning-level error rate (>10% but <25%)
        var operationType = "test-operation";
        
        // 15% error rate: 12 successes + 2 failures = 14 total, 2/14 = 14.3%
        for (int i = 0; i < 12; i++)
        {
            _healthMonitoringService.RecordSuccess(operationType, TimeSpan.FromMilliseconds(100));
        }
        
        for (int i = 0; i < 2; i++)
        {
            _healthMonitoringService.RecordFailure(operationType, new Exception("test"), TimeSpan.FromMilliseconds(200));
        }

        // Act
        var status = _healthMonitoringService.GetOverallHealthStatus();

        // Assert
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Warning, status);
    }

    [TestMethod]
    public void GetOverallHealthStatus_WithCriticalOperations_ReturnsCritical()
    {
        // Arrange - Record operations with critical-level error rate (>=25%)
        var operationType = "test-operation";
        
        // 30% error rate: 7 successes + 3 failures = 10 total, 3/10 = 30%
        for (int i = 0; i < 7; i++)
        {
            _healthMonitoringService.RecordSuccess(operationType, TimeSpan.FromMilliseconds(100));
        }
        
        for (int i = 0; i < 3; i++)
        {
            _healthMonitoringService.RecordFailure(operationType, new Exception("test"), TimeSpan.FromMilliseconds(200));
        }

        // Act
        var status = _healthMonitoringService.GetOverallHealthStatus();

        // Assert
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Critical, status);
    }

    [TestMethod]
    public void GetDetailedHealthReport_WithOperations_ReturnsCompleteReport()
    {
        // Arrange
        var operationType1 = "operation-1";
        var operationType2 = "operation-2";
        
        // Healthy operation
        for (int i = 0; i < 10; i++)
        {
            _healthMonitoringService.RecordSuccess(operationType1, TimeSpan.FromMilliseconds(100));
        }
        
        // Warning operation
        for (int i = 0; i < 8; i++)
        {
            _healthMonitoringService.RecordSuccess(operationType2, TimeSpan.FromMilliseconds(150));
        }
        for (int i = 0; i < 2; i++)
        {
            _healthMonitoringService.RecordFailure(operationType2, new Exception("test error"), TimeSpan.FromMilliseconds(300));
        }

        // Act
        var report = _healthMonitoringService.GetDetailedHealthReport();

        // Assert
        Assert.AreEqual("test-tenant", report.TenantId);
        Assert.AreEqual(2, report.OperationReports.Count);
        
        var operation1Report = report.OperationReports[operationType1];
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Healthy, operation1Report.Status);
        Assert.AreEqual(10, operation1Report.TotalOperations);
        Assert.AreEqual(10, operation1Report.SuccessfulOperations);
        Assert.AreEqual(0, operation1Report.FailedOperations);
        Assert.AreEqual(0.0, operation1Report.ErrorRate, 0.01);
        
        var operation2Report = report.OperationReports[operationType2];
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Warning, operation2Report.Status);
        Assert.AreEqual(10, operation2Report.TotalOperations);
        Assert.AreEqual(8, operation2Report.SuccessfulOperations);
        Assert.AreEqual(2, operation2Report.FailedOperations);
        Assert.AreEqual(0.2, operation2Report.ErrorRate, 0.01);
        Assert.AreEqual(2, operation2Report.RecentErrors.Count); // Two failures recorded
    }

    [TestMethod]
    public void GetDetailedHealthReport_IncludesCircuitBreakerState()
    {
        // Arrange
        var operationType = "test-operation";
        _healthMonitoringService.RecordSuccess(operationType, TimeSpan.FromMilliseconds(100));

        // Act
        var report = _healthMonitoringService.GetDetailedHealthReport();

        // Assert
        var operationReport = report.OperationReports[operationType];
        Assert.AreEqual("Closed", operationReport.CircuitBreakerState);
    }
}

[TestClass]
public class HealthMetricsTests
{
    private HealthMetrics _healthMetrics = null!;

    [TestInitialize]
    public void Setup()
    {
        _healthMetrics = new HealthMetrics("test-operation");
    }

    [TestMethod]
    public void InitialState_HasCorrectDefaults()
    {
        // Assert
        Assert.AreEqual("test-operation", _healthMetrics.OperationType);
        Assert.AreEqual(0, _healthMetrics.TotalOperations);
        Assert.AreEqual(0, _healthMetrics.SuccessfulOperations);
        Assert.AreEqual(0, _healthMetrics.FailedOperations);
        Assert.AreEqual(DateTime.MinValue, _healthMetrics.LastOperationTime);
    }

    [TestMethod]
    public void RecordSuccess_UpdatesMetrics()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(100);
        var beforeTime = DateTime.UtcNow;

        // Act
        _healthMetrics.RecordSuccess(duration);
        var afterTime = DateTime.UtcNow;

        // Assert
        Assert.AreEqual(1, _healthMetrics.TotalOperations);
        Assert.AreEqual(1, _healthMetrics.SuccessfulOperations);
        Assert.AreEqual(0, _healthMetrics.FailedOperations);
        Assert.IsTrue(_healthMetrics.LastOperationTime >= beforeTime && _healthMetrics.LastOperationTime <= afterTime);
    }

    [TestMethod]
    public void RecordFailure_UpdatesMetrics()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(200);
        var exception = new InvalidOperationException("test error");
        var beforeTime = DateTime.UtcNow;

        // Act
        _healthMetrics.RecordFailure(exception, duration);
        var afterTime = DateTime.UtcNow;

        // Assert
        Assert.AreEqual(1, _healthMetrics.TotalOperations);
        Assert.AreEqual(0, _healthMetrics.SuccessfulOperations);
        Assert.AreEqual(1, _healthMetrics.FailedOperations);
        Assert.IsTrue(_healthMetrics.LastOperationTime >= beforeTime && _healthMetrics.LastOperationTime <= afterTime);
    }

    [TestMethod]
    public void GetErrorRate_CalculatesCorrectly()
    {
        // Arrange
        _healthMetrics.RecordSuccess(TimeSpan.FromMilliseconds(100));
        _healthMetrics.RecordSuccess(TimeSpan.FromMilliseconds(100));
        _healthMetrics.RecordFailure(new Exception("test"), TimeSpan.FromMilliseconds(200));

        // Act
        var errorRate = _healthMetrics.GetErrorRate();

        // Assert
        Assert.AreEqual(1.0 / 3.0, errorRate, 0.01); // 1 failure out of 3 operations
    }

    [TestMethod]
    public void GetErrorRate_NoOperations_ReturnsZero()
    {
        // Act
        var errorRate = _healthMetrics.GetErrorRate();

        // Assert
        Assert.AreEqual(0.0, errorRate);
    }

    [TestMethod]
    public void GetAverageResponseTime_CalculatesCorrectly()
    {
        // Arrange
        _healthMetrics.RecordSuccess(TimeSpan.FromMilliseconds(100));
        _healthMetrics.RecordSuccess(TimeSpan.FromMilliseconds(200));
        _healthMetrics.RecordFailure(new Exception("test"), TimeSpan.FromMilliseconds(300));

        // Act
        var avgTime = _healthMetrics.GetAverageResponseTime();

        // Assert
        Assert.AreEqual(200.0, avgTime.TotalMilliseconds, 0.1); // (100 + 200 + 300) / 3 = 200
    }

    [TestMethod]
    public void GetHealthStatus_InsufficientSamples_ReturnsUnknown()
    {
        // Arrange - Record less than minimum sample size
        for (int i = 0; i < 5; i++)
        {
            _healthMetrics.RecordSuccess(TimeSpan.FromMilliseconds(100));
        }

        // Act
        var status = _healthMetrics.GetHealthStatus();

        // Assert
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Unknown, status);
    }

    [TestMethod]
    public void GetHealthStatus_WarningErrorRate_ReturnsWarning()
    {
        // Arrange - 15% error rate (between 10% and 25%)
        for (int i = 0; i < 8; i++)
        {
            _healthMetrics.RecordSuccess(TimeSpan.FromMilliseconds(100));
        }
        for (int i = 0; i < 2; i++) // 2 out of 10 = 20%
        {
            _healthMetrics.RecordFailure(new Exception("test"), TimeSpan.FromMilliseconds(200));
        }

        // Act
        var status = _healthMetrics.GetHealthStatus();

        // Assert
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Warning, status);
    }

    [TestMethod]
    public void GetHealthStatus_CriticalErrorRate_ReturnsCritical()
    {
        // Arrange - 30% error rate (>= 25%)
        for (int i = 0; i < 7; i++)
        {
            _healthMetrics.RecordSuccess(TimeSpan.FromMilliseconds(100));
        }
        for (int i = 0; i < 3; i++) // 3 out of 10 = 30%
        {
            _healthMetrics.RecordFailure(new Exception("test"), TimeSpan.FromMilliseconds(200));
        }

        // Act
        var status = _healthMetrics.GetHealthStatus();

        // Assert
        Assert.AreEqual(HealthMonitoringService.HealthStatus.Critical, status);
    }

    [TestMethod]
    public void GetRecentErrors_ReturnsLatestErrors()
    {
        // Arrange
        _healthMetrics.RecordFailure(new ArgumentException("error 1"), TimeSpan.FromMilliseconds(100));
        Thread.Sleep(1); // Ensure different timestamps
        _healthMetrics.RecordFailure(new InvalidOperationException("error 2"), TimeSpan.FromMilliseconds(100));
        Thread.Sleep(1);
        _healthMetrics.RecordFailure(new TimeoutException("error 3"), TimeSpan.FromMilliseconds(100));

        // Act
        var recentErrors = _healthMetrics.GetRecentErrors(2);

        // Assert
        Assert.AreEqual(2, recentErrors.Count);
        Assert.IsTrue(recentErrors[0].Contains("TimeoutException")); // Most recent first
        Assert.IsTrue(recentErrors[1].Contains("InvalidOperationException"));
    }

    [TestMethod]
    public void CleanupOldData_RemovesOldOperations()
    {
        // Arrange
        _healthMetrics.RecordSuccess(TimeSpan.FromMilliseconds(100));
        _healthMetrics.RecordFailure(new Exception("old error"), TimeSpan.FromMilliseconds(200));
        
        var cutoffTime = DateTime.UtcNow.AddMinutes(1); // Future cutoff

        // Act
        _healthMetrics.CleanupOldData(cutoffTime);

        // Assert
        Assert.AreEqual(0, _healthMetrics.TotalOperations);
        Assert.AreEqual(0, _healthMetrics.SuccessfulOperations);
        Assert.AreEqual(0, _healthMetrics.FailedOperations);
    }
}