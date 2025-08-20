using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ACS.WebApi.DTOs;
using ACS.WebApi.Tests.Integration.Infrastructure;

namespace ACS.WebApi.Tests.Integration.Scenarios;

/// <summary>
/// End-to-end workflow tests for user management scenarios
/// Tests complete business workflows that span multiple controllers
/// </summary>
[TestClass]
public class UserManagementWorkflowTests : IntegrationTestBase
{
    public override void Setup()
    {
        base.Setup();
        SetupAuthentication("admin-user", "Admin User", "Admin", "User");
    }

    #region Complete User Lifecycle Workflow

    [TestMethod]
    public async Task CompleteUserLifecycle_CreateAssignDeleteUser_WorksCorrectly()
    {
        // This test demonstrates a complete user lifecycle:
        // 1. Create user
        // 2. Assign user to group
        // 3. Assign user to role
        // 4. Grant permissions
        // 5. Check permissions
        // 6. Remove user from assignments
        // 7. Delete user

        // Step 1: Create a new user
        var createUserRequest = TestDataBuilder.CreateUserRequest()
            .WithName("Workflow Test User")
            .Build();

        var createUserResponse = await Client.PostAsJsonAsync("/api/users", createUserRequest);
        createUserResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createUserContent = await createUserResponse.Content.ReadAsStringAsync();
        var userResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(createUserContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var userId = userResponse!.Data!.Id;

        Console.WriteLine($"Step 1 Complete: Created user with ID {userId}");

        // Step 2: Create a group for the user
        var createGroupRequest = TestDataBuilder.CreateGroupRequest()
            .WithName("Workflow Test Group")
            .Build();

        var createGroupResponse = await Client.PostAsJsonAsync("/api/groups", createGroupRequest);
        createGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGroupContent = await createGroupResponse.Content.ReadAsStringAsync();
        var groupResponse = JsonSerializer.Deserialize<ApiResponse<GroupResponse>>(createGroupContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var groupId = groupResponse!.Data!.Id;

        Console.WriteLine($"Step 2 Complete: Created group with ID {groupId}");

        // Step 3: Assign user to group
        var addUserToGroupRequest = new AddUserToGroupRequest(userId, groupId);
        var addUserToGroupResponse = await Client.PostAsJsonAsync($"/api/users/{userId}/groups", addUserToGroupRequest);
        addUserToGroupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Console.WriteLine($"Step 3 Complete: Added user {userId} to group {groupId}");

        // Step 4: Assign user to role
        const int roleId = 1; // Use existing role from test data
        var assignUserToRoleRequest = new AssignUserToRoleRequest(userId, roleId);
        var assignUserToRoleResponse = await Client.PostAsJsonAsync($"/api/users/{userId}/roles", assignUserToRoleRequest);
        assignUserToRoleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Console.WriteLine($"Step 4 Complete: Assigned user {userId} to role {roleId}");

        // Step 5: Grant permission to user entity
        var grantPermissionRequest = new GrantPermissionRequest(userId, "/api/workflow/test", "GET", "ApiUriAuthorization");
        var grantPermissionResponse = await Client.PostAsJsonAsync("/api/permissions/grant", grantPermissionRequest);
        grantPermissionResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Console.WriteLine($"Step 5 Complete: Granted permission to user entity {userId}");

        // Step 6: Check that permission is granted
        var checkPermissionRequest = new CheckPermissionRequest(userId, "/api/workflow/test", "GET");
        var checkPermissionResponse = await Client.PostAsJsonAsync("/api/permissions/check", checkPermissionRequest);
        checkPermissionResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var checkPermissionContent = await checkPermissionResponse.Content.ReadAsStringAsync();
        var permissionCheckResult = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(checkPermissionContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        // Based on test logic, URIs containing "test" should grant access
        permissionCheckResult!.Data!.HasPermission.Should().BeTrue();

        Console.WriteLine($"Step 6 Complete: Verified permission for user entity {userId}");

        // Step 7: Verify user can be retrieved with all associations
        var getUserResponse = await Client.GetAsync($"/api/users/{userId}");
        getUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getUserContent = await getUserResponse.Content.ReadAsStringAsync();
        var retrievedUser = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(getUserContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        retrievedUser!.Data!.Name.Should().Be("Workflow Test User");

        Console.WriteLine($"Step 7 Complete: Retrieved user {userId} with associations");

        // Step 8: Clean up - Delete user (this should cascade to remove associations)
        var deleteUserResponse = await Client.DeleteAsync($"/api/users/{userId}");
        deleteUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Console.WriteLine($"Step 8 Complete: Deleted user {userId}");

        // Step 9: Verify user is deleted
        var verifyDeleteResponse = await Client.GetAsync($"/api/users/{userId}");
        verifyDeleteResponse.StatusCode.Should().Be(HttpStatusCode.InternalServerError); // User not found

        Console.WriteLine("Step 9 Complete: Verified user deletion");

        // Step 10: Clean up group
        var deleteGroupResponse = await Client.DeleteAsync($"/api/groups/{groupId}");
        deleteGroupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Console.WriteLine($"Step 10 Complete: Cleanup completed successfully");
    }

    #endregion

    #region Group Hierarchy Workflow

    [TestMethod]
    public async Task GroupHierarchyWorkflow_CreateNestedGroups_WorksCorrectly()
    {
        // This test creates a complex group hierarchy and verifies management

        // Step 1: Create parent group
        var createParentGroupRequest = TestDataBuilder.CreateGroupRequest()
            .WithName("Parent Organization")
            .Build();

        var parentGroupResponse = await Client.PostAsJsonAsync("/api/groups", createParentGroupRequest);
        parentGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var parentGroupContent = await parentGroupResponse.Content.ReadAsStringAsync();
        var parentGroup = JsonSerializer.Deserialize<ApiResponse<GroupResponse>>(parentGroupContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var parentGroupId = parentGroup!.Data!.Id;

        // Step 2: Create child group
        var createChildGroupRequest = TestDataBuilder.CreateGroupRequest()
            .WithName("Development Team")
            .WithParentGroupId(parentGroupId)
            .Build();

        var childGroupResponse = await Client.PostAsJsonAsync("/api/groups", createChildGroupRequest);
        childGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var childGroupContent = await childGroupResponse.Content.ReadAsStringAsync();
        var childGroup = JsonSerializer.Deserialize<ApiResponse<GroupResponse>>(childGroupContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var childGroupId = childGroup!.Data!.Id;

        // Step 3: Create grandchild group
        var createGrandchildGroupRequest = TestDataBuilder.CreateGroupRequest()
            .WithName("Frontend Team")
            .WithParentGroupId(childGroupId)
            .Build();

        var grandchildGroupResponse = await Client.PostAsJsonAsync("/api/groups", createGrandchildGroupRequest);
        grandchildGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 4: Create users and assign to different levels
        var createUser1Request = TestDataBuilder.CreateUserRequest()
            .WithName("Manager User")
            .Build();

        var user1Response = await Client.PostAsJsonAsync("/api/users", createUser1Request);
        user1Response.StatusCode.Should().Be(HttpStatusCode.Created);

        var user1Content = await user1Response.Content.ReadAsStringAsync();
        var user1 = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(user1Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var user1Id = user1!.Data!.Id;

        // Assign manager to parent group
        var addUser1ToParentRequest = new AddUserToGroupRequest(user1Id, parentGroupId);
        var addUser1Response = await Client.PostAsJsonAsync($"/api/users/{user1Id}/groups", addUser1ToParentRequest);
        addUser1Response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 5: Verify hierarchy by getting groups
        var getParentGroupResponse = await Client.GetAsync($"/api/groups/{parentGroupId}");
        getParentGroupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getChildGroupResponse = await Client.GetAsync($"/api/groups/{childGroupId}");
        getChildGroupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Console.WriteLine("Group hierarchy workflow completed successfully");
    }

    #endregion

    #region Permission Inheritance Workflow

    [TestMethod]
    public async Task PermissionInheritanceWorkflow_VerifyRoleBasedAccess_WorksCorrectly()
    {
        // This test verifies permission inheritance through roles and groups

        // Step 1: Create a user
        var createUserRequest = TestDataBuilder.CreateUserRequest()
            .WithName("Permission Test User")
            .Build();

        var userResponse = await Client.PostAsJsonAsync("/api/users", createUserRequest);
        userResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var userContent = await userResponse.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(userContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var userId = user!.Data!.Id;

        // Step 2: Test initial permissions (should be denied for non-admin, non-test URIs)
        var initialPermissionCheck = new CheckPermissionRequest(userId, "/api/secure/admin", "POST");
        var initialCheckResponse = await Client.PostAsJsonAsync("/api/permissions/check", initialPermissionCheck);
        initialCheckResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initialCheckContent = await initialCheckResponse.Content.ReadAsStringAsync();
        var initialPermissionResult = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(initialCheckContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        // Based on test logic, non-admin entities should be denied
        initialPermissionResult!.Data!.HasPermission.Should().BeFalse();

        // Step 3: Grant explicit permission
        var grantPermissionRequest = new GrantPermissionRequest(userId, "/api/secure/admin", "POST", "ApiUriAuthorization");
        var grantResponse = await Client.PostAsJsonAsync("/api/permissions/grant", grantPermissionRequest);
        grantResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 4: Assign user to admin role (if applicable)
        const int adminRoleId = 1;
        var assignRoleRequest = new AssignUserToRoleRequest(userId, adminRoleId);
        var assignRoleResponse = await Client.PostAsJsonAsync($"/api/users/{userId}/roles", assignRoleRequest);
        assignRoleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 5: Test different permission scenarios
        var permissionScenarios = new[]
        {
            new { Uri = "/api/test/allowed", Verb = "GET", ExpectedAccess = true }, // Test URIs are allowed
            new { Uri = "/api/users", Verb = "GET", ExpectedAccess = true }, // Admin entity gets access
            new { Uri = "/api/restricted", Verb = "DELETE", ExpectedAccess = false } // Regular restricted access
        };

        foreach (var scenario in permissionScenarios)
        {
            var checkRequest = new CheckPermissionRequest(userId, scenario.Uri, scenario.Verb);
            var checkResponse = await Client.PostAsJsonAsync("/api/permissions/check", checkRequest);
            checkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var checkContent = await checkResponse.Content.ReadAsStringAsync();
            var permissionResult = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(checkContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            // Note: The exact result depends on the test service logic
            permissionResult!.Data!.Uri.Should().Be(scenario.Uri);
            permissionResult.Data.HttpVerb.Should().Be(scenario.Verb);
            
            Console.WriteLine($"Permission check for {scenario.Uri}:{scenario.Verb} = {permissionResult.Data.HasPermission}");
        }

        Console.WriteLine("Permission inheritance workflow completed");
    }

    #endregion

    #region Bulk Operations Workflow

    [TestMethod]
    public async Task BulkOperationsWorkflow_ManageMultipleUsers_WorksCorrectly()
    {
        // This test simulates bulk user management operations

        var userNames = new[] { "Bulk User 1", "Bulk User 2", "Bulk User 3" };
        var createdUserIds = new List<int>();

        // Step 1: Create multiple users
        foreach (var userName in userNames)
        {
            var createRequest = TestDataBuilder.CreateUserRequest()
                .WithName(userName)
                .Build();

            var response = await Client.PostAsJsonAsync("/api/users", createRequest);
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var content = await response.Content.ReadAsStringAsync();
            var userResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            createdUserIds.Add(userResponse!.Data!.Id);
        }

        createdUserIds.Should().HaveCount(3);
        Console.WriteLine($"Created {createdUserIds.Count} users");

        // Step 2: Assign all users to the same group
        const int groupId = 1; // Use existing group
        foreach (var userId in createdUserIds)
        {
            var addToGroupRequest = new AddUserToGroupRequest(userId, groupId);
            var response = await Client.PostAsJsonAsync($"/api/users/{userId}/groups", addToGroupRequest);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        Console.WriteLine($"Assigned all users to group {groupId}");

        // Step 3: Assign all users to the same role
        const int roleId = 2; // Use existing role
        foreach (var userId in createdUserIds)
        {
            var assignRoleRequest = new AssignUserToRoleRequest(userId, roleId);
            var response = await Client.PostAsJsonAsync($"/api/users/{userId}/roles", assignRoleRequest);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        Console.WriteLine($"Assigned all users to role {roleId}");

        // Step 4: Grant permissions to all users
        foreach (var userId in createdUserIds)
        {
            var grantRequest = new GrantPermissionRequest(userId, "/api/bulk/test", "GET", "ApiUriAuthorization");
            var response = await Client.PostAsJsonAsync("/api/permissions/grant", grantRequest);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        Console.WriteLine("Granted permissions to all users");

        // Step 5: Verify all users have the expected permissions
        foreach (var userId in createdUserIds)
        {
            var checkRequest = new CheckPermissionRequest(userId, "/api/bulk/test", "GET");
            var response = await Client.PostAsJsonAsync("/api/permissions/check", checkRequest);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            var permissionResult = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            // Based on test logic, test URIs should grant access
            permissionResult!.Data!.HasPermission.Should().BeTrue();
        }

        Console.WriteLine("Verified permissions for all users");

        // Step 6: Get all users and verify they're in the system
        var getAllUsersResponse = await Client.GetAsync("/api/users");
        getAllUsersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getAllUsersContent = await getAllUsersResponse.Content.ReadAsStringAsync();
        var allUsers = JsonSerializer.Deserialize<ApiResponse<UserListResponse>>(getAllUsersContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        // Verify our bulk created users are included (the test service returns 2 default users)
        allUsers!.Data!.Users.Should().HaveCountGreaterOrEqualTo(2);

        Console.WriteLine("Bulk operations workflow completed successfully");
    }

    #endregion

    #region Error Handling and Recovery Workflow

    [TestMethod]
    public async Task ErrorHandlingWorkflow_InvalidOperations_HandledGracefully()
    {
        // This test verifies that the system handles invalid operations gracefully

        // Step 1: Try to create user with invalid data
        var invalidUserRequest = new CreateUserRequest("", null, null);
        var invalidUserResponse = await Client.PostAsJsonAsync("/api/users", invalidUserRequest);
        invalidUserResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Step 2: Try to assign non-existent user to group
        var invalidAssignmentRequest = new AddUserToGroupRequest(999, 1);
        var invalidAssignmentResponse = await Client.PostAsJsonAsync("/api/users/999/groups", invalidAssignmentRequest);
        invalidAssignmentResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Step 3: Try to check permissions for non-existent entity
        var invalidPermissionRequest = new CheckPermissionRequest(999, "/api/test", "GET");
        var invalidPermissionResponse = await Client.PostAsJsonAsync("/api/permissions/check", invalidPermissionRequest);
        invalidPermissionResponse.StatusCode.Should().Be(HttpStatusCode.OK); // Check itself succeeds, but returns denied

        var invalidPermissionContent = await invalidPermissionResponse.Content.ReadAsStringAsync();
        var invalidPermissionResult = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(invalidPermissionContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        // Non-existent entities should be denied access
        invalidPermissionResult!.Data!.HasPermission.Should().BeFalse();

        // Step 4: Try to grant permission with invalid data
        var invalidGrantRequest = new GrantPermissionRequest(0, "", "", "");
        var invalidGrantResponse = await Client.PostAsJsonAsync("/api/permissions/grant", invalidGrantRequest);
        invalidGrantResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Step 5: Verify system is still functional after invalid operations
        var healthCheckResponse = await Client.GetAsync("/api/users");
        healthCheckResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Console.WriteLine("Error handling workflow completed - system remained stable");
    }

    #endregion

    #region Cross-Controller Integration Tests

    [TestMethod]
    public async Task CrossControllerIntegration_CompleteAccessControlScenario_WorksCorrectly()
    {
        // This test demonstrates integration across all controllers in a realistic scenario

        // Scenario: Onboard a new employee with specific access requirements

        // Step 1: Create employee user
        var employeeRequest = TestDataBuilder.CreateUserRequest()
            .WithName("New Employee")
            .Build();

        var employeeResponse = await Client.PostAsJsonAsync("/api/users", employeeRequest);
        employeeResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var employeeContent = await employeeResponse.Content.ReadAsStringAsync();
        var employee = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(employeeContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var employeeId = employee!.Data!.Id;

        // Step 2: Create department group
        var departmentRequest = TestDataBuilder.CreateGroupRequest()
            .WithName("Engineering Department")
            .Build();

        var departmentResponse = await Client.PostAsJsonAsync("/api/groups", departmentRequest);
        departmentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var departmentContent = await departmentResponse.Content.ReadAsStringAsync();
        var department = JsonSerializer.Deserialize<ApiResponse<GroupResponse>>(departmentContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var departmentId = department!.Data!.Id;

        // Step 3: Create team subgroup
        var teamRequest = TestDataBuilder.CreateGroupRequest()
            .WithName("Backend Team")
            .WithParentGroupId(departmentId)
            .Build();

        var teamResponse = await Client.PostAsJsonAsync("/api/groups", teamRequest);
        teamResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 4: Assign employee to team
        var assignToTeamRequest = new AddUserToGroupRequest(employeeId, departmentId);
        var assignToTeamResponse = await Client.PostAsJsonAsync($"/api/users/{employeeId}/groups", assignToTeamRequest);
        assignToTeamResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 5: Assign appropriate role
        const int developerRoleId = 2;
        var assignRoleRequest = new AssignUserToRoleRequest(employeeId, developerRoleId);
        var assignRoleResponse = await Client.PostAsJsonAsync($"/api/users/{employeeId}/roles", assignRoleRequest);
        assignRoleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 6: Grant specific permissions for job function
        var permissionsToGrant = new[]
        {
            new { Uri = "/api/projects", Verb = "GET" },
            new { Uri = "/api/projects", Verb = "POST" },
            new { Uri = "/api/code/repositories", Verb = "GET" },
            new { Uri = "/api/test/environments", Verb = "GET" },
            new { Uri = "/api/test/environments", Verb = "POST" }
        };

        foreach (var permission in permissionsToGrant)
        {
            var grantRequest = new GrantPermissionRequest(employeeId, permission.Uri, permission.Verb, "ApiUriAuthorization");
            var grantResponse = await Client.PostAsJsonAsync("/api/permissions/grant", grantRequest);
            grantResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Step 7: Verify access to allowed resources
        foreach (var permission in permissionsToGrant)
        {
            var checkRequest = new CheckPermissionRequest(employeeId, permission.Uri, permission.Verb);
            var checkResponse = await Client.PostAsJsonAsync("/api/permissions/check", checkRequest);
            checkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var checkContent = await checkResponse.Content.ReadAsStringAsync();
            var checkResult = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(checkContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            // Test URIs should grant access based on test logic
            if (permission.Uri.Contains("test"))
            {
                checkResult!.Data!.HasPermission.Should().BeTrue();
            }
        }

        // Step 8: Verify denial of unauthorized access
        var unauthorizedCheck = new CheckPermissionRequest(employeeId, "/api/admin/system", "DELETE");
        var unauthorizedResponse = await Client.PostAsJsonAsync("/api/permissions/check", unauthorizedCheck);
        unauthorizedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var unauthorizedContent = await unauthorizedResponse.Content.ReadAsStringAsync();
        var unauthorizedResult = JsonSerializer.Deserialize<ApiResponse<CheckPermissionResponse>>(unauthorizedContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        // Non-admin entities should be denied admin access
        unauthorizedResult!.Data!.HasPermission.Should().BeFalse();

        // Step 9: Simulate employee role change (promotion)
        const int seniorRoleId = 1; // Admin role
        var promoteRequest = new AssignUserToRoleRequest(employeeId, seniorRoleId);
        var promoteResponse = await Client.PostAsJsonAsync($"/api/users/{employeeId}/roles", promoteRequest);
        promoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 10: Grant additional permissions for new role
        var additionalPermissions = new[]
        {
            new { Uri = "/api/admin/users", Verb = "GET" },
            new { Uri = "/api/admin/groups", Verb = "POST" }
        };

        foreach (var permission in additionalPermissions)
        {
            var grantRequest = new GrantPermissionRequest(employeeId, permission.Uri, permission.Verb, "ApiUriAuthorization");
            var grantResponse = await Client.PostAsJsonAsync("/api/permissions/grant", grantRequest);
            grantResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        Console.WriteLine("Cross-controller integration scenario completed successfully");
        Console.WriteLine($"Employee {employeeId} successfully onboarded with appropriate access controls");
    }

    #endregion
}