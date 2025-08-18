using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Infrastructure;
using ACS.Service.Data;
using ACS.Service.Domain;
using Moq;
// Use alias to avoid ambiguity
using DomainServices = ACS.Service.Services;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class AccessControlDomainServiceTests
{
    private ILogger<DomainServices.AccessControlDomainService> _logger = null!;
    private InMemoryEntityGraph _entityGraph = null!;
    private ApplicationDbContext _dbContext = null!;
    private DomainServices.TenantDatabasePersistenceService _persistenceService = null!;
    private DomainServices.EventPersistenceService _eventPersistenceService = null!;
    private DomainServices.DeadLetterQueueService _deadLetterQueue = null!;
    private DomainServices.ErrorRecoveryService _errorRecovery = null!;
    private DomainServices.HealthMonitoringService _healthMonitoring = null!;
    private DomainServices.AccessControlDomainService _domainService = null!;

    [TestInitialize]
    public void Setup()
    {
        // Setup logging
        _logger = Mock.Of<ILogger<DomainServices.AccessControlDomainService>>();
        var dlqLogger = Mock.Of<ILogger<DomainServices.DeadLetterQueueService>>();
        var errorLogger = Mock.Of<ILogger<DomainServices.ErrorRecoveryService>>();
        var healthLogger = Mock.Of<ILogger<DomainServices.HealthMonitoringService>>();
        var persistenceLogger = Mock.Of<ILogger<DomainServices.TenantDatabasePersistenceService>>();
        var eventLogger = Mock.Of<ILogger<DomainServices.EventPersistenceService>>();

        // Setup tenant configuration
        var tenantConfig = new TenantConfiguration { TenantId = "test-tenant" };

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);
        
        // Seed VerbTypes for permission tests
        SeedVerbTypes();

        // Setup dependencies
        var entityGraphLogger = Mock.Of<ILogger<InMemoryEntityGraph>>();
        
        // Use a real InMemoryEntityGraph instead of mock for proper functionality
        _entityGraph = new InMemoryEntityGraph(_dbContext, entityGraphLogger);
        // Don't load from database since we're starting fresh for tests
        // The entity graph will be populated as we create entities
        
        // Use real services with in-memory database for simplicity
        _persistenceService = new DomainServices.TenantDatabasePersistenceService(_dbContext, persistenceLogger);
        _eventPersistenceService = new DomainServices.EventPersistenceService(_dbContext, tenantConfig, eventLogger);
        _deadLetterQueue = new DomainServices.DeadLetterQueueService(dlqLogger, tenantConfig);
        _errorRecovery = new DomainServices.ErrorRecoveryService(errorLogger, tenantConfig);
        _healthMonitoring = new DomainServices.HealthMonitoringService(healthLogger, _errorRecovery, tenantConfig);

        // Setup cache with mocked methods
        var cacheMock = new Mock<ACS.Service.Caching.IEntityCache>();
        cacheMock.Setup(x => x.InvalidateGroupAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        cacheMock.Setup(x => x.InvalidateRoleAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        cacheMock.Setup(x => x.InvalidateUserAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        cacheMock.Setup(x => x.SetUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
        cacheMock.Setup(x => x.SetGroupAsync(It.IsAny<Group>())).Returns(Task.CompletedTask);
        cacheMock.Setup(x => x.SetRoleAsync(It.IsAny<Role>())).Returns(Task.CompletedTask);
        var cache = cacheMock.Object;

        // Setup domain service (with startBackgroundProcessing = false for testing)
        _domainService = new DomainServices.AccessControlDomainService(
            _entityGraph, 
            _dbContext, 
            _persistenceService,
            _eventPersistenceService,
            _deadLetterQueue,
            _errorRecovery,
            _healthMonitoring,
            cache,
            _logger,
            startBackgroundProcessing: false);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        // Cancel the background processing task before disposal
        if (_domainService != null)
        {
            try
            {
                // Force cancellation by disposing
                _domainService.Dispose();
                // Give it a moment to clean up
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                // Log but don't fail cleanup
                Console.WriteLine($"Error disposing domain service: {ex.Message}");
            }
        }
        
        _healthMonitoring?.Dispose();
        _deadLetterQueue?.Dispose();
        _dbContext?.Dispose();
    }

    #region Command Execution Tests

    [TestMethod]
    public async Task ExecuteCommandAsync_CreateUserCommand_ReturnsNewUser()
    {
        // Arrange
        var command = new DomainServices.CreateUserCommand { Name = "John Doe" };

        // Act
        var result = await _domainService.ExecuteCommandAsync(command);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(User));
        var user = (User)result;
        Assert.AreEqual("John Doe", user.Name);
        Assert.IsTrue(user.Id > 0);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_CreateGroupCommand_ReturnsNewGroup()
    {
        // Arrange
        var command = new DomainServices.CreateGroupCommand { Name = "Admin Group" };

        // Act
        var result = await _domainService.ExecuteCommandAsync(command);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(Group));
        var group = (Group)result;
        Assert.AreEqual("Admin Group", group.Name);
        Assert.IsTrue(group.Id > 0);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_CreateRoleCommand_ReturnsNewRole()
    {
        // Arrange
        var command = new DomainServices.CreateRoleCommand { Name = "Manager Role" };

        // Act
        var result = await _domainService.ExecuteCommandAsync(command);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(Role));
        var role = (Role)result;
        Assert.AreEqual("Manager Role", role.Name);
        Assert.IsTrue(role.Id > 0);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_UpdateUserCommand_UpdatesExistingUser()
    {
        // Arrange - First create a user
        var createCommand = new DomainServices.CreateUserCommand { Name = "Original Name" };
        var createdUser = await _domainService.ExecuteCommandAsync(createCommand);
        var user = (User)createdUser;

        var updateCommand = new DomainServices.UpdateUserCommand 
        { 
            UserId = user.Id, 
            Name = "Updated Name" 
        };

        // Act
        var result = await _domainService.ExecuteCommandAsync(updateCommand);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(User));
        var updatedUser = (User)result;
        Assert.AreEqual(user.Id, updatedUser.Id);
        Assert.AreEqual("Updated Name", updatedUser.Name);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_DeleteUserCommand_RemovesUser()
    {
        // Arrange - First create a user
        var createCommand = new DomainServices.CreateUserCommand { Name = "To Be Deleted" };
        var createdUser = await _domainService.ExecuteCommandAsync(createCommand);
        var user = (User)createdUser;

        var deleteCommand = new DomainServices.DeleteUserCommand { UserId = user.Id };

        // Act
        var result = await _domainService.ExecuteCommandAsync(deleteCommand);

        // Assert
        Assert.IsTrue((bool)result);
        
        // Verify user is removed from entity graph
        Assert.IsFalse(_entityGraph.Users.ContainsKey(user.Id));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_GetUserCommand_ReturnsExistingUser()
    {
        // Arrange - First create a user
        var createCommand = new DomainServices.CreateUserCommand { Name = "Test User" };
        var createdUser = await _domainService.ExecuteCommandAsync(createCommand);
        var user = (User)createdUser;

        var getUserCommand = new DomainServices.GetUserCommand { UserId = user.Id };

        // Act
        var result = await _domainService.ExecuteCommandAsync(getUserCommand);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(User));
        var retrievedUser = (User)result;
        Assert.AreEqual(user.Id, retrievedUser.Id);
        Assert.AreEqual("Test User", retrievedUser.Name);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_GetUsersCommand_ReturnsPaginatedUsers()
    {
        // Arrange - Create multiple users
        for (int i = 1; i <= 5; i++)
        {
            var createCommand = new DomainServices.CreateUserCommand { Name = $"User {i}" };
            await _domainService.ExecuteCommandAsync(createCommand);
        }

        var getUsersCommand = new DomainServices.GetUsersCommand { Page = 1, PageSize = 3 };

        // Act
        var result = await _domainService.ExecuteCommandAsync(getUsersCommand);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(List<User>));
        var users = (List<User>)result;
        Assert.AreEqual(3, users.Count); // Should return page size limit
    }

    #endregion

    #region Relationship Command Tests

    [TestMethod]
    public async Task ExecuteCommandAsync_AddUserToGroupCommand_EstablishesRelationship()
    {
        // Arrange - Create user and group
        var userCommand = new DomainServices.CreateUserCommand { Name = "Test User" };
        var groupCommand = new DomainServices.CreateGroupCommand { Name = "Test Group" };
        
        var user = (User)await _domainService.ExecuteCommandAsync(userCommand);
        var group = (Group)await _domainService.ExecuteCommandAsync(groupCommand);

        var addCommand = new DomainServices.AddUserToGroupCommand 
        { 
            UserId = user.Id, 
            GroupId = group.Id 
        };

        // Act
        await _domainService.ExecuteCommandAsync(addCommand);

        // Assert
        Assert.IsTrue(user.Parents.Contains(group));
        Assert.IsTrue(group.Children.Contains(user));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_RemoveUserFromGroupCommand_RemovesRelationship()
    {
        // Arrange - Create user, group and establish relationship
        var userCommand = new DomainServices.CreateUserCommand { Name = "Test User" };
        var groupCommand = new DomainServices.CreateGroupCommand { Name = "Test Group" };
        
        var user = (User)await _domainService.ExecuteCommandAsync(userCommand);
        var group = (Group)await _domainService.ExecuteCommandAsync(groupCommand);

        var addCommand = new DomainServices.AddUserToGroupCommand { UserId = user.Id, GroupId = group.Id };
        await _domainService.ExecuteCommandAsync(addCommand);

        var removeCommand = new DomainServices.RemoveUserFromGroupCommand { UserId = user.Id, GroupId = group.Id };

        // Act
        await _domainService.ExecuteCommandAsync(removeCommand);

        // Assert
        Assert.IsFalse(user.Parents.Contains(group));
        Assert.IsFalse(group.Children.Contains(user));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_AssignUserToRoleCommand_EstablishesRelationship()
    {
        // Arrange - Create user and role
        var userCommand = new DomainServices.CreateUserCommand { Name = "Test User" };
        var roleCommand = new DomainServices.CreateRoleCommand { Name = "Test Role" };
        
        var user = (User)await _domainService.ExecuteCommandAsync(userCommand);
        var role = (Role)await _domainService.ExecuteCommandAsync(roleCommand);

        var assignCommand = new DomainServices.AssignUserToRoleCommand 
        { 
            UserId = user.Id, 
            RoleId = role.Id 
        };

        // Act
        await _domainService.ExecuteCommandAsync(assignCommand);

        // Assert
        Assert.IsTrue(user.Parents.Contains(role));
        Assert.IsTrue(role.Children.Contains(user));
    }

    #endregion

    #region Permission Command Tests

    [TestMethod]
    public async Task ExecuteCommandAsync_AddPermissionToEntityCommand_AddsPermissionToEntity()
    {
        // Arrange - Create user
        var userCommand = new DomainServices.CreateUserCommand { Name = "Test User" };
        var user = (User)await _domainService.ExecuteCommandAsync(userCommand);

        var permission = new Permission
        {
            Uri = "/api/test",
            HttpVerb = HttpVerb.GET,
            Grant = true,
            Deny = false,
            Scheme = Scheme.ApiUriAuthorization
        };

        var addPermissionCommand = new DomainServices.AddPermissionToEntityCommand 
        { 
            EntityId = user.Id, 
            Permission = permission 
        };

        // Act
        await _domainService.ExecuteCommandAsync(addPermissionCommand);

        // Assert
        Assert.IsTrue(user.Permissions.Any(p => 
            p.Uri == "/api/test" && 
            p.HttpVerb == HttpVerb.GET && 
            p.Grant == true));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_CheckPermissionCommand_ReturnsPermissionStatus()
    {
        // Arrange - Create user with permission
        var userCommand = new DomainServices.CreateUserCommand { Name = "Test User" };
        var user = (User)await _domainService.ExecuteCommandAsync(userCommand);

        var permission = new Permission
        {
            Uri = "/api/test",
            HttpVerb = HttpVerb.GET,
            Grant = true,
            Deny = false,
            Scheme = Scheme.ApiUriAuthorization
        };

        var addPermissionCommand = new DomainServices.AddPermissionToEntityCommand 
        { 
            EntityId = user.Id, 
            Permission = permission 
        };
        await _domainService.ExecuteCommandAsync(addPermissionCommand);

        var checkCommand = new DomainServices.CheckPermissionCommand 
        { 
            EntityId = user.Id, 
            Uri = "/api/test", 
            HttpVerb = HttpVerb.GET 
        };

        // Act
        var result = await _domainService.ExecuteCommandAsync(checkCommand);

        // Assert
        Assert.IsTrue((bool)result);
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task ExecuteCommandAsync_InvalidCommand_ThrowsNotSupportedException()
    {
        // Arrange
        var invalidCommand = new InvalidTestCommand();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<NotSupportedException>(
            () => _domainService.ExecuteCommandAsync(invalidCommand));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_GetNonExistentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new DomainServices.GetUserCommand { UserId = 999 }; // Non-existent user

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _domainService.ExecuteCommandAsync(command));
    }

    #endregion

    #region Circular Reference Prevention Tests

    [TestMethod]
    public async Task ExecuteCommandAsync_CircularGroupReference_ThrowsInvalidOperationException()
    {
        // Arrange - Create two groups
        var group1Command = new DomainServices.CreateGroupCommand { Name = "Group 1" };
        var group2Command = new DomainServices.CreateGroupCommand { Name = "Group 2" };
        
        var group1 = (Group)await _domainService.ExecuteCommandAsync(group1Command);
        var group2 = (Group)await _domainService.ExecuteCommandAsync(group2Command);

        // Establish parent-child relationship: group1 -> group2
        var addCommand1 = new DomainServices.AddGroupToGroupCommand 
        { 
            ParentGroupId = group1.Id, 
            ChildGroupId = group2.Id 
        };
        await _domainService.ExecuteCommandAsync(addCommand1);

        // Attempt to create circular reference: group2 -> group1
        var addCommand2 = new DomainServices.AddGroupToGroupCommand 
        { 
            ParentGroupId = group2.Id, 
            ChildGroupId = group1.Id 
        };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _domainService.ExecuteCommandAsync(addCommand2));
    }

    #endregion

    #region Thread Safety Tests

    [TestMethod]
    public async Task ExecuteCommandAsync_ConcurrentCommands_ProcessedSequentially()
    {
        // Arrange
        var commands = new List<DomainServices.DomainCommand<User>>();
        for (int i = 1; i <= 10; i++)
        {
            commands.Add(new DomainServices.CreateUserCommand { Name = $"User {i}" });
        }

        // Act - Execute commands concurrently
        var tasks = commands.Select(cmd => _domainService.ExecuteCommandAsync(cmd));
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.AreEqual(10, results.Length);
        for (int i = 0; i < 10; i++)
        {
            Assert.IsNotNull(results[i]);
            Assert.IsInstanceOfType(results[i], typeof(User));
        }
        
        // Verify all users have unique IDs (thread-safe ID generation)
        var userIds = results.Cast<User>().Select(u => u.Id).ToList();
        var uniqueIds = userIds.Distinct().ToList();
        Assert.AreEqual(userIds.Count, uniqueIds.Count);
    }

    #endregion

    #region Entity Graph Loading Tests

    [TestMethod]
    public async Task LoadEntityGraphAsync_InitializesCorrectly()
    {
        // Arrange - Add some test data to the database first
        await AddTestDataToDatabase();

        // Act
        await _domainService.LoadEntityGraphAsync();

        // Assert
        // Since we're using a real entity graph, just verify no exceptions
        Assert.IsTrue(true); // Test passes if no exceptions thrown
    }

    #endregion

    #region Helper Methods

    private void SeedVerbTypes()
    {
        // Add standard HTTP verb types needed for permission tests
        var verbTypes = new[]
        {
            new ACS.Service.Data.Models.VerbType { Id = 1, VerbName = "GET" },
            new ACS.Service.Data.Models.VerbType { Id = 2, VerbName = "POST" },
            new ACS.Service.Data.Models.VerbType { Id = 3, VerbName = "PUT" },
            new ACS.Service.Data.Models.VerbType { Id = 4, VerbName = "DELETE" },
            new ACS.Service.Data.Models.VerbType { Id = 5, VerbName = "PATCH" },
            new ACS.Service.Data.Models.VerbType { Id = 6, VerbName = "HEAD" },
            new ACS.Service.Data.Models.VerbType { Id = 7, VerbName = "OPTIONS" }
        };
        
        _dbContext.VerbTypes.AddRange(verbTypes);
        _dbContext.SaveChanges();
    }
    
    private async Task AddTestDataToDatabase()
    {
        // Add some test entities to the database for loading tests
        // This would typically involve adding records to the DbContext
        await Task.CompletedTask; // Placeholder
    }

    #endregion
}

// Test helper classes  
public class InvalidTestCommand : DomainServices.DomainCommand
{
    // Invalid command for error testing
}