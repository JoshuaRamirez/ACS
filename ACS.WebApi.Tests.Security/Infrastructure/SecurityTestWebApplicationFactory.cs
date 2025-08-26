using ACS.Service.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACS.WebApi.Tests.Security.Infrastructure;

/// <summary>
/// Test factory for security testing with specialized configuration
/// </summary>
public class SecurityTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("SecurityTestDb");
            });

            // Configure logging to capture security events
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Disable HTTPS redirection for testing
            services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
                options.HttpsPort = null;
            });
        });

        builder.UseEnvironment("Testing");
        
        // Disable HTTPS for security testing
        builder.UseUrls("http://localhost");
    }
}

/// <summary>
/// Base class for security tests with common setup
/// </summary>
public abstract class SecurityTestBase : IDisposable
{
    protected readonly SecurityTestWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected SecurityTestBase()
    {
        Factory = new SecurityTestWebApplicationFactory();
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    protected async Task<string> GetCsrfTokenAsync(string path = "/")
    {
        var response = await Client.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();
        
        // Extract CSRF token from response (implementation depends on your CSRF token generation)
        var tokenStart = content.IndexOf("__RequestVerificationToken");
        if (tokenStart == -1) return string.Empty;
        
        var valueStart = content.IndexOf("value=\"", tokenStart) + 7;
        var valueEnd = content.IndexOf("\"", valueStart);
        
        return content.Substring(valueStart, valueEnd - valueStart);
    }

    protected async Task<string> GetJwtTokenAsync(string username = "testuser", string role = "User")
    {
        // Create a test JWT token for authentication testing
        var loginRequest = new
        {
            Username = username,
            Password = "TestPassword123!"
        };

        var loginContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(loginRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var loginResponse = await Client.PostAsync("/api/auth/login", loginContent);
        
        if (loginResponse.IsSuccessStatusCode)
        {
            var loginResult = await loginResponse.Content.ReadAsStringAsync();
            var tokenData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(loginResult);
            return tokenData?["token"]?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    protected void SetAuthorizationHeader(string token)
    {
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public void Dispose()
    {
        Client?.Dispose();
        Factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}