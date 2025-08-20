using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using ACS.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class PermissionEvaluationServiceTests
{
    private Mock<ApplicationDbContext> _mockDbContext = null!;
    private Mock<ILogger<PermissionEvaluationService>> _mockLogger = null!;
    private Mock<IMemoryCache> _mockMemoryCache = null!;
    private PermissionEvaluationService _permissionService = null!;
    private Mock<DbSet<Data.Models.Entity>> _mockEntityDbSet = null!;
    private Mock<DbSet<PermissionScheme>> _mockPermissionSchemeDbSet = null!;
    private Mock<DbSet<Data.Models.Resource>> _mockResourceDbSet = null!;
    private Mock<DbSet<UriAccess>> _mockUriAccessDbSet = null!;
    private Mock<DbSet<VerbType>> _mockVerbTypeDbSet = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _mockDbContext = new Mock<ApplicationDbContext>(options);
        _mockLogger = new Mock<ILogger<PermissionEvaluationService>>();
        _mockMemoryCache = new Mock<IMemoryCache>();
        
        _mockEntityDbSet = new Mock<DbSet<Data.Models.Entity>>();
        _mockPermissionSchemeDbSet = new Mock<DbSet<PermissionScheme>>();
        _mockResourceDbSet = new Mock<DbSet<Data.Models.Resource>>();
        _mockUriAccessDbSet = new Mock<DbSet<UriAccess>>();
        _mockVerbTypeDbSet = new Mock<DbSet<VerbType>>();
        
        _mockDbContext.Setup(x => x.Entities).Returns(_mockEntityDbSet.Object);
        _mockDbContext.Setup(x => x.PermissionSchemes).Returns(_mockPermissionSchemeDbSet.Object);
        _mockDbContext.Setup(x => x.Resources).Returns(_mockResourceDbSet.Object);
        _mockDbContext.Setup(x => x.UriAccesses).Returns(_mockUriAccessDbSet.Object);
        _mockDbContext.Setup(x => x.VerbTypes).Returns(_mockVerbTypeDbSet.Object);
        
        _permissionService = new PermissionEvaluationService(
            _mockDbContext.Object,
            _mockLogger.Object,
            _mockMemoryCache.Object);
    }

    #region HasPermissionAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_HasPermissionAsync_ReturnsTrueForGrantedPermission()
    {
        // Arrange
        var entityId = 1;
        var uri = "/api/users";
        var httpVerb = "GET";
        
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1 };
        var permissionScheme = new PermissionScheme { EntityId = entityId, UriAccessId = 1, Grant = true };

        var resources = new List<Data.Models.Resource> { resource };
        var mockResourceQueryable = resources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockResourceQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockResourceQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockResourceQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockResourceQueryable.GetEnumerator());

        var verbTypes = new List<VerbType> { verbType };
        var mockVerbTypeQueryable = verbTypes.AsQueryable();
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Provider).Returns(mockVerbTypeQueryable.Provider);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Expression).Returns(mockVerbTypeQueryable.Expression);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.ElementType).Returns(mockVerbTypeQueryable.ElementType);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.GetEnumerator()).Returns(mockVerbTypeQueryable.GetEnumerator());

        var uriAccesses = new List<UriAccess> { uriAccess };
        var mockUriAccessQueryable = uriAccesses.AsQueryable();
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Provider).Returns(mockUriAccessQueryable.Provider);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Expression).Returns(mockUriAccessQueryable.Expression);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.ElementType).Returns(mockUriAccessQueryable.ElementType);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.GetEnumerator()).Returns(mockUriAccessQueryable.GetEnumerator());

        var permissionSchemes = new List<PermissionScheme> { permissionScheme };
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.HasPermissionAsync(entityId, uri, httpVerb);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task PermissionEvaluationService_HasPermissionAsync_ReturnsFalseForDeniedPermission()
    {
        // Arrange
        var entityId = 1;
        var uri = "/api/users";
        var httpVerb = "DELETE";
        
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "DELETE" };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1 };
        var permissionScheme = new PermissionScheme { EntityId = entityId, UriAccessId = 1, Grant = false };

        var resources = new List<Data.Models.Resource> { resource };
        var mockResourceQueryable = resources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockResourceQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockResourceQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockResourceQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockResourceQueryable.GetEnumerator());

        var verbTypes = new List<VerbType> { verbType };
        var mockVerbTypeQueryable = verbTypes.AsQueryable();
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Provider).Returns(mockVerbTypeQueryable.Provider);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Expression).Returns(mockVerbTypeQueryable.Expression);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.ElementType).Returns(mockVerbTypeQueryable.ElementType);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.GetEnumerator()).Returns(mockVerbTypeQueryable.GetEnumerator());

        var uriAccesses = new List<UriAccess> { uriAccess };
        var mockUriAccessQueryable = uriAccesses.AsQueryable();
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Provider).Returns(mockUriAccessQueryable.Provider);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Expression).Returns(mockUriAccessQueryable.Expression);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.ElementType).Returns(mockUriAccessQueryable.ElementType);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.GetEnumerator()).Returns(mockUriAccessQueryable.GetEnumerator());

        var permissionSchemes = new List<PermissionScheme> { permissionScheme };
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.HasPermissionAsync(entityId, uri, httpVerb);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task PermissionEvaluationService_HasPermissionAsync_ReturnsFalseWhenNoPermissionExists()
    {
        // Arrange
        var entityId = 1;
        var uri = "/api/protected";
        var httpVerb = "GET";

        var resources = new List<Data.Models.Resource>();
        var mockResourceQueryable = resources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockResourceQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockResourceQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockResourceQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockResourceQueryable.GetEnumerator());

        var verbTypes = new List<VerbType>();
        var mockVerbTypeQueryable = verbTypes.AsQueryable();
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Provider).Returns(mockVerbTypeQueryable.Provider);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Expression).Returns(mockVerbTypeQueryable.Expression);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.ElementType).Returns(mockVerbTypeQueryable.ElementType);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.GetEnumerator()).Returns(mockVerbTypeQueryable.GetEnumerator());

        var uriAccesses = new List<UriAccess>();
        var mockUriAccessQueryable = uriAccesses.AsQueryable();
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Provider).Returns(mockUriAccessQueryable.Provider);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Expression).Returns(mockUriAccessQueryable.Expression);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.ElementType).Returns(mockUriAccessQueryable.ElementType);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.GetEnumerator()).Returns(mockUriAccessQueryable.GetEnumerator());

        var permissionSchemes = new List<PermissionScheme>();
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.HasPermissionAsync(entityId, uri, httpVerb);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region HasPermissionAsync with HttpVerb Tests

    [TestMethod]
    public async Task PermissionEvaluationService_HasPermissionAsync_WithHttpVerb_ReturnsTrueForGrantedPermission()
    {
        // Arrange
        var entityId = 1;
        var uri = "/api/users";
        var httpVerb = Domain.HttpVerb.GET;
        
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1 };
        var permissionScheme = new PermissionScheme { EntityId = entityId, UriAccessId = 1, Grant = true };

        var resources = new List<Data.Models.Resource> { resource };
        var mockResourceQueryable = resources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockResourceQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockResourceQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockResourceQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockResourceQueryable.GetEnumerator());

        var verbTypes = new List<VerbType> { verbType };
        var mockVerbTypeQueryable = verbTypes.AsQueryable();
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Provider).Returns(mockVerbTypeQueryable.Provider);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Expression).Returns(mockVerbTypeQueryable.Expression);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.ElementType).Returns(mockVerbTypeQueryable.ElementType);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.GetEnumerator()).Returns(mockVerbTypeQueryable.GetEnumerator());

        var uriAccesses = new List<UriAccess> { uriAccess };
        var mockUriAccessQueryable = uriAccesses.AsQueryable();
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Provider).Returns(mockUriAccessQueryable.Provider);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Expression).Returns(mockUriAccessQueryable.Expression);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.ElementType).Returns(mockUriAccessQueryable.ElementType);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.GetEnumerator()).Returns(mockUriAccessQueryable.GetEnumerator());

        var permissionSchemes = new List<PermissionScheme> { permissionScheme };
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.HasPermissionAsync(entityId, uri, httpVerb);

        // Assert
        Assert.IsTrue(result);
    }

    #endregion

    #region GetEffectivePermissionsAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_GetEffectivePermissionsAsync_ReturnsDirectPermissions()
    {
        // Arrange
        var entityId = 1;
        
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, Resource = resource, VerbType = verbType };
        var permissionScheme = new PermissionScheme { EntityId = entityId, UriAccessId = 1, Grant = true, UriAccess = uriAccess };

        var permissionSchemes = new List<PermissionScheme> { permissionScheme };
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.GetEffectivePermissionsAsync(entityId);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("/api/users", result.First().Uri);
        Assert.AreEqual(Domain.HttpVerb.GET, result.First().HttpVerb);
        Assert.IsTrue(result.First().Grant);
    }

    [TestMethod]
    public async Task PermissionEvaluationService_GetEffectivePermissionsAsync_ReturnsEmptyListWhenNoPermissions()
    {
        // Arrange
        var entityId = 999;

        var permissionSchemes = new List<PermissionScheme>();
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.GetEffectivePermissionsAsync(entityId);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    #endregion

    #region CanUserAccessResourceAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_CanUserAccessResourceAsync_ReturnsTrueForUserWithPermission()
    {
        // Arrange
        var userId = 1;
        var uri = "/api/users";
        var httpVerb = Domain.HttpVerb.GET;

        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1 };
        var permissionScheme = new PermissionScheme { EntityId = userId, UriAccessId = 1, Grant = true };

        var resources = new List<Data.Models.Resource> { resource };
        var mockResourceQueryable = resources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockResourceQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockResourceQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockResourceQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockResourceQueryable.GetEnumerator());

        var verbTypes = new List<VerbType> { verbType };
        var mockVerbTypeQueryable = verbTypes.AsQueryable();
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Provider).Returns(mockVerbTypeQueryable.Provider);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Expression).Returns(mockVerbTypeQueryable.Expression);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.ElementType).Returns(mockVerbTypeQueryable.ElementType);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.GetEnumerator()).Returns(mockVerbTypeQueryable.GetEnumerator());

        var uriAccesses = new List<UriAccess> { uriAccess };
        var mockUriAccessQueryable = uriAccesses.AsQueryable();
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Provider).Returns(mockUriAccessQueryable.Provider);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Expression).Returns(mockUriAccessQueryable.Expression);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.ElementType).Returns(mockUriAccessQueryable.ElementType);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.GetEnumerator()).Returns(mockUriAccessQueryable.GetEnumerator());

        var permissionSchemes = new List<PermissionScheme> { permissionScheme };
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.CanUserAccessResourceAsync(userId, uri, httpVerb);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task PermissionEvaluationService_CanUserAccessResourceAsync_ReturnsFalseForUserWithoutPermission()
    {
        // Arrange
        var userId = 1;
        var uri = "/api/admin";
        var httpVerb = Domain.HttpVerb.GET;

        var resources = new List<Data.Models.Resource>();
        var mockResourceQueryable = resources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockResourceQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockResourceQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockResourceQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockResourceQueryable.GetEnumerator());

        var verbTypes = new List<VerbType>();
        var mockVerbTypeQueryable = verbTypes.AsQueryable();
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Provider).Returns(mockVerbTypeQueryable.Provider);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Expression).Returns(mockVerbTypeQueryable.Expression);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.ElementType).Returns(mockVerbTypeQueryable.ElementType);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.GetEnumerator()).Returns(mockVerbTypeQueryable.GetEnumerator());

        var uriAccesses = new List<UriAccess>();
        var mockUriAccessQueryable = uriAccesses.AsQueryable();
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Provider).Returns(mockUriAccessQueryable.Provider);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Expression).Returns(mockUriAccessQueryable.Expression);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.ElementType).Returns(mockUriAccessQueryable.ElementType);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.GetEnumerator()).Returns(mockUriAccessQueryable.GetEnumerator());

        var permissionSchemes = new List<PermissionScheme>();
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.CanUserAccessResourceAsync(userId, uri, httpVerb);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region EvaluatePermissionAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_EvaluatePermissionAsync_ReturnsDetailedResult()
    {
        // Arrange
        var entityId = 1;
        var uri = "/api/users";
        var httpVerb = Domain.HttpVerb.GET;
        
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1 };
        var permissionScheme = new PermissionScheme { EntityId = entityId, UriAccessId = 1, Grant = true };

        var resources = new List<Data.Models.Resource> { resource };
        var mockResourceQueryable = resources.AsQueryable();
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Provider).Returns(mockResourceQueryable.Provider);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.Expression).Returns(mockResourceQueryable.Expression);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.ElementType).Returns(mockResourceQueryable.ElementType);
        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>().Setup(m => m.GetEnumerator()).Returns(mockResourceQueryable.GetEnumerator());

        var verbTypes = new List<VerbType> { verbType };
        var mockVerbTypeQueryable = verbTypes.AsQueryable();
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Provider).Returns(mockVerbTypeQueryable.Provider);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.Expression).Returns(mockVerbTypeQueryable.Expression);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.ElementType).Returns(mockVerbTypeQueryable.ElementType);
        _mockVerbTypeDbSet.As<IQueryable<VerbType>>().Setup(m => m.GetEnumerator()).Returns(mockVerbTypeQueryable.GetEnumerator());

        var uriAccesses = new List<UriAccess> { uriAccess };
        var mockUriAccessQueryable = uriAccesses.AsQueryable();
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Provider).Returns(mockUriAccessQueryable.Provider);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.Expression).Returns(mockUriAccessQueryable.Expression);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.ElementType).Returns(mockUriAccessQueryable.ElementType);
        _mockUriAccessDbSet.As<IQueryable<UriAccess>>().Setup(m => m.GetEnumerator()).Returns(mockUriAccessQueryable.GetEnumerator());

        var permissionSchemes = new List<PermissionScheme> { permissionScheme };
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.EvaluatePermissionAsync(entityId, uri, httpVerb);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsAllowed);
        Assert.IsNotNull(result.Reason);
        Assert.IsTrue(result.EvaluationTime.TotalMilliseconds >= 0);
    }

    #endregion

    #region InvalidatePermissionCacheAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_InvalidatePermissionCacheAsync_CallsCacheRemove()
    {
        // Arrange
        var entityId = 1;
        var cacheKey = $"permissions_{entityId}";

        _mockMemoryCache.Setup(x => x.Remove(cacheKey))
            .Verifiable();

        // Act
        await _permissionService.InvalidatePermissionCacheAsync(entityId);

        // Assert
        _mockMemoryCache.Verify(x => x.Remove(cacheKey), Times.Once);
    }

    #endregion

    #region GetCacheStatisticsAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_GetCacheStatisticsAsync_ReturnsStatistics()
    {
        // Arrange & Act
        var result = await _permissionService.GetCacheStatisticsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.TotalRequests >= 0);
        Assert.IsTrue(result.HitRate >= 0 && result.HitRate <= 1);
    }

    #endregion

    #region GetEntityPermissionsAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_GetEntityPermissionsAsync_ReturnsDirectPermissionsOnly()
    {
        // Arrange
        var entityId = 1;
        var includeInherited = false;
        
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, Resource = resource, VerbType = verbType };
        var permissionScheme = new PermissionScheme { EntityId = entityId, UriAccessId = 1, Grant = true, UriAccess = uriAccess };

        var permissionSchemes = new List<PermissionScheme> { permissionScheme };
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.GetEntityPermissionsAsync(entityId, includeInherited);

        // Assert
        Assert.AreEqual(1, result.Count());
        Assert.AreEqual("/api/users", result.First().Uri);
        Assert.AreEqual(Domain.HttpVerb.GET, result.First().HttpVerb);
        Assert.IsTrue(result.First().Grant);
    }

    #endregion

    #region GetDirectPermissionsAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_GetDirectPermissionsAsync_ReturnsOnlyDirectPermissions()
    {
        // Arrange
        var entityId = 1;
        
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, Resource = resource, VerbType = verbType };
        var permissionScheme = new PermissionScheme { EntityId = entityId, UriAccessId = 1, Grant = true, UriAccess = uriAccess };

        var permissionSchemes = new List<PermissionScheme> { permissionScheme };
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        // Act
        var result = await _permissionService.GetDirectPermissionsAsync(entityId);

        // Assert
        Assert.AreEqual(1, result.Count());
        Assert.AreEqual("/api/users", result.First().Uri);
        Assert.AreEqual(Domain.HttpVerb.GET, result.First().HttpVerb);
        Assert.IsTrue(result.First().Grant);
    }

    #endregion

    #region HasCachedPermissionAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_HasCachedPermissionAsync_ReturnsFalseWhenNotCached()
    {
        // Arrange
        var entityId = 1;
        var uri = "/api/users";
        var httpVerb = Domain.HttpVerb.GET;
        var cacheKey = $"permission_{entityId}_{uri}_{httpVerb}";

        object? cachedValue;
        _mockMemoryCache.Setup(x => x.TryGetValue(cacheKey, out cachedValue))
            .Returns(false);

        // Act
        var result = await _permissionService.HasCachedPermissionAsync(entityId, uri, httpVerb);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task PermissionEvaluationService_HasCachedPermissionAsync_ReturnsTrueWhenCached()
    {
        // Arrange
        var entityId = 1;
        var uri = "/api/users";
        var httpVerb = Domain.HttpVerb.GET;
        var cacheKey = $"permission_{entityId}_{uri}_{httpVerb}";

        object? cachedValue = true;
        _mockMemoryCache.Setup(x => x.TryGetValue(cacheKey, out cachedValue))
            .Returns(true);

        // Act
        var result = await _permissionService.HasCachedPermissionAsync(entityId, uri, httpVerb);

        // Assert
        Assert.IsTrue(result);
    }

    #endregion

    #region PreloadPermissionsAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_PreloadPermissionsAsync_LoadsPermissionsIntoCache()
    {
        // Arrange
        var entityId = 1;
        
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, Resource = resource, VerbType = verbType };
        var permissionScheme = new PermissionScheme { EntityId = entityId, UriAccessId = 1, Grant = true, UriAccess = uriAccess };

        var permissionSchemes = new List<PermissionScheme> { permissionScheme };
        var mockPermissionSchemeQueryable = permissionSchemes.AsQueryable();
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Provider).Returns(mockPermissionSchemeQueryable.Provider);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.Expression).Returns(mockPermissionSchemeQueryable.Expression);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.ElementType).Returns(mockPermissionSchemeQueryable.ElementType);
        _mockPermissionSchemeDbSet.As<IQueryable<PermissionScheme>>().Setup(m => m.GetEnumerator()).Returns(mockPermissionSchemeQueryable.GetEnumerator());

        var mockCacheEntry = new Mock<ICacheEntry>();
        _mockMemoryCache.Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(mockCacheEntry.Object);

        // Act
        await _permissionService.PreloadPermissionsAsync(entityId);

        // Assert
        _mockMemoryCache.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.AtLeastOnce);
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task PermissionEvaluationService_HasPermissionAsync_HandlesExceptionGracefully()
    {
        // Arrange
        var entityId = 1;
        var uri = "/api/users";
        var httpVerb = "GET";

        _mockResourceDbSet.As<IQueryable<Data.Models.Resource>>()
            .Setup(m => m.Provider)
            .Throws(new InvalidOperationException("Database error"));

        // Act
        var result = await _permissionService.HasPermissionAsync(entityId, uri, httpVerb);

        // Assert
        Assert.IsFalse(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error evaluating permission")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}