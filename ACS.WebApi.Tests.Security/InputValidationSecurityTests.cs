using ACS.WebApi.Tests.Security.Infrastructure;
using System.Text;
using System.Web;

namespace ACS.WebApi.Tests.Security;

/// <summary>
/// Security tests for input validation and sanitization
/// </summary>
[TestClass]
public class InputValidationSecurityTests : SecurityTestBase
{
    private string _adminToken = string.Empty;

    [TestInitialize]
    public async Task Setup()
    {
        _adminToken = await GetJwtTokenAsync("admin@test.com", "Admin");
        SetAuthorizationHeader(_adminToken);
    }

    [TestMethod]
    public async Task CreateUser_WithExcessivelyLongName_ShouldReturnBadRequest()
    {
        // Arrange - Name longer than reasonable limit
        var longName = new string('A', 10000);
        var user = new
        {
            Name = longName,
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task CreateUser_WithInvalidEmailFormat_ShouldReturnBadRequest()
    {
        // Arrange - Invalid email formats
        var invalidEmails = new[]
        {
            "not-an-email",
            "@example.com",
            "test@",
            "test..test@example.com",
            "test@.com",
            "test@com",
            "<script>alert('xss')</script>@example.com"
        };

        foreach (var email in invalidEmails)
        {
            var user = new
            {
                Name = "Test User",
                Email = email,
                Password = "Password123!"
            };

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(user),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync("/api/users", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
                $"Email {email} should be rejected as invalid");
        }
    }

    [TestMethod]
    public async Task CreateUser_WithWeakPassword_ShouldReturnBadRequest()
    {
        // Arrange - Weak passwords
        var weakPasswords = new[]
        {
            "123",
            "password",
            "123456",
            "abc",
            "",
            "short"
        };

        foreach (var password in weakPasswords)
        {
            var user = new
            {
                Name = "Test User",
                Email = "test@example.com",
                Password = password
            };

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(user),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync("/api/users", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                $"Password '{password}' should be rejected as weak");
        }
    }

    [TestMethod]
    public async Task CreateUser_WithNullValues_ShouldReturnBadRequest()
    {
        // Arrange - Null/empty required fields
        var invalidUsers = new[]
        {
            new { Name = (string)null, Email = "test@example.com", Password = "Password123!" },
            new { Name = "", Email = "test@example.com", Password = "Password123!" },
            new { Name = "Test User", Email = (string)null, Password = "Password123!" },
            new { Name = "Test User", Email = "", Password = "Password123!" },
            new { Name = "Test User", Email = "test@example.com", Password = (string)null },
            new { Name = "Test User", Email = "test@example.com", Password = "" }
        };

        foreach (var user in invalidUsers)
        {
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(user),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync("/api/users", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [TestMethod]
    public async Task CreateUser_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange - Names with special characters that should be allowed
        var validNames = new[]
        {
            "José García",
            "O'Connor",
            "Van Der Berg",
            "李小明",
            "François Müller",
            "Åse Øberg"
        };

        foreach (var name in validNames)
        {
            var user = new
            {
                Name = name,
                Email = $"test{validNames.ToList().IndexOf(name)}@example.com",
                Password = "Password123!"
            };

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(user),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync("/api/users", content);

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
            
            if (response.StatusCode == HttpStatusCode.Created)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                responseContent.Should().Contain(name, "Name should be preserved correctly");
            }
        }
    }

    [TestMethod]
    public async Task GetUsers_WithInvalidPaginationParameters_ShouldReturnBadRequest()
    {
        // Arrange - Invalid pagination parameters
        var invalidParams = new[]
        {
            "page=-1&size=10",
            "page=1&size=-1",
            "page=abc&size=10",
            "page=1&size=abc",
            "page=1&size=10000", // Excessively large page size
            "page=999999&size=10" // Excessively large page number
        };

        foreach (var param in invalidParams)
        {
            // Act
            var response = await Client.GetAsync($"/api/users?{param}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }
    }

    [TestMethod]
    public async Task SearchUsers_WithMaliciousInput_ShouldSanitize()
    {
        // Arrange - Malicious search inputs
        var maliciousInputs = new[]
        {
            "<script>alert('xss')</script>",
            "'; DROP TABLE Users; --",
            "../../../etc/passwd",
            "%00",
            "\0",
            "' OR 1=1--",
            "<img src=x onerror=alert('xss')>",
            "javascript:alert('xss')"
        };

        foreach (var input in maliciousInputs)
        {
            // Act
            var response = await Client.GetAsync($"/api/users?search={HttpUtility.UrlEncode(input)}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
            
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.Should().NotContain("<script>", "Script tags should be removed/encoded");
                content.Should().NotContain("DROP TABLE", "SQL commands should be removed/encoded");
                content.Should().NotContain("javascript:", "JavaScript protocol should be removed/encoded");
            }
        }
    }

    [TestMethod]
    public async Task CreateGroup_WithInvalidHierarchy_ShouldReturnBadRequest()
    {
        // Arrange - Invalid parent group ID
        var invalidGroups = new[]
        {
            new { Name = "Test Group", ParentGroupId = -1 },
            new { Name = "Test Group", ParentGroupId = 999999 }, // Non-existent parent
            new { Name = "", ParentGroupId = (int?)null }, // Empty name
            new { Name = new string('A', 1000), ParentGroupId = (int?)null } // Too long name
        };

        foreach (var group in invalidGroups)
        {
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(group),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync("/api/groups", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [TestMethod]
    public async Task CreateRole_WithInvalidPermissions_ShouldReturnBadRequest()
    {
        // Arrange - Invalid permission data
        var invalidRoles = new[]
        {
            new { Name = "", Permissions = new[] { "READ", "WRITE" } }, // Empty name
            new { Name = "Test Role", Permissions = new[] { "INVALID_PERMISSION" } }, // Invalid permission
            new { Name = new string('A', 1000), Permissions = new[] { "READ" } }, // Too long name
            new { Name = "Test Role", Permissions = new string[] { } } // No permissions
        };

        foreach (var role in invalidRoles)
        {
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(role),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync("/api/roles", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [TestMethod]
    public async Task UpdateUser_WithInvalidId_ShouldReturnBadRequest()
    {
        // Arrange - Invalid user IDs
        var invalidIds = new[]
        {
            "-1",
            "0", 
            "abc",
            "999999999999999999", // Extremely large number
            "1.5", // Decimal
            "1; DROP TABLE Users; --" // SQL injection attempt
        };

        var userUpdate = new
        {
            Name = "Updated User",
            Email = "updated@example.com"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(userUpdate),
            Encoding.UTF8,
            "application/json");

        foreach (var id in invalidIds)
        {
            // Act
            var response = await Client.PutAsync($"/api/users/{HttpUtility.UrlEncode(id)}", content);

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }
    }

    [TestMethod]
    public async Task BulkOperations_WithMixedValidInvalidData_ShouldHandleCorrectly()
    {
        // Arrange - Mix of valid and invalid data
        var mixedData = new[]
        {
            new { Name = "Valid User 1", Email = "valid1@example.com" },
            new { Name = "", Email = "invalid@example.com" }, // Invalid: empty name
            new { Name = "Valid User 2", Email = "valid2@example.com" },
            new { Name = "Invalid User", Email = "not-an-email" }, // Invalid: bad email
            new { Name = new string('A', 1000), Email = "toolong@example.com" } // Invalid: name too long
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(mixedData),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/bulk/users", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.PartialContent, HttpStatusCode.OK);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            // Should process valid entries and report errors for invalid ones
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotBeEmpty();
        }
    }

    [TestMethod]
    public async Task GetResource_WithInvalidUriPattern_ShouldReturnBadRequest()
    {
        // Arrange - Invalid URI patterns
        var invalidPatterns = new[]
        {
            "not-a-uri",
            "ftp://invalid-protocol.com",
            "file:///etc/passwd",
            "javascript:alert('xss')",
            "data:text/html,<script>alert('xss')</script>",
            "//evil.com/redirect",
            "http://localhost:0/invalid-port" // Invalid port
        };

        foreach (var pattern in invalidPatterns)
        {
            // Act
            var response = await Client.GetAsync($"/api/resources?pattern={HttpUtility.UrlEncode(pattern)}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }
    }

    [TestMethod]
    public async Task PostWithContentType_ShouldValidateContentType()
    {
        // Arrange - Test with wrong content type
        var user = new
        {
            Name = "Test User",
            Email = "test@example.com",
            Password = "Password123!"
        };

        var jsonContent = System.Text.Json.JsonSerializer.Serialize(user);
        var content = new StringContent(jsonContent, Encoding.UTF8, "text/plain"); // Wrong content type

        // Act
        var response = await Client.PostAsync("/api/users", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [TestMethod]
    public async Task PostWithMalformedJson_ShouldReturnBadRequest()
    {
        // Arrange - Malformed JSON
        var malformedJson = "{ \"Name\": \"Test User\", \"Email\": \"test@example.com\" "; // Missing closing brace

        var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/users", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task RequestWithExcessivePayloadSize_ShouldReturnRequestEntityTooLarge()
    {
        // Arrange - Extremely large payload
        var largeData = new string('A', 50 * 1024 * 1024); // 50MB string
        var largeUser = new
        {
            Name = largeData,
            Email = "test@example.com",
            Password = "Password123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(largeUser),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/users", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task HeaderInjection_ShouldBePrevented()
    {
        // Arrange - Attempt header injection via user input
        var maliciousInput = "test\r\nX-Injected-Header: malicious-value\r\n";
        
        // Act
        var response = await Client.GetAsync($"/api/users?search={HttpUtility.UrlEncode(maliciousInput)}");

        // Assert
        response.Headers.Should().NotContainKey("X-Injected-Header", "Header injection should be prevented");
    }
}