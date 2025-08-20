using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using ACS.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class RoleServiceTests
{
    private Mock<ApplicationDbContext> _mockDbContext = null!;
    private Mock<ILogger<RoleService>> _mockLogger = null!;
    private Mock<IPermissionEvaluationService> _mockPermissionService = null!;
    private RoleService _roleService = null!;
    private Mock<DbSet<Data.Models.Role>> _mockRoleDbSet = null!;
    private Mock<DbSet<Data.Models.Entity>> _mockEntityDbSet = null!;
    private Mock<DbSet<UserRole>> _mockUserRoleDbSet = null!;
    private Mock<DbSet<GroupRole>> _mockGroupRoleDbSet = null!;
    private Mock<DbSet<AuditLog>> _mockAuditLogDbSet = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _mockDbContext = new Mock<ApplicationDbContext>(options);
        _mockLogger = new Mock<ILogger<RoleService>>();
        _mockPermissionService = new Mock<IPermissionEvaluationService>();
        
        _mockRoleDbSet = new Mock<DbSet<Data.Models.Role>>();
        _mockEntityDbSet = new Mock<DbSet<Data.Models.Entity>>();
        _mockUserRoleDbSet = new Mock<DbSet<UserRole>>();
        _mockGroupRoleDbSet = new Mock<DbSet<GroupRole>>();
        _mockAuditLogDbSet = new Mock<DbSet<AuditLog>>();
        
        _mockDbContext.Setup(x => x.Roles).Returns(_mockRoleDbSet.Object);
        _mockDbContext.Setup(x => x.Entities).Returns(_mockEntityDbSet.Object);
        _mockDbContext.Setup(x => x.UserRoles).Returns(_mockUserRoleDbSet.Object);
        _mockDbContext.Setup(x => x.GroupRoles).Returns(_mockGroupRoleDbSet.Object);
        _mockDbContext.Setup(x => x.AuditLogs).Returns(_mockAuditLogDbSet.Object);
        
        _roleService = new RoleService(
            _mockDbContext.Object,
            _mockLogger.Object,
            _mockPermissionService.Object);
    }

    #region GetAllRolesAsync Tests

    [TestMethod]
    public async Task RoleService_GetAllRolesAsync_ReturnsAllRoles()
    {
        // Arrange
        var dataRoles = new List<Data.Models.Role>
        {
            new() { Id = 1, Name = "Admin", Entity = new Data.Models.Entity { Id = 1, EntityType = "Role" }, UserRoles = new List<UserRole>(), GroupRoles = new List<GroupRole>() },
            new() { Id = 2, Name = "User", Entity = new Data.Models.Entity { Id = 2, EntityType = "Role" }, UserRoles = new List<UserRole>(), GroupRoles = new List<GroupRole>() }
        };

        var mockQueryable = dataRoles.AsQueryable();
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _roleService.GetAllRolesAsync();

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task RoleService_GetAllRolesAsync_ReturnsEmptyCollectionWhenNoRoles()
    {
        // Arrange
        var dataRoles = new List<Data.Models.Role>();
        var mockQueryable = dataRoles.AsQueryable();
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _roleService.GetAllRolesAsync();

        // Assert
        Assert.AreEqual(0, result.Count());
    }

    #endregion

    #region GetRoleByIdAsync Tests

    [TestMethod]
    public async Task RoleService_GetRoleByIdAsync_ReturnsRoleWhenExists()
    {
        // Arrange
        var roleId = 1;
        var dataRole = new Data.Models.Role 
        { 
            Id = roleId, 
            Name = "TestRole", 
            Entity = new Data.Models.Entity { Id = 1, EntityType = "Role" },
            UserRoles = new List<UserRole>(),
            GroupRoles = new List<GroupRole>()
        };

        var dataRoles = new List<Data.Models.Role> { dataRole };
        var mockQueryable = dataRoles.AsQueryable();
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _roleService.GetRoleByIdAsync(roleId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(roleId, result.Id);
        Assert.AreEqual("TestRole", result.Name);
    }

    [TestMethod]
    public async Task RoleService_GetRoleByIdAsync_ReturnsNullWhenRoleNotExists()
    {
        // Arrange
        var roleId = 999;
        var dataRoles = new List<Data.Models.Role>();
        var mockQueryable = dataRoles.AsQueryable();
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _roleService.GetRoleByIdAsync(roleId);

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region CreateRoleAsync Tests

    [TestMethod]
    public async Task RoleService_CreateRoleAsync_CreatesEntityAndRole()
    {
        // Arrange
        var name = "NewRole";
        var description = "Role Description";
        var createdBy = "TestAdmin";
        var entityId = 1;
        var roleId = 1;

        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        _mockEntityDbSet.Setup(x => x.Add(It.IsAny<Data.Models.Entity>()))
            .Callback<Data.Models.Entity>(e => e.Id = entityId);

        _mockRoleDbSet.Setup(x => x.Add(It.IsAny<Data.Models.Role>()))
            .Callback<Data.Models.Role>(r => r.Id = roleId);

        // Act
        var result = await _roleService.CreateRoleAsync(name, description, createdBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(roleId, result.Id);
        Assert.AreEqual(name, result.Name);
        _mockEntityDbSet.Verify(x => x.Add(It.Is<Data.Models.Entity>(e => e.EntityType == "Role")), Times.Once);
        _mockRoleDbSet.Verify(x => x.Add(It.Is<Data.Models.Role>(r => r.Name == name && r.Description == description)), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Exactly(2));
    }

    [TestMethod]
    public async Task RoleService_CreateRoleAsync_LogsRoleCreation()
    {
        // Arrange
        var name = "NewRole";
        var description = "Role Description";
        var createdBy = "TestAdmin";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _roleService.CreateRoleAsync(name, description, createdBy);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created role")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region UpdateRoleAsync Tests

    [TestMethod]
    public async Task RoleService_UpdateRoleAsync_UpdatesExistingRole()
    {
        // Arrange
        var roleId = 1;
        var name = "UpdatedRole";
        var description = "Updated Description";
        var updatedBy = "TestAdmin";
        var dataRole = new Data.Models.Role { Id = roleId, Name = "OriginalRole", Description = "Original Description" };

        _mockRoleDbSet.Setup(x => x.FindAsync(roleId))
            .ReturnsAsync(dataRole);
        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        // Act
        var result = await _roleService.UpdateRoleAsync(roleId, name, description, updatedBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(roleId, result.Id);
        Assert.AreEqual(name, result.Name);
        Assert.AreEqual(name, dataRole.Name);
        Assert.AreEqual(description, dataRole.Description);
        _mockRoleDbSet.Verify(x => x.Update(dataRole), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task RoleService_UpdateRoleAsync_ThrowsExceptionWhenRoleNotFound()
    {
        // Arrange
        var roleId = 999;
        var name = "UpdatedRole";
        var description = "Updated Description";
        var updatedBy = "TestAdmin";

        _mockRoleDbSet.Setup(x => x.FindAsync(roleId))
            .ReturnsAsync((Data.Models.Role?)null);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _roleService.UpdateRoleAsync(roleId, name, description, updatedBy));
    }

    #endregion

    #region DeleteRoleAsync Tests

    [TestMethod]
    public async Task RoleService_DeleteRoleAsync_DeletesRoleAndEntity()
    {
        // Arrange
        var roleId = 1;
        var entity = new Data.Models.Entity { Id = 1, EntityType = "Role" };
        var dataRole = new Data.Models.Role { Id = roleId, Name = "RoleToDelete", Entity = entity };

        var dataRoles = new List<Data.Models.Role> { dataRole };
        var mockQueryable = dataRoles.AsQueryable();
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        var userRoles = new List<UserRole>();
        var mockUserRoleQueryable = userRoles.AsQueryable();
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.Provider).Returns(mockUserRoleQueryable.Provider);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.Expression).Returns(mockUserRoleQueryable.Expression);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.ElementType).Returns(mockUserRoleQueryable.ElementType);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.GetEnumerator()).Returns(mockUserRoleQueryable.GetEnumerator());

        var groupRoles = new List<GroupRole>();
        var mockGroupRoleQueryable = groupRoles.AsQueryable();
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.Provider).Returns(mockGroupRoleQueryable.Provider);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.Expression).Returns(mockGroupRoleQueryable.Expression);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.ElementType).Returns(mockGroupRoleQueryable.ElementType);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.GetEnumerator()).Returns(mockGroupRoleQueryable.GetEnumerator());

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _roleService.DeleteRoleAsync(roleId, "TestAdmin");

        // Assert
        _mockRoleDbSet.Verify(x => x.Remove(dataRole), Times.Once);
        _mockEntityDbSet.Verify(x => x.Remove(entity), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task RoleService_DeleteRoleAsync_ThrowsExceptionWhenRoleNotFound()
    {
        // Arrange
        var roleId = 999;
        var dataRoles = new List<Data.Models.Role>();
        var mockQueryable = dataRoles.AsQueryable();
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _roleService.DeleteRoleAsync(roleId, "TestAdmin"));
    }

    [TestMethod]
    public async Task RoleService_DeleteRoleAsync_ThrowsExceptionWhenRoleHasAssignments()
    {
        // Arrange
        var roleId = 1;
        var entity = new Data.Models.Entity { Id = 1, EntityType = "Role" };
        var dataRole = new Data.Models.Role { Id = roleId, Name = "RoleWithAssignments", Entity = entity };

        var dataRoles = new List<Data.Models.Role> { dataRole };
        var mockQueryable = dataRoles.AsQueryable();
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        var userRoles = new List<UserRole> { new() { UserId = 1, RoleId = roleId } };
        var mockUserRoleQueryable = userRoles.AsQueryable();
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.Provider).Returns(mockUserRoleQueryable.Provider);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.Expression).Returns(mockUserRoleQueryable.Expression);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.ElementType).Returns(mockUserRoleQueryable.ElementType);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.GetEnumerator()).Returns(mockUserRoleQueryable.GetEnumerator());

        var groupRoles = new List<GroupRole>();
        var mockGroupRoleQueryable = groupRoles.AsQueryable();
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.Provider).Returns(mockGroupRoleQueryable.Provider);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.Expression).Returns(mockGroupRoleQueryable.Expression);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.ElementType).Returns(mockGroupRoleQueryable.ElementType);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.GetEnumerator()).Returns(mockGroupRoleQueryable.GetEnumerator());

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _roleService.DeleteRoleAsync(roleId, "TestAdmin"));
    }

    #endregion

    #region GetRoleUsersAsync Tests

    [TestMethod]
    public async Task RoleService_GetRoleUsersAsync_ReturnsDirectUsers()
    {
        // Arrange
        var roleId = 1;
        var user1 = new Data.Models.User { Id = 1, Name = "User1" };
        var user2 = new Data.Models.User { Id = 2, Name = "User2" };

        var userRoles = new List<UserRole>
        {
            new() { UserId = 1, RoleId = roleId, User = user1 },
            new() { UserId = 2, RoleId = roleId, User = user2 }
        };

        var mockUserRoleQueryable = userRoles.AsQueryable();
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.Provider).Returns(mockUserRoleQueryable.Provider);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.Expression).Returns(mockUserRoleQueryable.Expression);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.ElementType).Returns(mockUserRoleQueryable.ElementType);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.GetEnumerator()).Returns(mockUserRoleQueryable.GetEnumerator());

        var groupRoles = new List<GroupRole>();
        var mockGroupRoleQueryable = groupRoles.AsQueryable();
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.Provider).Returns(mockGroupRoleQueryable.Provider);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.Expression).Returns(mockGroupRoleQueryable.Expression);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.ElementType).Returns(mockGroupRoleQueryable.ElementType);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.GetEnumerator()).Returns(mockGroupRoleQueryable.GetEnumerator());

        // Act
        var result = await _roleService.GetRoleUsersAsync(roleId);

        // Assert
        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.Any(u => u.Name == "User1"));
        Assert.IsTrue(result.Any(u => u.Name == "User2"));
    }

    #endregion

    #region IsUserInRoleAsync Tests

    [TestMethod]
    public async Task RoleService_IsUserInRoleAsync_ReturnsTrueForDirectAssignment()
    {
        // Arrange
        var userId = 1;
        var roleId = 1;

        var userRoles = new List<UserRole> { new() { UserId = userId, RoleId = roleId } };
        var mockUserRoleQueryable = userRoles.AsQueryable();
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.Provider).Returns(mockUserRoleQueryable.Provider);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.Expression).Returns(mockUserRoleQueryable.Expression);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.ElementType).Returns(mockUserRoleQueryable.ElementType);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.GetEnumerator()).Returns(mockUserRoleQueryable.GetEnumerator());

        // Act
        var result = await _roleService.IsUserInRoleAsync(userId, roleId);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task RoleService_IsUserInRoleAsync_ReturnsFalseWhenNotAssigned()
    {
        // Arrange
        var userId = 1;
        var roleId = 1;

        var userRoles = new List<UserRole>();
        var mockUserRoleQueryable = userRoles.AsQueryable();
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.Provider).Returns(mockUserRoleQueryable.Provider);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.Expression).Returns(mockUserRoleQueryable.Expression);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.ElementType).Returns(mockUserRoleQueryable.ElementType);
        _mockUserRoleDbSet.As<IQueryable<UserRole>>().Setup(m => m.GetEnumerator()).Returns(mockUserRoleQueryable.GetEnumerator());

        var userGroups = new List<UserGroup>();
        var mockUserGroupQueryable = userGroups.AsQueryable();
        _mockUserGroupDbSet.As<IQueryable<UserGroup>>().Setup(m => m.Provider).Returns(mockUserGroupQueryable.Provider);
        _mockUserGroupDbSet.As<IQueryable<UserGroup>>().Setup(m => m.Expression).Returns(mockUserGroupQueryable.Expression);
        _mockUserGroupDbSet.As<IQueryable<UserGroup>>().Setup(m => m.ElementType).Returns(mockUserGroupQueryable.ElementType);
        _mockUserGroupDbSet.As<IQueryable<UserGroup>>().Setup(m => m.GetEnumerator()).Returns(mockUserGroupQueryable.GetEnumerator());

        var groupRoles = new List<GroupRole>();
        var mockGroupRoleQueryable = groupRoles.AsQueryable();
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.Provider).Returns(mockGroupRoleQueryable.Provider);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.Expression).Returns(mockGroupRoleQueryable.Expression);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.ElementType).Returns(mockGroupRoleQueryable.ElementType);
        _mockGroupRoleDbSet.As<IQueryable<GroupRole>>().Setup(m => m.GetEnumerator()).Returns(mockGroupRoleQueryable.GetEnumerator());

        var mockUserGroupDbSet = new Mock<DbSet<UserGroup>>();
        _mockDbContext.Setup(x => x.UserGroups).Returns(mockUserGroupDbSet.Object);

        // Act
        var result = await _roleService.IsUserInRoleAsync(userId, roleId);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region RoleHasPermissionAsync Tests

    [TestMethod]
    public async Task RoleService_RoleHasPermissionAsync_CallsPermissionService()
    {
        // Arrange
        var roleId = 1;
        var resource = "/api/users";
        var action = "GET";

        _mockPermissionService.Setup(x => x.HasPermissionAsync(roleId, resource, action))
            .ReturnsAsync(true);

        // Act
        var result = await _roleService.RoleHasPermissionAsync(roleId, resource, action);

        // Assert
        Assert.IsTrue(result);
        _mockPermissionService.Verify(x => x.HasPermissionAsync(roleId, resource, action), Times.Once);
    }

    #endregion

    #region SearchRolesAsync Tests

    [TestMethod]
    public async Task RoleService_SearchRolesAsync_ReturnsMatchingRoles()
    {
        // Arrange
        var searchTerm = "Admin";
        var dataRoles = new List<Data.Models.Role>
        {
            new() { Id = 1, Name = "Administrator", Description = "Admin role" },
            new() { Id = 2, Name = "User", Description = "Regular user" },
            new() { Id = 3, Name = "SuperAdmin", Description = "Super administrator" }
        };

        var mockQueryable = dataRoles.AsQueryable();
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockRoleDbSet.As<IQueryable<Data.Models.Role>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _roleService.SearchRolesAsync(searchTerm);

        // Assert
        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.All(r => r.Name.Contains(searchTerm) || r.Description?.Contains(searchTerm) == true));
    }

    #endregion

    #region CloneRoleAsync Tests

    [TestMethod]
    public async Task RoleService_CloneRoleAsync_CreatesNewRoleWithSamePermissions()
    {
        // Arrange
        var sourceRoleId = 1;
        var newRoleName = "ClonedRole";
        var clonedBy = "TestAdmin";
        var sourceRole = new Data.Models.Role 
        { 
            Id = sourceRoleId, 
            Name = "SourceRole", 
            Entity = new Data.Models.Entity { Id = 1, EntityType = "Role" } 
        };

        _mockRoleDbSet.Setup(x => x.FindAsync(sourceRoleId))
            .ReturnsAsync(sourceRole);
        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        var permissions = new List<Domain.Permission>
        {
            new() { Uri = "/api/users", HttpVerb = Domain.HttpVerb.GET, Grant = true }
        };
        _mockPermissionService.Setup(x => x.GetEntityPermissionsAsync(sourceRoleId, false))
            .ReturnsAsync(permissions);

        // Act
        var result = await _roleService.CloneRoleAsync(sourceRoleId, newRoleName, clonedBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(newRoleName, result.Name);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cloned role")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RoleService_CloneRoleAsync_ThrowsExceptionWhenSourceRoleNotFound()
    {
        // Arrange
        var sourceRoleId = 999;
        var newRoleName = "ClonedRole";
        var clonedBy = "TestAdmin";

        _mockRoleDbSet.Setup(x => x.FindAsync(sourceRoleId))
            .ReturnsAsync((Data.Models.Role?)null);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _roleService.CloneRoleAsync(sourceRoleId, newRoleName, clonedBy));
    }

    #endregion

    #region CreateRoleFromTemplateAsync Tests

    [TestMethod]
    public async Task RoleService_CreateRoleFromTemplateAsync_CreatesRoleWithAdminTemplate()
    {
        // Arrange
        var templateName = "Admin";
        var roleName = "NewAdminRole";
        var createdBy = "TestAdmin";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        // Act
        var result = await _roleService.CreateRoleFromTemplateAsync(templateName, roleName, createdBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(roleName, result.Name);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created role") && v.ToString()!.Contains("from template")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RoleService_CreateRoleFromTemplateAsync_ThrowsExceptionForInvalidTemplate()
    {
        // Arrange
        var templateName = "InvalidTemplate";
        var roleName = "NewRole";
        var createdBy = "TestAdmin";

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _roleService.CreateRoleFromTemplateAsync(templateName, roleName, createdBy));
    }

    #endregion

    #region CreateRolesBulkAsync Tests

    [TestMethod]
    public async Task RoleService_CreateRolesBulkAsync_CreatesMultipleRoles()
    {
        // Arrange
        var roles = new List<(string Name, string Description)>
        {
            ("Role1", "Description1"),
            ("Role2", "Description2"),
            ("Role3", "Description3")
        };
        var createdBy = "TestAdmin";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _roleService.CreateRolesBulkAsync(roles, createdBy);

        // Assert
        Assert.AreEqual(3, result.Count());
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created 3 roles in bulk")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}