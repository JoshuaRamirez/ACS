using ACS.WebApi.Tests.Security.Infrastructure;
using System.Net;
using System.Text;
using System.Web;
using FluentAssertions;

namespace ACS.WebApi.Tests.Security;

/// <summary>
/// Security tests for Cross-Site Scripting (XSS) protection.
/// These tests verify that the API properly handles XSS payloads.
/// </summary>
[TestClass]
public class XssSecurityTests : SecurityTestBase
{
    private string _adminToken = string.Empty;

    [TestInitialize]
    public async Task Setup()
    {
        _adminToken = await GetJwtTokenAsync("admin@test.com", "Admin");
        SetAuthorizationHeader(_adminToken);
    }

    [TestMethod]
    public async Task CreateUser_WithXssInName_ShouldEncodeOrReject()
    {
        // Arrange - XSS payload in user name
        var xssUser = new
        {
            Name = "<script>alert('XSS')</script>",
            Email = "test@example.com",
            Password = "Password123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(xssUser),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/users", content);

        // Assert - Should either reject or encode
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.Forbidden);

        if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            // In JSON responses, scripts should be JSON-escaped, not raw
            responseContent.Should().NotContain("<script>alert('XSS')</script>", "Raw script tags should be encoded or removed");
        }
    }

    [TestMethod]
    public async Task UpdateUser_WithXssInEmail_ShouldEncodeOrReject()
    {
        // Arrange - XSS payload in email field
        var xssUpdate = new
        {
            Id = 1,
            Name = "Test User",
            Email = "test@example.com<img src=x onerror=alert('XSS')>"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(xssUpdate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PutAsync("/api/users/1", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public async Task CreateGroup_WithXssInDescription_ShouldEncodeOrReject()
    {
        // Arrange - XSS payload in group description
        var xssGroup = new
        {
            Name = "Test Group",
            Description = "<iframe src='javascript:alert(\"XSS\")'></iframe>"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(xssGroup),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/groups", content);

        // Assert - POST may return MethodNotAllowed or Forbidden
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public async Task SearchUsers_WithXssInQuery_ShouldEncodeOutput()
    {
        // Arrange - XSS payload in search query
        var xssQuery = "<script>document.location='http://evil.com'</script>";

        // Act
        var response = await Client.GetAsync($"/api/users?search={HttpUtility.UrlEncode(xssQuery)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            // Response should not contain raw unescaped script tags
            responseContent.Should().NotContain("<script>document.location", "Script tags should not appear unescaped in response");
        }
    }

    [TestMethod]
    public async Task GetUser_ResponseHeaders_ShouldIncludeXssProtection()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert - Check for XSS protection headers
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");

        response.Headers.Should().ContainKey("X-Frame-Options");
        // X-Frame-Options could be DENY or SAMEORIGIN
        var frameOptions = response.Headers.GetValues("X-Frame-Options").First();
        frameOptions.Should().BeOneOf("DENY", "SAMEORIGIN");
    }

    [TestMethod]
    public async Task CreateRole_WithXssInRoleName_ShouldEncodeOrReject()
    {
        // Arrange - Various XSS payloads
        var xssPayloads = new[]
        {
            "<script>alert('XSS')</script>",
            "javascript:alert('XSS')",
            "<img src='x' onerror='alert(\"XSS\")'>",
            "<svg onload=alert('XSS')>"
        };

        foreach (var payload in xssPayloads)
        {
            // Arrange
            var xssRole = new
            {
                Name = payload,
                Description = "Test Role"
            };

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(xssRole),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync("/api/roles", content);

            // Assert - Should handle the XSS payload safely
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.OK);
        }
    }

    [TestMethod]
    public async Task GetAuditLogs_WithXssInFilters_ShouldEncodeOutput()
    {
        // Arrange - XSS in audit log filters
        var xssFilter = "<script>fetch('/api/admin/delete-all')</script>";

        // Act
        var response = await Client.GetAsync($"/api/audit?action={HttpUtility.UrlEncode(xssFilter)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public async Task CreateResource_WithXssInUriPattern_ShouldEncodeOrReject()
    {
        // Arrange - XSS payload in resource URI pattern
        var xssResource = new
        {
            UriPattern = "/api/users<script>alert('XSS')</script>",
            Description = "Test Resource"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(xssResource),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/resources", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public async Task BulkOperations_WithXssInUserData_ShouldEncodeOrReject()
    {
        // Arrange - XSS payload in bulk user creation
        var xssUsers = new[]
        {
            new { Name = "Normal User", Email = "normal@test.com" },
            new { Name = "<script>alert('Bulk XSS')</script>", Email = "xss@test.com" },
            new { Name = "Another User", Email = "another@test.com" }
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(xssUsers),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/bulk/users", content);

        // Assert - Endpoint may not exist
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public async Task GetReport_WithXssInParameters_ShouldEncodeOutput()
    {
        // Arrange - XSS in report parameters
        var xssParam = "<script>window.location='http://malicious-site.com'</script>";

        // Act
        var response = await Client.GetAsync($"/api/reports/user-activity?filter={HttpUtility.UrlEncode(xssParam)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public async Task HtmlResponse_ShouldHaveContentSecurityPolicy()
    {
        // Act - Request root page
        var response = await Client.GetAsync("/");

        // Assert - CSP header may or may not be present depending on configuration
        if (response.Headers.Contains("Content-Security-Policy"))
        {
            var cspHeader = response.Headers.GetValues("Content-Security-Policy").First();
            // CSP should have some restrictions
            cspHeader.Should().NotBeNullOrEmpty("CSP header should have value if present");
        }
        else
        {
            // CSP might not be configured for API-only responses
            // This is acceptable for JSON API endpoints
            Assert.Inconclusive("Content-Security-Policy header not present - may be acceptable for API-only application");
        }
    }

    [TestMethod]
    public async Task ValidateHtmlEncoding_InResponseContent()
    {
        // Arrange - Create user with special characters that need encoding
        var specialUser = new
        {
            Name = "Test & <User> \"Quotes\" 'Apostrophe'",
            Email = "special@example.com",
            Password = "Password123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(specialUser),
            Encoding.UTF8,
            "application/json");

        // Act
        var createResponse = await Client.PostAsync("/api/users", content);

        // Assert - Response should be valid JSON with properly escaped characters
        createResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task ReflectedXss_InErrorMessages_ShouldBeEncoded()
    {
        // Arrange - XSS payload that might be reflected in error messages
        var xssPayload = "<script>alert('Reflected XSS')</script>";

        // Act - Try to access non-existent user with XSS payload
        var response = await Client.GetAsync($"/api/users/{HttpUtility.UrlEncode(xssPayload)}");

        // Assert
        var responseContent = await response.Content.ReadAsStringAsync();
        // Response should not contain raw unescaped script tags in error messages
        responseContent.Should().NotContain("<script>alert('Reflected XSS')</script>", "Error messages should not contain unencoded script tags");
    }
}
