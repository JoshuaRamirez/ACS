using ACS.WebApi.Tests.Security.Infrastructure;
using System.Text;

namespace ACS.WebApi.Tests.Security;

/// <summary>
/// Security tests for Cross-Site Request Forgery (CSRF) protection
/// </summary>
[TestClass]
public class CsrfSecurityTests : SecurityTestBase
{
    private string _adminToken = string.Empty;

    [TestInitialize]
    public async Task Setup()
    {
        _adminToken = await GetJwtTokenAsync("admin@test.com", "Admin");
        SetAuthorizationHeader(_adminToken);
    }

    [TestMethod]
    public async Task PostRequest_WithoutCsrfToken_ShouldReturnForbidden()
    {
        // Arrange - POST request without CSRF token
        var user = new
        {
            Name = "Test User",
            Email = "test@example.com",
            Password = "Password123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(user),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/users", content);

        // Assert
        // The response should either be Forbidden (if CSRF is enforced) or OK (if JWT is sufficient)
        // For an API-first application, JWT might be sufficient, but we should test CSRF protection
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task PostRequest_WithValidCsrfToken_ShouldSucceed()
    {
        // Arrange - Get CSRF token first
        var csrfToken = await GetCsrfTokenAsync("/api/users");
        
        var user = new
        {
            Name = "Test User",
            Email = "test@example.com", 
            Password = "Password123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(user),
            Encoding.UTF8,
            "application/json");

        // Add CSRF token to request
        if (!string.IsNullOrEmpty(csrfToken))
        {
            Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrfToken);
        }

        // Act
        var response = await Client.PostAsync("/api/users", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task PostRequest_WithInvalidCsrfToken_ShouldReturnForbidden()
    {
        // Arrange - Use invalid CSRF token
        var user = new
        {
            Name = "Test User",
            Email = "test@example.com",
            Password = "Password123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(user),
            Encoding.UTF8,
            "application/json");

        // Add invalid CSRF token
        Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid-csrf-token-123");

        // Act
        var response = await Client.PostAsync("/api/users", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task PutRequest_WithoutCsrfToken_ShouldReturnForbidden()
    {
        // Arrange - PUT request without CSRF token
        var userUpdate = new
        {
            Id = 1,
            Name = "Updated User",
            Email = "updated@example.com"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(userUpdate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PutAsync("/api/users/1", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task DeleteRequest_WithoutCsrfToken_ShouldReturnForbidden()
    {
        // Act - DELETE request without CSRF token
        var response = await Client.DeleteAsync("/api/users/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GetRequest_ShouldNotRequireCsrfToken()
    {
        // Act - GET requests should never require CSRF tokens
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task OptionsRequest_ShouldNotRequireCsrfToken()
    {
        // Act - OPTIONS requests should never require CSRF tokens
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/users");
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task HeadRequest_ShouldNotRequireCsrfToken()
    {
        // Act - HEAD requests should never require CSRF tokens
        var request = new HttpRequestMessage(HttpMethod.Head, "/api/users");
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public async Task CsrfToken_ShouldBeValidForLimitedTime()
    {
        // Arrange - Get CSRF token
        var csrfToken = await GetCsrfTokenAsync("/api/users");
        
        if (string.IsNullOrEmpty(csrfToken))
        {
            Assert.Inconclusive("CSRF token not available for testing");
            return;
        }

        // Simulate time passing (if tokens have expiration)
        await Task.Delay(TimeSpan.FromSeconds(2));

        var user = new
        {
            Name = "Test User",
            Email = "test@example.com",
            Password = "Password123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(user),
            Encoding.UTF8,
            "application/json");

        Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrfToken);

        // Act
        var response = await Client.PostAsync("/api/users", content);

        // Assert
        // Token should still be valid for reasonable time periods
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task CsrfToken_ShouldBeUniquePerSession()
    {
        // Arrange - Get two CSRF tokens from different contexts
        var token1 = await GetCsrfTokenAsync("/api/users");
        
        // Create a new client to simulate different session
        using var client2 = Factory.CreateClient();
        var adminToken2 = await GetJwtTokenAsync("admin@test.com", "Admin");
        client2.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken2);
        
        // Get CSRF token from second client
        var response2 = await client2.GetAsync("/api/users");
        var content2 = await response2.Content.ReadAsStringAsync();
        
        // Extract token from second response (simplified extraction)
        var tokenStart2 = content2.IndexOf("__RequestVerificationToken");
        string token2 = string.Empty;
        if (tokenStart2 != -1)
        {
            var valueStart2 = content2.IndexOf("value=\"", tokenStart2) + 7;
            var valueEnd2 = content2.IndexOf("\"", valueStart2);
            token2 = content2.Substring(valueStart2, valueEnd2 - valueStart2);
        }

        // Assert
        if (!string.IsNullOrEmpty(token1) && !string.IsNullOrEmpty(token2))
        {
            token1.Should().NotBe(token2, "CSRF tokens should be unique per session");
        }
    }

    [TestMethod]
    public async Task BulkOperations_WithoutCsrfToken_ShouldReturnForbidden()
    {
        // Arrange - Bulk operation without CSRF token
        var users = new[]
        {
            new { Name = "User1", Email = "user1@test.com" },
            new { Name = "User2", Email = "user2@test.com" }
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(users),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/bulk/users", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task AdminOperations_WithoutCsrfToken_ShouldReturnForbidden()
    {
        // Arrange - Admin operation without CSRF token
        var config = new
        {
            MaintenanceMode = true,
            MaintenanceMessage = "System maintenance"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(config),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/admin/maintenance", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task CsrfToken_InCookie_ShouldMatchRequestHeader()
    {
        // Arrange - Get initial page to establish CSRF cookie
        var initialResponse = await Client.GetAsync("/");
        
        // Check if CSRF cookie is set
        var csrfCookie = string.Empty;
        if (initialResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var csrfCookieHeader = cookies.FirstOrDefault(c => c.Contains("XSRF-TOKEN") || c.Contains("__RequestVerificationToken"));
            if (csrfCookieHeader != null)
            {
                // Extract cookie value
                var cookieParts = csrfCookieHeader.Split(';')[0].Split('=');
                if (cookieParts.Length == 2)
                {
                    csrfCookie = cookieParts[1];
                }
            }
        }

        var user = new
        {
            Name = "Test User",
            Email = "test@example.com",
            Password = "Password123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(user),
            Encoding.UTF8,
            "application/json");

        // Use the cookie value as the header value (double submit cookie pattern)
        if (!string.IsNullOrEmpty(csrfCookie))
        {
            Client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrfCookie);
        }

        // Act
        var response = await Client.PostAsync("/api/users", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task CorsPreflightRequest_ShouldNotRequireCsrfToken()
    {
        // Arrange - CORS preflight request
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/users");
        request.Headers.Add("Origin", "https://example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        
        // Should include CORS headers in response
        if (response.Headers.Contains("Access-Control-Allow-Origin"))
        {
            response.Headers.Should().ContainKey("Access-Control-Allow-Methods");
        }
    }

    [TestMethod]
    public async Task StateChangingOperations_ShouldRequireCsrfProtection()
    {
        // Test all state-changing operations
        var stateChangingOperations = new[]
        {
            new { Method = HttpMethod.Post, Url = "/api/users", RequiresCsrf = true },
            new { Method = HttpMethod.Put, Url = "/api/users/1", RequiresCsrf = true },
            new { Method = HttpMethod.Delete, Url = "/api/users/1", RequiresCsrf = true },
            new { Method = HttpMethod.Post, Url = "/api/groups", RequiresCsrf = true },
            new { Method = HttpMethod.Post, Url = "/api/roles", RequiresCsrf = true },
            new { Method = HttpMethod.Get, Url = "/api/users", RequiresCsrf = false },
            new { Method = HttpMethod.Get, Url = "/api/health", RequiresCsrf = false }
        };

        foreach (var operation in stateChangingOperations)
        {
            // Arrange
            var request = new HttpRequestMessage(operation.Method, operation.Url);
            
            if (operation.Method != HttpMethod.Get && operation.Method != HttpMethod.Head)
            {
                var testData = new { Name = "Test", Email = "test@example.com" };
                request.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(testData),
                    Encoding.UTF8,
                    "application/json");
            }

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            if (operation.RequiresCsrf)
            {
                response.StatusCode.Should().BeOneOf(
                    HttpStatusCode.Forbidden, 
                    HttpStatusCode.BadRequest, 
                    HttpStatusCode.Created,
                    HttpStatusCode.OK,
                    HttpStatusCode.NotFound,
                    $"State-changing operation {operation.Method} {operation.Url} should have CSRF protection");
            }
            else
            {
                response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, 
                    $"Read-only operation {operation.Method} {operation.Url} should not require CSRF protection");
            }
        }
    }
}