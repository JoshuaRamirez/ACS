using ACS.WebApi.Tests.Performance.Infrastructure;
using NBomber.CSharp;

namespace ACS.WebApi.Tests.Performance.StressTests;

/// <summary>
/// Stress tests to determine system breaking points and behavior under extreme load
/// </summary>
[TestClass]
public class ApiStressTests : PerformanceTestBase
{
    [TestInitialize]
    public async Task Setup()
    {
        await InitializeAsync(enableDetailedLogging: true, useInMemoryDatabase: false);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await CleanupAsync();
    }

    [TestMethod]
    public async Task StressTest_GradualLoadIncrease_ShouldIdentifyBreakingPoint()
    {
        var scenario = Scenario.Create("gradual_load_increase", async context =>
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/users?page=1&size=10");
            var response = await HttpClient.SendAsync(request);

            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            // Gradually increase load to find breaking point
            Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromSeconds(30)),
            Simulation.InjectPerSec(rate: 25, during: TimeSpan.FromSeconds(30)),
            Simulation.InjectPerSec(rate: 50, during: TimeSpan.FromSeconds(30)),
            Simulation.InjectPerSec(rate: 100, during: TimeSpan.FromSeconds(30)),
            Simulation.InjectPerSec(rate: 200, during: TimeSpan.FromSeconds(30))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Gradual Load Increase Stress Test", stats);

        // Document the breaking point
        Console.WriteLine($"Error Rate: {(double)stats.AllFailCount / stats.AllRequestCount * 100:F2}%");
        
        // The system should handle at least moderate load without complete failure
        (stats.AllOkCount > 0).Should().BeTrue("System should handle some requests even under stress");
    }

    [TestMethod]
    public async Task StressTest_HighConcurrency_ShouldMaintainStability()
    {
        var scenario = Scenario.Create("high_concurrency", async context =>
        {
            var endpoints = new[]
            {
                "/api/users?page=1&size=5",
                "/api/groups",
                "/api/roles",
                "/api/health"
            };

            var endpoint = endpoints[Random.Shared.Next(endpoints.Length)];
            var request = CreateAuthenticatedRequest(HttpMethod.Get, endpoint);
            var response = await HttpClient.SendAsync(request);

            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: GetMaxVirtualUsers(), during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("High Concurrency Stress Test", stats);

        // Assertions for stress conditions
        stats.AllOkCount.Should().BeGreaterThan(0, "System should process some requests under high concurrency");
        var errorRate = (double)stats.AllFailCount / stats.AllRequestCount;
        errorRate.Should().BeLessThan(0.5, "Error rate should be less than 50% even under stress");
    }

    [TestMethod]
    public async Task StressTest_DatabaseIntensiveOperations_ShouldHandleDbLoad()
    {
        var userCounter = 0;

        var readScenario = Scenario.Create("database_read_stress", async context =>
        {
            // Complex queries that stress the database
            var operations = new[]
            {
                "/api/users?search=User&page=1&size=50",
                "/api/groups?includeUsers=true",
                "/api/roles?includePermissions=true",
                "/api/audit?startDate=2024-01-01&endDate=2024-12-31"
            };

            var operation = operations[Random.Shared.Next(operations.Length)];
            var request = CreateAuthenticatedRequest(HttpMethod.Get, operation);
            var response = await HttpClient.SendAsync(request);

            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithWeight(80)
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 30, during: GetTestDuration())
        );

        var writeScenario = Scenario.Create("database_write_stress", async context =>
        {
            var id = Interlocked.Increment(ref userCounter);
            var newUser = new
            {
                Name = $"StressTestUser_{id}",
                Email = $"stress{id}@example.com",
                Password = "Password123!"
            };

            var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/users", useAdminToken: true);
            request.Content = CreateJsonContent(newUser);

            var response = await HttpClient.SendAsync(request);
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithWeight(20)
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(readScenario, writeScenario)
            .Run();

        PrintScenarioResults("Database Intensive Operations Stress Test", stats);

        // Database operations should complete even under stress
        stats.AllOkCount.Should().BeGreaterThan(0);
        var errorRate = (double)stats.AllFailCount / stats.AllRequestCount;
        errorRate.Should().BeLessThan(0.3, "Database should handle stress with acceptable error rate");
    }

    [TestMethod]
    public async Task StressTest_MemoryIntensiveOperations_ShouldHandleMemoryPressure()
    {
        var scenario = Scenario.Create("memory_intensive_operations", async context =>
        {
            // Operations that might consume more memory
            var operations = new[]
            {
                "/api/users?page=1&size=1000", // Large page size
                "/api/reports/user-activity?format=detailed&includeAudit=true",
                "/api/bulk/users", // Bulk operations
                "/api/export/users?format=json&includeAll=true"
            };

            var operation = operations[Random.Shared.Next(operations.Length)];
            var request = CreateAuthenticatedRequest(HttpMethod.Get, operation, useAdminToken: true);
            var response = await HttpClient.SendAsync(request);

            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 20, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Memory Intensive Operations Stress Test", stats);

        // System should handle memory-intensive operations
        stats.AllOkCount.Should().BeGreaterThan(0);
        var errorRate = (double)stats.AllFailCount / stats.AllRequestCount;
        errorRate.Should().BeLessThan(0.4, "System should handle memory pressure with acceptable error rate");
    }

    [TestMethod]
    public async Task StressTest_BurstTraffic_ShouldHandleTrafficSpikes()
    {
        var scenario = Scenario.Create("burst_traffic", async context =>
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/users");
            var response = await HttpClient.SendAsync(request);

            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            // Simulate traffic bursts
            Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromSeconds(10)),   // Calm period
            Simulation.InjectPerSec(rate: 100, during: TimeSpan.FromSeconds(15)), // Burst
            Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromSeconds(10)),   // Calm period
            Simulation.InjectPerSec(rate: 150, during: TimeSpan.FromSeconds(15)), // Larger burst
            Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromSeconds(10))    // Recovery
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Burst Traffic Stress Test", stats);

        // System should recover from traffic bursts
        stats.AllOkCount.Should().BeGreaterThan(0);
        Console.WriteLine($"System handled {stats.AllOkCount} requests during traffic bursts");
    }

    [TestMethod]
    public async Task StressTest_LongRunningConnections_ShouldHandleConnectionLoad()
    {
        var scenario = Scenario.Create("long_running_connections", async context =>
        {
            // Simulate long-running operations
            await Task.Delay(Random.Shared.Next(100, 1000)); // Random delay 100ms-1s

            var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/users?page=1&size=10");
            var response = await HttpClient.SendAsync(request);

            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 50, during: TimeSpan.FromMinutes(2))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Long Running Connections Stress Test", stats);

        // Connection pool should handle long-running connections
        stats.AllOkCount.Should().BeGreaterThan(0);
        var errorRate = (double)stats.AllFailCount / stats.AllRequestCount;
        errorRate.Should().BeLessThan(0.2, "Connection handling should be robust under stress");
    }

    [TestMethod]
    public async Task StressTest_ErrorRecovery_ShouldRecoverFromErrors()
    {
        var scenario = Scenario.Create("error_recovery", async context =>
        {
            // Mix of valid and invalid requests to test error handling
            var validRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/users");
            var invalidRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/nonexistent");
            var malformedRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/users");
            malformedRequest.Content = new StringContent("invalid json", Encoding.UTF8, "application/json");

            var requests = new[] { validRequest, invalidRequest, malformedRequest };
            var request = requests[Random.Shared.Next(requests.Length)];

            var response = await HttpClient.SendAsync(request);

            // Consider 4xx as OK for this test since we're intentionally sending bad requests
            return response.IsSuccessStatusCode || ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                ? Response.Ok() 
                : Response.Fail($"Unexpected status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 25, during: GetTestDuration())
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        PrintScenarioResults("Error Recovery Stress Test", stats);

        // System should handle mixed valid/invalid requests
        stats.AllOkCount.Should().BeGreaterThan(0);
        var errorRate = (double)stats.AllFailCount / stats.AllRequestCount;
        errorRate.Should().BeLessThan(0.1, "System should gracefully handle mixed request types");
    }
}