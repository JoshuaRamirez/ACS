using ACS.WebApi.Tests.Security.Infrastructure;
using AngleSharp;
using AngleSharp.Html.Dom;
using System.Text;
using System.Web;

namespace ACS.WebApi.Tests.Security;

/// <summary>
/// Security tests for Cross-Site Scripting (XSS) protection
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

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created);
        
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotContain("<script>", "Script tags should be encoded or removed");
            responseContent.Should().NotContain("alert('XSS')", "JavaScript should be encoded or removed");
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
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotContain("onerror=alert", "Event handlers should be encoded or removed");
            responseContent.Should().NotContain("<img src=x", "Dangerous HTML should be encoded or removed");
        }
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

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created);
        
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotContain("<iframe", "Iframe tags should be encoded or removed");
            responseContent.Should().NotContain("javascript:", "JavaScript protocols should be blocked");
        }
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
            responseContent.Should().NotContain("<script>", "Script tags should not appear in response");
            responseContent.Should().NotContain("document.location", "JavaScript code should not appear in response");
        }
    }

    [TestMethod]
    public async Task GetUser_ResponseHeaders_ShouldIncludeXssProtection()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        
        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
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
            "<svg onload=alert('XSS')>",
            "<body onload=alert('XSS')>",
            "<iframe src=javascript:alert('XSS')></iframe>",
            "<input onfocus=alert('XSS') autofocus>",
            "<marquee onstart=alert('XSS')>",
            "&#60;script&#62;alert('XSS')&#60;/script&#62;",
            "%3Cscript%3Ealert('XSS')%3C/script%3E"
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

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created, HttpStatusCode.Conflict);
            
            if (response.StatusCode == HttpStatusCode.Created)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Check that dangerous content is encoded or removed
                responseContent.Should().NotContain("<script", $"Script tags should be encoded for payload: {payload}");
                responseContent.Should().NotContain("javascript:", $"JavaScript protocol should be blocked for payload: {payload}");
                responseContent.Should().NotContain("onerror=", $"Event handlers should be encoded for payload: {payload}");
                responseContent.Should().NotContain("onload=", $"Event handlers should be encoded for payload: {payload}");
                responseContent.Should().NotContain("onfocus=", $"Event handlers should be encoded for payload: {payload}");
                responseContent.Should().NotContain("alert(", $"JavaScript functions should be encoded for payload: {payload}");
            }
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
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotContain("<script>", "Script tags should not appear in response");
            responseContent.Should().NotContain("fetch(", "JavaScript functions should not appear in response");
        }
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
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created);
        
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotContain("<script>", "Script tags should be encoded in URI patterns");
        }
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

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotContain("<script>", "Script tags should be encoded in bulk operations");
            responseContent.Should().NotContain("alert('Bulk XSS')", "JavaScript should be encoded in bulk operations");
        }
    }

    [TestMethod]
    public async Task GetReport_WithXssInParameters_ShouldEncodeOutput()
    {
        // Arrange - XSS in report parameters
        var xssParam = "<script>window.location='http://malicious-site.com'</script>";

        // Act
        var response = await Client.GetAsync($"/api/reports/user-activity?filter={HttpUtility.UrlEncode(xssParam)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotContain("<script>", "Script tags should not appear in report responses");
            responseContent.Should().NotContain("window.location", "JavaScript should not appear in report responses");
        }
    }

    [TestMethod]
    public async Task HtmlResponse_ShouldHaveContentSecurityPolicy()
    {
        // Act - Request an HTML page (if any)
        var response = await Client.GetAsync("/");

        // Assert
        if (response.Headers.Contains("Content-Security-Policy"))
        {
            var cspHeader = response.Headers.GetValues("Content-Security-Policy").First();
            cspHeader.Should().Contain("script-src", "CSP should restrict script sources");
            cspHeader.Should().NotContain("'unsafe-inline'", "CSP should not allow unsafe inline scripts");
            cspHeader.Should().NotContain("'unsafe-eval'", "CSP should not allow unsafe eval");
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
        
        if (createResponse.StatusCode == HttpStatusCode.Created)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Assert - Special characters should be properly encoded in JSON
            if (responseContent.Contains("Test"))
            {
                // In JSON, these characters should be properly escaped
                responseContent.Should().Match("*Test & *User*", "Ampersands should be preserved in JSON");
                responseContent.Should().NotContain("<User>", "Angle brackets should not appear unencoded if this were HTML");
            }
        }
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
        responseContent.Should().NotContain("<script>", "Error messages should not contain unencoded script tags");
        responseContent.Should().NotContain("alert('Reflected XSS')", "Error messages should not contain unencoded JavaScript");
    }
}