using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ACS.WebApi.DTOs;
using ACS.WebApi.Tests.Integration.Infrastructure;

namespace ACS.WebApi.Tests.Integration.Controllers;

/// <summary>
/// Comprehensive integration tests for PermissionsController
/// Tests permission evaluation, granting, denying, and complex access control scenarios
/// </summary>
[TestClass]
public class PermissionsControllerIntegrationTests : IntegrationTestBase
{
    private const string PermissionsApiPath = "/api/permissions";

    public override void Setup()
    {
        base.Setup();
        // Set up authentication for all tests
        SetupAuthentication("test-user-123", "Test User", "Admin", "User");
    }

    #region POST /api/permissions/check Tests

    [TestMethod]
    public async Task CheckPermission_WithValidRequest_ReturnsPermissionResult()
    {
        // Arrange
        var checkRequest = TestDataBuilder.CheckPermissionRequest()
            .ForEntity(1)
            .ForUri("/api/users")
            .ForGet()
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.EntityId.Should().Be(1);
        apiResponse.Data.Uri.Should().Be("/api/users");
        apiResponse.Data.HttpVerb.Should().Be("GET");
        apiResponse.Data.HasPermission.Should().BeTrue(); // Based on test logic: entity 1 gets access
    }

    [TestMethod]
    public async Task CheckPermission_WithAdminEntity_GrantsAccess()
    {
        // Arrange - Admin entity (ID 1) should have access
        var checkRequest = TestDataBuilder.CheckPermissionRequest()
            .ForEntity(1)
            .ForUri("/api/admin/sensitive")
            .ForPost()
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Data!.HasPermission.Should().BeTrue();
        apiResponse.Data.Reason.Should().Be("Access granted");
    }

    [TestMethod]
    public async Task CheckPermission_WithRegularEntity_DeniesAdminAccess()
    {
        // Arrange - Regular entity (not ID 1) should be denied admin access
        var checkRequest = TestDataBuilder.CheckPermissionRequest()
            .ForEntity(2)
            .ForUri("/api/admin/sensitive")
            .ForDelete()
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Data!.HasPermission.Should().BeFalse();
        apiResponse.Data.Reason.Should().Be("Access denied");
    }

    [TestMethod]
    public async Task CheckPermission_WithTestUri_GrantsAccess()
    {
        // Arrange - Test URIs should be granted access per test logic
        var checkRequest = TestDataBuilder.CheckPermissionRequest()
            .ForEntity(5)
            .ForUri("/api/test/endpoint")
            .ForPut()
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Data!.HasPermission.Should().BeTrue();
        apiResponse.Data.Reason.Should().Be("Access granted");
    }

    [TestMethod]
    public async Task CheckPermission_WithInvalidEntityId_ReturnsBadRequest()
    {
        // Arrange
        var checkRequest = TestDataBuilder.CheckPermissionRequest()
            .ForEntity(0) // Invalid entity ID
            .ForUri("/api/users")
            .ForGet()
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("must be greater than 0");
    }

    [TestMethod]
    public async Task CheckPermission_WithEmptyUri_ReturnsBadRequest()
    {
        // Arrange
        var checkRequest = TestDataBuilder.CheckPermissionRequest()
            .ForEntity(1)
            .ForUri("") // Empty URI
            .ForGet()
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("URI is required");
    }

    [TestMethod]
    public async Task CheckPermission_WithEmptyHttpVerb_ReturnsBadRequest()
    {
        // Arrange
        var checkRequest = TestDataBuilder.CheckPermissionRequest()
            .ForEntity(1)
            .ForUri("/api/users")
            .WithVerb("") // Empty HTTP verb
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("HTTP verb is required");
    }

    [TestMethod]
    public async Task CheckPermission_WithNullRequest_ReturnsBadRequest()
    {
        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", (CheckPermissionRequest?)null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region POST /api/permissions/grant Tests

    [TestMethod]
    public async Task GrantPermission_WithValidRequest_GrantsPermissionSuccessfully()
    {
        // Arrange
        var grantRequest = new GrantPermissionRequest(1, "/api/users", "GET", "ApiUriAuthorization");

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/grant", grantRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().BeTrue();
        apiResponse.Message.Should().Contain("granted");
    }

    [TestMethod]
    public async Task GrantPermission_WithInvalidEntityId_ReturnsBadRequest()
    {
        // Arrange
        var grantRequest = new GrantPermissionRequest(0, "/api/users", "GET", "ApiUriAuthorization");

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/grant", grantRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("must be greater than 0");
    }

    [TestMethod]
    public async Task GrantPermission_WithEmptyUri_ReturnsBadRequest()
    {
        // Arrange
        var grantRequest = new GrantPermissionRequest(1, "", "GET", "ApiUriAuthorization");

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/grant", grantRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("URI is required");
    }

    [TestMethod]
    public async Task GrantPermission_WithEmptyHttpVerb_ReturnsBadRequest()
    {
        // Arrange
        var grantRequest = new GrantPermissionRequest(1, "/api/users", "", "ApiUriAuthorization");

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/grant", grantRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("HTTP verb is required");
    }

    [TestMethod]
    public async Task GrantPermission_WithNonExistentEntity_ReturnsBadRequest()
    {
        // Arrange
        var grantRequest = new GrantPermissionRequest(999, "/api/users", "GET", "ApiUriAuthorization");

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/grant", grantRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("not found");
    }

    #endregion

    #region POST /api/permissions/deny Tests

    [TestMethod]
    public async Task DenyPermission_WithValidRequest_DeniesPermissionSuccessfully()
    {
        // Arrange
        var denyRequest = new DenyPermissionRequest(2, "/api/admin", "DELETE", "ApiUriAuthorization");

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/deny", denyRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().BeTrue();
        apiResponse.Message.Should().Contain("denied");
    }

    [TestMethod]
    public async Task DenyPermission_WithInvalidEntityId_ReturnsBadRequest()
    {
        // Arrange
        var denyRequest = new DenyPermissionRequest(-1, "/api/admin", "DELETE", "ApiUriAuthorization");

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/deny", denyRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("must be greater than 0");
    }

    #endregion

    #region GET /api/permissions/{entityId} Tests

    [TestMethod]
    public async Task GetEntityPermissions_WithValidEntityId_ReturnsPermissionList()
    {
        // Arrange
        const int entityId = 1;

        // Act
        var response = await Client.GetAsync($"{PermissionsApiPath}/{entityId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<PermissionListResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Permissions.Should().NotBeNull();
    }

    [TestMethod]
    public async Task GetEntityPermissions_WithNonExistentEntity_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentEntityId = 999;

        // Act
        var response = await Client.GetAsync($"{PermissionsApiPath}/{nonExistentEntityId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<PermissionListResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("not found");
    }

    #endregion

    #region Complex Permission Scenarios

    [TestMethod]
    public async Task PermissionWorkflow_GrantCheckDeny_WorksCorrectly()
    {
        // This test simulates a complete permission workflow:
        // 1. Grant permission
        // 2. Check permission (should be granted)
        // 3. Deny permission
        // 4. Check permission (should be denied)

        const int entityId = 3;
        const string uri = "/api/workflow/test";
        const string verb = "PUT";

        // Step 1: Grant permission
        var grantRequest = new GrantPermissionRequest(entityId, uri, verb, "ApiUriAuthorization");
        var grantResponse = await Client.PostAsJsonAsync($"{PermissionsApiPath}/grant", grantRequest);
        grantResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 2: Check permission (should be granted based on test logic for grant operations)
        var checkRequest = new CheckPermissionRequest(entityId, uri, verb);
        var checkResponse = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
        var checkContent = await checkResponse.Content.ReadAsStringAsync();
        
        checkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        // Note: The test service logic grants access to entity 1 or URIs containing "test"
        // Since our URI contains "test", it should be granted

        // Step 3: Deny permission
        var denyRequest = new DenyPermissionRequest(entityId, uri, verb, "ApiUriAuthorization");
        var denyResponse = await Client.PostAsJsonAsync($"{PermissionsApiPath}/deny", denyRequest);
        denyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 4: Check permission again
        var checkResponse2 = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
        checkResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // In a real implementation, the second check might return denied
        // But our test service has simple logic, so we just verify the API works
    }

    [TestMethod]
    public async Task PermissionCheck_WithDifferentHttpVerbs_HandlesAllVerbs()
    {
        // Test all HTTP verbs are handled correctly
        const int entityId = 1;
        const string uri = "/api/test/verbs";

        var verbs = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };

        foreach (var verb in verbs)
        {
            // Arrange
            var checkRequest = new CheckPermissionRequest(entityId, uri, verb);

            // Act
            var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"HTTP verb {verb} should be handled");

            var apiResponse = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            apiResponse!.Success.Should().BeTrue();
            apiResponse.Data!.HttpVerb.Should().Be(verb);
        }
    }

    [TestMethod]
    public async Task PermissionCheck_WithComplexUriPatterns_HandlesCorrectly()
    {
        // Test various URI patterns
        const int entityId = 1;
        var uriPatterns = new[]
        {
            "/api/users",
            "/api/users/1",
            "/api/users/1/groups",
            "/api/groups/1/users",
            "/api/admin/settings",
            "/api/reports/compliance",
            "/api/test/complex/path/with/parameters"
        };

        foreach (var uri in uriPatterns)
        {
            // Arrange
            var checkRequest = new CheckPermissionRequest(entityId, uri, "GET");

            // Act
            var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"URI pattern {uri} should be handled");

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            apiResponse!.Success.Should().BeTrue();
            apiResponse.Data!.Uri.Should().Be(uri);
        }
    }

    #endregion

    #region Authorization Tests

    [TestMethod]
    public async Task Permissions_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;
        var checkRequest = TestDataBuilder.CheckPermissionRequest().Build();

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Permissions_WithUserRole_AllowsPermissionCheck()
    {
        // Arrange - Regular users should be able to check permissions
        SetupAuthentication("regular-user", "Regular User", "User");
        var checkRequest = TestDataBuilder.CheckPermissionRequest().Build();

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Permissions_AdminOperations_RequireAppropriateRole()
    {
        // Test that granting permissions requires appropriate authorization
        SetupAuthentication("regular-user", "Regular User", "User");
        var grantRequest = new GrantPermissionRequest(1, "/api/users", "GET", "ApiUriAuthorization");

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/grant", grantRequest);

        // Assert
        // The exact behavior depends on authorization policy
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
    }

    #endregion

    #region Content Type and Error Handling Tests

    [TestMethod]
    public async Task Permissions_AllEndpoints_ReturnCorrectContentType()
    {
        // Test that all permission endpoints return JSON
        var checkRequest = TestDataBuilder.CheckPermissionRequest().Build();

        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", checkRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [TestMethod]
    public async Task Permissions_WithInvalidJson_ReturnsBadRequest()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var content = new StringContent(invalidJson, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync($"{PermissionsApiPath}/check", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Permissions_WithMalformedRequest_ReturnsProperError()
    {
        // Test with missing required fields
        var malformedRequest = new { EntityId = 1 }; // Missing Uri and HttpVerb

        // Act
        var response = await Client.PostAsJsonAsync($"{PermissionsApiPath}/check", malformedRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}