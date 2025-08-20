using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using ACS.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class GroupServiceTests
{
    private Mock<ApplicationDbContext> _mockDbContext = null!;
    private Mock<ILogger<GroupService>> _mockLogger = null!;
    private Mock<IPermissionEvaluationService> _mockPermissionService = null!;
    private GroupService _groupService = null!;
    private Mock<DbSet<Data.Models.Group>> _mockGroupDbSet = null!;
    private Mock<DbSet<Data.Models.Entity>> _mockEntityDbSet = null!;
    private Mock<DbSet<GroupHierarchy>> _mockGroupHierarchyDbSet = null!;
    private Mock<DbSet<UserGroup>> _mockUserGroupDbSet = null!;
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
        _mockLogger = new Mock<ILogger<GroupService>>();
        _mockPermissionService = new Mock<IPermissionEvaluationService>();
        
        _mockGroupDbSet = new Mock<DbSet<Data.Models.Group>>();
        _mockEntityDbSet = new Mock<DbSet<Data.Models.Entity>>();
        _mockGroupHierarchyDbSet = new Mock<DbSet<GroupHierarchy>>();
        _mockUserGroupDbSet = new Mock<DbSet<UserGroup>>();
        _mockGroupRoleDbSet = new Mock<DbSet<GroupRole>>();
        _mockAuditLogDbSet = new Mock<DbSet<AuditLog>>();
        
        _mockDbContext.Setup(x => x.Groups).Returns(_mockGroupDbSet.Object);
        _mockDbContext.Setup(x => x.Entities).Returns(_mockEntityDbSet.Object);
        _mockDbContext.Setup(x => x.GroupHierarchies).Returns(_mockGroupHierarchyDbSet.Object);
        _mockDbContext.Setup(x => x.UserGroups).Returns(_mockUserGroupDbSet.Object);
        _mockDbContext.Setup(x => x.GroupRoles).Returns(_mockGroupRoleDbSet.Object);
        _mockDbContext.Setup(x => x.AuditLogs).Returns(_mockAuditLogDbSet.Object);
        
        _groupService = new GroupService(
            _mockDbContext.Object,
            _mockLogger.Object,
            _mockPermissionService.Object);
    }

    #region GetAllGroupsAsync Tests

    [TestMethod]
    public async Task GroupService_GetAllGroupsAsync_ReturnsAllGroups()
    {
        // Arrange
        var dataGroups = new List<Data.Models.Group>
        {
            new() { Id = 1, Name = "Group1", Entity = new Data.Models.Entity { Id = 1, EntityType = "Group" }, UserGroups = new List<UserGroup>(), GroupRoles = new List<GroupRole>() },
            new() { Id = 2, Name = "Group2", Entity = new Data.Models.Entity { Id = 2, EntityType = "Group" }, UserGroups = new List<UserGroup>(), GroupRoles = new List<GroupRole>() }
        };

        var mockQueryable = dataGroups.AsQueryable();
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _groupService.GetAllGroupsAsync();

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task GroupService_GetAllGroupsAsync_ReturnsEmptyCollectionWhenNoGroups()
    {
        // Arrange
        var dataGroups = new List<Data.Models.Group>();
        var mockQueryable = dataGroups.AsQueryable();
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _groupService.GetAllGroupsAsync();

        // Assert
        Assert.AreEqual(0, result.Count());
    }

    #endregion

    #region GetGroupByIdAsync Tests

    [TestMethod]
    public async Task GroupService_GetGroupByIdAsync_ReturnsGroupWhenExists()
    {
        // Arrange
        var groupId = 1;
        var dataGroup = new Data.Models.Group 
        { 
            Id = groupId, 
            Name = "TestGroup", 
            Entity = new Data.Models.Entity { Id = 1, EntityType = "Group" },
            UserGroups = new List<UserGroup>(),
            GroupRoles = new List<GroupRole>(),
            ChildGroupRelations = new List<GroupHierarchy>(),
            ParentGroupRelations = new List<GroupHierarchy>()
        };

        var dataGroups = new List<Data.Models.Group> { dataGroup };
        var mockQueryable = dataGroups.AsQueryable();
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _groupService.GetGroupByIdAsync(groupId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(groupId, result.Id);
        Assert.AreEqual("TestGroup", result.Name);
    }

    [TestMethod]
    public async Task GroupService_GetGroupByIdAsync_ReturnsNullWhenGroupNotExists()
    {
        // Arrange
        var groupId = 999;
        var dataGroups = new List<Data.Models.Group>();
        var mockQueryable = dataGroups.AsQueryable();
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _groupService.GetGroupByIdAsync(groupId);

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region CreateGroupAsync Tests

    [TestMethod]
    public async Task GroupService_CreateGroupAsync_CreatesEntityAndGroup()
    {
        // Arrange
        var name = "NewGroup";
        var description = "Group Description";
        var createdBy = "TestAdmin";
        var entityId = 1;
        var groupId = 1;

        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        _mockEntityDbSet.Setup(x => x.Add(It.IsAny<Data.Models.Entity>()))
            .Callback<Data.Models.Entity>(e => e.Id = entityId);

        _mockGroupDbSet.Setup(x => x.Add(It.IsAny<Data.Models.Group>()))
            .Callback<Data.Models.Group>(g => g.Id = groupId);

        // Act
        var result = await _groupService.CreateGroupAsync(name, description, createdBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(groupId, result.Id);
        Assert.AreEqual(name, result.Name);
        _mockEntityDbSet.Verify(x => x.Add(It.Is<Data.Models.Entity>(e => e.EntityType == "Group")), Times.Once);
        _mockGroupDbSet.Verify(x => x.Add(It.Is<Data.Models.Group>(g => g.Name == name && g.Description == description)), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Exactly(2));
    }

    [TestMethod]
    public async Task GroupService_CreateGroupAsync_LogsGroupCreation()
    {
        // Arrange
        var name = "NewGroup";
        var description = "Group Description";
        var createdBy = "TestAdmin";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _groupService.CreateGroupAsync(name, description, createdBy);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created group")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region UpdateGroupAsync Tests

    [TestMethod]
    public async Task GroupService_UpdateGroupAsync_UpdatesExistingGroup()
    {
        // Arrange
        var groupId = 1;
        var name = "UpdatedGroup";
        var description = "Updated Description";
        var updatedBy = "TestAdmin";
        var dataGroup = new Data.Models.Group { Id = groupId, Name = "OriginalGroup", Description = "Original Description" };

        _mockGroupDbSet.Setup(x => x.FindAsync(groupId))
            .ReturnsAsync(dataGroup);
        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        // Act
        var result = await _groupService.UpdateGroupAsync(groupId, name, description, updatedBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(groupId, result.Id);
        Assert.AreEqual(name, result.Name);
        Assert.AreEqual(name, dataGroup.Name);
        Assert.AreEqual(description, dataGroup.Description);
        _mockGroupDbSet.Verify(x => x.Update(dataGroup), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task GroupService_UpdateGroupAsync_ThrowsExceptionWhenGroupNotFound()
    {
        // Arrange
        var groupId = 999;
        var name = "UpdatedGroup";
        var description = "Updated Description";
        var updatedBy = "TestAdmin";

        _mockGroupDbSet.Setup(x => x.FindAsync(groupId))
            .ReturnsAsync((Data.Models.Group?)null);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _groupService.UpdateGroupAsync(groupId, name, description, updatedBy));
    }

    #endregion

    #region DeleteGroupAsync Tests

    [TestMethod]
    public async Task GroupService_DeleteGroupAsync_DeletesGroupAndEntity()
    {
        // Arrange
        var groupId = 1;
        var entity = new Data.Models.Entity { Id = 1, EntityType = "Group" };
        var dataGroup = new Data.Models.Group { Id = groupId, Name = "GroupToDelete", Entity = entity };

        var dataGroups = new List<Data.Models.Group> { dataGroup };
        var mockQueryable = dataGroups.AsQueryable();
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        var groupHierarchies = new List<GroupHierarchy>();
        var mockHierarchyQueryable = groupHierarchies.AsQueryable();
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Provider).Returns(mockHierarchyQueryable.Provider);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Expression).Returns(mockHierarchyQueryable.Expression);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.ElementType).Returns(mockHierarchyQueryable.ElementType);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.GetEnumerator()).Returns(mockHierarchyQueryable.GetEnumerator());

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _groupService.DeleteGroupAsync(groupId, "TestAdmin");

        // Assert
        _mockGroupDbSet.Verify(x => x.Remove(dataGroup), Times.Once);
        _mockEntityDbSet.Verify(x => x.Remove(entity), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task GroupService_DeleteGroupAsync_ThrowsExceptionWhenGroupNotFound()
    {
        // Arrange
        var groupId = 999;
        var dataGroups = new List<Data.Models.Group>();
        var mockQueryable = dataGroups.AsQueryable();
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _groupService.DeleteGroupAsync(groupId, "TestAdmin"));
    }

    [TestMethod]
    public async Task GroupService_DeleteGroupAsync_ThrowsExceptionWhenGroupHasChildren()
    {
        // Arrange
        var groupId = 1;
        var entity = new Data.Models.Entity { Id = 1, EntityType = "Group" };
        var dataGroup = new Data.Models.Group { Id = groupId, Name = "GroupWithChildren", Entity = entity };

        var dataGroups = new List<Data.Models.Group> { dataGroup };
        var mockQueryable = dataGroups.AsQueryable();
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        var groupHierarchies = new List<GroupHierarchy> { new() { ParentGroupId = groupId, ChildGroupId = 2 } };
        var mockHierarchyQueryable = groupHierarchies.AsQueryable();
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Provider).Returns(mockHierarchyQueryable.Provider);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Expression).Returns(mockHierarchyQueryable.Expression);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.ElementType).Returns(mockHierarchyQueryable.ElementType);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.GetEnumerator()).Returns(mockHierarchyQueryable.GetEnumerator());

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _groupService.DeleteGroupAsync(groupId, "TestAdmin"));
    }

    #endregion

    #region WouldCreateCycleAsync Tests

    [TestMethod]
    public async Task GroupService_WouldCreateCycleAsync_ReturnsTrueWhenParentEqualChild()
    {
        // Arrange
        var groupId = 1;

        // Act
        var result = await _groupService.WouldCreateCycleAsync(groupId, groupId);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task GroupService_WouldCreateCycleAsync_ReturnsFalseWhenNoCycle()
    {
        // Arrange
        var parentGroupId = 1;
        var childGroupId = 2;

        var groupHierarchies = new List<GroupHierarchy>();
        var mockHierarchyQueryable = groupHierarchies.AsQueryable();
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Provider).Returns(mockHierarchyQueryable.Provider);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Expression).Returns(mockHierarchyQueryable.Expression);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.ElementType).Returns(mockHierarchyQueryable.ElementType);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.GetEnumerator()).Returns(mockHierarchyQueryable.GetEnumerator());

        // Act
        var result = await _groupService.WouldCreateCycleAsync(parentGroupId, childGroupId);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GroupService_WouldCreateCycleAsync_ReturnsTrueWhenCycleExists()
    {
        // Arrange
        var parentGroupId = 1;
        var childGroupId = 2;

        // Set up hierarchy where group 2 is parent of group 1
        var groupHierarchies = new List<GroupHierarchy> 
        { 
            new() { ParentGroupId = 2, ChildGroupId = 1 }
        };
        var mockHierarchyQueryable = groupHierarchies.AsQueryable();
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Provider).Returns(mockHierarchyQueryable.Provider);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Expression).Returns(mockHierarchyQueryable.Expression);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.ElementType).Returns(mockHierarchyQueryable.ElementType);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.GetEnumerator()).Returns(mockHierarchyQueryable.GetEnumerator());

        // Act - trying to make group 1 parent of group 2 would create cycle
        var result = await _groupService.WouldCreateCycleAsync(parentGroupId, childGroupId);

        // Assert
        Assert.IsTrue(result);
    }

    #endregion

    #region ValidateGroupHierarchyAsync Tests

    [TestMethod]
    public async Task GroupService_ValidateGroupHierarchyAsync_ReturnsTrueWhenValid()
    {
        // Arrange
        var parentGroupId = 1;
        var childGroupId = 2;

        var groupHierarchies = new List<GroupHierarchy>();
        var mockHierarchyQueryable = groupHierarchies.AsQueryable();
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Provider).Returns(mockHierarchyQueryable.Provider);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Expression).Returns(mockHierarchyQueryable.Expression);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.ElementType).Returns(mockHierarchyQueryable.ElementType);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.GetEnumerator()).Returns(mockHierarchyQueryable.GetEnumerator());

        // Act
        var result = await _groupService.ValidateGroupHierarchyAsync(parentGroupId, childGroupId);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task GroupService_ValidateGroupHierarchyAsync_ReturnsFalseWhenInvalid()
    {
        // Arrange
        var parentGroupId = 1;
        var childGroupId = 1; // Same group

        // Act
        var result = await _groupService.ValidateGroupHierarchyAsync(parentGroupId, childGroupId);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region GetGroupUsersAsync Tests

    [TestMethod]
    public async Task GroupService_GetGroupUsersAsync_ReturnsDirectUsers()
    {
        // Arrange
        var groupId = 1;
        var user1 = new Data.Models.User { Id = 1, Name = "User1" };
        var user2 = new Data.Models.User { Id = 2, Name = "User2" };

        var userGroups = new List<UserGroup>
        {
            new() { UserId = 1, GroupId = groupId, User = user1 },
            new() { UserId = 2, GroupId = groupId, User = user2 }
        };

        var mockUserGroupsQueryable = userGroups.AsQueryable();
        _mockUserGroupDbSet.As<IQueryable<UserGroup>>().Setup(m => m.Provider).Returns(mockUserGroupsQueryable.Provider);
        _mockUserGroupDbSet.As<IQueryable<UserGroup>>().Setup(m => m.Expression).Returns(mockUserGroupsQueryable.Expression);
        _mockUserGroupDbSet.As<IQueryable<UserGroup>>().Setup(m => m.ElementType).Returns(mockUserGroupsQueryable.ElementType);
        _mockUserGroupDbSet.As<IQueryable<UserGroup>>().Setup(m => m.GetEnumerator()).Returns(mockUserGroupsQueryable.GetEnumerator());

        var groupHierarchies = new List<GroupHierarchy>();
        var mockHierarchyQueryable = groupHierarchies.AsQueryable();
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Provider).Returns(mockHierarchyQueryable.Provider);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.Expression).Returns(mockHierarchyQueryable.Expression);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.ElementType).Returns(mockHierarchyQueryable.ElementType);
        _mockGroupHierarchyDbSet.As<IQueryable<GroupHierarchy>>().Setup(m => m.GetEnumerator()).Returns(mockHierarchyQueryable.GetEnumerator());

        // Act
        var result = await _groupService.GetGroupUsersAsync(groupId, includeNested: false);

        // Assert
        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.Any(u => u.Name == "User1"));
        Assert.IsTrue(result.Any(u => u.Name == "User2"));
    }

    #endregion

    #region HasPermissionAsync Tests

    [TestMethod]
    public async Task GroupService_HasPermissionAsync_CallsPermissionService()
    {
        // Arrange
        var groupId = 1;
        var resource = "/api/users";
        var action = "GET";

        _mockPermissionService.Setup(x => x.HasPermissionAsync(groupId, resource, action))
            .ReturnsAsync(true);

        // Act
        var result = await _groupService.HasPermissionAsync(groupId, resource, action);

        // Assert
        Assert.IsTrue(result);
        _mockPermissionService.Verify(x => x.HasPermissionAsync(groupId, resource, action), Times.Once);
    }

    #endregion

    #region SearchGroupsAsync Tests

    [TestMethod]
    public async Task GroupService_SearchGroupsAsync_ReturnsMatchingGroups()
    {
        // Arrange
        var searchTerm = "Test";
        var dataGroups = new List<Data.Models.Group>
        {
            new() { Id = 1, Name = "TestGroup", Description = "A test group" },
            new() { Id = 2, Name = "ProductionGroup", Description = "Production environment" },
            new() { Id = 3, Name = "AnotherTestGroup", Description = "Another test" }
        };

        var mockQueryable = dataGroups.AsQueryable();
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockGroupDbSet.As<IQueryable<Data.Models.Group>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _groupService.SearchGroupsAsync(searchTerm);

        // Assert
        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.All(g => g.Name.Contains(searchTerm) || g.Description?.Contains(searchTerm) == true));
    }

    #endregion

    #region CreateGroupsBulkAsync Tests

    [TestMethod]
    public async Task GroupService_CreateGroupsBulkAsync_CreatesMultipleGroups()
    {
        // Arrange
        var groups = new List<(string Name, string Description)>
        {
            ("Group1", "Description1"),
            ("Group2", "Description2"),
            ("Group3", "Description3")
        };
        var createdBy = "TestAdmin";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _groupService.CreateGroupsBulkAsync(groups, createdBy);

        // Assert
        Assert.AreEqual(3, result.Count());
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created 3 groups in bulk")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}