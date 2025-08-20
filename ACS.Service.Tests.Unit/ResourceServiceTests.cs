using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using ACS.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class ResourceServiceTests
{
    private Mock<ApplicationDbContext> _mockDbContext = null!;
    private Mock<ILogger<ResourceService>> _mockLogger = null!;
    private Mock<IPermissionEvaluationService> _mockPermissionService = null!;
    private ResourceService _resourceService = null!;
    private Mock<DbSet<Data.Models.Resource>> _mockResourceDbSet = null!;
    private Mock<DbSet<Data.Models.Entity>> _mockEntityDbSet = null!;
    private Mock<DbSet<UriAccess>> _mockUriAccessDbSet = null!;
    private Mock<DbSet<VerbType>> _mockVerbTypeDbSet = null!;
    private Mock<DbSet<PermissionScheme>> _mockPermissionSchemeDbSet = null!;
    private Mock<DbSet<AuditLog>> _mockAuditLogDbSet = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _mockDbContext = new Mock<ApplicationDbContext>(options);
        _mockLogger = new Mock<ILogger<ResourceService>>();
        _mockPermissionService = new Mock<IPermissionEvaluationService>();
        
        _mockResourceDbSet = new Mock<DbSet<Data.Models.Resource>>();
        _mockEntityDbSet = new Mock<DbSet<Data.Models.Entity>>();
        _mockUriAccessDbSet = new Mock<DbSet<UriAccess>>();
        _mockVerbTypeDbSet = new Mock<DbSet<VerbType>>();
        _mockPermissionSchemeDbSet = new Mock<DbSet<PermissionScheme>>();
        _mockAuditLogDbSet = new Mock<DbSet<AuditLog>>();
        
        _mockDbContext.Setup(x => x.Resources).Returns(_mockResourceDbSet.Object);
        _mockDbContext.Setup(x => x.Entities).Returns(_mockEntityDbSet.Object);
        _mockDbContext.Setup(x => x.UriAccesses).Returns(_mockUriAccessDbSet.Object);
        _mockDbContext.Setup(x => x.VerbTypes).Returns(_mockVerbTypeDbSet.Object);
        _mockDbContext.Setup(x => x.PermissionSchemes).Returns(_mockPermissionSchemeDbSet.Object);
        _mockDbContext.Setup(x => x.AuditLogs).Returns(_mockAuditLogDbSet.Object);
        
        _resourceService = new ResourceService(
            _mockDbContext.Object,
            _mockLogger.Object,
            _mockPermissionService.Object);
    }

    #region GetAllResourcesAsync Tests

    [TestMethod]
    public async Task ResourceService_GetAllResourcesAsync_ReturnsAllResources()
    {
        // Arrange
        var dataResources = new List<Data.Models.Resource>
        {
            new() { Id = 1, Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new() { Id = 2, Uri = "/api/groups", Description = "Group API", ResourceType = "API" }
        };

        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.GetAllResourcesAsync();

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task ResourceService_GetAllResourcesAsync_ReturnsEmptyCollectionWhenNoResources()
    {
        // Arrange
        var dataResources = new List<Data.Models.Resource>();
        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.GetAllResourcesAsync();

        // Assert
        Assert.AreEqual(0, result.Count());
    }

    #endregion

    #region GetResourceByIdAsync Tests

    [TestMethod]
    public async Task ResourceService_GetResourceByIdAsync_ReturnsResourceWhenExists()
    {
        // Arrange
        var resourceId = 1;
        var dataResource = new Data.Models.Resource 
        { 
            Id = resourceId, 
            Uri = "/api/users", 
            Description = "User API",
            ResourceType = "API"
        };

        var dataResources = new List<Data.Models.Resource> { dataResource };
        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.GetResourceByIdAsync(resourceId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(resourceId, result.Id);
        Assert.AreEqual("/api/users", result.Uri);
        Assert.AreEqual("User API", result.Description);
    }

    [TestMethod]
    public async Task ResourceService_GetResourceByIdAsync_ReturnsNullWhenResourceNotExists()
    {
        // Arrange
        var resourceId = 999;
        var dataResources = new List<Data.Models.Resource>();
        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.GetResourceByIdAsync(resourceId);

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region GetResourceByUriAsync Tests

    [TestMethod]
    public async Task ResourceService_GetResourceByUriAsync_ReturnsResourceWhenExists()
    {
        // Arrange
        var uri = "/api/users";
        var dataResource = new Data.Models.Resource 
        { 
            Id = 1, 
            Uri = uri, 
            Description = "User API",
            ResourceType = "API"
        };

        var dataResources = new List<Data.Models.Resource> { dataResource };
        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.GetResourceByUriAsync(uri);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(uri, result.Uri);
    }

    #endregion

    #region CreateResourceAsync Tests

    [TestMethod]
    public async Task ResourceService_CreateResourceAsync_CreatesNewResource()
    {
        // Arrange
        var uri = "/api/users";
        var description = "User API";
        var resourceType = "API";
        var createdBy = "TestAdmin";
        var resourceId = 1;

        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        _mockResourceDbSet.Setup(x => x.Add(It.IsAny<Data.Models.Resource>()))
            .Callback<Data.Models.Resource>(r => r.Id = resourceId);

        // Act
        var result = await _resourceService.CreateResourceAsync(uri, description, resourceType, createdBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(resourceId, result.Id);
        Assert.AreEqual(uri, result.Uri);
        Assert.AreEqual(description, result.Description);
        Assert.AreEqual(resourceType, result.ResourceType);
        _mockResourceDbSet.Verify(x => x.Add(It.Is<Data.Models.Resource>(r => 
            r.Uri == uri && 
            r.Description == description && 
            r.ResourceType == resourceType)), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task ResourceService_CreateResourceAsync_LogsResourceCreation()
    {
        // Arrange
        var uri = "/api/users";
        var description = "User API";
        var resourceType = "API";
        var createdBy = "TestAdmin";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _resourceService.CreateResourceAsync(uri, description, resourceType, createdBy);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created resource")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region UpdateResourceAsync Tests

    [TestMethod]
    public async Task ResourceService_UpdateResourceAsync_UpdatesExistingResource()
    {
        // Arrange
        var resourceId = 1;
        var uri = "/api/users/v2";
        var description = "Updated User API";
        var resourceType = "API";
        var updatedBy = "TestAdmin";
        var dataResource = new Data.Models.Resource 
        { 
            Id = resourceId, 
            Uri = "/api/users", 
            Description = "User API",
            ResourceType = "API"
        };

        _mockResourceDbSet.Setup(x => x.FindAsync(resourceId))
            .ReturnsAsync(dataResource);
        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        // Act
        var result = await _resourceService.UpdateResourceAsync(resourceId, uri, description, resourceType, updatedBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(resourceId, result.Id);
        Assert.AreEqual(uri, result.Uri);
        Assert.AreEqual(description, result.Description);
        Assert.AreEqual(uri, dataResource.Uri);
        Assert.AreEqual(description, dataResource.Description);
        _mockResourceDbSet.Verify(x => x.Update(dataResource), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task ResourceService_UpdateResourceAsync_ThrowsExceptionWhenResourceNotFound()
    {
        // Arrange
        var resourceId = 999;
        var uri = "/api/users/v2";
        var description = "Updated User API";
        var resourceType = "API";
        var updatedBy = "TestAdmin";

        _mockResourceDbSet.Setup(x => x.FindAsync(resourceId))
            .ReturnsAsync((Data.Models.Resource?)null);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _resourceService.UpdateResourceAsync(resourceId, uri, description, resourceType, updatedBy));
    }

    #endregion

    #region DeleteResourceAsync Tests

    [TestMethod]
    public async Task ResourceService_DeleteResourceAsync_DeletesResource()
    {
        // Arrange
        var resourceId = 1;
        var deletedBy = "TestAdmin";
        var dataResource = new Data.Models.Resource 
        { 
            Id = resourceId, 
            Uri = "/api/users", 
            Description = "User API",
            ResourceType = "API"
        };

        var dataResources = new List<Data.Models.Resource> { dataResource };
        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        var uriAccesses = new List<UriAccess>();
        var mockUriAccessQueryable = uriAccesses.AsQueryable();
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Provider).Returns(mockUriAccessQueryable.Provider);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Expression).Returns(mockUriAccessQueryable.Expression);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.ElementType).Returns(mockUriAccessQueryable.ElementType);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.GetEnumerator()).Returns(mockUriAccessQueryable.GetEnumerator());

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        await _resourceService.DeleteResourceAsync(resourceId, deletedBy);

        // Assert
        _mockResourceDbSet.Verify(x => x.Remove(dataResource), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task ResourceService_DeleteResourceAsync_ThrowsExceptionWhenResourceNotFound()
    {
        // Arrange
        var resourceId = 999;
        var deletedBy = "TestAdmin";
        var dataResources = new List<Data.Models.Resource>();
        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _resourceService.DeleteResourceAsync(resourceId, deletedBy));
    }

    #endregion

    #region DoesUriMatchResourceAsync Tests

    [TestMethod]
    public async Task ResourceService_DoesUriMatchResourceAsync_ReturnsTrueForExactMatch()
    {
        // Arrange
        var requestUri = "/api/users";
        var resourceId = 1;
        var dataResource = new Data.Models.Resource 
        { 
            Id = resourceId, 
            Uri = "/api/users", 
            Description = "User API",
            ResourceType = "API"
        };

        _mockResourceDbSet.Setup(x => x.FindAsync(resourceId))
            .ReturnsAsync(dataResource);

        // Act
        var result = await _resourceService.DoesUriMatchResourceAsync(requestUri, resourceId);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ResourceService_DoesUriMatchResourceAsync_ReturnsTrueForWildcardMatch()
    {
        // Arrange
        var requestUri = "/api/users/123";
        var resourceId = 1;
        var dataResource = new Data.Models.Resource 
        { 
            Id = resourceId, 
            Uri = "/api/users/*", 
            Description = "User API with wildcard",
            ResourceType = "API"
        };

        _mockResourceDbSet.Setup(x => x.FindAsync(resourceId))
            .ReturnsAsync(dataResource);

        // Act
        var result = await _resourceService.DoesUriMatchResourceAsync(requestUri, resourceId);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ResourceService_DoesUriMatchResourceAsync_ReturnsFalseForNoMatch()
    {
        // Arrange
        var requestUri = "/api/groups";
        var resourceId = 1;
        var dataResource = new Data.Models.Resource 
        { 
            Id = resourceId, 
            Uri = "/api/users", 
            Description = "User API",
            ResourceType = "API"
        };

        _mockResourceDbSet.Setup(x => x.FindAsync(resourceId))
            .ReturnsAsync(dataResource);

        // Act
        var result = await _resourceService.DoesUriMatchResourceAsync(requestUri, resourceId);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ResourceService_DoesUriMatchResourceAsync_ReturnsFalseWhenResourceNotFound()
    {
        // Arrange
        var requestUri = "/api/users";
        var resourceId = 999;

        _mockResourceDbSet.Setup(x => x.FindAsync(resourceId))
            .ReturnsAsync((Data.Models.Resource?)null);

        // Act
        var result = await _resourceService.DoesUriMatchResourceAsync(requestUri, resourceId);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region FindBestMatchingResourceAsync Tests

    [TestMethod]
    public async Task ResourceService_FindBestMatchingResourceAsync_ReturnsExactMatchFirst()
    {
        // Arrange
        var requestUri = "/api/users";
        var dataResources = new List<Data.Models.Resource>
        {
            new() { Id = 1, Uri = "/api/users/*", Description = "User API wildcard", ResourceType = "API" },
            new() { Id = 2, Uri = "/api/users", Description = "User API exact", ResourceType = "API" }
        };

        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.FindBestMatchingResourceAsync(requestUri);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Id); // Exact match should be returned
        Assert.AreEqual("/api/users", result.Uri);
    }

    [TestMethod]
    public async Task ResourceService_FindBestMatchingResourceAsync_ReturnsWildcardMatchWhenNoExact()
    {
        // Arrange
        var requestUri = "/api/users/123";
        var dataResources = new List<Data.Models.Resource>
        {
            new() { Id = 1, Uri = "/api/users/*", Description = "User API wildcard", ResourceType = "API" },
            new() { Id = 2, Uri = "/api/groups", Description = "Group API", ResourceType = "API" }
        };

        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.FindBestMatchingResourceAsync(requestUri);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Id);
        Assert.AreEqual("/api/users/*", result.Uri);
    }

    [TestMethod]
    public async Task ResourceService_FindBestMatchingResourceAsync_ReturnsNullWhenNoMatch()
    {
        // Arrange
        var requestUri = "/api/unknown";
        var dataResources = new List<Data.Models.Resource>
        {
            new() { Id = 1, Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new() { Id = 2, Uri = "/api/groups", Description = "Group API", ResourceType = "API" }
        };

        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.FindBestMatchingResourceAsync(requestUri);

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region IsUriProtectedAsync Tests

    [TestMethod]
    public async Task ResourceService_IsUriProtectedAsync_ReturnsTrueForProtectedResource()
    {
        // Arrange
        var requestUri = "/api/users";
        var dataResources = new List<Data.Models.Resource>
        {
            new() { Id = 1, Uri = "/api/users", Description = "User API", ResourceType = "API" }
        };

        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.IsUriProtectedAsync(requestUri);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ResourceService_IsUriProtectedAsync_ReturnsFalseForUnprotectedResource()
    {
        // Arrange
        var requestUri = "/api/public";
        var dataResources = new List<Data.Models.Resource>
        {
            new() { Id = 1, Uri = "/api/users", Description = "User API", ResourceType = "API" }
        };

        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.IsUriProtectedAsync(requestUri);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region ValidateUriPatternAsync Tests

    [TestMethod]
    public async Task ResourceService_ValidateUriPatternAsync_ReturnsTrueForValidPattern()
    {
        // Arrange
        var pattern = "/api/users/*";

        // Act
        var result = await _resourceService.ValidateUriPatternAsync(pattern);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ResourceService_ValidateUriPatternAsync_ReturnsFalseForInvalidPattern()
    {
        // Arrange
        var pattern = "/api/[invalid";

        // Act
        var result = await _resourceService.ValidateUriPatternAsync(pattern);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ResourceService_ValidateUriPatternAsync_ReturnsTrueForParameterizedPattern()
    {
        // Arrange
        var pattern = "/api/users/{id}/profile";

        // Act
        var result = await _resourceService.ValidateUriPatternAsync(pattern);

        // Assert
        Assert.IsTrue(result);
    }

    #endregion

    #region GetResourcesByTypeAsync Tests

    [TestMethod]
    public async Task ResourceService_GetResourcesByTypeAsync_ReturnsMatchingResources()
    {
        // Arrange
        var resourceType = "API";
        var dataResources = new List<Data.Models.Resource>
        {
            new() { Id = 1, Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new() { Id = 2, Uri = "/web/home", Description = "Home Page", ResourceType = "Web" },
            new() { Id = 3, Uri = "/api/groups", Description = "Group API", ResourceType = "API" }
        };

        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.GetResourcesByTypeAsync(resourceType);

        // Assert
        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.All(r => r.ResourceType == resourceType));
    }

    #endregion

    #region SearchResourcesAsync Tests

    [TestMethod]
    public async Task ResourceService_SearchResourcesAsync_ReturnsMatchingResources()
    {
        // Arrange
        var searchTerm = "user";
        var dataResources = new List<Data.Models.Resource>
        {
            new() { Id = 1, Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new() { Id = 2, Uri = "/api/groups", Description = "Group API", ResourceType = "API" },
            new() { Id = 3, Uri = "/api/user-management", Description = "User Management", ResourceType = "API" }
        };

        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.SearchResourcesAsync(searchTerm);

        // Assert
        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.Any(r => r.Uri.Contains("users", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(result.Any(r => r.Description.Contains("User", StringComparison.OrdinalIgnoreCase)));
    }

    #endregion

    #region GetTotalResourceCountAsync Tests

    [TestMethod]
    public async Task ResourceService_GetTotalResourceCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var dataResources = new List<Data.Models.Resource>
        {
            new() { Id = 1, Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new() { Id = 2, Uri = "/api/groups", Description = "Group API", ResourceType = "API" },
            new() { Id = 3, Uri = "/web/home", Description = "Home Page", ResourceType = "Web" }
        };

        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.GetTotalResourceCountAsync();

        // Assert
        Assert.AreEqual(3, result);
    }

    #endregion

    #region GetResourceCountByTypeAsync Tests

    [TestMethod]
    public async Task ResourceService_GetResourceCountByTypeAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var dataResources = new List<Data.Models.Resource>
        {
            new() { Id = 1, Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new() { Id = 2, Uri = "/api/groups", Description = "Group API", ResourceType = "API" },
            new() { Id = 3, Uri = "/web/home", Description = "Home Page", ResourceType = "Web" },
            new() { Id = 4, Uri = "/web/about", Description = "About Page", ResourceType = "Web" }
        };

        var mockQueryable = dataResources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _resourceService.GetResourceCountByTypeAsync();

        // Assert
        Assert.AreEqual(2, result["API"]);
        Assert.AreEqual(2, result["Web"]);
    }

    #endregion

    #region CreateResourcesBulkAsync Tests

    [TestMethod]
    public async Task ResourceService_CreateResourcesBulkAsync_CreatesMultipleResources()
    {
        // Arrange
        var resources = new List<(string Uri, string Description, string Type)>
        {
            ("/api/users", "User API", "API"),
            ("/api/groups", "Group API", "API"),
            ("/web/home", "Home Page", "Web")
        };
        var createdBy = "TestAdmin";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _resourceService.CreateResourcesBulkAsync(resources, createdBy);

        // Assert
        Assert.AreEqual(3, result.Count());
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created 3 resources in bulk")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region IsResourceAccessibleAsync Tests

    [TestMethod]
    public async Task ResourceService_IsResourceAccessibleAsync_CallsPermissionService()
    {
        // Arrange
        var resourceId = 1;
        var entityId = 1;
        var verb = Domain.HttpVerb.GET;

        _mockPermissionService.Setup(x => x.HasPermissionAsync(entityId, It.IsAny<string>(), verb))
            .ReturnsAsync(true);

        // Act
        var result = await _resourceService.IsResourceAccessibleAsync(resourceId, entityId, verb);

        // Assert
        Assert.IsTrue(result);
        _mockPermissionService.Verify(x => x.HasPermissionAsync(entityId, It.IsAny<string>(), verb), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task ResourceService_CreateResourceAsync_HandlesExceptionGracefully()
    {
        // Arrange
        var uri = "/api/users";
        var description = "User API";
        var resourceType = "API";
        var createdBy = "TestAdmin";

        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _resourceService.CreateResourceAsync(uri, description, resourceType, createdBy));
    }

    #endregion
}