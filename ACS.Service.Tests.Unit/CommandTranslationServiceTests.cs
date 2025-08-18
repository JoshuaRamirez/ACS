using Microsoft.Extensions.Logging;
using ACS.Service.Services;
using ACS.Service.Infrastructure;
using ACS.Service.Domain;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class CommandTranslationServiceTests
{
    private ILogger<CommandTranslationService> _logger = null!;
    private CommandTranslationService _translationService = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Mock.Of<ILogger<CommandTranslationService>>();
        _translationService = new CommandTranslationService(_logger);
    }

    #region User Commands Tests

    [TestMethod]
    public void TranslateCommand_CreateUserCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.CreateUserCommand("req-1", DateTime.UtcNow, "user-1", "John Doe");

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.CreateUserCommand));
        var createUserCommand = (Services.CreateUserCommand)domainCommand;
        Assert.AreEqual("John Doe", createUserCommand.Name);
    }

    [TestMethod]
    public void TranslateCommand_UpdateUserCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.UpdateUserCommand("req-1", DateTime.UtcNow, "user-1", 123, "Jane Doe");

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.UpdateUserCommand));
        var updateUserCommand = (Services.UpdateUserCommand)domainCommand;
        Assert.AreEqual(123, updateUserCommand.UserId);
        Assert.AreEqual("Jane Doe", updateUserCommand.Name);
    }

    [TestMethod]
    public void TranslateCommand_GetUserCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.GetUserCommand("req-1", DateTime.UtcNow, "user-1", 456);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.GetUserCommand));
        var getUserCommand = (Services.GetUserCommand)domainCommand;
        Assert.AreEqual(456, getUserCommand.UserId);
    }

    [TestMethod]
    public void TranslateCommand_DeleteUserCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.DeleteUserCommand("req-1", DateTime.UtcNow, "user-1", 789);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.DeleteUserCommand));
        var deleteUserCommand = (Services.DeleteUserCommand)domainCommand;
        Assert.AreEqual(789, deleteUserCommand.UserId);
    }

    #endregion

    #region Group Commands Tests

    [TestMethod]
    public void TranslateCommand_CreateGroupCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.CreateGroupCommand("req-1", DateTime.UtcNow, "user-1", "Admin Group", 100);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.CreateGroupCommand));
        var createGroupCommand = (Services.CreateGroupCommand)domainCommand;
        Assert.AreEqual("Admin Group", createGroupCommand.Name);
        Assert.AreEqual(100, createGroupCommand.ParentGroupId);
    }

    [TestMethod]
    public void TranslateCommand_UpdateGroupCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.UpdateGroupCommand("req-1", DateTime.UtcNow, "user-1", 200, "Updated Group");

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.UpdateGroupCommand));
        var updateGroupCommand = (Services.UpdateGroupCommand)domainCommand;
        Assert.AreEqual(200, updateGroupCommand.GroupId);
        Assert.AreEqual("Updated Group", updateGroupCommand.Name);
    }

    #endregion

    #region Role Commands Tests

    [TestMethod]
    public void TranslateCommand_CreateRoleCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.CreateRoleCommand("req-1", DateTime.UtcNow, "user-1", "Manager Role", 300);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.CreateRoleCommand));
        var createRoleCommand = (Services.CreateRoleCommand)domainCommand;
        Assert.AreEqual("Manager Role", createRoleCommand.Name);
        Assert.AreEqual(300, createRoleCommand.GroupId);
    }

    [TestMethod]
    public void TranslateCommand_UpdateRoleCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.UpdateRoleCommand("req-1", DateTime.UtcNow, "user-1", 400, "Updated Role");

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.UpdateRoleCommand));
        var updateRoleCommand = (Services.UpdateRoleCommand)domainCommand;
        Assert.AreEqual(400, updateRoleCommand.RoleId);
        Assert.AreEqual("Updated Role", updateRoleCommand.Name);
    }

    #endregion

    #region Relationship Commands Tests

    [TestMethod]
    public void TranslateCommand_AddUserToGroupCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.AddUserToGroupCommand("req-1", DateTime.UtcNow, "user-1", 123, 456);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.AddUserToGroupCommand));
        var addUserToGroupCommand = (Services.AddUserToGroupCommand)domainCommand;
        Assert.AreEqual(123, addUserToGroupCommand.UserId);
        Assert.AreEqual(456, addUserToGroupCommand.GroupId);
    }

    [TestMethod]
    public void TranslateCommand_RemoveUserFromGroupCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.RemoveUserFromGroupCommand("req-1", DateTime.UtcNow, "user-1", 123, 456);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.RemoveUserFromGroupCommand));
        var removeUserFromGroupCommand = (Services.RemoveUserFromGroupCommand)domainCommand;
        Assert.AreEqual(123, removeUserFromGroupCommand.UserId);
        Assert.AreEqual(456, removeUserFromGroupCommand.GroupId);
    }

    [TestMethod]
    public void TranslateCommand_AssignUserToRoleCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.AssignUserToRoleCommand("req-1", DateTime.UtcNow, "user-1", 123, 789);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.AssignUserToRoleCommand));
        var assignUserToRoleCommand = (Services.AssignUserToRoleCommand)domainCommand;
        Assert.AreEqual(123, assignUserToRoleCommand.UserId);
        Assert.AreEqual(789, assignUserToRoleCommand.RoleId);
    }

    [TestMethod]
    public void TranslateCommand_UnAssignUserFromRoleCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.UnAssignUserFromRoleCommand("req-1", DateTime.UtcNow, "user-1", 123, 789);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.UnAssignUserFromRoleCommand));
        var unAssignUserFromRoleCommand = (Services.UnAssignUserFromRoleCommand)domainCommand;
        Assert.AreEqual(123, unAssignUserFromRoleCommand.UserId);
        Assert.AreEqual(789, unAssignUserFromRoleCommand.RoleId);
    }

    [TestMethod]
    public void TranslateCommand_AddRoleToGroupCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.AddRoleToGroupCommand("req-1", DateTime.UtcNow, "user-1", 456, 789);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.AddRoleToGroupCommand));
        var addRoleToGroupCommand = (Services.AddRoleToGroupCommand)domainCommand;
        Assert.AreEqual(456, addRoleToGroupCommand.GroupId);
        Assert.AreEqual(789, addRoleToGroupCommand.RoleId);
    }

    [TestMethod]
    public void TranslateCommand_RemoveRoleFromGroupCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.RemoveRoleFromGroupCommand("req-1", DateTime.UtcNow, "user-1", 456, 789);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.RemoveRoleFromGroupCommand));
        var removeRoleFromGroupCommand = (Services.RemoveRoleFromGroupCommand)domainCommand;
        Assert.AreEqual(456, removeRoleFromGroupCommand.GroupId);
        Assert.AreEqual(789, removeRoleFromGroupCommand.RoleId);
    }

    [TestMethod]
    public void TranslateCommand_RemoveGroupFromGroupCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.RemoveGroupFromGroupCommand("req-1", DateTime.UtcNow, "user-1", 100, 200);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.RemoveGroupFromGroupCommand));
        var removeGroupFromGroupCommand = (Services.RemoveGroupFromGroupCommand)domainCommand;
        Assert.AreEqual(100, removeGroupFromGroupCommand.ParentGroupId);
        Assert.AreEqual(200, removeGroupFromGroupCommand.ChildGroupId);
    }

    #endregion

    #region Permission Commands Tests

    [TestMethod]
    public void TranslateCommand_GrantPermissionCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.GrantPermissionCommand("req-1", DateTime.UtcNow, "user-1", 
            123, "/api/users", HttpVerb.GET, Scheme.ApiUriAuthorization);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.AddPermissionToEntityCommand));
        var addPermissionCommand = (Services.AddPermissionToEntityCommand)domainCommand;
        Assert.AreEqual(123, addPermissionCommand.EntityId);
        Assert.AreEqual("/api/users", addPermissionCommand.Permission.Uri);
        Assert.AreEqual(HttpVerb.GET, addPermissionCommand.Permission.HttpVerb);
        Assert.AreEqual(Scheme.ApiUriAuthorization, addPermissionCommand.Permission.Scheme);
        Assert.IsTrue(addPermissionCommand.Permission.Grant);
        Assert.IsFalse(addPermissionCommand.Permission.Deny);
    }

    [TestMethod]
    public void TranslateCommand_DenyPermissionCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.DenyPermissionCommand("req-1", DateTime.UtcNow, "user-1", 
            123, "/api/admin", HttpVerb.POST, Scheme.ApiUriAuthorization);

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.AddPermissionToEntityCommand));
        var addPermissionCommand = (Services.AddPermissionToEntityCommand)domainCommand;
        Assert.AreEqual(123, addPermissionCommand.EntityId);
        Assert.AreEqual("/api/admin", addPermissionCommand.Permission.Uri);
        Assert.AreEqual(HttpVerb.POST, addPermissionCommand.Permission.HttpVerb);
        Assert.AreEqual(Scheme.ApiUriAuthorization, addPermissionCommand.Permission.Scheme);
        Assert.IsFalse(addPermissionCommand.Permission.Grant);
        Assert.IsTrue(addPermissionCommand.Permission.Deny);
    }

    [TestMethod]
    public void TranslateCommand_CheckPermissionCommand_TranslatesCorrectly()
    {
        // Arrange
        var webCommand = new Infrastructure.CheckPermissionCommand("req-1", DateTime.UtcNow, "user-1", 
            123, "/api/data", "GET");

        // Act
        var domainCommand = _translationService.TranslateCommand(webCommand);

        // Assert
        Assert.IsInstanceOfType(domainCommand, typeof(Services.CheckPermissionCommand));
        var checkPermissionCommand = (Services.CheckPermissionCommand)domainCommand;
        Assert.AreEqual(123, checkPermissionCommand.EntityId);
        Assert.AreEqual("/api/data", checkPermissionCommand.Uri);
        Assert.AreEqual(HttpVerb.GET, checkPermissionCommand.HttpVerb);
    }

    #endregion

    #region Command Classification Tests

    [TestMethod]
    public void IsQueryCommand_WithQueryCommands_ReturnsTrue()
    {
        // Arrange
        var queryCommands = new WebRequestCommand[]
        {
            new Infrastructure.GetUserCommand("req-1", DateTime.UtcNow, "user-1", 123),
            new Infrastructure.GetUsersCommand("req-1", DateTime.UtcNow, "user-1", 1, 10),
            new Infrastructure.GetGroupCommand("req-1", DateTime.UtcNow, "user-1", 456),
            new Infrastructure.GetGroupsCommand("req-1", DateTime.UtcNow, "user-1", 1, 10),
            new Infrastructure.GetRoleCommand("req-1", DateTime.UtcNow, "user-1", 789),
            new Infrastructure.GetRolesCommand("req-1", DateTime.UtcNow, "user-1", 1, 10),
            new Infrastructure.CheckPermissionCommand("req-1", DateTime.UtcNow, "user-1", 123, "/api/test", "GET"),
            new Infrastructure.EvaluatePermissionCommand("req-1", DateTime.UtcNow, "user-1", 123, "/api/test", HttpVerb.GET),
            new Infrastructure.GetEntityPermissionsCommand("req-1", DateTime.UtcNow, "user-1", 123, 1, 10)
        };

        // Act & Assert
        foreach (var command in queryCommands)
        {
            Assert.IsTrue(_translationService.IsQueryCommand(command), 
                $"Command {command.GetType().Name} should be classified as query");
        }
    }

    [TestMethod]
    public void IsQueryCommand_WithMutationCommands_ReturnsFalse()
    {
        // Arrange
        var mutationCommands = new WebRequestCommand[]
        {
            new Infrastructure.CreateUserCommand("req-1", DateTime.UtcNow, "user-1", "John"),
            new Infrastructure.UpdateUserCommand("req-1", DateTime.UtcNow, "user-1", 123, "John Updated"),
            new Infrastructure.DeleteUserCommand("req-1", DateTime.UtcNow, "user-1", 123),
            new Infrastructure.AddUserToGroupCommand("req-1", DateTime.UtcNow, "user-1", 123, 456),
            new Infrastructure.GrantPermissionCommand("req-1", DateTime.UtcNow, "user-1", 123, "/api/test", HttpVerb.GET, Scheme.ApiUriAuthorization)
        };

        // Act & Assert
        foreach (var command in mutationCommands)
        {
            Assert.IsFalse(_translationService.IsQueryCommand(command), 
                $"Command {command.GetType().Name} should not be classified as query");
        }
    }

    [TestMethod]
    public void IsMutationCommand_WithMutationCommands_ReturnsTrue()
    {
        // Arrange
        var mutationCommands = new WebRequestCommand[]
        {
            new Infrastructure.CreateUserCommand("req-1", DateTime.UtcNow, "user-1", "John"),
            new Infrastructure.UpdateUserCommand("req-1", DateTime.UtcNow, "user-1", 123, "John Updated"),
            new Infrastructure.DeleteUserCommand("req-1", DateTime.UtcNow, "user-1", 123),
            new Infrastructure.CreateGroupCommand("req-1", DateTime.UtcNow, "user-1", "Group", null),
            new Infrastructure.CreateRoleCommand("req-1", DateTime.UtcNow, "user-1", "Role", null),
            new Infrastructure.AddUserToGroupCommand("req-1", DateTime.UtcNow, "user-1", 123, 456),
            new Infrastructure.RemoveUserFromGroupCommand("req-1", DateTime.UtcNow, "user-1", 123, 456),
            new Infrastructure.AssignUserToRoleCommand("req-1", DateTime.UtcNow, "user-1", 123, 789),
            new Infrastructure.UnAssignUserFromRoleCommand("req-1", DateTime.UtcNow, "user-1", 123, 789),
            new Infrastructure.AddRoleToGroupCommand("req-1", DateTime.UtcNow, "user-1", 456, 789),
            new Infrastructure.RemoveRoleFromGroupCommand("req-1", DateTime.UtcNow, "user-1", 456, 789),
            new Infrastructure.RemoveGroupFromGroupCommand("req-1", DateTime.UtcNow, "user-1", 100, 200),
            new Infrastructure.GrantPermissionCommand("req-1", DateTime.UtcNow, "user-1", 123, "/api/test", HttpVerb.GET, Scheme.ApiUriAuthorization),
            new Infrastructure.DenyPermissionCommand("req-1", DateTime.UtcNow, "user-1", 123, "/api/test", HttpVerb.GET, Scheme.ApiUriAuthorization),
            new Infrastructure.RemovePermissionCommand("req-1", DateTime.UtcNow, "user-1", 123, "/api/test", HttpVerb.GET)
        };

        // Act & Assert
        foreach (var command in mutationCommands)
        {
            Assert.IsTrue(_translationService.IsMutationCommand(command), 
                $"Command {command.GetType().Name} should be classified as mutation");
        }
    }

    [TestMethod]
    public void GetCommandDescription_ReturnsDescriptiveText()
    {
        // Arrange
        var commands = new (WebRequestCommand command, string expectedDescription)[]
        {
            (new Infrastructure.AddUserToGroupCommand("req-1", DateTime.UtcNow, "user-1", 123, 456), 
             "Add user 123 to group 456"),
            (new Infrastructure.RemoveRoleFromGroupCommand("req-1", DateTime.UtcNow, "user-1", 456, 789), 
             "Remove role 789 from group 456"),
            (new Infrastructure.GrantPermissionCommand("req-1", DateTime.UtcNow, "user-1", 123, "/api/test", HttpVerb.GET, Scheme.ApiUriAuthorization), 
             "Grant permission /api/test:GET to entity 123")
        };

        // Act & Assert
        foreach (var (command, expectedDescription) in commands)
        {
            var description = _translationService.GetCommandDescription(command);
            Assert.AreEqual(expectedDescription, description);
        }
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public void TranslateCommand_UnsupportedCommand_ThrowsNotSupportedException()
    {
        // Arrange
        var unsupportedCommand = new UnsupportedTestCommand("req-1", DateTime.UtcNow, "user-1");

        // Act & Assert
        Assert.ThrowsException<NotSupportedException>(() => 
            _translationService.TranslateCommand(unsupportedCommand));
    }

    #endregion
}

// Test helper class
public record UnsupportedTestCommand(string RequestId, DateTime Timestamp, string UserId) 
    : WebRequestCommand(RequestId, Timestamp, UserId);