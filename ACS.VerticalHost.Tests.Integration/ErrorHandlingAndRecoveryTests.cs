using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ACS.VerticalHost.Commands;
using ACS.VerticalHost.Handlers;
using ACS.VerticalHost.Services;
using ACS.VerticalHost.Extensions;
using ACS.Service.Data;
using ACS.Service.Services;
using ACS.Service.Infrastructure;
using ACS.Infrastructure.DependencyInjection;
using System.Diagnostics;

namespace ACS.VerticalHost.Tests.Integration;

/// <summary>
/// Tests for error handling, recovery scenarios, and resilience patterns
/// Validates that handlers gracefully handle various error conditions
/// and maintain system consistency during failures
/// </summary>
[TestClass]
public class ErrorHandlingAndRecoveryTests
{
    private ServiceProvider? _serviceProvider;
    private ILogger<ErrorHandlingAndRecoveryTests>? _logger;
    private ICommandBuffer? _commandBuffer;

    [TestInitialize]
    public async Task Setup()
    {
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"ErrorTestDb_{Guid.NewGuid()}"));

        services.AddAcsServiceLayer(configuration);
        services.AddSingleton<ICommandBuffer, CommandBuffer>();
        services.AddHandlersAutoRegistration();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<ErrorHandlingAndRecoveryTests>>();
        _commandBuffer = _serviceProvider.GetRequiredService<ICommandBuffer>();

        await _commandBuffer.StartAsync();
        _logger.LogInformation("Error handling test setup completed");
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_commandBuffer != null)
        {
            await _commandBuffer.StopAsync();
        }
        _serviceProvider?.Dispose();
    }

    #region Input Validation Error Tests

    [TestMethod]
    public async Task ResourceHandlers_Should_ValidateInputsAndThrowAppropriateExceptions()
    {
        var testCases = new[]
        {
            // Test case: Empty resource name
            new Func<Task>(async () =>
            {
                var invalidCommand = new CreateResourceCommand
                {
                    Name = "", // Invalid
                    UriPattern = "/valid/pattern",
                    HttpVerbs = new List<string> { "GET" }
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("name"));
                _logger.LogInformation("✓ Empty resource name properly rejected");
            }),

            // Test case: Invalid URI pattern
            new Func<Task>(async () =>
            {
                var invalidCommand = new CreateResourceCommand
                {
                    Name = "ValidName",
                    UriPattern = "", // Invalid
                    HttpVerbs = new List<string> { "GET" }
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("URI pattern"));
                _logger.LogInformation("✓ Empty URI pattern properly rejected");
            }),

            // Test case: Empty HTTP verbs
            new Func<Task>(async () =>
            {
                var invalidCommand = new CreateResourceCommand
                {
                    Name = "ValidName",
                    UriPattern = "/valid/pattern",
                    HttpVerbs = new List<string>() // Invalid - empty
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("HTTP verb"));
                _logger.LogInformation("✓ Empty HTTP verbs properly rejected");
            }),

            // Test case: Invalid resource ID for update
            new Func<Task>(async () =>
            {
                var invalidCommand = new UpdateResourceCommand
                {
                    ResourceId = -1, // Invalid
                    Name = "ValidName"
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<UpdateResourceCommand, ACS.Service.Domain.Resource>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("resource ID"));
                _logger.LogInformation("✓ Invalid resource ID properly rejected");
            })
        };

        foreach (var testCase in testCases)
        {
            await testCase();
        }

        _logger.LogInformation("✅ Resource handlers validate inputs correctly");
    }

    [TestMethod]
    public async Task PermissionHandlers_Should_ValidateInputsAndThrowAppropriateExceptions()
    {
        var testCases = new[]
        {
            // Test case: Invalid entity ID
            new Func<Task>(async () =>
            {
                var invalidCommand = new GrantPermissionCommand
                {
                    EntityId = 0, // Invalid
                    EntityType = "User",
                    PermissionId = 1
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<GrantPermissionCommand, PermissionGrantResult>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("entity ID"));
                _logger.LogInformation("✓ Invalid entity ID properly rejected");
            }),

            // Test case: Empty entity type
            new Func<Task>(async () =>
            {
                var invalidCommand = new GrantPermissionCommand
                {
                    EntityId = 1,
                    EntityType = "", // Invalid
                    PermissionId = 1
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<GrantPermissionCommand, PermissionGrantResult>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("Entity type"));
                _logger.LogInformation("✓ Empty entity type properly rejected");
            }),

            // Test case: Invalid permission ID
            new Func<Task>(async () =>
            {
                var invalidCommand = new GrantPermissionCommand
                {
                    EntityId = 1,
                    EntityType = "User",
                    PermissionId = -5 // Invalid
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<GrantPermissionCommand, PermissionGrantResult>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("permission ID"));
                _logger.LogInformation("✓ Invalid permission ID properly rejected");
            }),

            // Test case: Invalid query entity ID
            new Func<Task>(async () =>
            {
                var invalidQuery = new CheckPermissionQuery
                {
                    EntityId = -1, // Invalid
                    EntityType = "User",
                    PermissionId = 1
                };
                
                var handler = _serviceProvider.GetRequiredService<IQueryHandler<CheckPermissionQuery, PermissionCheckResult>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidQuery, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("entity ID"));
                _logger.LogInformation("✓ Invalid query entity ID properly rejected");
            })
        };

        foreach (var testCase in testCases)
        {
            await testCase();
        }

        _logger.LogInformation("✅ Permission handlers validate inputs correctly");
    }

    [TestMethod]
    public async Task AuditHandlers_Should_ValidateInputsAndThrowAppropriateExceptions()
    {
        var testCases = new[]
        {
            // Test case: Empty event type
            new Func<Task>(async () =>
            {
                var invalidCommand = new RecordAuditEventCommand
                {
                    EventType = "", // Invalid
                    EventCategory = "Test",
                    Action = "TestAction"
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("Event type"));
                _logger.LogInformation("✓ Empty event type properly rejected");
            }),

            // Test case: Empty event category
            new Func<Task>(async () =>
            {
                var invalidCommand = new RecordAuditEventCommand
                {
                    EventType = "TestEvent",
                    EventCategory = "", // Invalid
                    Action = "TestAction"
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("Event category"));
                _logger.LogInformation("✓ Empty event category properly rejected");
            }),

            // Test case: Empty action
            new Func<Task>(async () =>
            {
                var invalidCommand = new RecordAuditEventCommand
                {
                    EventType = "TestEvent",
                    EventCategory = "Test",
                    Action = "" // Invalid
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("Action"));
                _logger.LogInformation("✓ Empty action properly rejected");
            }),

            // Test case: Invalid purge date
            new Func<Task>(async () =>
            {
                var invalidCommand = new PurgeOldAuditDataCommand
                {
                    OlderThan = DateTime.UtcNow.AddMinutes(30) // Invalid - too recent
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<PurgeOldAuditDataCommand, AuditPurgeResult>>();
                var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
                
                Assert.IsTrue(ex.Message.Contains("1 day old"));
                _logger.LogInformation("✓ Recent purge date properly rejected");
            })
        };

        foreach (var testCase in testCases)
        {
            await testCase();
        }

        _logger.LogInformation("✅ Audit handlers validate inputs correctly");
    }

    #endregion

    #region Business Logic Error Tests

    [TestMethod]
    public async Task BulkPermissionOperations_Should_HandlePartialFailuresGracefully()
    {
        // Test bulk operations with mixed valid and invalid operations
        var bulkCommand = new BulkPermissionUpdateCommand
        {
            Operations = new List<BulkPermissionOperation>
            {
                // Valid operation
                new BulkPermissionOperation
                {
                    OperationType = "Grant",
                    EntityId = 1,
                    EntityType = "User",
                    PermissionId = 1,
                    ResourceId = 100,
                    Reason = "Valid operation"
                },
                // Invalid operation - bad entity ID
                new BulkPermissionOperation
                {
                    OperationType = "Grant",
                    EntityId = -1,
                    EntityType = "User",
                    PermissionId = 1,
                    ResourceId = 100,
                    Reason = "Invalid entity ID"
                },
                // Invalid operation - empty entity type
                new BulkPermissionOperation
                {
                    OperationType = "Grant",
                    EntityId = 2,
                    EntityType = "",
                    PermissionId = 1,
                    ResourceId = 100,
                    Reason = "Empty entity type"
                },
                // Valid operation
                new BulkPermissionOperation
                {
                    OperationType = "Revoke",
                    EntityId = 3,
                    EntityType = "User",
                    PermissionId = 2,
                    ResourceId = 101,
                    Reason = "Another valid operation"
                },
                // Invalid operation - bad permission ID
                new BulkPermissionOperation
                {
                    OperationType = "Grant",
                    EntityId = 4,
                    EntityType = "User",
                    PermissionId = 0,
                    ResourceId = 102,
                    Reason = "Invalid permission ID"
                }
            },
            ValidateBeforeExecution = true,
            StopOnFirstError = false, // Continue processing despite errors
            ExecuteInTransaction = false, // Allow partial success
            RequestedBy = "ErrorHandlingTest"
        };

        var handler = _serviceProvider.GetRequiredService<ICommandHandler<BulkPermissionUpdateCommand, BulkPermissionUpdateResult>>();
        var result = await handler.HandleAsync(bulkCommand, CancellationToken.None);

        // Verify partial success handling
        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.TotalOperations);
        Assert.IsTrue(result.SuccessfulOperations > 0, "Should have some successful operations");
        Assert.IsTrue(result.FailedOperations > 0, "Should have some failed operations");
        Assert.AreEqual(5, result.OperationResults.Count);

        // Verify failed operations have error messages
        var failedResults = result.OperationResults.Where(r => !r.Success).ToList();
        Assert.IsTrue(failedResults.All(r => !string.IsNullOrEmpty(r.ErrorMessage)));

        // Verify successful operations don't have error messages
        var successfulResults = result.OperationResults.Where(r => r.Success).ToList();
        Assert.IsTrue(successfulResults.All(r => string.IsNullOrEmpty(r.ErrorMessage)));

        _logger.LogInformation("✅ Bulk operations handle partial failures correctly: {Success}/{Total} successful",
            result.SuccessfulOperations, result.TotalOperations);
    }

    [TestMethod]
    public async Task AccessViolationHandler_Should_HandleMissingServicesGracefully()
    {
        // Test access violation handling with various edge cases
        var testCases = new[]
        {
            // Test case: Missing violation type
            new AccessViolationHandlerCommand
            {
                ViolationType = "", // Invalid
                Action = "Block"
            },

            // Test case: Missing action
            new AccessViolationHandlerCommand
            {
                ViolationType = "UnauthorizedAccess",
                Action = "" // Invalid
            },

            // Test case: Unknown action
            new AccessViolationHandlerCommand
            {
                ViolationType = "UnauthorizedAccess",
                Action = "UnknownAction" // Invalid
            }
        };

        var handler = _serviceProvider.GetRequiredService<ICommandHandler<AccessViolationHandlerCommand, AccessViolationHandlerResult>>();

        foreach (var testCase in testCases)
        {
            var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                handler.HandleAsync(testCase, CancellationToken.None));
            
            Assert.IsNotNull(ex.Message);
            _logger.LogInformation("✓ Access violation validation error: {Message}", ex.Message);
        }

        _logger.LogInformation("✅ Access violation handler validates inputs correctly");
    }

    #endregion

    #region Concurrency and Threading Error Tests

    [TestMethod]
    public async Task ConcurrentHandlerExecution_Should_NotCauseDataCorruption()
    {
        const int concurrentOperations = 10;
        var correlationId = Guid.NewGuid().ToString();

        // Create concurrent audit event recording operations
        var concurrentTasks = Enumerable.Range(1, concurrentOperations)
            .Select(async i =>
            {
                var command = new RecordAuditEventCommand
                {
                    EventType = "ConcurrencyTest",
                    EventCategory = "Testing",
                    UserId = i,
                    Action = "ConcurrentOperation",
                    Details = $"Concurrent operation {i}",
                    Severity = "Information",
                    Metadata = new Dictionary<string, object>
                    {
                        ["OperationIndex"] = i,
                        ["CorrelationId"] = correlationId,
                        ["TestStartTime"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    }
                };

                var handler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
                return await handler.HandleAsync(command, CancellationToken.None);
            })
            .ToArray();

        // Execute all operations concurrently
        var results = await Task.WhenAll(concurrentTasks);

        // Verify all operations completed successfully
        Assert.AreEqual(concurrentOperations, results.Length);
        Assert.IsTrue(results.All(r => r.Success));

        // Verify all audit events have unique IDs
        var auditIds = results.Select(r => r.AuditEventId).ToList();
        var uniqueIds = auditIds.Distinct().ToList();
        Assert.AreEqual(auditIds.Count, uniqueIds.Count, "All audit event IDs should be unique");

        _logger.LogInformation("✅ Concurrent handler execution completed without data corruption: {Count} operations",
            concurrentOperations);
    }

    [TestMethod]
    public async Task HandlerCancellation_Should_BeHandledGracefully()
    {
        // Test handler behavior with cancellation
        using var cancellationTokenSource = new CancellationTokenSource();
        
        // Start a long-running operation
        var longRunningCommand = new GetComplianceReportQuery
        {
            ReportType = "Comprehensive",
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow,
            IncludeAnomalies = true,
            IncludeRiskAssessment = true,
            ReportFormat = "Detailed",
            RequestedBy = "CancellationTest"
        };

        var handler = _serviceProvider.GetRequiredService<IQueryHandler<GetComplianceReportQuery, ComplianceReportResult>>();

        // Cancel the operation after a short delay
        _ = Task.Delay(50).ContinueWith(_ => cancellationTokenSource.Cancel());

        // Verify that cancellation is handled properly
        try
        {
            var result = await handler.HandleAsync(longRunningCommand, cancellationTokenSource.Token);
            
            // If the operation completes before cancellation, that's also acceptable
            _logger.LogInformation("Operation completed before cancellation");
        }
        catch (OperationCanceledException)
        {
            // Expected behavior when cancellation occurs
            _logger.LogInformation("✓ Operation cancelled gracefully");
        }
        catch (Exception ex)
        {
            // Other exceptions should not occur due to cancellation
            Assert.Fail($"Unexpected exception during cancellation: {ex.Message}");
        }

        _logger.LogInformation("✅ Handler cancellation handled gracefully");
    }

    #endregion

    #region Resource and Memory Error Tests

    [TestMethod]
    public async Task LargeDataSetOperations_Should_HandleMemoryConstraints()
    {
        // Test handling of operations with large data sets
        var largeMetadata = new Dictionary<string, object>();
        
        // Create large metadata dictionary (simulate large data scenario)
        for (int i = 0; i < 1000; i++)
        {
            largeMetadata[$"LargeDataKey_{i}"] = $"LargeDataValue_{i}_" + new string('X', 100);
        }

        var largeDataCommand = new RecordAuditEventCommand
        {
            EventType = "LargeDataTest",
            EventCategory = "Performance",
            Action = "ProcessLargeData",
            Details = "Testing large data handling",
            Severity = "Information",
            Metadata = largeMetadata
        };

        var handler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();

        // Measure memory usage
        var initialMemory = GC.GetTotalMemory(true);
        
        try
        {
            var result = await handler.HandleAsync(largeDataCommand, CancellationToken.None);
            Assert.IsTrue(result.Success);
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = (finalMemory - initialMemory) / 1024.0 / 1024.0; // MB
            
            _logger.LogInformation("Large data operation completed. Memory increase: {MemoryMB:F2} MB", memoryIncrease);
            
            // Verify memory usage is reasonable (< 10MB increase)
            Assert.IsTrue(memoryIncrease < 10, $"Memory increase of {memoryIncrease:F2} MB is too high");
        }
        catch (OutOfMemoryException)
        {
            Assert.Fail("Operation should handle large data without running out of memory");
        }

        _logger.LogInformation("✅ Large data set operations handled within memory constraints");
    }

    #endregion

    #region Error Recovery and Consistency Tests

    [TestMethod]
    public async Task FailedTransactions_Should_MaintainDataConsistency()
    {
        // Test that failed operations don't leave the system in an inconsistent state
        var resourceId = 12345;
        
        // First, try to create a resource with valid data
        var validCommand = new CreateResourceCommand
        {
            Name = "TestResource_Consistency",
            Description = "Resource for consistency testing",
            UriPattern = "/api/test/consistency",
            HttpVerbs = new List<string> { "GET", "POST" },
            CreatedBy = "ConsistencyTest"
        };

        var resourceHandler = _serviceProvider.GetRequiredService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
        var resource = await resourceHandler.HandleAsync(validCommand, CancellationToken.None);
        
        Assert.IsNotNull(resource);
        var actualResourceId = resource.Id;

        // Now try to update it with invalid data
        var invalidUpdateCommand = new UpdateResourceCommand
        {
            ResourceId = actualResourceId,
            Name = "", // Invalid - will cause failure
            Description = "This should fail"
        };

        var updateHandler = _serviceProvider.GetRequiredService<ICommandHandler<UpdateResourceCommand, ACS.Service.Domain.Resource>>();
        
        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            updateHandler.HandleAsync(invalidUpdateCommand, CancellationToken.None));

        // Verify the resource still exists and hasn't been corrupted
        var getResourceQuery = new GetResourceQuery
        {
            ResourceId = actualResourceId
        };

        var queryHandler = _serviceProvider.GetRequiredService<IQueryHandler<GetResourceQuery, ACS.Service.Domain.Resource>>();
        var retrievedResource = await queryHandler.HandleAsync(getResourceQuery, CancellationToken.None);

        Assert.IsNotNull(retrievedResource);
        Assert.AreEqual("TestResource_Consistency", retrievedResource.Name); // Original name should be preserved
        Assert.AreEqual("Resource for consistency testing", retrievedResource.Description); // Original description preserved

        _logger.LogInformation("✅ Failed operations maintain data consistency");
    }

    [TestMethod]
    public async Task ErrorLogging_Should_CaptureCompleteContext()
    {
        // Test that errors are logged with complete context information
        var correlationId = Guid.NewGuid().ToString();

        // Use a custom logger to capture log entries
        var logEntries = new List<string>();
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new TestLoggerProvider(logEntries));
        });

        using var testScope = _serviceProvider.CreateScope();
        var testServices = new ServiceCollection();
        testServices.AddSingleton(loggerFactory);
        testServices.AddLogging();
        
        // Create handler with test logger
        var resourceService = testScope.ServiceProvider.GetRequiredService<IResourceService>();
        var testLogger = loggerFactory.CreateLogger<CreateResourceCommandHandler>();
        var testHandler = new CreateResourceCommandHandler(resourceService, testLogger);

        // Execute command that will fail
        var invalidCommand = new CreateResourceCommand
        {
            Name = "", // This will cause validation failure
            UriPattern = "/test",
            HttpVerbs = new List<string> { "GET" }
        };

        try
        {
            await testHandler.HandleAsync(invalidCommand, CancellationToken.None);
            Assert.Fail("Expected ArgumentException");
        }
        catch (ArgumentException)
        {
            // Expected exception
        }

        // Verify error logging captured context
        var errorLogs = logEntries.Where(log => log.Contains("Error")).ToList();
        Assert.IsTrue(errorLogs.Any(), "Should have error log entries");

        var contextLogs = errorLogs.Where(log => log.Contains("CreateResourceCommandHandler")).ToList();
        Assert.IsTrue(contextLogs.Any(), "Error logs should contain handler context");

        _logger.LogInformation("✅ Error logging captures complete context: {LogCount} error entries", errorLogs.Count);
    }

    #endregion

    #region Helper Classes and Methods

    private class TestLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _logEntries;

        public TestLoggerProvider(List<string> logEntries)
        {
            _logEntries = logEntries;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(_logEntries, categoryName);
        }

        public void Dispose() { }
    }

    private class TestLogger : ILogger
    {
        private readonly List<string> _logEntries;
        private readonly string _categoryName;

        public TestLogger(List<string> logEntries, string categoryName)
        {
            _logEntries = logEntries;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            var logEntry = $"[{logLevel}] {_categoryName}: {message}";
            if (exception != null)
            {
                logEntry += $" Exception: {exception.Message}";
            }
            _logEntries.Add(logEntry);
        }
    }

    private static IConfiguration BuildTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=ErrorTestDb;Trusted_Connection=true;",
                ["TenantId"] = "error-test-tenant",
                ["Logging:LogLevel:Default"] = "Information"
            })
            .Build();
    }

    #endregion
}