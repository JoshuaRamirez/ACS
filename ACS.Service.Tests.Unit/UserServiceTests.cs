using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using ACS.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class UserServiceTests
{
    private Mock<ApplicationDbContext> _mockDbContext = null!;
    private Mock<ICommandProcessingService> _mockCommandProcessingService = null!;
    private Mock<ILogger<UserService>> _mockLogger = null!;
    private UserService _userService = null!;
    private Mock<DbSet<Data.Models.User>> _mockUserDbSet = null!;
    private Mock<DbSet<Data.Models.Entity>> _mockEntityDbSet = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _mockDbContext = new Mock<ApplicationDbContext>(options);
        _mockCommandProcessingService = new Mock<ICommandProcessingService>();
        _mockLogger = new Mock<ILogger<UserService>>();
        
        _mockUserDbSet = new Mock<DbSet<Data.Models.User>>();
        _mockEntityDbSet = new Mock<DbSet<Data.Models.Entity>>();
        
        _mockDbContext.Setup(x => x.Users).Returns(_mockUserDbSet.Object);
        _mockDbContext.Setup(x => x.Entities).Returns(_mockEntityDbSet.Object);
        
        _userService = new UserService(
            _mockDbContext.Object,
            _mockCommandProcessingService.Object,
            _mockLogger.Object);
    }

    #region GetAllAsync Tests

    [TestMethod]
    public async Task UserService_GetAllAsync_ReturnsAllUsers()
    {
        // Arrange
        var dataUsers = new List<Data.Models.User>
        {
            new() { Id = 1, Name = "User1", Entity = new Data.Models.Entity { Id = 1, EntityType = "User" }, UserGroups = new List<UserGroup>(), UserRoles = new List<UserRole>() },
            new() { Id = 2, Name = "User2", Entity = new Data.Models.Entity { Id = 2, EntityType = "User" }, UserGroups = new List<UserGroup>(), UserRoles = new List<UserRole>() }
        };

        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _userService.GetAllAsync();

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task UserService_GetAllAsync_ReturnsEmptyCollectionWhenNoUsers()
    {
        // Arrange
        var dataUsers = new List<Data.Models.User>();
        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _userService.GetAllAsync();

        // Assert
        Assert.AreEqual(0, result.Count());
    }

    #endregion

    #region GetByIdAsync Tests

    [TestMethod]
    public async Task UserService_GetByIdAsync_ReturnsUserWhenExists()
    {
        // Arrange
        var userId = 1;
        var dataUser = new Data.Models.User 
        { 
            Id = userId, 
            Name = "TestUser", 
            Entity = new Data.Models.Entity { Id = 1, EntityType = "User" },
            UserGroups = new List<UserGroup>(),
            UserRoles = new List<UserRole>()
        };

        var dataUsers = new List<Data.Models.User> { dataUser };
        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _userService.GetByIdAsync(userId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.Id);
        Assert.AreEqual("TestUser", result.Name);
    }

    [TestMethod]
    public async Task UserService_GetByIdAsync_ReturnsNullWhenUserNotExists()
    {
        // Arrange
        var userId = 999;
        var dataUsers = new List<Data.Models.User>();
        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _userService.GetByIdAsync(userId);

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region AddAsync Tests

    [TestMethod]
    public async Task UserService_AddAsync_CreatesEntityAndUser()
    {
        // Arrange
        var domainUser = new Domain.User { Name = "NewUser" };
        var createdBy = "TestAdmin";
        var entityId = 1;
        var userId = 1;

        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        _mockEntityDbSet.Setup(x => x.Add(It.IsAny<Data.Models.Entity>()))
            .Callback<Data.Models.Entity>(e => e.Id = entityId);

        _mockUserDbSet.Setup(x => x.Add(It.IsAny<Data.Models.User>()))
            .Callback<Data.Models.User>(u => u.Id = userId);

        // Act
        var result = await _userService.AddAsync(domainUser, createdBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.Id);
        Assert.AreEqual("NewUser", result.Name);
        _mockEntityDbSet.Verify(x => x.Add(It.Is<Data.Models.Entity>(e => e.EntityType == "User")), Times.Once);
        _mockUserDbSet.Verify(x => x.Add(It.Is<Data.Models.User>(u => u.Name == "NewUser")), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Exactly(2));
    }

    [TestMethod]
    public async Task UserService_AddAsync_LogsUserCreation()
    {
        // Arrange
        var domainUser = new Domain.User { Name = "NewUser" };
        var createdBy = "TestAdmin";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _userService.AddAsync(domainUser, createdBy);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region UpdateAsync Tests

    [TestMethod]
    public async Task UserService_UpdateAsync_UpdatesExistingUser()
    {
        // Arrange
        var userId = 1;
        var domainUser = new Domain.User { Id = userId, Name = "UpdatedUser" };
        var dataUser = new Data.Models.User { Id = userId, Name = "OriginalUser" };

        _mockUserDbSet.Setup(x => x.FindAsync(userId))
            .ReturnsAsync(dataUser);
        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        // Act
        var result = await _userService.UpdateAsync(domainUser);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.Id);
        Assert.AreEqual("UpdatedUser", result.Name);
        Assert.AreEqual("UpdatedUser", dataUser.Name);
        _mockUserDbSet.Verify(x => x.Update(dataUser), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task UserService_UpdateAsync_ThrowsExceptionWhenUserNotFound()
    {
        // Arrange
        var userId = 999;
        var domainUser = new Domain.User { Id = userId, Name = "UpdatedUser" };

        _mockUserDbSet.Setup(x => x.FindAsync(userId))
            .ReturnsAsync((Data.Models.User?)null);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _userService.UpdateAsync(domainUser));
    }

    [TestMethod]
    public async Task UserService_UpdateAsync_LogsUserUpdate()
    {
        // Arrange
        var userId = 1;
        var domainUser = new Domain.User { Id = userId, Name = "UpdatedUser" };
        var dataUser = new Data.Models.User { Id = userId, Name = "OriginalUser" };

        _mockUserDbSet.Setup(x => x.FindAsync(userId)).ReturnsAsync(dataUser);
        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _userService.UpdateAsync(domainUser);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Updated user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region DeleteAsync Tests

    [TestMethod]
    public async Task UserService_DeleteAsync_DeletesUserAndEntity()
    {
        // Arrange
        var userId = 1;
        var entity = new Data.Models.Entity { Id = 1, EntityType = "User" };
        var dataUser = new Data.Models.User { Id = userId, Name = "UserToDelete", Entity = entity };

        var dataUsers = new List<Data.Models.User> { dataUser };
        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _userService.DeleteAsync(userId);

        // Assert
        _mockUserDbSet.Verify(x => x.Remove(dataUser), Times.Once);
        _mockEntityDbSet.Verify(x => x.Remove(entity), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task UserService_DeleteAsync_ThrowsExceptionWhenUserNotFound()
    {
        // Arrange
        var userId = 999;
        var dataUsers = new List<Data.Models.User>();
        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _userService.DeleteAsync(userId));
    }

    [TestMethod]
    public async Task UserService_DeleteAsync_LogsUserDeletion()
    {
        // Arrange
        var userId = 1;
        var entity = new Data.Models.Entity { Id = 1, EntityType = "User" };
        var dataUser = new Data.Models.User { Id = userId, Name = "UserToDelete", Entity = entity };

        var dataUsers = new List<Data.Models.User> { dataUser };
        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _userService.DeleteAsync(userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Legacy Sync Methods Tests

    [TestMethod]
    public void UserService_GetAll_CallsGetAllAsync()
    {
        // Arrange
        var dataUsers = new List<Data.Models.User>
        {
            new() { Id = 1, Name = "User1", Entity = new Data.Models.Entity(), UserGroups = new List<UserGroup>(), UserRoles = new List<UserRole>() }
        };

        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = _userService.GetAll();

        // Assert
        Assert.AreEqual(1, result.Count());
    }

    [TestMethod]
    public void UserService_GetById_CallsGetByIdAsync()
    {
        // Arrange
        var userId = 1;
        var dataUser = new Data.Models.User 
        { 
            Id = userId, 
            Name = "TestUser", 
            Entity = new Data.Models.Entity(),
            UserGroups = new List<UserGroup>(),
            UserRoles = new List<UserRole>()
        };

        var dataUsers = new List<Data.Models.User> { dataUser };
        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = _userService.GetById(userId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.Id);
    }

    [TestMethod]
    public void UserService_Add_CallsAddAsync()
    {
        // Arrange
        var domainUser = new Domain.User { Name = "NewUser" };

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = _userService.Add(domainUser);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("NewUser", result.Name);
    }

    #endregion

    #region ConvertToDomainUser Tests

    [TestMethod]
    public async Task UserService_ConvertToDomainUser_MapsUserGroupsCorrectly()
    {
        // Arrange
        var group1 = new Data.Models.Group { Id = 1, Name = "Group1" };
        var group2 = new Data.Models.Group { Id = 2, Name = "Group2" };
        
        var dataUser = new Data.Models.User
        {
            Id = 1,
            Name = "TestUser",
            Entity = new Data.Models.Entity { Id = 1, EntityType = "User" },
            UserGroups = new List<UserGroup>
            {
                new() { UserId = 1, GroupId = 1, Group = group1 },
                new() { UserId = 1, GroupId = 2, Group = group2 }
            },
            UserRoles = new List<UserRole>()
        };

        var dataUsers = new List<Data.Models.User> { dataUser };
        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _userService.GetByIdAsync(1);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Parents.Count);
        Assert.IsTrue(result.Parents.Any(p => p.Name == "Group1"));
        Assert.IsTrue(result.Parents.Any(p => p.Name == "Group2"));
    }

    [TestMethod]
    public async Task UserService_ConvertToDomainUser_MapsUserRolesCorrectly()
    {
        // Arrange
        var role1 = new Data.Models.Role { Id = 1, Name = "Role1" };
        var role2 = new Data.Models.Role { Id = 2, Name = "Role2" };
        
        var dataUser = new Data.Models.User
        {
            Id = 1,
            Name = "TestUser",
            Entity = new Data.Models.Entity { Id = 1, EntityType = "User" },
            UserGroups = new List<UserGroup>(),
            UserRoles = new List<UserRole>
            {
                new() { UserId = 1, RoleId = 1, Role = role1 },
                new() { UserId = 1, RoleId = 2, Role = role2 }
            }
        };

        var dataUsers = new List<Data.Models.User> { dataUser };
        var mockQueryable = dataUsers.AsQueryable();
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockUserDbSet.As<IQueryable<Data.Models.User>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _userService.GetByIdAsync(1);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Parents.Count);
        Assert.IsTrue(result.Parents.Any(p => p.Name == "Role1"));
        Assert.IsTrue(result.Parents.Any(p => p.Name == "Role2"));
    }

    #endregion
}