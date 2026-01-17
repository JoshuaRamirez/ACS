using NBomber.CSharp;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ACS.WebApi.Tests.Performance.Infrastructure;

/// <summary>
/// Base class for performance tests with common utilities
/// </summary>
public abstract class PerformanceTestBase
{
    protected PerformanceTestWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient HttpClient { get; private set; } = null!;
    protected string AdminToken { get; private set; } = string.Empty;
    protected string UserToken { get; private set; } = string.Empty;

    protected virtual async Task InitializeAsync(bool enableDetailedLogging = false, bool useInMemoryDatabase = true)
    {
        Factory = new PerformanceTestWebApplicationFactory(enableDetailedLogging, useInMemoryDatabase);

        // Use client with tenant header to satisfy middleware requirements
        HttpClient = Factory.CreateClientWithTenantHeader("test-tenant");

        // Seed test data
        await Factory.SeedTestDataAsync();

        // Get authentication tokens
        AdminToken = await GetJwtTokenAsync("admin@test.com", "Admin");
        UserToken = await GetJwtTokenAsync("user1@test.com", "User");
    }

    protected virtual async Task CleanupAsync()
    {
        HttpClient?.Dispose();
        if (Factory != null)
        {
            await Factory.DisposeAsync();
        }
    }

    protected async Task<string> GetJwtTokenAsync(string email, string role)
    {
        var loginRequest = new
        {
            Email = email,
            Password = "Password123!"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        var response = await HttpClient.PostAsync("/api/auth/login", content);
        
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            
            if (loginResponse.TryGetProperty("token", out var tokenElement))
            {
                return tokenElement.GetString() ?? string.Empty;
            }
        }

        // Return a mock token for testing purposes
        return GenerateMockJwtToken(email, role);
    }

    protected string GenerateMockJwtToken(string email, string role)
    {
        // For performance testing, use a simple mock token
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            email = email,
            role = role,
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        })));

        return $"Bearer.{payload}.signature";
    }

    protected HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string requestUri, bool useAdminToken = false)
    {
        var request = new HttpRequestMessage(method, requestUri);
        var token = useAdminToken ? AdminToken : UserToken;

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Add tenant header required by middleware
        request.Headers.Add("X-Tenant-ID", "test-tenant");

        return request;
    }

    protected StringContent CreateJsonContent(object data)
    {
        return new StringContent(
            JsonSerializer.Serialize(data),
            Encoding.UTF8,
            "application/json");
    }

    protected static void PrintScenarioResults(string scenarioName, Response response)
    {
        Console.WriteLine($"\n=== {scenarioName} Results ===");
        // Console.WriteLine($"Total Requests: {response.AllRequestCount}"); // TODO: NBomber Response API changed
        // Console.WriteLine($"OK Requests: {response.AllOkCount}"); // TODO: NBomber Response API changed
        // Console.WriteLine($"Failed Requests: {response.AllFailCount}"); // TODO: NBomber Response API changed
        // Console.WriteLine($"RPS: {response.ScenarioStats[0].Ok.Request.Mean}"); // TODO: NBomber Response API changed
        // Console.WriteLine($"Mean Latency: {response.ScenarioStats[0].Ok.Latency.Mean} ms"); // TODO: NBomber Response API changed
        // Console.WriteLine($"P95 Latency: {response.ScenarioStats[0].Ok.Latency.Percentile95} ms"); // TODO: NBomber Response API changed
        // Console.WriteLine($"P99 Latency: {response.ScenarioStats[0].Ok.Latency.Percentile99} ms"); // TODO: NBomber Response API changed
        // Console.WriteLine($"Data Transfer: {response.AllDataMB:F2} MB"); // TODO: NBomber Response API changed
    }

    protected static TimeSpan GetTestDuration()
    {
        // Short duration for development, longer for CI/CD
        return Environment.GetEnvironmentVariable("CI") == "true" 
            ? TimeSpan.FromMinutes(5) 
            : TimeSpan.FromMinutes(1);
    }

    protected static int GetMaxVirtualUsers()
    {
        // More users in CI environment with more resources
        return Environment.GetEnvironmentVariable("CI") == "true" ? 100 : 50;
    }
}