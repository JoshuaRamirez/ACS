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
/// Comprehensive integration tests for business domain handlers
/// Validates auto-registration, command buffer integration, cross-handler dependencies,
/// and end-to-end business workflows
/// </summary>
[TestClass]
public class HandlerIntegrationTests
{
    private ServiceProvider? _serviceProvider;
    private ILogger<HandlerIntegrationTests>? _logger;
    private ICommandBuffer? _commandBuffer;

    [TestInitialize]
    public async Task Setup()
    {
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();

        // Configure logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Add test database context
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

        // Add all service dependencies
        services.AddAcsServiceLayer(configuration);
        services.AddSingleton<ICommandBuffer, CommandBuffer>();
        services.AddHandlersAutoRegistration();

        // Build service provider
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<HandlerIntegrationTests>>();
        _commandBuffer = _serviceProvider.GetRequiredService<ICommandBuffer>();

        // Start command buffer
        await _commandBuffer.StartAsync();

        _logger.LogInformation("Test setup completed - Command buffer active");
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

    #region Auto-Registration Integration Tests

    [TestMethod]
    public void AutoRegistration_Should_RegisterAllBusinessDomainHandlers()
    {
        // Verify Resource Handlers
        Assert.IsNotNull(_serviceProvider.GetService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>());
        Assert.IsNotNull(_serviceProvider.GetService<ICommandHandler<UpdateResourceCommand, ACS.Service.Domain.Resource>>());
        Assert.IsNotNull(_serviceProvider.GetService<ICommandHandler<DeleteResourceCommand, DeleteResourceResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<GetResourceQuery, ACS.Service.Domain.Resource>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<GetResourcesQuery, List<ACS.Service.Domain.Resource>>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<GetResourcePermissionsQuery, List<ResourcePermissionInfo>>>());

        // Verify Permission Handlers
        Assert.IsNotNull(_serviceProvider.GetService<ICommandHandler<GrantPermissionCommand, PermissionGrantResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<ICommandHandler<RevokePermissionCommand, PermissionRevokeResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<ICommandHandler<ValidatePermissionStructureCommand, PermissionValidationResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<CheckPermissionQuery, PermissionCheckResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<GetEntityPermissionsQuery, List<EntityPermissionInfo>>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<GetPermissionUsageQuery, List<PermissionUsageInfo>>>());

        // Verify Audit Handlers
        Assert.IsNotNull(_serviceProvider.GetService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<ICommandHandler<PurgeOldAuditDataCommand, AuditPurgeResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<GetAuditLogQuery, List<AuditLogEntry>>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<GetUserAuditTrailQuery, List<UserAuditTrailEntry>>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<GetComplianceReportQuery, ComplianceReportResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<ValidateAuditIntegrityQuery, AuditIntegrityResult>>());

        // Verify Access Control Handlers
        Assert.IsNotNull(_serviceProvider.GetService<ICommandHandler<BulkPermissionUpdateCommand, BulkPermissionUpdateResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<ICommandHandler<AccessViolationHandlerCommand, AccessViolationHandlerResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<EvaluateComplexPermissionQuery, ComplexPermissionEvaluationResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<GetEffectivePermissionsQuery, EffectivePermissionsResult>>());
        Assert.IsNotNull(_serviceProvider.GetService<IQueryHandler<PermissionImpactAnalysisQuery, PermissionImpactAnalysisResult>>());

        _logger.LogInformation("✅ All 23 business domain handlers are properly registered");
    }

    [TestMethod]
    public void AutoRegistration_Should_UseCorrectServiceLifetime()
    {
        // All handlers should be registered as Transient
        using var scope1 = _serviceProvider.CreateScope();
        using var scope2 = _serviceProvider.CreateScope();

        var handler1_scope1 = scope1.ServiceProvider.GetService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
        var handler1_scope2 = scope2.ServiceProvider.GetService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();

        // Different scopes should have different instances (Transient)
        Assert.AreNotSame(handler1_scope1, handler1_scope2);

        _logger.LogInformation("✅ Handler registration uses correct Transient lifetime");
    }

    #endregion

    #region Command Buffer Integration Tests

    [TestMethod]
    public async Task CommandBuffer_Should_ProcessCommandsSequentially()
    {
        var commandBuffer = _serviceProvider.GetRequiredService<ICommandBuffer>();
        var executionOrder = new List<string>();
        var lockObject = new object();

        // Create multiple commands that will be processed
        var commands = Enumerable.Range(1, 5).Select(i => new TestTrackingCommand
        {
            Id = i,
            OnExecute = () =>
            {
                lock (lockObject)
                {
                    executionOrder.Add($"Command_{i}");
                    Thread.Sleep(10); // Simulate work
                }
            }
        }).ToList();

        // Submit all commands
        var tasks = commands.Select(cmd => commandBuffer.SubmitCommandAsync(cmd)).ToArray();
        await Task.WhenAll(tasks);

        // Verify sequential execution (should be 1, 2, 3, 4, 5)
        CollectionAssert.AreEqual(new[] { "Command_1", "Command_2", "Command_3", "Command_4", "Command_5" }, executionOrder);

        _logger.LogInformation("✅ Command buffer processes commands sequentially");
    }

    [TestMethod]
    public async Task QueryHandlers_Should_ExecuteImmediately()
    {
        var commandBuffer = _serviceProvider.GetRequiredService<ICommandBuffer>();
        var stopwatch = Stopwatch.StartNew();

        // Submit a query - should execute immediately
        var queryResult = await commandBuffer.SubmitQueryAsync(new TestImmediateQuery());
        
        stopwatch.Stop();
        
        // Query should complete very quickly (not wait in buffer)
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 100);
        Assert.AreEqual("QueryExecuted", queryResult);

        _logger.LogInformation("✅ Queries execute immediately without buffering");
    }

    #endregion

    #region Cross-Handler Dependencies Tests

    [TestMethod]
    public async Task ResourceAndPermissionHandlers_Should_IntegrateCorrectly()
    {
        // This test simulates a workflow: Create Resource → Grant Permission → Check Permission
        var resourceService = _serviceProvider.GetRequiredService<IResourceService>();
        var permissionService = _serviceProvider.GetRequiredService<IPermissionService>();

        // Step 1: Create a test resource
        var createResourceCommand = new CreateResourceCommand
        {
            Name = "TestResource_Integration",
            Description = "Integration test resource",
            UriPattern = "/api/test/{id}",
            HttpVerbs = new List<string> { "GET", "POST" },
            CreatedBy = "IntegrationTest"
        };

        var resourceHandler = _serviceProvider.GetRequiredService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
        var resource = await resourceHandler.HandleAsync(createResourceCommand, CancellationToken.None);

        Assert.IsNotNull(resource);
        Assert.AreEqual("TestResource_Integration", resource.Name);

        // Step 2: Grant permission for this resource
        var grantPermissionCommand = new GrantPermissionCommand
        {
            EntityId = 1, // Test user
            EntityType = "User",
            PermissionId = 1, // Test permission
            ResourceId = resource.Id,
            GrantedBy = "IntegrationTest"
        };

        var grantHandler = _serviceProvider.GetRequiredService<ICommandHandler<GrantPermissionCommand, PermissionGrantResult>>();
        var grantResult = await grantHandler.HandleAsync(grantPermissionCommand, CancellationToken.None);

        Assert.IsTrue(grantResult.Success);
        Assert.AreEqual(resource.Id, grantResult.ResourceId);

        // Step 3: Check the granted permission
        var checkPermissionQuery = new CheckPermissionQuery
        {
            EntityId = 1,
            EntityType = "User",
            PermissionId = 1,
            ResourceId = resource.Id
        };

        var checkHandler = _serviceProvider.GetRequiredService<IQueryHandler<CheckPermissionQuery, PermissionCheckResult>>();
        var checkResult = await checkHandler.HandleAsync(checkPermissionQuery, CancellationToken.None);

        Assert.IsTrue(checkResult.HasPermission);
        Assert.AreEqual(resource.Id, checkResult.ResourceId);

        _logger.LogInformation("✅ Resource and Permission handlers integrate correctly");
    }

    [TestMethod]
    public async Task AuditHandlers_Should_IntegrateWithBusinessOperations()
    {
        // Test that business operations generate audit events
        var auditService = _serviceProvider.GetRequiredService<IAuditService>();

        // Record an audit event
        var recordAuditCommand = new RecordAuditEventCommand
        {
            EventType = "PermissionGrant",
            EventCategory = "Security",
            UserId = 1,
            ResourceId = 100,
            Action = "GrantPermission",
            Details = "Integration test audit event",
            Severity = "Information"
        };

        var auditHandler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
        var auditResult = await auditHandler.HandleAsync(recordAuditCommand, CancellationToken.None);

        Assert.IsTrue(auditResult.Success);
        Assert.IsTrue(auditResult.AuditEventId > 0);

        // Query the audit log to verify the event was recorded
        var getAuditQuery = new GetAuditLogQuery
        {
            EventTypes = new List<string> { "PermissionGrant" },
            UserId = 1,
            PageSize = 10
        };

        var auditQueryHandler = _serviceProvider.GetRequiredService<IQueryHandler<GetAuditLogQuery, List<AuditLogEntry>>>();
        var auditEntries = await auditQueryHandler.HandleAsync(getAuditQuery, CancellationToken.None);

        Assert.IsTrue(auditEntries.Any());
        var recordedEvent = auditEntries.First();
        Assert.AreEqual("PermissionGrant", recordedEvent.EventType);
        Assert.AreEqual(1, recordedEvent.UserId);

        _logger.LogInformation("✅ Audit handlers integrate with business operations");
    }

    #endregion

    #region End-to-End Business Workflow Tests

    [TestMethod]
    public async Task CompleteAccessControlWorkflow_Should_Work()
    {
        // Test a complete access control workflow:
        // 1. Create user and resource
        // 2. Grant permissions
        // 3. Evaluate complex permissions
        // 4. Audit the operations
        // 5. Generate compliance report

        var correlationId = Guid.NewGuid().ToString();

        // Step 1: Create a resource
        var resourceCommand = new CreateResourceCommand
        {
            Name = "SensitiveAPI",
            Description = "Sensitive API endpoint for testing",
            UriPattern = "/api/sensitive/{id}",
            HttpVerbs = new List<string> { "GET", "PUT", "DELETE" },
            CreatedBy = "E2ETest"
        };

        var resourceHandler = _serviceProvider.GetRequiredService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
        var resource = await resourceHandler.HandleAsync(resourceCommand, CancellationToken.None);

        // Step 2: Grant permissions with complex conditions
        var grantCommand = new GrantPermissionCommand
        {
            EntityId = 1,
            EntityType = "User",
            PermissionId = 1,
            ResourceId = resource.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            GrantedBy = "E2ETest",
            Reason = "End-to-end integration test"
        };

        var grantHandler = _serviceProvider.GetRequiredService<ICommandHandler<GrantPermissionCommand, PermissionGrantResult>>();
        var grantResult = await grantHandler.HandleAsync(grantCommand, CancellationToken.None);

        Assert.IsTrue(grantResult.Success);

        // Step 3: Evaluate complex permissions
        var complexPermissionQuery = new EvaluateComplexPermissionQuery
        {
            UserId = 1,
            ResourceId = resource.Id,
            Action = "READ",
            Context = new Dictionary<string, object>
            {
                { "IpAddress", "192.168.1.100" },
                { "TimeOfDay", DateTime.UtcNow.Hour }
            },
            Conditions = new List<PermissionCondition>
            {
                new PermissionCondition
                {
                    Type = "TimeWindow",
                    Operator = "Between",
                    Value = new { Start = 8, End = 18 } // Business hours
                }
            },
            IncludeReasoningTrace = true
        };

        var complexPermissionHandler = _serviceProvider.GetRequiredService<IQueryHandler<EvaluateComplexPermissionQuery, ComplexPermissionEvaluationResult>>();
        var permissionResult = await complexPermissionHandler.HandleAsync(complexPermissionQuery, CancellationToken.None);

        Assert.IsNotNull(permissionResult);
        // Note: Result may vary based on current time, but should not throw

        // Step 4: Record audit events for all operations
        var auditEvents = new[]
        {
            new RecordAuditEventCommand
            {
                EventType = "ResourceCreated",
                EventCategory = "Business",
                ResourceId = resource.Id,
                Action = "CreateResource",
                Details = $"Resource {resource.Name} created",
                Severity = "Information"
            },
            new RecordAuditEventCommand
            {
                EventType = "PermissionGranted",
                EventCategory = "Security",
                UserId = 1,
                ResourceId = resource.Id,
                Action = "GrantPermission",
                Details = $"Permission granted to user 1 for resource {resource.Id}",
                Severity = "Information"
            },
            new RecordAuditEventCommand
            {
                EventType = "ComplexPermissionEvaluated",
                EventCategory = "Security",
                UserId = 1,
                ResourceId = resource.Id,
                Action = "EvaluatePermission",
                Details = $"Complex permission evaluation completed",
                Severity = "Information"
            }
        };

        var auditHandler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
        foreach (var auditEvent in auditEvents)
        {
            var auditResult = await auditHandler.HandleAsync(auditEvent, CancellationToken.None);
            Assert.IsTrue(auditResult.Success);
        }

        // Step 5: Generate compliance report
        var complianceQuery = new GetComplianceReportQuery
        {
            ReportType = "GDPR",
            StartDate = DateTime.UtcNow.AddHours(-1),
            EndDate = DateTime.UtcNow,
            IncludeAnomalies = true,
            IncludeRiskAssessment = true,
            ReportFormat = "Summary",
            RequestedBy = "E2ETest"
        };

        var complianceHandler = _serviceProvider.GetRequiredService<IQueryHandler<GetComplianceReportQuery, ComplianceReportResult>>();
        var complianceReport = await complianceHandler.HandleAsync(complianceQuery, CancellationToken.None);

        Assert.IsNotNull(complianceReport);
        Assert.IsNotNull(complianceReport.Summary);
        Assert.IsTrue(complianceReport.Summary.TotalEvents >= 3); // Our audit events

        _logger.LogInformation("✅ Complete access control workflow executed successfully");
        _logger.LogInformation("  - Resource created: {ResourceName}", resource.Name);
        _logger.LogInformation("  - Permission granted: {Success}", grantResult.Success);
        _logger.LogInformation("  - Complex permission evaluated: {HasAccess}", permissionResult?.HasAccess);
        _logger.LogInformation("  - Audit events recorded: {Count}", auditEvents.Length);
        _logger.LogInformation("  - Compliance report generated: {ReportId}", complianceReport.ReportId);
    }

    [TestMethod]
    public async Task BulkPermissionOperations_Should_HandleComplexScenarios()
    {
        // Test bulk permission operations with error handling
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
                    Reason = "Bulk test - valid operation"
                },
                // Invalid operation (bad entity ID)
                new BulkPermissionOperation
                {
                    OperationType = "Grant",
                    EntityId = -1,
                    EntityType = "User",
                    PermissionId = 1,
                    ResourceId = 100,
                    Reason = "Bulk test - invalid operation"
                },
                // Another valid operation
                new BulkPermissionOperation
                {
                    OperationType = "Grant",
                    EntityId = 2,
                    EntityType = "User",
                    PermissionId = 2,
                    ResourceId = 101,
                    Reason = "Bulk test - another valid operation"
                }
            },
            ValidateBeforeExecution = true,
            StopOnFirstError = false,
            ExecuteInTransaction = false, // Allow partial success
            RequestedBy = "BulkTest"
        };

        var bulkHandler = _serviceProvider.GetRequiredService<ICommandHandler<BulkPermissionUpdateCommand, BulkPermissionUpdateResult>>();
        var bulkResult = await bulkHandler.HandleAsync(bulkCommand, CancellationToken.None);

        Assert.IsNotNull(bulkResult);
        Assert.AreEqual(3, bulkResult.TotalOperations);
        // Should have some successful and some failed operations
        Assert.IsTrue(bulkResult.SuccessfulOperations > 0);
        Assert.IsTrue(bulkResult.FailedOperations > 0);
        Assert.AreEqual(3, bulkResult.OperationResults.Count);

        _logger.LogInformation("✅ Bulk permission operations handled correctly");
        _logger.LogInformation("  - Total: {Total}, Success: {Success}, Failed: {Failed}", 
            bulkResult.TotalOperations, bulkResult.SuccessfulOperations, bulkResult.FailedOperations);
    }

    #endregion

    #region Error Handling and Recovery Tests

    [TestMethod]
    public async Task Handlers_Should_HandleErrorsGracefully()
    {
        // Test error handling in various scenarios
        var scenarios = new[]
        {
            // Invalid resource creation
            new Func<Task>(async () =>
            {
                var invalidCommand = new CreateResourceCommand
                {
                    Name = "", // Invalid - empty name
                    UriPattern = "/test",
                    HttpVerbs = new List<string> { "GET" }
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
            }),
            
            // Invalid permission check
            new Func<Task>(async () =>
            {
                var invalidQuery = new CheckPermissionQuery
                {
                    EntityId = -1, // Invalid entity ID
                    EntityType = "User",
                    PermissionId = 1
                };
                
                var handler = _serviceProvider.GetRequiredService<IQueryHandler<CheckPermissionQuery, PermissionCheckResult>>();
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidQuery, CancellationToken.None));
            }),
            
            // Invalid audit event
            new Func<Task>(async () =>
            {
                var invalidCommand = new RecordAuditEventCommand
                {
                    EventType = "", // Invalid - empty event type
                    EventCategory = "Test",
                    Action = "Test"
                };
                
                var handler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                    handler.HandleAsync(invalidCommand, CancellationToken.None));
            })
        };

        foreach (var scenario in scenarios)
        {
            await scenario();
        }

        _logger.LogInformation("✅ Error handling scenarios completed successfully");
    }

    #endregion

    #region Helper Methods and Test Classes

    private static IConfiguration BuildTestConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;Trusted_Connection=true;",
                ["TenantId"] = "test-tenant",
                ["Logging:LogLevel:Default"] = "Information"
            })
            .Build();

        return configuration;
    }

    private class TestTrackingCommand : ICommand<string>
    {
        public int Id { get; set; }
        public Action OnExecute { get; set; } = () => { };
    }

    private class TestTrackingCommandHandler : ICommandHandler<TestTrackingCommand, string>
    {
        public Task<string> HandleAsync(TestTrackingCommand command, CancellationToken cancellationToken)
        {
            command.OnExecute();
            return Task.FromResult($"Executed_{command.Id}");
        }
    }

    private class TestImmediateQuery : IQuery<string>
    {
    }

    private class TestImmediateQueryHandler : IQueryHandler<TestImmediateQuery, string>
    {
        public Task<string> HandleAsync(TestImmediateQuery query, CancellationToken cancellationToken)
        {
            return Task.FromResult("QueryExecuted");
        }
    }

    #endregion
}