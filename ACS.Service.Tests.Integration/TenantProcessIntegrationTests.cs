using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ACS.Service.Infrastructure;
using ACS.Service.Services;
using ACS.Service.Data;
using ACS.Service.Domain;
using ACS.Service.Data.Models;

namespace ACS.Service.Tests.Integration;

[TestClass]
public class TenantProcessIntegrationTests
{
    private ServiceProvider? _serviceProvider;
    private string _testTenantId = "test-tenant-integration";

    [TestInitialize]
    public async Task Setup()
    {
        var services = new ServiceCollection();
        
        // Configure test services
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        // Use in-memory database for testing
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{_testTenantId}_{Guid.NewGuid()}"));
            
        // Register infrastructure services
        services.AddSingleton<TenantConfiguration>(new TenantConfiguration { TenantId = _testTenantId });
        services.AddScoped<InMemoryEntityGraph>();
        services.AddSingleton<TenantDatabasePersistenceService>();
        services.AddSingleton<EventPersistenceService>();
        services.AddSingleton<AccessControlDomainService>();
        services.AddSingleton<CommandTranslationService>();
        services.AddSingleton<TenantRingBuffer>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        // Seed test data
        await SeedTestData();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    private async Task SeedTestData()
    {
        using var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Create test entities
        var testUser = new ACS.Service.Data.Models.User
        {
            Id = 1,
            Name = "Test User",
            Entity = new ACS.Service.Data.Models.Entity { Id = 1 }
        };
        
        var testGroup = new ACS.Service.Data.Models.Group
        {
            Id = 2,
            Name = "Test Group",
            Entity = new ACS.Service.Data.Models.Entity { Id = 2 }
        };
        
        var testRole = new ACS.Service.Data.Models.Role
        {
            Id = 3,
            Name = "Test Role",
            Entity = new ACS.Service.Data.Models.Entity { Id = 3 }
        };

        // Create scheme type and resource for permissions
        var schemeType = new SchemeType { Id = 1, SchemeName = "URI" };
        var resource = new Resource { Id = 1, Uri = "/api/test" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };

        dbContext.Users.Add(testUser);
        dbContext.Groups.Add(testGroup);
        dbContext.Roles.Add(testRole);
        dbContext.Set<SchemeType>().Add(schemeType);
        dbContext.Resources.Add(resource);
        dbContext.VerbTypes.Add(verbType);
        
        await dbContext.SaveChangesAsync();
    }

    [TestMethod]
    public async Task TestEntityGraphLoading()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var entityGraph = scope.ServiceProvider.GetRequiredService<InMemoryEntityGraph>();
        
        // Act
        await entityGraph.LoadFromDatabaseAsync();
        
        // Assert
        Assert.AreEqual(1, entityGraph.Users.Count, "Should load 1 user");
        Assert.AreEqual(1, entityGraph.Groups.Count, "Should load 1 group");
        Assert.AreEqual(1, entityGraph.Roles.Count, "Should load 1 role");
        Assert.IsTrue(entityGraph.LastLoadTime > DateTime.MinValue, "Should record load time");
        Assert.IsTrue(entityGraph.LoadDuration.TotalMilliseconds > 0, "Should record load duration");
    }

    [TestMethod]
    public async Task TestDomainServiceCommandExecution()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var domainService = scope.ServiceProvider.GetRequiredService<AccessControlDomainService>();
        await domainService.LoadEntityGraphAsync();
        
        // Act - Add user to group
        var addCommand = new Services.AddUserToGroupCommand { UserId = 1, GroupId = 2 };
        var result = await domainService.ExecuteCommandAsync(addCommand);
        
        // Assert
        Assert.IsTrue(result, "Add user to group command should succeed");
        
        // Verify domain state
        var entityGraph = scope.ServiceProvider.GetRequiredService<InMemoryEntityGraph>();
        var user = entityGraph.GetUser(1);
        var group = entityGraph.GetGroup(2);
        
        Assert.IsTrue(user.Parents.Contains(group), "User should be in group's parents");
        Assert.IsTrue(group.Children.Contains(user), "Group should contain user as child");
    }

    [TestMethod]
    public async Task TestCommandTranslation()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var translator = scope.ServiceProvider.GetRequiredService<CommandTranslationService>();
        
        // Create a web request command
        var webCommand = new Infrastructure.AddUserToGroupCommand(
            "test-request-123", 
            DateTime.UtcNow, 
            "test-user-admin",
            1, // TargetUserId
            2  // GroupId
        );
        
        // Act
        var domainCommand = translator.TranslateCommand(webCommand);
        
        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.AddUserToGroupCommand));
        var addCmd = (Services.AddUserToGroupCommand)domainCommand;
        Assert.AreEqual(1, addCmd.UserId);
        Assert.AreEqual(2, addCmd.GroupId);
        
        // Test command categorization
        Assert.IsTrue(translator.IsMutationCommand(webCommand));
        Assert.IsFalse(translator.IsQueryCommand(webCommand));
    }

    [TestMethod]
    public async Task TestPermissionOperations()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var domainService = scope.ServiceProvider.GetRequiredService<AccessControlDomainService>();
        await domainService.LoadEntityGraphAsync();
        
        var permission = new Permission
        {
            Id = 100,
            Uri = "/api/test",
            HttpVerb = HttpVerb.GET,
            Grant = true,
            Deny = false,
            Scheme = Scheme.ApiUriAuthorization
        };
        
        // Act - Add permission to user
        var addPermissionCmd = new AddPermissionToEntityCommand 
        { 
            EntityId = 1, 
            Permission = permission 
        };
        var addResult = await domainService.ExecuteCommandAsync(addPermissionCmd);
        
        // Assert - Permission added
        Assert.IsTrue(addResult, "Add permission command should succeed");
        
        var entityGraph = scope.ServiceProvider.GetRequiredService<InMemoryEntityGraph>();
        var user = entityGraph.GetUser(1);
        Assert.IsTrue(user.Permissions.Any(p => p.Uri == "/api/test" && p.HttpVerb == HttpVerb.GET), 
            "User should have the permission");
        
        // Act - Check permission
        var checkCmd = new CheckPermissionCommand 
        { 
            EntityId = 1, 
            Uri = "/api/test", 
            HttpVerb = HttpVerb.GET 
        };
        var hasPermission = await domainService.ExecuteCommandAsync(checkCmd);
        
        // Assert - Permission check succeeds
        Assert.IsTrue(hasPermission, "User should have permission for /api/test:GET");
    }

    [TestMethod]
    public async Task TestAuditLogging()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var domainService = scope.ServiceProvider.GetRequiredService<AccessControlDomainService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await domainService.LoadEntityGraphAsync();
        
        // Get initial audit log count
        var initialCount = await dbContext.AuditLogs.CountAsync();
        
        // Act - Execute command that should create audit log
        var addCommand = new Services.AddUserToGroupCommand { UserId = 1, GroupId = 2 };
        await domainService.ExecuteCommandAsync(addCommand);
        
        // Assert - Audit log was created
        var finalCount = await dbContext.AuditLogs.CountAsync();
        Assert.AreEqual(initialCount + 1, finalCount, "Should create one audit log entry");
        
        var auditLog = await dbContext.AuditLogs.OrderByDescending(a => a.ChangeDate).FirstAsync();
        Assert.AreEqual("User", auditLog.EntityType);
        Assert.AreEqual(1, auditLog.EntityId);
        Assert.AreEqual("Add", auditLog.ChangeType);
        Assert.IsTrue(auditLog.ChangeDetails.Contains("AddUserToGroup"), "Audit details should contain action type");
    }

    [TestMethod]
    public async Task TestCircularReferenceDetection()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var domainService = scope.ServiceProvider.GetRequiredService<AccessControlDomainService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await domainService.LoadEntityGraphAsync();
        
        // Create additional groups for testing circular reference
        var group3 = new ACS.Service.Data.Models.Group
        {
            Id = 4,
            Name = "Test Group 3",
            Entity = new ACS.Service.Data.Models.Entity { Id = 4 }
        };
        
        dbContext.Groups.Add(group3);
        await dbContext.SaveChangesAsync();
        
        // Reload entity graph to include new group
        var entityGraph = scope.ServiceProvider.GetRequiredService<InMemoryEntityGraph>();
        await entityGraph.LoadFromDatabaseAsync();
        
        // Act & Assert - Try to create circular reference
        // Group 2 -> Group 4
        var cmd1 = new Services.AddGroupToGroupCommand { ParentGroupId = 2, ChildGroupId = 4 };
        await domainService.ExecuteCommandAsync(cmd1);
        
        // Group 4 -> Group 2 (this should fail as it would create a circular reference)
        var cmd2 = new Services.AddGroupToGroupCommand { ParentGroupId = 4, ChildGroupId = 2 };
        
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await domainService.ExecuteCommandAsync(cmd2),
            "Should prevent circular reference creation");
    }

    [TestMethod]
    public async Task TestPerformanceMetrics()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var entityGraph = scope.ServiceProvider.GetRequiredService<InMemoryEntityGraph>();
        
        // Act
        await entityGraph.LoadFromDatabaseAsync();
        
        // Assert performance metrics are captured
        Assert.IsTrue(entityGraph.LoadingPhaseTimings.ContainsKey("BulkEntityLoading"), 
            "Should track bulk entity loading time");
        Assert.IsTrue(entityGraph.LoadingPhaseTimings.ContainsKey("RelationshipBuilding"), 
            "Should track relationship building time");
        Assert.IsTrue(entityGraph.LoadingPhaseTimings.ContainsKey("IndexBuilding"), 
            "Should track index building time");
        Assert.IsTrue(entityGraph.LoadingPhaseTimings.ContainsKey("MemoryCalculation"), 
            "Should track memory calculation time");
        
        Assert.IsTrue(entityGraph.MemoryUsageBytes > 0, "Should calculate memory usage");
        Assert.IsTrue(entityGraph.TotalEntityCount > 0, "Should count entities");
    }

    [TestMethod]
    public async Task TestFastLookupMethods()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var entityGraph = scope.ServiceProvider.GetRequiredService<InMemoryEntityGraph>();
        var domainService = scope.ServiceProvider.GetRequiredService<AccessControlDomainService>();
        await domainService.LoadEntityGraphAsync();
        
        // Add some relationships for testing lookups
        var addUserToGroupCmd = new Services.AddUserToGroupCommand { UserId = 1, GroupId = 2 };
        await domainService.ExecuteCommandAsync(addUserToGroupCmd);
        
        var addRoleToGroupCmd = new Services.AddRoleToGroupCommand { GroupId = 2, RoleId = 3 };
        await domainService.ExecuteCommandAsync(addRoleToGroupCmd);
        
        // Act & Assert - Test fast lookup methods
        var userGroups = entityGraph.GetUserGroups(1);
        Assert.AreEqual(1, userGroups.Count, "Should find user's groups");
        Assert.AreEqual(2, userGroups[0], "Should find correct group ID");
        
        var groupRoles = entityGraph.GetGroupRoles(2);
        Assert.AreEqual(1, groupRoles.Count, "Should find group's roles");
        Assert.AreEqual(3, groupRoles[0], "Should find correct role ID");
        
        // Test empty results
        var nonExistentUserGroups = entityGraph.GetUserGroups(999);
        Assert.AreEqual(0, nonExistentUserGroups.Count, "Should return empty list for non-existent user");
    }
}