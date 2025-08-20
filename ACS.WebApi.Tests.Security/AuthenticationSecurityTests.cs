using ACS.WebApi.Tests.Security.Infrastructure;
using System.Text;

namespace ACS.WebApi.Tests.Security;

/// <summary>
/// Security tests for authentication and authorization mechanisms
/// </summary>
[TestClass]
public class AuthenticationSecurityTests : SecurityTestBase
{
    [TestMethod]
    public async Task Login_WithValidCredentials_ShouldReturnJwtToken()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin@test.com",
            Password = "AdminPassword123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("token");
    }

    [TestMethod]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin@test.com",
            Password = "WrongPassword"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Login_WithSqlInjectionAttempt_ShouldReturnBadRequest()
    {
        // Arrange - SQL injection attempt in username
        var loginRequest = new
        {
            Username = "admin'; DROP TABLE Users; --",
            Password = "password"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/auth/login", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotContain("DROP TABLE");
    }

    [TestMethod]
    public async Task Login_WithEmptyCredentials_ShouldReturnBadRequest()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "",
            Password = ""
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Login_WithExtremelyLongUsername_ShouldReturnBadRequest()
    {
        // Arrange - Test buffer overflow protection
        var longUsername = new string('a', 10000);
        var loginRequest = new
        {
            Username = longUsername,
            Password = "password"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturnUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task ProtectedEndpoint_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task ProtectedEndpoint_WithExpiredToken_ShouldReturnUnauthorized()
    {
        // Arrange - Create an expired JWT token
        var expiredToken = CreateExpiredJwtToken();
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task AdminEndpoint_WithUserRole_ShouldReturnForbidden()
    {
        // Arrange
        var userToken = await GetJwtTokenAsync("user@test.com", "User");
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

        // Act
        var response = await Client.GetAsync("/api/admin/system-info");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task AdminEndpoint_WithAdminRole_ShouldReturnOk()
    {
        // Arrange
        var adminToken = await GetJwtTokenAsync("admin@test.com", "Admin");
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await Client.GetAsync("/api/admin/system-info");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task JwtToken_ShouldNotContainSensitiveInformation()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin@test.com",
            Password = "AdminPassword123!"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/auth/login", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
        var token = tokenData?["token"]?.ToString();

        // Assert
        token.Should().NotBeNullOrEmpty();
        
        // Decode JWT payload (base64 decode the middle part)
        var tokenParts = token!.Split('.');
        tokenParts.Should().HaveCount(3); // Header.Payload.Signature
        
        var payload = Encoding.UTF8.GetString(Convert.FromBase64String(AddPadding(tokenParts[1])));
        
        // Ensure no sensitive information is in the token
        payload.Should().NotContain("password", "Password should not be in JWT payload");
        payload.Should().NotContain("secret", "Secrets should not be in JWT payload");
        payload.Should().NotContain("key", "Keys should not be in JWT payload");
    }

    [TestMethod]
    public async Task Login_WithValidCredentials_ShouldLimitAttempts()
    {
        // Arrange - Simulate brute force attack
        var loginRequest = new
        {
            Username = "admin@test.com",
            Password = "WrongPassword"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        var failedAttempts = 0;
        var maxAttempts = 5;

        // Act - Attempt multiple failed logins
        for (int i = 0; i < maxAttempts + 1; i++)
        {
            var response = await Client.PostAsync("/api/auth/login", content);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
            failedAttempts++;
        }

        // Assert - Should be rate limited after max attempts
        failedAttempts.Should().BeLessOrEqualTo(maxAttempts, 
            "Rate limiting should prevent excessive login attempts");
    }

    private string CreateExpiredJwtToken()
    {
        // Create a JWT token that's already expired for testing
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes("your-super-secret-key-here-at-least-256-bits-long-for-production");
        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("sub", "testuser"),
                new System.Security.Claims.Claim("role", "User")
            }),
            Expires = DateTime.UtcNow.AddMinutes(-1), // Expired 1 minute ago
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string AddPadding(string base64)
    {
        var padding = base64.Length % 4;
        if (padding > 0)
        {
            base64 += new string('=', 4 - padding);
        }
        return base64;
    }
}