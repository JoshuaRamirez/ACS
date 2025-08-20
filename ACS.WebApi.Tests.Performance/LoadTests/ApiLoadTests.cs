using ACS.WebApi.Tests.Performance.Infrastructure;
using NBomber.CSharp;
using System.Text;
using System.Text.Json;

namespace ACS.WebApi.Tests.Performance.LoadTests;

/// <summary>
/// Load tests for API endpoints to measure performance under normal load
/// </summary>
[TestClass]
public class ApiLoadTests : PerformanceTestBase
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
    public async Task LoadTest_GetUsers_ShouldHandleNormalLoad()
    {
        var scenario = Scenario.Create("get_users_load_test", async context =>
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/users?page=1&size=20");
            var response = await HttpClient.SendAsync(request);

            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Get Users Load Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        stats.AllFailCount.Should().Be(0);
        stats.ScenarioStats[0].Ok.Latency.Mean.Should().BeLessThan(500); // < 500ms mean latency
    }

    [TestMethod]
    public async Task LoadTest_CreateUsers_ShouldHandleNormalLoad()
    {
        var userCounter = 0;

        var scenario = Scenario.Create("create_users_load_test", async context =>
        {
            var userId = Interlocked.Increment(ref userCounter);
            var newUser = new
            {
                Name = $"LoadTestUser_{userId}",
                Email = $"loadtest{userId}@example.com",
                Password = "Password123!"
            };

            var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/users", useAdminToken: true);
            request.Content = CreateJsonContent(newUser);

            var response = await HttpClient.SendAsync(request);
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 5, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Create Users Load Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        stats.AllFailCount.Should().Be(0);
        stats.ScenarioStats[0].Ok.Latency.Mean.Should().BeLessThan(1000); // < 1s mean latency
    }

    [TestMethod]
    public async Task LoadTest_MixedApiOperations_ShouldHandleVariedLoad()
    {
        var readScenario = Scenario.Create("read_operations", async context =>
        {
            var operations = new[]
            {
                "/api/users?page=1&size=10",
                "/api/groups",
                "/api/roles",
                "/api/health"
            };

            var randomOperation = operations[Random.Shared.Next(operations.Length)];
            var request = CreateAuthenticatedRequest(HttpMethod.Get, randomOperation);
            var response = await HttpClient.SendAsync(request);

            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithWeight(70) // 70% read operations
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 15, during: GetTestDuration())
        );

        var writeCounter = 0;
        var writeScenario = Scenario.Create("write_operations", async context =>
        {
            var operationType = Random.Shared.Next(3);
            var id = Interlocked.Increment(ref writeCounter);

            HttpRequestMessage request;
            
            switch (operationType)
            {
                case 0: // Create user
                    var newUser = new
                    {
                        Name = $"MixedTestUser_{id}",
                        Email = $"mixed{id}@example.com",
                        Password = "Password123!"
                    };
                    request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/users", useAdminToken: true);
                    request.Content = CreateJsonContent(newUser);
                    break;

                case 1: // Create group
                    var newGroup = new
                    {
                        Name = $"MixedTestGroup_{id}",
                        Description = $"Load test group {id}"
                    };
                    request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/groups", useAdminToken: true);
                    request.Content = CreateJsonContent(newGroup);
                    break;

                default: // Create role
                    var newRole = new
                    {
                        Name = $"MixedTestRole_{id}",
                        Description = $"Load test role {id}"
                    };
                    request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/roles", useAdminToken: true);
                    request.Content = CreateJsonContent(newRole);
                    break;
            }

            var response = await HttpClient.SendAsync(request);
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithWeight(30) // 30% write operations
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 5, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(readScenario, writeScenario)
            .Run();

        PrintScenarioResults("Mixed API Operations Load Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        stats.AllFailCount.Should().BeLessThan(stats.AllRequestCount * 0.05); // < 5% failure rate
    }

    [TestMethod]
    public async Task LoadTest_SearchOperations_ShouldHandleSearchLoad()
    {
        var searchTerms = new[]
        {
            "User_1", "User_2", "User_3", "Test", "Group", "Role", 
            "admin", "test.com", "User_10", "Group_5"
        };

        var scenario = Scenario.Create("search_operations", async context =>
        {
            var searchTerm = searchTerms[Random.Shared.Next(searchTerms.Length)];
            var endpoint = Random.Shared.Next(3) switch
            {
                0 => $"/api/users?search={searchTerm}",
                1 => $"/api/groups?search={searchTerm}",
                _ => $"/api/roles?search={searchTerm}"
            };

            var request = CreateAuthenticatedRequest(HttpMethod.Get, endpoint);
            var response = await HttpClient.SendAsync(request);

            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 8, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Search Operations Load Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        stats.AllFailCount.Should().Be(0);
        stats.ScenarioStats[0].Ok.Latency.Mean.Should().BeLessThan(800); // < 800ms for search operations
    }

    [TestMethod]
    public async Task LoadTest_PaginationOperations_ShouldHandlePageLoad()
    {
        var scenario = Scenario.Create("pagination_operations", async context =>
        {
            var page = Random.Shared.Next(1, 11); // Pages 1-10
            var size = Random.Shared.Next(1, 4) * 10; // 10, 20, or 30 items per page
            
            var endpoints = new[]
            {
                $"/api/users?page={page}&size={size}",
                $"/api/groups?page={page}&size={size}",
                $"/api/roles?page={page}&size={size}"
            };

            var endpoint = endpoints[Random.Shared.Next(endpoints.Length)];
            var request = CreateAuthenticatedRequest(HttpMethod.Get, endpoint);
            var response = await HttpClient.SendAsync(request);

            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 12, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Pagination Operations Load Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        stats.AllFailCount.Should().Be(0);
        stats.ScenarioStats[0].Ok.Latency.Mean.Should().BeLessThan(600); // < 600ms for pagination
    }

    [TestMethod]
    public async Task LoadTest_AuthenticationOperations_ShouldHandleAuthLoad()
    {
        var userCounter = 0;

        var scenario = Scenario.Create("authentication_operations", async context =>
        {
            var userId = userCounter % 1000 + 1; // Cycle through existing users
            Interlocked.Increment(ref userCounter);

            var loginRequest = new
            {
                Email = $"user{userId}@test.com",
                Password = "Password123!"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
            request.Content = CreateJsonContent(loginRequest);

            var response = await HttpClient.SendAsync(request);
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 6, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Authentication Operations Load Test", stats);

        // Assertions
        stats.AllOkCount.Should().BeGreaterThan(0);
        stats.AllFailCount.Should().BeLessThan(stats.AllRequestCount * 0.1); // < 10% failure rate (some users might not exist)
        stats.ScenarioStats[0].Ok.Latency.Mean.Should().BeLessThan(1500); // < 1.5s for authentication
    }
}