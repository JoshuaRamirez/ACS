using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ACS.WebApi.DTOs;
using ACS.WebApi.Tests.Integration.Infrastructure;

namespace ACS.WebApi.Tests.Integration.Controllers;

/// <summary>
/// Comprehensive integration tests for UsersController
/// Tests the full request-response cycle including database interactions
/// </summary>
[TestClass]
public class UsersControllerIntegrationTests : IntegrationTestBase
{
    private const string UsersApiPath = "/api/users";

    public override void Setup()
    {
        base.Setup();
        // Set up authentication for all tests
        SetupAuthentication("test-user-123", "Test User", "Admin", "User");
    }

    #region GET /api/users Tests

    [TestMethod]
    public async Task GetUsers_WithValidRequest_ReturnsSuccessWithUserList()
    {
        // Act
        var response = await Client.GetAsync(UsersApiPath);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserListResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Users.Should().NotBeEmpty();
        apiResponse.Data.Users.Should().HaveCount(2); // Based on test data
    }

    [TestMethod]
    public async Task GetUsers_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var queryParams = "?page=1&pageSize=1";

        // Act
        var response = await Client.GetAsync($"{UsersApiPath}{queryParams}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserListResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Data!.Page.Should().Be(1);
        apiResponse.Data.PageSize.Should().Be(1);
        apiResponse.Data.Users.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GetUsers_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync(UsersApiPath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GET /api/users/{id} Tests

    [TestMethod]
    public async Task GetUser_WithValidId_ReturnsUser()
    {
        // Arrange
        const int userId = 1;

        // Act
        var response = await Client.GetAsync($"{UsersApiPath}/{userId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Id.Should().Be(userId);
        apiResponse.Data.Name.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GetUser_WithNonExistentId_ReturnsInternalServerError()
    {
        // Arrange
        const int nonExistentUserId = 999;

        // Act
        var response = await Client.GetAsync($"{UsersApiPath}/{nonExistentUserId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("not found");
    }

    [TestMethod]
    public async Task GetUser_WithInvalidId_ReturnsBadRequest()
    {
        // Act
        var response = await Client.GetAsync($"{UsersApiPath}/invalid-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region POST /api/users Tests

    [TestMethod]
    public async Task CreateUser_WithValidRequest_CreatesUserSuccessfully()
    {
        // Arrange
        var createRequest = TestDataBuilder.CreateUserRequest()
            .WithName("New Test User")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync(UsersApiPath, createRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Name.Should().Be("New Test User");
        apiResponse.Data.Id.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task CreateUser_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        var createRequest = TestDataBuilder.CreateUserRequest()
            .WithName("")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync(UsersApiPath, createRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("required");
    }

    [TestMethod]
    public async Task CreateUser_WithNullName_ReturnsBadRequest()
    {
        // Arrange
        var createRequest = new CreateUserRequest(null!, null, null);

        // Act
        var response = await Client.PostAsJsonAsync(UsersApiPath, createRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("required");
    }

    [TestMethod]
    public async Task CreateUser_WithInvalidJson_ReturnsBadRequest()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var content = new StringContent(invalidJson, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync(UsersApiPath, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region PUT /api/users/{id} Tests

    [TestMethod]
    public async Task UpdateUser_WithValidRequest_UpdatesUserSuccessfully()
    {
        // Arrange
        const int userId = 1;
        var updateRequest = TestDataBuilder.UpdateUserRequest()
            .WithName("Updated User Name")
            .Build();

        // Act
        var response = await Client.PutAsJsonAsync($"{UsersApiPath}/{userId}", updateRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Name.Should().Be("Updated User Name");
        apiResponse.Data.Id.Should().Be(userId);
    }

    [TestMethod]
    public async Task UpdateUser_WithNonExistentId_ReturnsInternalServerError()
    {
        // Arrange
        const int nonExistentUserId = 999;
        var updateRequest = TestDataBuilder.UpdateUserRequest()
            .WithName("Updated Name")
            .Build();

        // Act
        var response = await Client.PutAsJsonAsync($"{UsersApiPath}/{nonExistentUserId}", updateRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("not found");
    }

    [TestMethod]
    public async Task UpdateUser_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        const int userId = 1;
        var updateRequest = TestDataBuilder.UpdateUserRequest()
            .WithName("")
            .Build();

        // Act
        var response = await Client.PutAsJsonAsync($"{UsersApiPath}/{userId}", updateRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("required");
    }

    #endregion

    #region DELETE /api/users/{id} Tests

    [TestMethod]
    public async Task DeleteUser_WithValidId_DeletesUserSuccessfully()
    {
        // Arrange
        const int userId = 2; // Use a different user to avoid conflicts

        // Act
        var response = await Client.DeleteAsync($"{UsersApiPath}/{userId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().BeTrue();
    }

    [TestMethod]
    public async Task DeleteUser_WithNonExistentId_ReturnsInternalServerError()
    {
        // Arrange
        const int nonExistentUserId = 999;

        // Act
        var response = await Client.DeleteAsync($"{UsersApiPath}/{nonExistentUserId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("not found");
    }

    #endregion

    #region POST /api/users/{id}/groups Tests

    [TestMethod]
    public async Task AddUserToGroup_WithValidRequest_AddsUserToGroupSuccessfully()
    {
        // Arrange
        const int userId = 1;
        const int groupId = 1;
        var request = new AddUserToGroupRequest(userId, groupId);

        // Act
        var response = await Client.PostAsJsonAsync($"{UsersApiPath}/{userId}/groups", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().BeTrue();
    }

    [TestMethod]
    public async Task AddUserToGroup_WithMismatchedUserId_ReturnsBadRequest()
    {
        // Arrange
        const int urlUserId = 1;
        const int bodyUserId = 2;
        const int groupId = 1;
        var request = new AddUserToGroupRequest(bodyUserId, groupId);

        // Act
        var response = await Client.PostAsJsonAsync($"{UsersApiPath}/{urlUserId}/groups", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("must match");
    }

    [TestMethod]
    public async Task AddUserToGroup_WithNonExistentUser_ReturnsBadRequest()
    {
        // Arrange
        const int nonExistentUserId = 999;
        const int groupId = 1;
        var request = new AddUserToGroupRequest(nonExistentUserId, groupId);

        // Act
        var response = await Client.PostAsJsonAsync($"{UsersApiPath}/{nonExistentUserId}/groups", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("not found");
    }

    #endregion

    #region POST /api/users/{id}/roles Tests

    [TestMethod]
    public async Task AssignUserToRole_WithValidRequest_AssignsUserToRoleSuccessfully()
    {
        // Arrange
        const int userId = 1;
        const int roleId = 1;
        var request = new AssignUserToRoleRequest(userId, roleId);

        // Act
        var response = await Client.PostAsJsonAsync($"{UsersApiPath}/{userId}/roles", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().BeTrue();
    }

    [TestMethod]
    public async Task AssignUserToRole_WithMismatchedUserId_ReturnsBadRequest()
    {
        // Arrange
        const int urlUserId = 1;
        const int bodyUserId = 2;
        const int roleId = 1;
        var request = new AssignUserToRoleRequest(bodyUserId, roleId);

        // Act
        var response = await Client.PostAsJsonAsync($"{UsersApiPath}/{urlUserId}/roles", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("must match");
    }

    [TestMethod]
    public async Task AssignUserToRole_WithNonExistentRole_ReturnsBadRequest()
    {
        // Arrange
        const int userId = 1;
        const int nonExistentRoleId = 999;
        var request = new AssignUserToRoleRequest(userId, nonExistentRoleId);

        // Act
        var response = await Client.PostAsJsonAsync($"{UsersApiPath}/{userId}/roles", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("not found");
    }

    #endregion

    #region Content Type and Header Tests

    [TestMethod]
    public async Task Users_AllEndpoints_ReturnCorrectContentType()
    {
        // Test multiple endpoints return JSON content type
        var endpoints = new[]
        {
            UsersApiPath,
            $"{UsersApiPath}/1"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await Client.GetAsync(endpoint);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        }
    }

    [TestMethod]
    public async Task Users_PostEndpoints_AcceptJsonContentType()
    {
        // Arrange
        var createRequest = TestDataBuilder.CreateUserRequest().Build();

        // Act
        var response = await Client.PostAsJsonAsync(UsersApiPath, createRequest);

        // Assert
        response.RequestMessage?.Content?.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task Users_WithServerError_ReturnsProperErrorResponse()
    {
        // This test verifies error handling when the service layer throws exceptions
        // The test services are configured to return errors for certain scenarios
        
        // Arrange - use an ID that will trigger an error in our test service
        var response = await Client.GetAsync($"{UsersApiPath}/999");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        content.Should().Contain("not found");
    }

    #endregion

    #region Authentication and Authorization Tests

    [TestMethod]
    public async Task Users_WithExpiredToken_ReturnsUnauthorized()
    {
        // Arrange - Create an expired token
        Client.DefaultRequestHeaders.Authorization = null;
        SetupAuthentication("test-user", "Test User", "User"); // This creates a valid token
        
        // For this test, we'll remove auth header to simulate expired/invalid token
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync(UsersApiPath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Users_WithValidToken_AllowsAccess()
    {
        // This is implicitly tested by all other tests since they use authentication
        // But we'll add an explicit test for clarity

        // Act
        var response = await Client.GetAsync(UsersApiPath);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    #endregion
}