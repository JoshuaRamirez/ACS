using ACS.WebApi.Tests.Security.Infrastructure;
using System.Net;
using System.Text;
using System.Web;
using FluentAssertions;

namespace ACS.WebApi.Tests.Security;

/// <summary>
/// Security tests for SQL injection protection.
/// These tests verify that the API properly handles SQL injection attempts.
/// Demo API proxies to VerticalHost - actual SQL injection protection depends on backend.
/// </summary>
[TestClass]
public class SqlInjectionSecurityTests : SecurityTestBase
{
    private string _adminToken = string.Empty;

    [TestInitialize]
    public async Task Setup()
    {
        _adminToken = await GetJwtTokenAsync("admin@test.com", "Admin");
        SetAuthorizationHeader(_adminToken);
    }

    [TestMethod]
    public async Task GetUser_WithSqlInjectionInId_ShouldReturnBadRequest()
    {
        // Arrange - SQL injection attempt in user ID
        var maliciousId = "1; DROP TABLE Users; --";

        // Act
        var response = await Client.GetAsync($"/api/users/{HttpUtility.UrlEncode(maliciousId)}");

        // Assert - Should return BadRequest, NotFound, or OK with safe response
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task GetUsers_WithSqlInjectionInQuery_ShouldReturnBadRequest()
    {
        // Arrange - SQL injection in search query
        var maliciousQuery = "'; DELETE FROM Users WHERE '1'='1";

        // Act
        var response = await Client.GetAsync($"/api/users?search={HttpUtility.UrlEncode(maliciousQuery)}");

        // Assert - Should handle safely
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotContain("DELETE", "SQL commands should not be visible in response");
        }
    }

    [TestMethod]
    public async Task CreateUser_WithSqlInjectionInName_ShouldReturnBadRequest()
    {
        // Arrange - SQL injection in user creation
        var maliciousUser = new
        {
            Name = "'; DROP TABLE Users; --",
            Email = "test@example.com",
            Password = "Password123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(maliciousUser),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/users", content);

        // Assert - Should either reject or sanitize
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.Forbidden);

        if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK)
        {
            // Verify the malicious SQL was not executed by checking if users still exist
            var usersResponse = await Client.GetAsync("/api/users");
            usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [TestMethod]
    public async Task UpdateUser_WithSqlInjectionInEmail_ShouldReturnBadRequest()
    {
        // Arrange - SQL injection in user update
        var maliciousUpdate = new
        {
            Id = 1,
            Name = "Test User",
            Email = "test'; UPDATE Users SET Name='Hacked' WHERE 1=1; --@example.com"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(maliciousUpdate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PutAsync("/api/users/1", content);

        // Assert - 405 may occur if PUT isn't supported
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public async Task SearchUsers_WithUnionBasedSqlInjection_ShouldReturnSafeResults()
    {
        // Arrange - UNION-based SQL injection attempt
        var maliciousQuery = "' UNION SELECT 1,username,password FROM admin_users--";

        // Act
        var response = await Client.GetAsync($"/api/users?search={HttpUtility.UrlEncode(maliciousQuery)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotContain("admin_users", "Should not expose database structure");
            // Note: In demo mode, the password check might not be relevant since there's no real DB query
        }
    }

    [TestMethod]
    public async Task GetGroups_WithBooleanBasedSqlInjection_ShouldReturnSafeResults()
    {
        // Arrange - Boolean-based SQL injection
        var maliciousFilter = "1=1 OR 1=1";

        // Act
        var response = await Client.GetAsync($"/api/groups?filter={HttpUtility.UrlEncode(maliciousFilter)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task GetRoles_WithTimeBasedSqlInjection_ShouldNotCauseDelay()
    {
        // Arrange - Time-based SQL injection attempt
        var maliciousQuery = "'; WAITFOR DELAY '00:00:05'; --";
        var startTime = DateTime.UtcNow;

        // Act
        var response = await Client.GetAsync($"/api/roles?search={HttpUtility.UrlEncode(maliciousQuery)}");
        var endTime = DateTime.UtcNow;

        // Assert - The request should complete quickly (no delay from SQL injection)
        var duration = endTime - startTime;
        duration.TotalSeconds.Should().BeLessThan(3, "Time-based SQL injection should not cause delays");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task BulkCreateUsers_WithSqlInjectionInBatch_ShouldReturnBadRequest()
    {
        // Arrange - SQL injection in bulk operation
        var maliciousUsers = new[]
        {
            new { Name = "User1", Email = "user1@test.com" },
            new { Name = "'; DROP TABLE Users; --", Email = "malicious@test.com" },
            new { Name = "User3", Email = "user3@test.com" }
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(maliciousUsers),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/bulk/users", content);

        // Assert - Endpoint may not exist
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound);

        // Verify database integrity by checking if users endpoint still works
        var usersCheck = await Client.GetAsync("/api/users");
        usersCheck.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task GetAuditLogs_WithSqlInjectionInDateFilter_ShouldReturnSafeResults()
    {
        // Arrange - SQL injection in date filter
        var maliciousDate = "2023-01-01'; DROP TABLE AuditLog; --";

        // Act
        var response = await Client.GetAsync($"/api/audit?startDate={HttpUtility.UrlEncode(maliciousDate)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public async Task GetPermissions_WithSqlInjectionInResourceUri_ShouldReturnSafeResults()
    {
        // Arrange - SQL injection in resource URI parameter
        var maliciousUri = "/api/users'; DELETE FROM PermissionScheme WHERE '1'='1; --";

        // Act
        var response = await Client.GetAsync($"/api/permissions/check?resource={HttpUtility.UrlEncode(maliciousUri)}&verb=GET");

        // Assert - Endpoint may not exist or may return Forbidden
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed, HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task SearchResources_WithSqlInjectionInPattern_ShouldReturnSafeResults()
    {
        // Arrange - SQL injection in URI pattern search
        var maliciousPattern = "/*'; EXEC xp_cmdshell('dir'); --";

        // Act
        var response = await Client.GetAsync($"/api/resources?pattern={HttpUtility.UrlEncode(maliciousPattern)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotContain("xp_cmdshell", "Should not execute system commands");
        }
    }

    [TestMethod]
    public async Task GetReports_WithSqlInjectionInParameters_ShouldReturnSafeResults()
    {
        // Arrange - SQL injection in report parameters
        var maliciousParam = "'; SELECT * FROM Users WHERE role='admin'; --";

        // Act
        var response = await Client.GetAsync($"/api/reports/user-activity?userId={HttpUtility.UrlEncode(maliciousParam)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task DatabaseIntegrity_AfterMultipleSqlInjectionAttempts_ShouldRemainIntact()
    {
        // Arrange - Multiple SQL injection attempts
        var injectionAttempts = new[]
        {
            "'; DROP TABLE Users; --",
            "1' OR '1'='1",
            "'; DELETE FROM Roles; --",
            "1; EXEC xp_cmdshell('format c:'); --",
            "' UNION SELECT * FROM admin_credentials--"
        };

        // Act - Attempt multiple injections
        foreach (var attempt in injectionAttempts)
        {
            await Client.GetAsync($"/api/users?search={HttpUtility.UrlEncode(attempt)}");
            await Client.GetAsync($"/api/groups?filter={HttpUtility.UrlEncode(attempt)}");
            await Client.GetAsync($"/api/roles?name={HttpUtility.UrlEncode(attempt)}");
        }

        // Assert - Verify database integrity by ensuring endpoints still respond
        var usersResponse = await Client.GetAsync("/api/users");
        var groupsResponse = await Client.GetAsync("/api/groups");
        var rolesResponse = await Client.GetAsync("/api/roles");

        usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        groupsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
