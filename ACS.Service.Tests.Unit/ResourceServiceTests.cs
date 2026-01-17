using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using ACS.Service.Requests;
using ACS.Service.Responses;
using ACS.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class ResourceServiceTests
{
    private ApplicationDbContext _dbContext = null!;
    private Mock<ILogger<ResourceService>> _mockLogger = null!;
    private Mock<IPermissionEvaluationService> _mockPermissionService = null!;
    private ResourceService _resourceService = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange - Use real in-memory database for integration-style testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<ResourceService>>();
        _mockPermissionService = new Mock<IPermissionEvaluationService>();

        _resourceService = new ResourceService(
            _dbContext,
            _mockLogger.Object,
            _mockPermissionService.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext?.Dispose();
        _mockLogger?.Reset();
        _mockPermissionService?.Reset();
    }

    #region GetAllResourcesAsync Tests

    [TestMethod]
    public async Task ResourceService_GetAllResourcesAsync_ReturnsAllResources()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/api/groups", Description = "Group API", ResourceType = "API" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.GetAllResourcesAsync();

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task ResourceService_GetAllResourcesAsync_ReturnsEmptyCollectionWhenNoResources()
    {
        // Arrange - no resources added

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
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.GetResourceByIdAsync(dataResource.Id);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(dataResource.Id, result.Id);
        Assert.AreEqual("/api/users", result.Uri);
        Assert.AreEqual("User API", result.Description);
    }

    [TestMethod]
    public async Task ResourceService_GetResourceByIdAsync_ReturnsNullWhenResourceNotExists()
    {
        // Arrange - no resources added

        // Act
        var result = await _resourceService.GetResourceByIdAsync(999);

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
            Uri = uri,
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

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

        // Act
        var result = await _resourceService.CreateResourceAsync(uri, description, resourceType, createdBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(uri, result.Uri);
        Assert.AreEqual(description, result.Description);
        Assert.AreEqual(resourceType, result.ResourceType);

        // Verify in database
        var savedResource = await _dbContext.Resources.FirstOrDefaultAsync(r => r.Uri == uri);
        Assert.IsNotNull(savedResource);
    }

    [TestMethod]
    public async Task ResourceService_CreateResourceAsync_LogsResourceCreation()
    {
        // Arrange
        var uri = "/api/users";
        var description = "User API";
        var resourceType = "API";
        var createdBy = "TestAdmin";

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
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        var newUri = "/api/users/v2";
        var newDescription = "Updated User API";
        var updatedBy = "TestAdmin";

        // Act
        var result = await _resourceService.UpdateResourceAsync(dataResource.Id, newUri, newDescription, "API", updatedBy);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(dataResource.Id, result.Id);
        Assert.AreEqual(newUri, result.Uri);
        Assert.AreEqual(newDescription, result.Description);
    }

    [TestMethod]
    public async Task ResourceService_UpdateResourceAsync_ThrowsExceptionWhenResourceNotFound()
    {
        // Arrange
        var resourceId = 999;

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _resourceService.UpdateResourceAsync(resourceId, "/api/test", "Test", "API", "Admin"));
    }

    #endregion

    #region DeleteResourceAsync Tests

    [TestMethod]
    public async Task ResourceService_DeleteResourceAsync_DeletesResource()
    {
        // Arrange
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        // Act
        await _resourceService.DeleteResourceAsync(dataResource.Id, "TestAdmin");

        // Assert
        var deletedResource = await _dbContext.Resources.FindAsync(dataResource.Id);
        Assert.IsNull(deletedResource);
    }

    [TestMethod]
    public async Task ResourceService_DeleteResourceAsync_ThrowsExceptionWhenResourceNotFound()
    {
        // Arrange - no resources added

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _resourceService.DeleteResourceAsync(999, "TestAdmin"));
    }

    #endregion

    #region DoesUriMatchResourceAsync Tests

    [TestMethod]
    public async Task ResourceService_DoesUriMatchResourceAsync_ReturnsTrueForExactMatch()
    {
        // Arrange
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.DoesUriMatchResourceAsync("/api/users", dataResource.Id);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ResourceService_DoesUriMatchResourceAsync_ReturnsTrueForWildcardMatch()
    {
        // Arrange
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users/*",
            Description = "User API with wildcard",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.DoesUriMatchResourceAsync("/api/users/123", dataResource.Id);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ResourceService_DoesUriMatchResourceAsync_ReturnsFalseForNoMatch()
    {
        // Arrange
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.DoesUriMatchResourceAsync("/api/groups", dataResource.Id);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ResourceService_DoesUriMatchResourceAsync_ReturnsFalseWhenResourceNotFound()
    {
        // Arrange - no resources added

        // Act
        var result = await _resourceService.DoesUriMatchResourceAsync("/api/users", 999);

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region FindBestMatchingResourceAsync Tests

    [TestMethod]
    public async Task ResourceService_FindBestMatchingResourceAsync_ReturnsExactMatchFirst()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users/*", Description = "User API wildcard", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/api/users", Description = "User API exact", ResourceType = "API" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.FindBestMatchingResourceAsync("/api/users");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("/api/users", result.Uri); // Exact match should be returned
    }

    [TestMethod]
    public async Task ResourceService_FindBestMatchingResourceAsync_ReturnsWildcardMatchWhenNoExact()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users/*", Description = "User API wildcard", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/api/groups", Description = "Group API", ResourceType = "API" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.FindBestMatchingResourceAsync("/api/users/123");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("/api/users/*", result.Uri);
    }

    [TestMethod]
    public async Task ResourceService_FindBestMatchingResourceAsync_ReturnsNullWhenNoMatch()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/api/groups", Description = "Group API", ResourceType = "API" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.FindBestMatchingResourceAsync("/api/unknown");

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region IsUriProtectedAsync Tests

    [TestMethod]
    public async Task ResourceService_IsUriProtectedAsync_ReturnsFalseForUnprotectedResource()
    {
        // Arrange - Resource exists but has no permissions
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.IsUriProtectedAsync("/api/users");

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
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/web/home", Description = "Home Page", ResourceType = "Web" },
            new Data.Models.Resource { Uri = "/api/groups", Description = "Group API", ResourceType = "API" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.GetResourcesByTypeAsync("API");

        // Assert
        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.All(r => r.ResourceType == "API"));
    }

    #endregion

    #region SearchResourcesAsync Tests

    [TestMethod]
    public async Task ResourceService_SearchResourcesAsync_ReturnsMatchingResources()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/api/groups", Description = "Group API", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/api/user-management", Description = "User Management", ResourceType = "API" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.SearchResourcesAsync("user");

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    #endregion

    #region GetTotalResourceCountAsync Tests

    [TestMethod]
    public async Task ResourceService_GetTotalResourceCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/api/groups", Description = "Group API", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/web/home", Description = "Home Page", ResourceType = "Web" }
        );
        await _dbContext.SaveChangesAsync();

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
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users", Description = "User API", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/api/groups", Description = "Group API", ResourceType = "API" },
            new Data.Models.Resource { Uri = "/web/home", Description = "Home Page", ResourceType = "Web" },
            new Data.Models.Resource { Uri = "/web/about", Description = "About Page", ResourceType = "Web" }
        );
        await _dbContext.SaveChangesAsync();

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

        // Act
        var result = await _resourceService.CreateResourcesBulkAsync(resources, createdBy);

        // Assert
        Assert.AreEqual(3, result.Count());
        Assert.AreEqual(3, await _dbContext.Resources.CountAsync());
    }

    #endregion

    #region IsResourceAccessibleAsync Tests

    [TestMethod]
    public async Task ResourceService_IsResourceAccessibleAsync_CallsPermissionService()
    {
        // Arrange
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        _mockPermissionService.Setup(x => x.HasPermissionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _resourceService.IsResourceAccessibleAsync(dataResource.Id, 1, Domain.HttpVerb.GET);

        // Assert
        Assert.IsTrue(result);
        _mockPermissionService.Verify(x => x.HasPermissionAsync(1, It.IsAny<string>(), "GET"), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task ResourceService_CreateResourceAsync_ThrowsOnDuplicateUri()
    {
        // Arrange
        var uri = "/api/users";
        _dbContext.Resources.Add(new Data.Models.Resource { Uri = uri, Description = "Test", ResourceType = "API" });
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _resourceService.CreateResourceAsync(uri, "Duplicate", "API", "TestAdmin"));
    }

    #endregion

    #region CreateAsync (Request/Response) Tests

    [TestMethod]
    public async Task CreateAsync_WithValidRequest_ReturnsSuccessResponse()
    {
        // Arrange
        var request = new CreateResourceRequest
        {
            Name = "API",
            Description = "User API endpoint",
            UriPattern = "/api/users",
            CreatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.CreateAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Resource);
        Assert.AreEqual("Resource created successfully", result.Message);
        Assert.AreEqual("/api/users", result.Resource.Uri);
    }

    [TestMethod]
    public async Task CreateAsync_WithEmptyUriPattern_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateResourceRequest
        {
            Name = "API",
            Description = "User API endpoint",
            UriPattern = "",
            CreatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.CreateAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("URI pattern is required", result.Message);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("URI pattern cannot be empty")));
    }

    [TestMethod]
    public async Task CreateAsync_WithEmptyName_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateResourceRequest
        {
            Name = "",
            Description = "User API endpoint",
            UriPattern = "/api/users",
            CreatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.CreateAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Resource name is required", result.Message);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Resource name cannot be empty")));
    }

    [TestMethod]
    public async Task CreateAsync_WithWhitespaceUriPattern_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateResourceRequest
        {
            Name = "API",
            Description = "User API endpoint",
            UriPattern = "   ",
            CreatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.CreateAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("URI pattern is required", result.Message);
    }

    [TestMethod]
    public async Task CreateAsync_WithDuplicateUri_ReturnsErrorResponse()
    {
        // Arrange
        _dbContext.Resources.Add(new Data.Models.Resource { Uri = "/api/users", Description = "Existing", ResourceType = "API" });
        await _dbContext.SaveChangesAsync();

        var request = new CreateResourceRequest
        {
            Name = "API",
            Description = "User API endpoint",
            UriPattern = "/api/users",
            CreatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.CreateAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("already exists")));
    }

    #endregion

    #region UpdateAsync (Request/Response) Tests

    [TestMethod]
    public async Task UpdateAsync_WithValidRequest_ReturnsSuccessResponse()
    {
        // Arrange
        var existingResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(existingResource);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateResourceRequest
        {
            ResourceId = existingResource.Id,
            Name = "API",
            Description = "Updated User API",
            UriPattern = "/api/users/v2",
            UpdatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.UpdateAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Resource);
        Assert.AreEqual("Resource updated successfully", result.Message);
        Assert.AreEqual("/api/users/v2", result.Resource.Uri);
    }

    [TestMethod]
    public async Task UpdateAsync_WithInvalidResourceId_ReturnsValidationError()
    {
        // Arrange
        var request = new UpdateResourceRequest
        {
            ResourceId = 0,
            Name = "API",
            Description = "Updated User API",
            UriPattern = "/api/users/v2",
            UpdatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.UpdateAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Invalid resource ID", result.Message);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Resource ID must be a positive integer")));
    }

    [TestMethod]
    public async Task UpdateAsync_WithNegativeResourceId_ReturnsValidationError()
    {
        // Arrange
        var request = new UpdateResourceRequest
        {
            ResourceId = -5,
            Name = "API",
            Description = "Updated User API",
            UriPattern = "/api/users/v2",
            UpdatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.UpdateAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Invalid resource ID", result.Message);
    }

    [TestMethod]
    public async Task UpdateAsync_WithEmptyUriPattern_ReturnsValidationError()
    {
        // Arrange
        var request = new UpdateResourceRequest
        {
            ResourceId = 1,
            Name = "API",
            Description = "Updated User API",
            UriPattern = "",
            UpdatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.UpdateAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("URI pattern is required", result.Message);
    }

    [TestMethod]
    public async Task UpdateAsync_WithEmptyName_ReturnsValidationError()
    {
        // Arrange
        var request = new UpdateResourceRequest
        {
            ResourceId = 1,
            Name = "",
            Description = "Updated User API",
            UriPattern = "/api/users/v2",
            UpdatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.UpdateAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Resource name is required", result.Message);
    }

    [TestMethod]
    public async Task UpdateAsync_WhenResourceNotFound_ReturnsOperationError()
    {
        // Arrange
        var request = new UpdateResourceRequest
        {
            ResourceId = 999,
            Name = "API",
            Description = "Updated User API",
            UriPattern = "/api/users/v2",
            UpdatedBy = "TestAdmin"
        };

        // Act
        var result = await _resourceService.UpdateAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Operation error", result.Message);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("not found")));
    }

    #endregion

    #region GetByIdAsync (Request/Response) Tests

    [TestMethod]
    public async Task GetByIdAsync_WithValidRequest_ReturnsSuccessResponse()
    {
        // Arrange
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        var request = new GetResourceRequest
        {
            ResourceId = dataResource.Id,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetByIdAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Resource);
        Assert.AreEqual("Resource retrieved successfully", result.Message);
        Assert.AreEqual(dataResource.Id, result.Resource.Id);
        Assert.AreEqual("/api/users", result.Resource.Uri);
    }

    [TestMethod]
    public async Task GetByIdAsync_WithInvalidResourceId_ReturnsValidationError()
    {
        // Arrange
        var request = new GetResourceRequest
        {
            ResourceId = 0,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetByIdAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Invalid resource ID", result.Message);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Resource ID must be a positive integer")));
    }

    [TestMethod]
    public async Task GetByIdAsync_WithNegativeResourceId_ReturnsValidationError()
    {
        // Arrange
        var request = new GetResourceRequest
        {
            ResourceId = -1,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetByIdAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Invalid resource ID", result.Message);
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenResourceNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var request = new GetResourceRequest
        {
            ResourceId = 999,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetByIdAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Resource not found", result.Message);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("999")));
    }

    #endregion

    #region GetAllAsync (Request/Response) Tests

    [TestMethod]
    public async Task GetAllAsync_WithDefaultRequest_ReturnsAllResources()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users", Description = "User API", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/groups", Description = "Group API", ResourceType = "API", IsActive = true }
        );
        await _dbContext.SaveChangesAsync();

        var request = new GetResourcesRequest
        {
            Page = 1,
            PageSize = 20,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(2, result.Resources.Count);
        Assert.AreEqual(1, result.Page);
        Assert.AreEqual(20, result.PageSize);
    }

    [TestMethod]
    public async Task GetAllAsync_WithSearchFilter_ReturnsFilteredResources()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users", Description = "User API", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/groups", Description = "Group API", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/user-profiles", Description = "User Profiles", ResourceType = "API", IsActive = true }
        );
        await _dbContext.SaveChangesAsync();

        var request = new GetResourcesRequest
        {
            Page = 1,
            PageSize = 20,
            Search = "user",
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.TotalCount);
        Assert.IsTrue(result.Resources.All(r => r.Uri.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                                                 r.Description?.Contains("user", StringComparison.OrdinalIgnoreCase) == true));
    }

    [TestMethod]
    public async Task GetAllAsync_WithResourceTypeFilter_ReturnsFilteredResources()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/users", Description = "User API", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/web/home", Description = "Home Page", ResourceType = "Web", IsActive = true },
            new Data.Models.Resource { Uri = "/api/groups", Description = "Group API", ResourceType = "API", IsActive = true }
        );
        await _dbContext.SaveChangesAsync();

        var request = new GetResourcesRequest
        {
            Page = 1,
            PageSize = 20,
            ResourceType = "API",
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.TotalCount);
        Assert.IsTrue(result.Resources.All(r => r.ResourceType == "API"));
    }

    [TestMethod]
    public async Task GetAllAsync_WithSortByUri_ReturnsSortedResources()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/zebra", Description = "Zebra API", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/alpha", Description = "Alpha API", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/middle", Description = "Middle API", ResourceType = "API", IsActive = true }
        );
        await _dbContext.SaveChangesAsync();

        var request = new GetResourcesRequest
        {
            Page = 1,
            PageSize = 20,
            SortBy = "uri",
            SortDescending = false,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        var resourceList = result.Resources.ToList();
        Assert.AreEqual("/api/alpha", resourceList[0].Uri);
        Assert.AreEqual("/api/middle", resourceList[1].Uri);
        Assert.AreEqual("/api/zebra", resourceList[2].Uri);
    }

    [TestMethod]
    public async Task GetAllAsync_WithSortByUriDescending_ReturnsSortedResourcesDescending()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/alpha", Description = "Alpha API", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/zebra", Description = "Zebra API", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/middle", Description = "Middle API", ResourceType = "API", IsActive = true }
        );
        await _dbContext.SaveChangesAsync();

        var request = new GetResourcesRequest
        {
            Page = 1,
            PageSize = 20,
            SortBy = "uri",
            SortDescending = true,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        var resourceList = result.Resources.ToList();
        Assert.AreEqual("/api/zebra", resourceList[0].Uri);
        Assert.AreEqual("/api/middle", resourceList[1].Uri);
        Assert.AreEqual("/api/alpha", resourceList[2].Uri);
    }

    [TestMethod]
    public async Task GetAllAsync_WithSortByType_ReturnsSortedResources()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/web/home", Description = "Home Page", ResourceType = "Web", IsActive = true },
            new Data.Models.Resource { Uri = "/api/users", Description = "User API", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/doc/readme", Description = "Docs", ResourceType = "Documentation", IsActive = true }
        );
        await _dbContext.SaveChangesAsync();

        var request = new GetResourcesRequest
        {
            Page = 1,
            PageSize = 20,
            SortBy = "type",
            SortDescending = false,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        var resourceList = result.Resources.ToList();
        Assert.AreEqual("API", resourceList[0].ResourceType);
        Assert.AreEqual("Documentation", resourceList[1].ResourceType);
        Assert.AreEqual("Web", resourceList[2].ResourceType);
    }

    [TestMethod]
    public async Task GetAllAsync_WithSortByCreated_ReturnsSortedResources()
    {
        // Arrange
        var baseDate = DateTime.UtcNow;
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/third", Description = "Third", ResourceType = "API", IsActive = true, CreatedAt = baseDate.AddDays(2) },
            new Data.Models.Resource { Uri = "/api/first", Description = "First", ResourceType = "API", IsActive = true, CreatedAt = baseDate },
            new Data.Models.Resource { Uri = "/api/second", Description = "Second", ResourceType = "API", IsActive = true, CreatedAt = baseDate.AddDays(1) }
        );
        await _dbContext.SaveChangesAsync();

        var request = new GetResourcesRequest
        {
            Page = 1,
            PageSize = 20,
            SortBy = "created",
            SortDescending = false,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        var resourceList = result.Resources.ToList();
        Assert.AreEqual("/api/first", resourceList[0].Uri);
        Assert.AreEqual("/api/second", resourceList[1].Uri);
        Assert.AreEqual("/api/third", resourceList[2].Uri);
    }

    [TestMethod]
    public async Task GetAllAsync_WithSortByUpdated_ReturnsSortedResources()
    {
        // Arrange
        var baseDate = DateTime.UtcNow;
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/oldest", Description = "Oldest", ResourceType = "API", IsActive = true, UpdatedAt = baseDate },
            new Data.Models.Resource { Uri = "/api/newest", Description = "Newest", ResourceType = "API", IsActive = true, UpdatedAt = baseDate.AddDays(2) },
            new Data.Models.Resource { Uri = "/api/middle", Description = "Middle", ResourceType = "API", IsActive = true, UpdatedAt = baseDate.AddDays(1) }
        );
        await _dbContext.SaveChangesAsync();

        var request = new GetResourcesRequest
        {
            Page = 1,
            PageSize = 20,
            SortBy = "updated",
            SortDescending = true,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        var resourceList = result.Resources.ToList();
        Assert.AreEqual("/api/newest", resourceList[0].Uri);
        Assert.AreEqual("/api/middle", resourceList[1].Uri);
        Assert.AreEqual("/api/oldest", resourceList[2].Uri);
    }

    [TestMethod]
    public async Task GetAllAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/a", Description = "A", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/b", Description = "B", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/c", Description = "C", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/d", Description = "D", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/e", Description = "E", ResourceType = "API", IsActive = true }
        );
        await _dbContext.SaveChangesAsync();

        var request = new GetResourcesRequest
        {
            Page = 2,
            PageSize = 2,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(5, result.TotalCount);
        Assert.AreEqual(2, result.Resources.Count);
        Assert.AreEqual(2, result.Page);
        Assert.AreEqual(2, result.PageSize);
    }

    [TestMethod]
    public async Task GetAllAsync_WithActiveOnlyFilter_ReturnsOnlyActiveResources()
    {
        // Arrange
        _dbContext.Resources.AddRange(
            new Data.Models.Resource { Uri = "/api/active1", Description = "Active 1", ResourceType = "API", IsActive = true },
            new Data.Models.Resource { Uri = "/api/inactive", Description = "Inactive", ResourceType = "API", IsActive = false },
            new Data.Models.Resource { Uri = "/api/active2", Description = "Active 2", ResourceType = "API", IsActive = true }
        );
        await _dbContext.SaveChangesAsync();

        var request = new GetResourcesRequest
        {
            Page = 1,
            PageSize = 20,
            ActiveOnly = true,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.TotalCount);
        Assert.IsTrue(result.Resources.All(r => r.IsActive));
    }

    [TestMethod]
    public async Task GetAllAsync_WhenEmptyDatabase_ReturnsEmptyCollection()
    {
        // Arrange
        var request = new GetResourcesRequest
        {
            Page = 1,
            PageSize = 20,
            RequestedBy = "TestUser"
        };

        // Act
        var result = await _resourceService.GetAllAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0, result.Resources.Count);
    }

    #endregion

    #region DeleteAsync (Request/Response) Tests

    [TestMethod]
    public async Task DeleteAsync_WithValidRequest_ReturnsSuccessResponse()
    {
        // Arrange
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        var request = new DeleteResourceRequest
        {
            ResourceId = dataResource.Id,
            DeletedBy = "TestAdmin",
            ForceDelete = false
        };

        // Act
        var result = await _resourceService.DeleteAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual("Resource deleted successfully", result.Message);

        // Verify deletion
        var deletedResource = await _dbContext.Resources.FindAsync(dataResource.Id);
        Assert.IsNull(deletedResource);
    }

    [TestMethod]
    public async Task DeleteAsync_WithInvalidResourceId_ReturnsValidationError()
    {
        // Arrange
        var request = new DeleteResourceRequest
        {
            ResourceId = 0,
            DeletedBy = "TestAdmin",
            ForceDelete = false
        };

        // Act
        var result = await _resourceService.DeleteAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Invalid resource ID", result.Message);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Resource ID must be a positive integer")));
    }

    [TestMethod]
    public async Task DeleteAsync_WithNegativeResourceId_ReturnsValidationError()
    {
        // Arrange
        var request = new DeleteResourceRequest
        {
            ResourceId = -10,
            DeletedBy = "TestAdmin",
            ForceDelete = false
        };

        // Act
        var result = await _resourceService.DeleteAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Invalid resource ID", result.Message);
    }

    [TestMethod]
    public async Task DeleteAsync_WhenResourceNotFound_ReturnsOperationError()
    {
        // Arrange
        var request = new DeleteResourceRequest
        {
            ResourceId = 999,
            DeletedBy = "TestAdmin",
            ForceDelete = true
        };

        // Act
        var result = await _resourceService.DeleteAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Operation error", result.Message);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("not found")));
    }

    [TestMethod]
    public async Task DeleteAsync_WithForceDelete_BypassesDependencyCheck()
    {
        // Arrange
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        var request = new DeleteResourceRequest
        {
            ResourceId = dataResource.Id,
            DeletedBy = "TestAdmin",
            ForceDelete = true
        };

        // Act
        var result = await _resourceService.DeleteAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual("Resource deleted successfully", result.Message);
    }

    #endregion

    #region CheckDependenciesAsync Tests

    [TestMethod]
    public async Task CheckDependenciesAsync_WhenResourceNotFound_ReturnsCannotDelete()
    {
        // Arrange - no resources added

        // Act
        var result = await _resourceService.CheckDependenciesAsync(999);

        // Assert
        Assert.IsFalse(result.CanDelete);
        Assert.IsTrue(result.Messages.Any(m => m.Contains("not found")));
    }

    [TestMethod]
    public async Task CheckDependenciesAsync_WhenNoDependencies_ReturnsCanDelete()
    {
        // Arrange
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.CheckDependenciesAsync(dataResource.Id);

        // Assert
        Assert.IsTrue(result.CanDelete);
        Assert.IsTrue(result.Messages.Any(m => m.Contains("can be safely deleted")));
    }

    [TestMethod]
    public async Task CheckDependenciesAsync_WithUriAccessDependencies_ReturnsCannotDelete()
    {
        // Arrange
        var dataResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(dataResource);
        await _dbContext.SaveChangesAsync();

        var entity = new Data.Models.Entity { EntityType = "User" };
        _dbContext.Entities.Add(entity);
        await _dbContext.SaveChangesAsync();

        var schemeType = new SchemeType { SchemeName = "ApiUriAuthorization" };
        _dbContext.SchemeTypes.Add(schemeType);
        await _dbContext.SaveChangesAsync();

        var permissionScheme = new PermissionScheme { EntityId = entity.Id, SchemeTypeId = schemeType.Id };
        _dbContext.EntityPermissions.Add(permissionScheme);
        await _dbContext.SaveChangesAsync();

        var verbType = new VerbType { VerbName = "GET" };
        _dbContext.VerbTypes.Add(verbType);
        await _dbContext.SaveChangesAsync();

        var uriAccess = new UriAccess
        {
            ResourceId = dataResource.Id,
            PermissionSchemeId = permissionScheme.Id,
            VerbTypeId = verbType.Id,
            Grant = true,
            Deny = false
        };
        _dbContext.UriAccesses.Add(uriAccess);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.CheckDependenciesAsync(dataResource.Id);

        // Assert
        Assert.IsFalse(result.CanDelete);
        Assert.IsTrue(result.Dependencies.Any());
        Assert.IsTrue(result.Messages.Any(m => m.Contains("active permission")));
    }

    [TestMethod]
    public async Task CheckDependenciesAsync_WithChildResources_ReturnsWarningsAboutChildren()
    {
        // Arrange
        var parentResource = new Data.Models.Resource
        {
            Uri = "/api/users",
            Description = "User API",
            ResourceType = "API"
        };
        _dbContext.Resources.Add(parentResource);
        await _dbContext.SaveChangesAsync();

        var childResource = new Data.Models.Resource
        {
            Uri = "/api/users/profile",
            Description = "User Profile API",
            ResourceType = "API",
            ParentResourceId = parentResource.Id
        };
        _dbContext.Resources.Add(childResource);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resourceService.CheckDependenciesAsync(parentResource.Id);

        // Assert
        Assert.IsTrue(result.Warnings.Any(w => w.Contains("child resource")));
        Assert.IsTrue(result.Dependencies.Any(d => d.DependencyType == "Child Resource"));
    }

    #endregion
}
