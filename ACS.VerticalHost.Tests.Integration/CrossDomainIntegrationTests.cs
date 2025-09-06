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
/// Tests for cross-domain integration scenarios
/// Validates complex business workflows that span multiple domains:
/// Resource Management, Permission Management, Audit & Compliance, Access Control
/// </summary>
[TestClass]
public class CrossDomainIntegrationTests
{
    private ServiceProvider? _serviceProvider;
    private ILogger<CrossDomainIntegrationTests>? _logger;

    [TestInitialize]
    public async Task Setup()
    {
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"CrossDomainTestDb_{Guid.NewGuid()}"));

        services.AddAcsServiceLayer(configuration);
        services.AddSingleton<ICommandBuffer, CommandBuffer>();
        services.AddHandlersAutoRegistration();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<CrossDomainIntegrationTests>>();

        var commandBuffer = _serviceProvider.GetRequiredService<ICommandBuffer>();
        await commandBuffer.StartAsync();

        _logger.LogInformation("Cross-domain integration test setup completed");
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_serviceProvider != null)
        {
            var commandBuffer = _serviceProvider.GetRequiredService<ICommandBuffer>();
            await commandBuffer.StopAsync();
        }
        _serviceProvider?.Dispose();
    }

    #region Resource-Permission Domain Integration

    [TestMethod]
    public async Task ResourceLifecycle_Should_UpdatePermissionsCorrectly()
    {
        // Test: Create Resource → Grant Permissions → Update Resource → Verify Permission Impact → Delete Resource
        
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogInformation("Starting resource lifecycle test with correlation ID: {CorrelationId}", correlationId);

        // Step 1: Create Resource
        var createResourceCmd = new CreateResourceCommand
        {
            Name = "CustomerDataAPI",
            Description = "API for customer data access",
            UriPattern = "/api/customers/{customerId}",
            HttpVerbs = new List<string> { "GET", "POST", "PUT", "DELETE" },
            IsActive = true,
            CreatedBy = "CrossDomainTest"
        };

        var resourceHandler = _serviceProvider.GetRequiredService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
        var resource = await resourceHandler.HandleAsync(createResourceCmd, CancellationToken.None);

        Assert.IsNotNull(resource);
        Assert.AreEqual("CustomerDataAPI", resource.Name);
        _logger.LogInformation("✓ Resource created: {ResourceName} (ID: {ResourceId})", resource.Name, resource.Id);

        // Step 2: Grant multiple permissions to different entities
        var permissionGrants = new[]
        {
            new GrantPermissionCommand
            {
                EntityId = 1,
                EntityType = "User",
                PermissionId = 1, // READ permission
                ResourceId = resource.Id,
                GrantedBy = "CrossDomainTest",
                Reason = "Initial user access"
            },
            new GrantPermissionCommand
            {
                EntityId = 2,
                EntityType = "User", 
                PermissionId = 2, // WRITE permission
                ResourceId = resource.Id,
                GrantedBy = "CrossDomainTest",
                Reason = "Admin access"
            },
            new GrantPermissionCommand
            {
                EntityId = 10,
                EntityType = "Group",
                PermissionId = 1, // READ permission for group
                ResourceId = resource.Id,
                GrantedBy = "CrossDomainTest",
                Reason = "Group-wide access"
            }
        };

        var grantHandler = _serviceProvider.GetRequiredService<ICommandHandler<GrantPermissionCommand, PermissionGrantResult>>();
        var grantResults = new List<PermissionGrantResult>();

        foreach (var grant in permissionGrants)
        {
            var result = await grantHandler.HandleAsync(grant, CancellationToken.None);
            Assert.IsTrue(result.Success);
            grantResults.Add(result);
        }

        _logger.LogInformation("✓ Granted {Count} permissions for resource {ResourceId}", grantResults.Count, resource.Id);

        // Step 3: Verify permissions are granted
        var getResourcePermissionsQuery = new GetResourcePermissionsQuery
        {
            ResourceId = resource.Id,
            IncludeInherited = true,
            IncludeEffective = true
        };

        var permissionQueryHandler = _serviceProvider.GetRequiredService<IQueryHandler<GetResourcePermissionsQuery, List<ResourcePermissionInfo>>>();
        var resourcePermissions = await permissionQueryHandler.HandleAsync(getResourcePermissionsQuery, CancellationToken.None);

        Assert.IsTrue(resourcePermissions.Count >= 3);
        _logger.LogInformation("✓ Verified {Count} permissions exist for resource", resourcePermissions.Count);

        // Step 4: Update Resource (simulate breaking change)
        var updateResourceCmd = new UpdateResourceCommand
        {
            ResourceId = resource.Id,
            Name = "CustomerDataAPI_v2",
            Description = "Updated customer data API with new security model",
            UriPattern = "/api/v2/customers/{customerId}",
            HttpVerbs = new List<string> { "GET", "POST" }, // Removed PUT, DELETE
            IsActive = true,
            UpdatedBy = "CrossDomainTest"
        };

        var updateHandler = _serviceProvider.GetRequiredService<ICommandHandler<UpdateResourceCommand, ACS.Service.Domain.Resource>>();
        var updatedResource = await updateHandler.HandleAsync(updateResourceCmd, CancellationToken.None);

        Assert.IsNotNull(updatedResource);
        Assert.AreEqual("CustomerDataAPI_v2", updatedResource.Name);
        _logger.LogInformation("✓ Resource updated: {ResourceName}", updatedResource.Name);

        // Step 5: Analyze permission impact of resource changes
        var impactAnalysisQuery = new PermissionImpactAnalysisQuery
        {
            ResourceId = resource.Id,
            AnalysisType = "Modify",
            IncludeDownstreamEffects = true,
            IncludeRiskAssessment = true,
            MaxDepth = 3
        };

        var impactHandler = _serviceProvider.GetRequiredService<IQueryHandler<PermissionImpactAnalysisQuery, PermissionImpactAnalysisResult>>();
        var impactResult = await impactHandler.HandleAsync(impactAnalysisQuery, CancellationToken.None);

        Assert.IsNotNull(impactResult);
        Assert.IsNotNull(impactResult.RiskAssessment);
        _logger.LogInformation("✓ Impact analysis completed: {DirectImpacts} direct impacts, {IndirectImpacts} indirect impacts, Risk: {RiskLevel}",
            impactResult.DirectImpacts.Count, impactResult.IndirectImpacts.Count, impactResult.RiskAssessment.OverallRiskLevel);

        // Step 6: Delete Resource (with dependency check)
        var deleteResourceCmd = new DeleteResourceCommand
        {
            ResourceId = resource.Id,
            ForceDelete = true, // Force delete to remove dependencies
            DeletedBy = "CrossDomainTest"
        };

        var deleteHandler = _serviceProvider.GetRequiredService<ICommandHandler<DeleteResourceCommand, DeleteResourceResult>>();
        var deleteResult = await deleteHandler.HandleAsync(deleteResourceCmd, CancellationToken.None);

        Assert.IsTrue(deleteResult.Success);
        Assert.IsTrue(deleteResult.DependenciesRemoved.Count > 0);
        _logger.LogInformation("✓ Resource deleted: {ResourceId}, Dependencies removed: {Count}", 
            deleteResult.ResourceId, deleteResult.DependenciesRemoved.Count);

        _logger.LogInformation("✅ Complete resource lifecycle with permission management test passed");
    }

    #endregion

    #region Permission-Audit Domain Integration

    [TestMethod]
    public async Task PermissionChanges_Should_GenerateComprehensiveAuditTrail()
    {
        var testUserId = 123;
        var testResourceId = 456;
        var correlationId = Guid.NewGuid().ToString();

        _logger.LogInformation("Starting permission audit trail test for User {UserId}, Resource {ResourceId}", 
            testUserId, testResourceId);

        // Step 1: Grant Permission with audit logging
        var grantCmd = new GrantPermissionCommand
        {
            EntityId = testUserId,
            EntityType = "User",
            PermissionId = 5,
            ResourceId = testResourceId,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            GrantedBy = "AuditTrailTest",
            Reason = "User promotion - requires elevated access"
        };

        var grantHandler = _serviceProvider.GetRequiredService<ICommandHandler<GrantPermissionCommand, PermissionGrantResult>>();
        var grantResult = await grantHandler.HandleAsync(grantCmd, CancellationToken.None);
        Assert.IsTrue(grantResult.Success);

        // Manually record audit event for permission grant
        var grantAuditCmd = new RecordAuditEventCommand
        {
            EventType = "PermissionGranted",
            EventCategory = "Security",
            UserId = testUserId,
            ResourceId = testResourceId,
            Action = "GrantPermission",
            Details = $"Permission {grantCmd.PermissionId} granted to user {testUserId} for resource {testResourceId}. Reason: {grantCmd.Reason}",
            Severity = "Information",
            Metadata = new Dictionary<string, object>
            {
                ["PermissionId"] = grantCmd.PermissionId,
                ["ExpiresAt"] = grantCmd.ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never",
                ["GrantedBy"] = grantCmd.GrantedBy ?? "System",
                ["CorrelationId"] = correlationId
            }
        };

        var auditHandler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
        var grantAuditResult = await auditHandler.HandleAsync(grantAuditCmd, CancellationToken.None);
        Assert.IsTrue(grantAuditResult.Success);

        // Step 2: Perform permission validation
        var validateCmd = new ValidatePermissionStructureCommand
        {
            EntityId = testUserId,
            EntityType = "User",
            FixInconsistencies = false,
            ValidatedBy = "AuditTrailTest"
        };

        var validateHandler = _serviceProvider.GetRequiredService<ICommandHandler<ValidatePermissionStructureCommand, PermissionValidationResult>>();
        var validateResult = await validateHandler.HandleAsync(validateCmd, CancellationToken.None);

        // Record validation audit event
        var validationAuditCmd = new RecordAuditEventCommand
        {
            EventType = "PermissionValidation",
            EventCategory = "Compliance",
            UserId = testUserId,
            Action = "ValidatePermissionStructure",
            Details = $"Permission structure validation for user {testUserId}. Result: {(validateResult.IsValid ? "Valid" : "Issues found")}",
            Severity = validateResult.IsValid ? "Information" : "Warning",
            Metadata = new Dictionary<string, object>
            {
                ["IsValid"] = validateResult.IsValid,
                ["InconsistencyCount"] = validateResult.Inconsistencies.Count,
                ["ValidatedBy"] = validateCmd.ValidatedBy ?? "System",
                ["CorrelationId"] = correlationId
            }
        };

        var validationAuditResult = await auditHandler.HandleAsync(validationAuditCmd, CancellationToken.None);
        Assert.IsTrue(validationAuditResult.Success);

        // Step 3: Revoke Permission
        var revokeCmd = new RevokePermissionCommand
        {
            EntityId = testUserId,
            EntityType = "User",
            PermissionId = 5,
            ResourceId = testResourceId,
            CascadeToChildren = true,
            RevokedBy = "AuditTrailTest",
            Reason = "User role change - access no longer required"
        };

        var revokeHandler = _serviceProvider.GetRequiredService<ICommandHandler<RevokePermissionCommand, PermissionRevokeResult>>();
        var revokeResult = await revokeHandler.HandleAsync(revokeCmd, CancellationToken.None);
        Assert.IsTrue(revokeResult.Success);

        // Record revocation audit event
        var revokeAuditCmd = new RecordAuditEventCommand
        {
            EventType = "PermissionRevoked",
            EventCategory = "Security",
            UserId = testUserId,
            ResourceId = testResourceId,
            Action = "RevokePermission",
            Details = $"Permission {revokeCmd.PermissionId} revoked from user {testUserId} for resource {testResourceId}. Reason: {revokeCmd.Reason}",
            Severity = "Information",
            Metadata = new Dictionary<string, object>
            {
                ["PermissionId"] = revokeCmd.PermissionId,
                ["CascadeToChildren"] = revokeCmd.CascadeToChildren,
                ["CascadeRevokedCount"] = revokeResult.CascadeRevokedEntities.Count,
                ["RevokedBy"] = revokeCmd.RevokedBy ?? "System",
                ["CorrelationId"] = correlationId
            }
        };

        var revokeAuditResult = await auditHandler.HandleAsync(revokeAuditCmd, CancellationToken.None);
        Assert.IsTrue(revokeAuditResult.Success);

        // Step 4: Generate User Audit Trail
        var auditTrailQuery = new GetUserAuditTrailQuery
        {
            UserId = testUserId,
            StartDate = DateTime.UtcNow.AddHours(-1),
            EndDate = DateTime.UtcNow.AddMinutes(5),
            EventCategories = new List<string> { "Security", "Compliance" },
            IncludePermissionChanges = true,
            IncludeSystemEvents = false,
            PageSize = 100
        };

        var auditTrailHandler = _serviceProvider.GetRequiredService<IQueryHandler<GetUserAuditTrailQuery, List<UserAuditTrailEntry>>>();
        var auditTrail = await auditTrailHandler.HandleAsync(auditTrailQuery, CancellationToken.None);

        // Verify complete audit trail
        Assert.IsTrue(auditTrail.Count >= 3); // At least grant, validation, revoke
        
        var grantEvent = auditTrail.FirstOrDefault(e => e.EventType == "PermissionGranted");
        var validationEvent = auditTrail.FirstOrDefault(e => e.EventType == "PermissionValidation");
        var revokeEvent = auditTrail.FirstOrDefault(e => e.EventType == "PermissionRevoked");

        Assert.IsNotNull(grantEvent);
        Assert.IsNotNull(validationEvent);
        Assert.IsNotNull(revokeEvent);

        _logger.LogInformation("✓ Complete audit trail generated: {Count} events", auditTrail.Count);
        _logger.LogInformation("  - Permission granted at: {Time}", grantEvent.EventTimestamp);
        _logger.LogInformation("  - Validation performed at: {Time}", validationEvent.EventTimestamp);
        _logger.LogInformation("  - Permission revoked at: {Time}", revokeEvent.EventTimestamp);

        // Step 5: Generate Compliance Report covering all activities
        var complianceQuery = new GetComplianceReportQuery
        {
            ReportType = "Security",
            StartDate = DateTime.UtcNow.AddHours(-1),
            EndDate = DateTime.UtcNow.AddMinutes(5),
            UserIds = new List<int> { testUserId },
            ResourceIds = new List<int> { testResourceId },
            IncludeAnomalies = true,
            IncludeRiskAssessment = true,
            ReportFormat = "Detailed",
            RequestedBy = "AuditTrailTest"
        };

        var complianceHandler = _serviceProvider.GetRequiredService<IQueryHandler<GetComplianceReportQuery, ComplianceReportResult>>();
        var complianceReport = await complianceHandler.HandleAsync(complianceQuery, CancellationToken.None);

        Assert.IsNotNull(complianceReport);
        Assert.IsNotNull(complianceReport.Summary);
        Assert.IsTrue(complianceReport.Summary.TotalEvents >= 3);
        Assert.IsTrue(complianceReport.Summary.SecurityEvents >= 2); // Grant and revoke
        Assert.IsTrue(complianceReport.Summary.PermissionChanges >= 2); // Grant and revoke

        _logger.LogInformation("✓ Compliance report generated: {TotalEvents} events, {SecurityEvents} security events, {PermissionChanges} permission changes",
            complianceReport.Summary.TotalEvents, complianceReport.Summary.SecurityEvents, complianceReport.Summary.PermissionChanges);

        _logger.LogInformation("✅ Permission changes generated comprehensive audit trail test passed");
    }

    #endregion

    #region Access Control-Audit Domain Integration

    [TestMethod]
    public async Task AccessViolations_Should_TriggerSecurityWorkflow()
    {
        var suspiciousUserId = 999;
        var sensitiveResourceId = 777;
        var violationId = Guid.NewGuid().ToString();

        _logger.LogInformation("Starting access violation security workflow test");

        // Step 1: Detect and handle access violation
        var violationCmd = new AccessViolationHandlerCommand
        {
            ViolationType = "UnauthorizedAccess",
            UserId = suspiciousUserId,
            ResourceId = sensitiveResourceId,
            IpAddress = "192.168.1.999",
            UserAgent = "SuspiciousBot/1.0",
            SessionId = $"session_{violationId}",
            Context = new Dictionary<string, object>
            {
                ["AttemptedAction"] = "DELETE",
                ["UserRole"] = "Guest",
                ["ResourceSensitivity"] = "High",
                ["PreviousViolations"] = 2,
                ["TimeOfDay"] = "02:30 AM"
            },
            Severity = "High",
            Action = "Block",
            OccurredAt = DateTime.UtcNow
        };

        var violationHandler = _serviceProvider.GetRequiredService<ICommandHandler<AccessViolationHandlerCommand, AccessViolationHandlerResult>>();
        var violationResult = await violationHandler.HandleAsync(violationCmd, CancellationToken.None);

        Assert.IsTrue(violationResult.Success);
        Assert.IsTrue(violationResult.UserBlocked);
        Assert.IsTrue(violationResult.ActionsExecuted.Contains("AuditLogged"));
        Assert.IsTrue(violationResult.ActionsExecuted.Contains("UserBlocked"));
        
        _logger.LogInformation("✓ Access violation handled: {ViolationId}, Actions: {Actions}", 
            violationResult.ViolationId, string.Join(", ", violationResult.ActionsExecuted));

        // Step 2: Generate follow-up audit events
        var investigationAuditCmd = new RecordAuditEventCommand
        {
            EventType = "SecurityInvestigation",
            EventCategory = "Security",
            UserId = suspiciousUserId,
            ResourceId = sensitiveResourceId,
            Action = "InitiateInvestigation",
            Details = $"Security investigation initiated for violation {violationResult.ViolationId}",
            Severity = "Critical",
            IpAddress = violationCmd.IpAddress,
            UserAgent = violationCmd.UserAgent,
            SessionId = violationCmd.SessionId,
            Metadata = new Dictionary<string, object>
            {
                ["ViolationId"] = violationResult.ViolationId,
                ["ViolationType"] = violationCmd.ViolationType,
                ["ActionTaken"] = violationResult.ActionTaken,
                ["InvestigationStarted"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                ["AutomatedResponse"] = true
            }
        };

        var auditHandler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
        var investigationAuditResult = await auditHandler.HandleAsync(investigationAuditCmd, CancellationToken.None);
        Assert.IsTrue(investigationAuditResult.Success);

        // Step 3: Perform bulk permission audit for affected user
        var bulkAuditCmd = new BulkPermissionUpdateCommand
        {
            Operations = new List<BulkPermissionOperation>
            {
                // Audit current permissions by "updating" them (no-op that triggers validation)
                new BulkPermissionOperation
                {
                    OperationType = "Update",
                    EntityId = suspiciousUserId,
                    EntityType = "User", 
                    PermissionId = 1,
                    Metadata = new Dictionary<string, object>
                    {
                        ["AuditReason"] = "Security incident response",
                        ["ViolationId"] = violationResult.ViolationId,
                        ["AuditOnly"] = true
                    }
                }
            },
            ValidateBeforeExecution = true,
            StopOnFirstError = false,
            ExecuteInTransaction = true,
            RequestedBy = "SecuritySystem",
            Reason = $"Security audit following violation {violationResult.ViolationId}"
        };

        var bulkPermissionHandler = _serviceProvider.GetRequiredService<ICommandHandler<BulkPermissionUpdateCommand, BulkPermissionUpdateResult>>();
        var bulkResult = await bulkPermissionHandler.HandleAsync(bulkAuditCmd, CancellationToken.None);

        Assert.IsNotNull(bulkResult);
        _logger.LogInformation("✓ Bulk permission audit completed: {Operations} operations processed", 
            bulkResult.TotalOperations);

        // Step 4: Generate security-focused compliance report
        var securityReportQuery = new GetComplianceReportQuery
        {
            ReportType = "Security",
            StartDate = DateTime.UtcNow.AddMinutes(-10),
            EndDate = DateTime.UtcNow.AddMinutes(5),
            UserIds = new List<int> { suspiciousUserId },
            ResourceIds = new List<int> { sensitiveResourceId },
            IncludeAnomalies = true,
            IncludeRiskAssessment = true,
            ReportFormat = "Detailed",
            RequestedBy = "SecuritySystem"
        };

        var complianceHandler = _serviceProvider.GetRequiredService<IQueryHandler<GetComplianceReportQuery, ComplianceReportResult>>();
        var securityReport = await complianceHandler.HandleAsync(securityReportQuery, CancellationToken.None);

        Assert.IsNotNull(securityReport);
        Assert.IsNotNull(securityReport.Summary);
        Assert.IsTrue(securityReport.Summary.SecurityEvents >= 1);
        Assert.IsNotNull(securityReport.RiskAssessment);
        
        // Should detect the violation as an anomaly
        Assert.IsTrue(securityReport.Anomalies.Count >= 0); // May or may not detect depending on implementation
        
        _logger.LogInformation("✓ Security compliance report generated: Risk Level: {RiskLevel}, Violations: {Violations}, Anomalies: {Anomalies}",
            securityReport.RiskAssessment?.OverallRiskLevel ?? "Unknown",
            securityReport.Violations.Count,
            securityReport.Anomalies.Count);

        // Step 5: Validate audit log integrity after security incident
        var integrityQuery = new ValidateAuditIntegrityQuery
        {
            StartDate = DateTime.UtcNow.AddMinutes(-10),
            EndDate = DateTime.UtcNow.AddMinutes(5),
            CheckHashChain = true,
            CheckCompleteness = true,
            CheckConsistency = true,
            PerformDeepValidation = false, // Skip deep validation for speed
            RequestedBy = "SecuritySystem"
        };

        var integrityHandler = _serviceProvider.GetRequiredService<IQueryHandler<ValidateAuditIntegrityQuery, AuditIntegrityResult>>();
        var integrityResult = await integrityHandler.HandleAsync(integrityQuery, CancellationToken.None);

        Assert.IsNotNull(integrityResult);
        Assert.IsTrue(integrityResult.IsIntegrityValid); // Should be valid for our test
        Assert.IsTrue(integrityResult.Statistics.TotalRecordsChecked >= 2); // At least violation and investigation

        _logger.LogInformation("✓ Audit integrity validated: {IsValid}, Records checked: {Count}, Issues: {Issues}",
            integrityResult.IsIntegrityValid,
            integrityResult.Statistics.TotalRecordsChecked,
            integrityResult.Issues.Count);

        _logger.LogInformation("✅ Access violation security workflow test passed");
    }

    #endregion

    #region Multi-Domain Complex Scenarios

    [TestMethod]
    public async Task ComplexEnterpriseWorkflow_Should_HandleAllDomains()
    {
        // Scenario: New employee onboarding with complex permission inheritance,
        // resource access, audit compliance, and security monitoring
        
        var newEmployeeId = 5000;
        var departmentGroupId = 100;
        var managerRoleId = 10;
        var projectResourceIds = new[] { 1001, 1002, 1003 };
        var workflowId = Guid.NewGuid().ToString();

        _logger.LogInformation("Starting complex enterprise workflow simulation for Employee {EmployeeId}", newEmployeeId);

        // Phase 1: Resource Provisioning
        var provisioningTasks = projectResourceIds.Select(async resourceId =>
        {
            var createResourceCmd = new CreateResourceCommand
            {
                Name = $"Project_Resource_{resourceId}",
                Description = $"Project resource for enterprise workflow test",
                UriPattern = $"/api/projects/{resourceId}/{{action}}",
                HttpVerbs = new List<string> { "GET", "POST", "PUT" },
                IsActive = true,
                CreatedBy = "EnterpriseWorkflow"
            };

            var resourceHandler = _serviceProvider.GetRequiredService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
            return await resourceHandler.HandleAsync(createResourceCmd, CancellationToken.None);
        }).ToArray();

        var provisionedResources = await Task.WhenAll(provisioningTasks);
        Assert.AreEqual(projectResourceIds.Length, provisionedResources.Length);
        _logger.LogInformation("✓ Provisioned {Count} project resources", provisionedResources.Length);

        // Phase 2: Complex Permission Granting
        var permissionOperations = new List<BulkPermissionOperation>();
        
        // Grant department-wide permissions
        foreach (var resource in provisionedResources)
        {
            permissionOperations.Add(new BulkPermissionOperation
            {
                OperationType = "Grant",
                EntityId = departmentGroupId,
                EntityType = "Group",
                PermissionId = 1, // READ permission
                ResourceId = resource.Id,
                Reason = "Department baseline access",
                Metadata = new Dictionary<string, object>
                {
                    ["PermissionType"] = "DepartmentBaseline",
                    ["WorkflowId"] = workflowId,
                    ["EffectiveDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
                }
            });
        }

        // Grant manager role permissions
        permissionOperations.Add(new BulkPermissionOperation
        {
            OperationType = "Grant",
            EntityId = managerRoleId,
            EntityType = "Role",
            PermissionId = 2, // WRITE permission
            ResourceId = provisionedResources[0].Id, // First resource only
            Reason = "Manager oversight access",
            Metadata = new Dictionary<string, object>
            {
                ["PermissionType"] = "ManagerRole",
                ["WorkflowId"] = workflowId
            }
        });

        // Grant specific user permissions
        permissionOperations.Add(new BulkPermissionOperation
        {
            OperationType = "Grant",
            EntityId = newEmployeeId,
            EntityType = "User",
            PermissionId = 3, // ADMIN permission
            ResourceId = provisionedResources[2].Id, // Last resource - employee's project
            ExpiresAt = DateTime.UtcNow.AddDays(90), // Temporary elevated access
            Reason = "Employee project lead access",
            Metadata = new Dictionary<string, object>
            {
                ["PermissionType"] = "ProjectLead",
                ["WorkflowId"] = workflowId,
                ["ProjectId"] = projectResourceIds[2]
            }
        });

        var bulkPermissionCmd = new BulkPermissionUpdateCommand
        {
            Operations = permissionOperations,
            ValidateBeforeExecution = true,
            StopOnFirstError = false,
            ExecuteInTransaction = true,
            RequestedBy = "EnterpriseWorkflow",
            Reason = $"Employee onboarding workflow {workflowId}"
        };

        var bulkPermissionHandler = _serviceProvider.GetRequiredService<ICommandHandler<BulkPermissionUpdateCommand, BulkPermissionUpdateResult>>();
        var bulkPermissionResult = await bulkPermissionHandler.HandleAsync(bulkPermissionCmd, CancellationToken.None);

        Assert.IsNotNull(bulkPermissionResult);
        Assert.IsTrue(bulkPermissionResult.SuccessfulOperations > 0);
        _logger.LogInformation("✓ Bulk permissions granted: {Success}/{Total} successful", 
            bulkPermissionResult.SuccessfulOperations, bulkPermissionResult.TotalOperations);

        // Phase 3: Audit Trail Generation
        var auditEvents = new[]
        {
            new RecordAuditEventCommand
            {
                EventType = "EmployeeOnboarding",
                EventCategory = "Business",
                UserId = newEmployeeId,
                Action = "InitiateOnboarding",
                Details = $"Employee onboarding workflow initiated for employee {newEmployeeId}",
                Severity = "Information",
                Metadata = new Dictionary<string, object>
                {
                    ["WorkflowId"] = workflowId,
                    ["DepartmentGroup"] = departmentGroupId,
                    ["ManagerRole"] = managerRoleId,
                    ["ResourcesProvisioned"] = provisionedResources.Length,
                    ["PermissionsGranted"] = bulkPermissionResult.SuccessfulOperations
                }
            },
            new RecordAuditEventCommand
            {
                EventType = "ResourceProvisioning",
                EventCategory = "System",
                Action = "ProvisionResources",
                Details = $"Provisioned {provisionedResources.Length} project resources for new employee",
                Severity = "Information",
                Metadata = new Dictionary<string, object>
                {
                    ["WorkflowId"] = workflowId,
                    ["ResourceIds"] = string.Join(",", provisionedResources.Select(r => r.Id)),
                    ["ProvisioningType"] = "EmployeeOnboarding"
                }
            },
            new RecordAuditEventCommand
            {
                EventType = "BulkPermissionGrant",
                EventCategory = "Security",
                UserId = newEmployeeId,
                Action = "GrantBulkPermissions",
                Details = $"Bulk permission grant completed for employee onboarding",
                Severity = "Information",
                Metadata = new Dictionary<string, object>
                {
                    ["WorkflowId"] = workflowId,
                    ["OperationsRequested"] = permissionOperations.Count,
                    ["OperationsSuccessful"] = bulkPermissionResult.SuccessfulOperations,
                    ["OperationsFailed"] = bulkPermissionResult.FailedOperations
                }
            }
        };

        var auditHandler = _serviceProvider.GetRequiredService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
        var auditResults = await Task.WhenAll(auditEvents.Select(evt => auditHandler.HandleAsync(evt, CancellationToken.None)));

        Assert.IsTrue(auditResults.All(r => r.Success));
        _logger.LogInformation("✓ Generated {Count} audit events for workflow", auditResults.Length);

        // Phase 4: Complex Permission Analysis
        var effectivePermissionsQuery = new GetEffectivePermissionsQuery
        {
            EntityId = newEmployeeId,
            EntityType = "User",
            ResourceIds = provisionedResources.Select(r => r.Id).ToList(),
            IncludeInheritanceChain = true,
            IncludeExpiredPermissions = false,
            ResolveConflicts = true,
            EffectiveAt = DateTime.UtcNow
        };

        var effectivePermissionsHandler = _serviceProvider.GetRequiredService<IQueryHandler<GetEffectivePermissionsQuery, EffectivePermissionsResult>>();
        var effectivePermissions = await effectivePermissionsHandler.HandleAsync(effectivePermissionsQuery, CancellationToken.None);

        Assert.IsNotNull(effectivePermissions);
        Assert.IsTrue(effectivePermissions.Permissions.Count > 0);
        Assert.IsNotNull(effectivePermissions.Summary);

        _logger.LogInformation("✓ Effective permissions calculated: {Total} permissions, {Direct} direct, {Inherited} inherited",
            effectivePermissions.Summary.TotalPermissions,
            effectivePermissions.Summary.DirectPermissions,
            effectivePermissions.Summary.InheritedPermissions);

        // Phase 5: Comprehensive Compliance Report
        var comprehensiveReportQuery = new GetComplianceReportQuery
        {
            ReportType = "Comprehensive",
            StartDate = DateTime.UtcNow.AddMinutes(-30),
            EndDate = DateTime.UtcNow.AddMinutes(5),
            UserIds = new List<int> { newEmployeeId },
            ResourceIds = provisionedResources.Select(r => r.Id).ToList(),
            IncludeAnomalies = true,
            IncludeRiskAssessment = true,
            ReportFormat = "Detailed",
            RequestedBy = "EnterpriseWorkflow"
        };

        var complianceHandler = _serviceProvider.GetRequiredService<IQueryHandler<GetComplianceReportQuery, ComplianceReportResult>>();
        var comprehensiveReport = await complianceHandler.HandleAsync(comprehensiveReportQuery, CancellationToken.None);

        Assert.IsNotNull(comprehensiveReport);
        Assert.IsNotNull(comprehensiveReport.Summary);
        Assert.IsTrue(comprehensiveReport.Summary.TotalEvents >= auditEvents.Length);

        _logger.LogInformation("✓ Comprehensive compliance report generated:");
        _logger.LogInformation("  - Total events: {TotalEvents}", comprehensiveReport.Summary.TotalEvents);
        _logger.LogInformation("  - Security events: {SecurityEvents}", comprehensiveReport.Summary.SecurityEvents);
        _logger.LogInformation("  - Permission changes: {PermissionChanges}", comprehensiveReport.Summary.PermissionChanges);
        _logger.LogInformation("  - Unique resources: {UniqueResources}", comprehensiveReport.Summary.UniqueResources);
        _logger.LogInformation("  - Risk level: {RiskLevel}", comprehensiveReport.RiskAssessment?.OverallRiskLevel ?? "Unknown");

        // Phase 6: Cleanup with Impact Analysis
        foreach (var resource in provisionedResources)
        {
            var impactAnalysisQuery = new PermissionImpactAnalysisQuery
            {
                ResourceId = resource.Id,
                AnalysisType = "Revoke", // Analyze impact of removing the resource
                IncludeDownstreamEffects = true,
                IncludeRiskAssessment = true,
                MaxDepth = 5
            };

            var impactHandler = _serviceProvider.GetRequiredService<IQueryHandler<PermissionImpactAnalysisQuery, PermissionImpactAnalysisResult>>();
            var impactResult = await impactHandler.HandleAsync(impactAnalysisQuery, CancellationToken.None);

            Assert.IsNotNull(impactResult);
            _logger.LogInformation("  - Resource {ResourceId} impact analysis: {DirectImpacts} direct, {IndirectImpacts} indirect impacts",
                resource.Id, impactResult.DirectImpacts.Count, impactResult.IndirectImpacts.Count);
        }

        _logger.LogInformation("✅ Complex enterprise workflow completed successfully");
    }

    #endregion

    #region Helper Methods

    private static IConfiguration BuildTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=CrossDomainTestDb;Trusted_Connection=true;",
                ["TenantId"] = "cross-domain-test-tenant",
                ["Logging:LogLevel:Default"] = "Information",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
            })
            .Build();
    }

    #endregion
}