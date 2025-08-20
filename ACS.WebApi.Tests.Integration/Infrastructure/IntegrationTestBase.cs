using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ACS.Service.Data;
using ACS.Service.Data.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Http.Headers;
using FluentAssertions;

namespace ACS.WebApi.Tests.Integration.Infrastructure;

/// <summary>
/// Base class for integration tests providing common setup and utility methods
/// </summary>
[TestClass]
public abstract class IntegrationTestBase
{
    protected TestWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;
    protected ApplicationDbContext DbContext => Factory.DbContext;

    [TestInitialize]
    public virtual void Setup()
    {
        Factory = new TestWebApplicationFactory();
        Client = Factory.CreateClient();
        
        // Seed test data
        SeedTestData();
    }

    [TestCleanup]
    public virtual void Cleanup()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }

    /// <summary>
    /// Configures the HTTP client with JWT authentication
    /// </summary>
    protected void SetupAuthentication(string userId = "test-user-123", string userName = "Test User", params string[] roles)
    {
        var token = GenerateJwtToken(userId, userName, roles);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Generates a JWT token for testing authentication
    /// </summary>
    private string GenerateJwtToken(string userId, string userName, string[] roles)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes("TestSecretKeyForJwtThatIsLongEnoughForHS256");
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName),
            new("user_id", userId)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "ACS.Tests",
            Audience = "ACS.Tests",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Seeds the test database with initial data
    /// </summary>
    protected virtual void SeedTestData()
    {
        // Clear existing data
        DbContext.Database.EnsureDeleted();
        DbContext.Database.EnsureCreated();

        // Seed basic test data
        SeedEntities();
        SeedUsers();
        SeedGroups();
        SeedRoles();
        SeedVerbTypes();
        SeedSchemeTypes();
        
        DbContext.SaveChanges();
    }

    private void SeedEntities()
    {
        var entities = new[]
        {
            new Entity { Id = 1, EntityType = "User", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 2, EntityType = "User", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 3, EntityType = "Group", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 4, EntityType = "Group", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 5, EntityType = "Role", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 6, EntityType = "Role", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        DbContext.Entities.AddRange(entities);
    }

    private void SeedUsers()
    {
        var users = new[]
        {
            new User 
            { 
                Id = 1, 
                Name = "John Doe", 
                Email = "john.doe@test.com",
                PasswordHash = "hashedpassword1",
                EntityId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow
            },
            new User 
            { 
                Id = 2, 
                Name = "Jane Smith", 
                Email = "jane.smith@test.com",
                PasswordHash = "hashedpassword2",
                EntityId = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                UpdatedAt = DateTime.UtcNow
            }
        };

        DbContext.Users.AddRange(users);
    }

    private void SeedGroups()
    {
        var groups = new[]
        {
            new Group 
            { 
                Id = 1, 
                Name = "Administrators", 
                EntityId = 3,
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                UpdatedAt = DateTime.UtcNow
            },
            new Group 
            { 
                Id = 2, 
                Name = "Users", 
                EntityId = 4,
                CreatedAt = DateTime.UtcNow.AddDays(-45),
                UpdatedAt = DateTime.UtcNow
            }
        };

        DbContext.Groups.AddRange(groups);
    }

    private void SeedRoles()
    {
        var roles = new[]
        {
            new Role 
            { 
                Id = 1, 
                Name = "Admin", 
                EntityId = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                UpdatedAt = DateTime.UtcNow
            },
            new Role 
            { 
                Id = 2, 
                Name = "User", 
                EntityId = 6,
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                UpdatedAt = DateTime.UtcNow
            }
        };

        DbContext.Roles.AddRange(roles);
    }

    private void SeedVerbTypes()
    {
        var verbTypes = new[]
        {
            new VerbType { Id = 1, VerbName = "GET" },
            new VerbType { Id = 2, VerbName = "POST" },
            new VerbType { Id = 3, VerbName = "PUT" },
            new VerbType { Id = 4, VerbName = "DELETE" }
        };

        DbContext.VerbTypes.AddRange(verbTypes);
    }

    private void SeedSchemeTypes()
    {
        var schemeTypes = new[]
        {
            new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" },
            new SchemeType { Id = 2, SchemeName = "BasicAuthorization" }
        };

        DbContext.SchemeTypes.AddRange(schemeTypes);
    }

    /// <summary>
    /// Creates a fresh database context for testing data persistence
    /// </summary>
    protected ApplicationDbContext CreateFreshDbContext()
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>
    /// Asserts that a response contains the expected error details
    /// </summary>
    protected static void AssertErrorResponse(string responseContent, int expectedStatusCode, string expectedMessage)
    {
        responseContent.Should().NotBeNullOrEmpty();
        
        if (expectedStatusCode >= 400)
        {
            responseContent.Should().Contain("false", "Response should indicate failure");
            responseContent.Should().Contain(expectedMessage, "Response should contain expected error message");
        }
    }

    /// <summary>
    /// Waits for any background operations to complete
    /// </summary>
    protected static async Task WaitForOperationsToComplete(int delayMs = 100)
    {
        await Task.Delay(delayMs);
    }
}