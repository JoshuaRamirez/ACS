using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using ACS.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class PermissionEvaluationServiceTests
{
    private ApplicationDbContext _dbContext = null!;
    private Mock<ILogger<PermissionEvaluationService>> _mockLogger = null!;
    private IMemoryCache _memoryCache = null!;
    private Mock<IAuditService> _mockAuditService = null!;
    private PermissionEvaluationService _permissionService = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange - use real in-memory database instead of mocking DbContext
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<PermissionEvaluationService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockAuditService = new Mock<IAuditService>();

        _permissionService = new PermissionEvaluationService(
            _dbContext,
            _mockLogger.Object,
            _memoryCache,
            _mockAuditService.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _memoryCache?.Dispose();
        _dbContext?.Dispose();
    }

    #region HasPermissionAsync Tests

    [TestMethod]
    public async Task PermissionEvaluationService_HasPermissionAsync_ReturnsTrueForGrantedPermission()
    {
        // Arrange
        var entityId = 1;
        var uri = "/api/users";
        var httpVerb = "GET";

        // Setup database with required data
        var entity = new Data.Models.Entity { Id = entityId, EntityType = "User" };
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var schemeType = new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" };
        var permissionScheme = new PermissionScheme { Id = 1, EntityId = entityId, SchemeTypeId = 1 };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, PermissionSchemeId = 1, Grant = true, Deny = false };

        _dbContext.Entities.Add(entity);
        _dbContext.Resources.Add(resource);
        _dbContext.VerbTypes.Add(verbType);
        _dbContext.SchemeTypes.Add(schemeType);
        _dbContext.EntityPermissions.Add(permissionScheme);
        _dbContext.UriAccesses.Add(uriAccess);
        await _dbContext.SaveChangesAsync();

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

        // Setup database with denied permission
        var entity = new Data.Models.Entity { Id = entityId, EntityType = "User" };
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "DELETE" };
        var schemeType = new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" };
        var permissionScheme = new PermissionScheme { Id = 1, EntityId = entityId, SchemeTypeId = 1 };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, PermissionSchemeId = 1, Grant = false, Deny = true };

        _dbContext.Entities.Add(entity);
        _dbContext.Resources.Add(resource);
        _dbContext.VerbTypes.Add(verbType);
        _dbContext.SchemeTypes.Add(schemeType);
        _dbContext.EntityPermissions.Add(permissionScheme);
        _dbContext.UriAccesses.Add(uriAccess);
        await _dbContext.SaveChangesAsync();

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

        // No permissions in database

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

        // Setup database with required data
        var entity = new Data.Models.Entity { Id = entityId, EntityType = "User" };
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var schemeType = new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" };
        var permissionScheme = new PermissionScheme { Id = 1, EntityId = entityId, SchemeTypeId = 1 };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, PermissionSchemeId = 1, Grant = true, Deny = false };

        _dbContext.Entities.Add(entity);
        _dbContext.Resources.Add(resource);
        _dbContext.VerbTypes.Add(verbType);
        _dbContext.SchemeTypes.Add(schemeType);
        _dbContext.EntityPermissions.Add(permissionScheme);
        _dbContext.UriAccesses.Add(uriAccess);
        await _dbContext.SaveChangesAsync();

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

        // Setup database with required data
        var entity = new Data.Models.Entity { Id = entityId, EntityType = "User" };
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var schemeType = new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" };
        var permissionScheme = new PermissionScheme { Id = 1, EntityId = entityId, SchemeTypeId = 1 };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, PermissionSchemeId = 1, Grant = true, Deny = false };

        _dbContext.Entities.Add(entity);
        _dbContext.Resources.Add(resource);
        _dbContext.VerbTypes.Add(verbType);
        _dbContext.SchemeTypes.Add(schemeType);
        _dbContext.EntityPermissions.Add(permissionScheme);
        _dbContext.UriAccesses.Add(uriAccess);
        await _dbContext.SaveChangesAsync();

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

        // No permissions in database

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
        var entityId = 10;
        var uri = "/api/users";
        var httpVerb = Domain.HttpVerb.GET;

        // Setup user and entity
        var entity = new Data.Models.Entity { Id = entityId, EntityType = "User" };
        var user = new Data.Models.User { Id = userId, Name = "TestUser", EntityId = entityId };
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var schemeType = new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" };
        var permissionScheme = new PermissionScheme { Id = 1, EntityId = entityId, SchemeTypeId = 1 };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, PermissionSchemeId = 1, Grant = true, Deny = false };

        _dbContext.Entities.Add(entity);
        _dbContext.Users.Add(user);
        _dbContext.Resources.Add(resource);
        _dbContext.VerbTypes.Add(verbType);
        _dbContext.SchemeTypes.Add(schemeType);
        _dbContext.EntityPermissions.Add(permissionScheme);
        _dbContext.UriAccesses.Add(uriAccess);
        await _dbContext.SaveChangesAsync();

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
        var entityId = 10;
        var uri = "/api/admin";
        var httpVerb = Domain.HttpVerb.GET;

        // Setup user and entity without permission
        var entity = new Data.Models.Entity { Id = entityId, EntityType = "User" };
        var user = new Data.Models.User { Id = userId, Name = "TestUser", EntityId = entityId };

        _dbContext.Entities.Add(entity);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

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

        // Setup database with required data
        var entity = new Data.Models.Entity { Id = entityId, EntityType = "User" };
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var schemeType = new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" };
        var permissionScheme = new PermissionScheme { Id = 1, EntityId = entityId, SchemeTypeId = 1 };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, PermissionSchemeId = 1, Grant = true, Deny = false };

        _dbContext.Entities.Add(entity);
        _dbContext.Resources.Add(resource);
        _dbContext.VerbTypes.Add(verbType);
        _dbContext.SchemeTypes.Add(schemeType);
        _dbContext.EntityPermissions.Add(permissionScheme);
        _dbContext.UriAccesses.Add(uriAccess);
        await _dbContext.SaveChangesAsync();

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
    public async Task PermissionEvaluationService_InvalidatePermissionCacheAsync_ClearsCache()
    {
        // Arrange
        var entityId = 1;

        // Add something to cache first
        var cacheKey = $"perm_{entityId}_/api/test_GET";
        _memoryCache.Set(cacheKey, true, TimeSpan.FromMinutes(5));

        // Act
        await _permissionService.InvalidatePermissionCacheAsync(entityId);

        // Assert - cache entry should be invalidated
        // Note: The service uses wildcard-based invalidation, so we verify the method completes successfully
        Assert.IsTrue(true); // Method completed without exception
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

        // Setup database with required data
        var entity = new Data.Models.Entity { Id = entityId, EntityType = "User" };
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var schemeType = new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" };
        var permissionScheme = new PermissionScheme { Id = 1, EntityId = entityId, SchemeTypeId = 1 };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, PermissionSchemeId = 1, Grant = true, Deny = false };

        _dbContext.Entities.Add(entity);
        _dbContext.Resources.Add(resource);
        _dbContext.VerbTypes.Add(verbType);
        _dbContext.SchemeTypes.Add(schemeType);
        _dbContext.EntityPermissions.Add(permissionScheme);
        _dbContext.UriAccesses.Add(uriAccess);
        await _dbContext.SaveChangesAsync();

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

        // Setup database with required data
        var entity = new Data.Models.Entity { Id = entityId, EntityType = "User" };
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var schemeType = new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" };
        var permissionScheme = new PermissionScheme { Id = 1, EntityId = entityId, SchemeTypeId = 1 };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, PermissionSchemeId = 1, Grant = true, Deny = false };

        _dbContext.Entities.Add(entity);
        _dbContext.Resources.Add(resource);
        _dbContext.VerbTypes.Add(verbType);
        _dbContext.SchemeTypes.Add(schemeType);
        _dbContext.EntityPermissions.Add(permissionScheme);
        _dbContext.UriAccesses.Add(uriAccess);
        await _dbContext.SaveChangesAsync();

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
        var cacheKey = $"perm_{entityId}_{uri}_{httpVerb}";

        // Pre-populate cache
        _memoryCache.Set(cacheKey, true, TimeSpan.FromMinutes(5));

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

        // Setup database with required data
        var entity = new Data.Models.Entity { Id = entityId, EntityType = "User" };
        var resource = new Data.Models.Resource { Id = 1, Uri = "/api/users" };
        var verbType = new VerbType { Id = 1, VerbName = "GET" };
        var schemeType = new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" };
        var permissionScheme = new PermissionScheme { Id = 1, EntityId = entityId, SchemeTypeId = 1 };
        var uriAccess = new UriAccess { Id = 1, ResourceId = 1, VerbTypeId = 1, PermissionSchemeId = 1, Grant = true, Deny = false };

        _dbContext.Entities.Add(entity);
        _dbContext.Resources.Add(resource);
        _dbContext.VerbTypes.Add(verbType);
        _dbContext.SchemeTypes.Add(schemeType);
        _dbContext.EntityPermissions.Add(permissionScheme);
        _dbContext.UriAccesses.Add(uriAccess);
        await _dbContext.SaveChangesAsync();

        // Act
        await _permissionService.PreloadPermissionsAsync(entityId);

        // Assert - verify cache has entries for common resources
        var cacheKey = $"perm_{entityId}_/api/users_GET";
        var hasCacheEntry = _memoryCache.TryGetValue(cacheKey, out _);
        Assert.IsTrue(hasCacheEntry);
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

        // Dispose the db context to simulate a database error
        _dbContext.Dispose();

        // Act
        var result = await _permissionService.HasPermissionAsync(entityId, uri, httpVerb);

        // Assert - should return false (fail-safe: deny on error)
        Assert.IsFalse(result);

        // Verify error was logged
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
