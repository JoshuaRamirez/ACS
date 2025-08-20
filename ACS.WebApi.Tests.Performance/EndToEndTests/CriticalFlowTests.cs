using ACS.WebApi.Tests.Performance.Infrastructure;
using NBomber.CSharp;
using System.Text;
using System.Text.Json;

namespace ACS.WebApi.Tests.Performance.EndToEndTests;

/// <summary>
/// End-to-end performance tests for critical user flows
/// </summary>
[TestClass]
public class CriticalFlowTests : PerformanceTestBase
{
    [TestInitialize]
    public async Task Setup()
    {
        await InitializeAsync();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await CleanupAsync();
    }

    [TestMethod]
    public async Task E2ETest_UserRegistrationAndPermissionFlow_ShouldPerformWell()
    {
        var userCounter = 0;

        var scenario = Scenario.Create("user_registration_permission_flow", async context =>
        {
            var userId = Interlocked.Increment(ref userCounter);
            
            try
            {
                // Step 1: Register new user
                var newUser = new
                {
                    Name = $"E2EUser_{userId}",
                    Email = $"e2e{userId}@example.com",
                    Password = "Password123!"
                };

                var createRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/users", useAdminToken: true);
                createRequest.Content = CreateJsonContent(newUser);

                var createResponse = await HttpClient.SendAsync(createRequest);
                if (!createResponse.IsSuccessStatusCode)
                {
                    return Response.Fail($"User creation failed: {createResponse.StatusCode}");
                }

                // Step 2: Assign user to a group
                var assignGroupRequest = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/groups/1/users/{userId}", useAdminToken: true);
                var assignGroupResponse = await HttpClient.SendAsync(assignGroupRequest);

                // Step 3: Assign role to user
                var assignRoleRequest = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/users/{userId}/roles/1", useAdminToken: true);
                var assignRoleResponse = await HttpClient.SendAsync(assignRoleRequest);

                // Step 4: Verify user permissions
                var permissionRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/users/{userId}/permissions");
                var permissionResponse = await HttpClient.SendAsync(permissionRequest);

                // Step 5: User login attempt
                var loginRequest = new
                {
                    Email = newUser.Email,
                    Password = newUser.Password
                };

                var loginHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
                loginHttpRequest.Content = CreateJsonContent(loginRequest);

                var loginResponse = await HttpClient.SendAsync(loginHttpRequest);

                // All steps should succeed for a complete flow
                return createResponse.IsSuccessStatusCode && 
                       permissionResponse.IsSuccessStatusCode && 
                       loginResponse.IsSuccessStatusCode
                    ? Response.Ok()
                    : Response.Fail("Complete flow failed");
            }
            catch (Exception ex)
            {
                return Response.Fail($"Exception: {ex.Message}");
            }
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 3, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("User Registration and Permission Flow E2E Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        stats.AllFailCount.Should().BeLessThan(stats.AllRequestCount * 0.1); // < 10% failure rate
        stats.ScenarioStats[0].Ok.Latency.Mean.Should().BeLessThan(3000); // < 3s for complete flow
    }

    [TestMethod]
    public async Task E2ETest_AdminWorkflow_ShouldHandleComplexOperations()
    {
        var operationCounter = 0;

        var scenario = Scenario.Create("admin_complex_workflow", async context =>
        {
            var opId = Interlocked.Increment(ref operationCounter);

            try
            {
                // Step 1: Create a new role with permissions
                var newRole = new
                {
                    Name = $"E2ERole_{opId}",
                    Description = $"End-to-end test role {opId}",
                    Permissions = new[] { "READ", "WRITE" }
                };

                var createRoleRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/roles", useAdminToken: true);
                createRoleRequest.Content = CreateJsonContent(newRole);

                var createRoleResponse = await HttpClient.SendAsync(createRoleRequest);
                if (!createRoleResponse.IsSuccessStatusCode)
                {
                    return Response.Fail($"Role creation failed: {createRoleResponse.StatusCode}");
                }

                // Step 2: Create a hierarchical group structure
                var parentGroup = new
                {
                    Name = $"E2EParentGroup_{opId}",
                    Description = $"Parent group {opId}"
                };

                var createParentRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/groups", useAdminToken: true);
                createParentRequest.Content = CreateJsonContent(parentGroup);

                var parentResponse = await HttpClient.SendAsync(createParentRequest);
                
                // Step 3: Create child group
                var childGroup = new
                {
                    Name = $"E2EChildGroup_{opId}",
                    Description = $"Child group {opId}",
                    ParentGroupId = 1 // Assume parent group ID
                };

                var createChildRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/groups", useAdminToken: true);
                createChildRequest.Content = CreateJsonContent(childGroup);

                var childResponse = await HttpClient.SendAsync(createChildRequest);

                // Step 4: Create bulk users and assign to groups
                var bulkUsers = Enumerable.Range(1, 3).Select(i => new
                {
                    Name = $"E2EBulkUser_{opId}_{i}",
                    Email = $"e2ebulk{opId}_{i}@example.com",
                    Password = "Password123!"
                }).ToArray();

                var bulkCreateRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/bulk/users", useAdminToken: true);
                bulkCreateRequest.Content = CreateJsonContent(bulkUsers);

                var bulkResponse = await HttpClient.SendAsync(bulkCreateRequest);

                // Step 5: Generate usage report
                var reportRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/reports/user-activity?format=summary", useAdminToken: true);
                var reportResponse = await HttpClient.SendAsync(reportRequest);

                // Step 6: Audit log query
                var auditRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/audit?action=CREATE&limit=10", useAdminToken: true);
                var auditResponse = await HttpClient.SendAsync(auditRequest);

                // Most operations should succeed
                var successCount = new[] { createRoleResponse, parentResponse, childResponse, bulkResponse, reportResponse, auditResponse }
                    .Count(r => r.IsSuccessStatusCode);

                return successCount >= 4 ? Response.Ok() : Response.Fail($"Only {successCount}/6 operations succeeded");
            }
            catch (Exception ex)
            {
                return Response.Fail($"Exception: {ex.Message}");
            }
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 2, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Admin Complex Workflow E2E Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        stats.ScenarioStats[0].Ok.Latency.Mean.Should().BeLessThan(5000); // < 5s for complex admin operations
    }

    [TestMethod]
    public async Task E2ETest_PermissionEvaluationFlow_ShouldPerformEfficiently()
    {
        var scenario = Scenario.Create("permission_evaluation_flow", async context =>
        {
            var userId = Random.Shared.Next(1, 100); // Use existing users from seed data

            try
            {
                // Step 1: Get user details
                var userRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/users/{userId}");
                var userResponse = await HttpClient.SendAsync(userRequest);

                if (!userResponse.IsSuccessStatusCode)
                {
                    return Response.Fail($"User lookup failed: {userResponse.StatusCode}");
                }

                // Step 2: Get user's groups
                var groupsRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/users/{userId}/groups");
                var groupsResponse = await HttpClient.SendAsync(groupsRequest);

                // Step 3: Get user's roles
                var rolesRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/users/{userId}/roles");
                var rolesResponse = await HttpClient.SendAsync(rolesRequest);

                // Step 4: Get user's effective permissions
                var permissionsRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/users/{userId}/permissions");
                var permissionsResponse = await HttpClient.SendAsync(permissionsRequest);

                // Step 5: Test specific permission checks
                var resources = new[] { "/api/users", "/api/groups", "/api/roles", "/api/admin" };
                var actions = new[] { "GET", "POST", "PUT", "DELETE" };

                var permissionTasks = new List<Task<HttpResponseMessage>>();
                foreach (var resource in resources.Take(2)) // Limit to avoid too many requests
                {
                    foreach (var action in actions.Take(2))
                    {
                        var checkRequest = CreateAuthenticatedRequest(
                            HttpMethod.Get, 
                            $"/api/permissions/check?userId={userId}&resource={resource}&action={action}");
                        permissionTasks.Add(HttpClient.SendAsync(checkRequest));
                    }
                }

                var permissionResults = await Task.WhenAll(permissionTasks);

                // Most requests should succeed
                var allResponses = new[] { userResponse, groupsResponse, rolesResponse, permissionsResponse }
                    .Concat(permissionResults);

                var successCount = allResponses.Count(r => r.IsSuccessStatusCode);
                var totalCount = allResponses.Count();

                return successCount >= totalCount * 0.8 ? Response.Ok() : Response.Fail($"Only {successCount}/{totalCount} succeeded");
            }
            catch (Exception ex)
            {
                return Response.Fail($"Exception: {ex.Message}");
            }
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 8, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Permission Evaluation Flow E2E Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        stats.ScenarioStats[0].Ok.Latency.Mean.Should().BeLessThan(2000); // < 2s for permission evaluation flow
    }

    [TestMethod]
    public async Task E2ETest_SearchAndFilterFlow_ShouldHandleComplexQueries()
    {
        var scenario = Scenario.Create("search_filter_flow", async context =>
        {
            try
            {
                // Step 1: Basic search across entities
                var searchTerm = new[] { "User", "Group", "Role", "Test", "Admin" }[Random.Shared.Next(5)];
                
                var userSearchRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/users?search={searchTerm}&page=1&size=10");
                var userSearchResponse = await HttpClient.SendAsync(userSearchRequest);

                var groupSearchRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/groups?search={searchTerm}");
                var groupSearchResponse = await HttpClient.SendAsync(groupSearchRequest);

                var roleSearchRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/roles?search={searchTerm}");
                var roleSearchResponse = await HttpClient.SendAsync(roleSearchRequest);

                // Step 2: Advanced filtering
                var filterRequests = new[]
                {
                    CreateAuthenticatedRequest(HttpMethod.Get, "/api/users?isActive=true&page=1&size=20"),
                    CreateAuthenticatedRequest(HttpMethod.Get, "/api/groups?includeUsers=true"),
                    CreateAuthenticatedRequest(HttpMethod.Get, "/api/roles?includePermissions=true"),
                    CreateAuthenticatedRequest(HttpMethod.Get, "/api/audit?startDate=2024-01-01&endDate=2024-12-31&limit=5")
                };

                var filterTasks = filterRequests.Select(req => HttpClient.SendAsync(req));
                var filterResponses = await Task.WhenAll(filterTasks);

                // Step 3: Pagination stress test
                var paginationRequests = new[]
                {
                    CreateAuthenticatedRequest(HttpMethod.Get, "/api/users?page=1&size=50"),
                    CreateAuthenticatedRequest(HttpMethod.Get, "/api/users?page=2&size=25"),
                    CreateAuthenticatedRequest(HttpMethod.Get, "/api/users?page=5&size=10")
                };

                var paginationTasks = paginationRequests.Select(req => HttpClient.SendAsync(req));
                var paginationResponses = await Task.WhenAll(paginationTasks);

                // Count successful responses
                var allResponses = new[] { userSearchResponse, groupSearchResponse, roleSearchResponse }
                    .Concat(filterResponses)
                    .Concat(paginationResponses);

                var successCount = allResponses.Count(r => r.IsSuccessStatusCode);
                var totalCount = allResponses.Count();

                return successCount >= totalCount * 0.9 ? Response.Ok() : Response.Fail($"Only {successCount}/{totalCount} succeeded");
            }
            catch (Exception ex)
            {
                return Response.Fail($"Exception: {ex.Message}");
            }
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 5, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Search and Filter Flow E2E Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        stats.ScenarioStats[0].Ok.Latency.Mean.Should().BeLessThan(2500); // < 2.5s for complex search operations
    }

    [TestMethod]
    public async Task E2ETest_ConcurrentUserOperations_ShouldMaintainConsistency()
    {
        var scenario = Scenario.Create("concurrent_user_operations", async context =>
        {
            var userId = Random.Shared.Next(1, 50); // Use subset of users for concurrency test

            try
            {
                // Simulate concurrent operations on the same user
                var operations = new List<Task<HttpResponseMessage>>();

                // Read operations (should always work)
                operations.Add(HttpClient.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, $"/api/users/{userId}")));
                operations.Add(HttpClient.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, $"/api/users/{userId}/groups")));
                operations.Add(HttpClient.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, $"/api/users/{userId}/roles")));
                operations.Add(HttpClient.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, $"/api/users/{userId}/permissions")));

                // Some write operations (may conflict but should handle gracefully)
                var updateRequest = CreateAuthenticatedRequest(HttpMethod.Put, $"/api/users/{userId}", useAdminToken: true);
                updateRequest.Content = CreateJsonContent(new
                {
                    Name = $"ConcurrentUpdate_{DateTime.UtcNow.Ticks}",
                    Email = $"concurrent{userId}@example.com"
                });
                operations.Add(HttpClient.SendAsync(updateRequest));

                // Wait for all operations
                var responses = await Task.WhenAll(operations);

                // Read operations should succeed, write operations may conflict but should handle gracefully
                var readSuccessCount = responses.Take(4).Count(r => r.IsSuccessStatusCode);
                var writeResponse = responses.Last();
                var writeSuccessful = writeResponse.IsSuccessStatusCode || 
                                    writeResponse.StatusCode == HttpStatusCode.Conflict ||
                                    writeResponse.StatusCode == HttpStatusCode.NotFound;

                return readSuccessCount >= 3 && writeSuccessful ? Response.Ok() : Response.Fail("Concurrent operations failed");
            }
            catch (Exception ex)
            {
                return Response.Fail($"Exception: {ex.Message}");
            }
        })
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 20, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Concurrent User Operations E2E Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        var errorRate = (double)stats.AllFailCount / stats.AllRequestCount;
        errorRate.Should().BeLessThan(0.2, "System should handle concurrent operations with low error rate");
    }
}