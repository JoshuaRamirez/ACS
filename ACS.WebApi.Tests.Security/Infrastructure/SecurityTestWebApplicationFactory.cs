using ACS.Infrastructure;
using ACS.Service.Data;
using ACS.WebApi.Security.Csrf;
using ACS.WebApi.Security.Validation;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Use aliases to disambiguate between Infrastructure and WebApi namespaces
using InfraIUserContextService = ACS.Infrastructure.Services.IUserContextService;
using InfraITenantContextService = ACS.Infrastructure.Services.ITenantContextService;
using InfraTenantProcessInfo = ACS.Infrastructure.Services.TenantProcessInfo;

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
                options.UseInMemoryDatabase("SecurityTestDb_" + Guid.NewGuid().ToString());
            });

            // Register mock IUserContextService for testing (Infrastructure namespace)
            RemoveService<InfraIUserContextService>(services);
            services.AddScoped<InfraIUserContextService, MockUserContextService>();

            // Register mock ITenantContextService for testing (Infrastructure namespace)
            RemoveService<InfraITenantContextService>(services);
            services.AddScoped<InfraITenantContextService, MockTenantContextService>();

            // Register mock TenantProcessDiscoveryService for testing
            RemoveService<TenantProcessDiscoveryService>(services);
            services.AddSingleton<TenantProcessDiscoveryService>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<TenantProcessDiscoveryService>>();
                return new TenantProcessDiscoveryService(logger);
            });

            // Register InputValidator and its options for security filters
            services.Configure<InputValidationOptions>(options =>
            {
                // Use default options for testing
            });
            RemoveService<IInputValidator>(services);
            services.AddScoped<IInputValidator, InputValidator>();

            // Register CSRF protection service and its options
            services.Configure<CsrfProtectionOptions>(options =>
            {
                options.RequireHttps = false; // Disable HTTPS requirement for testing
            });
            RemoveService<ICsrfProtectionService>(services);
            services.AddScoped<ICsrfProtectionService, CsrfProtectionService>();

            // Configure logging to capture security events
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
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

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }
    }
}

/// <summary>
/// Mock implementation of IUserContextService for testing (Infrastructure namespace)
/// </summary>
public class MockUserContextService : InfraIUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MockUserContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentUserId() => "test-user-id";
    public string GetCurrentUserName() => "Test User";
    public string GetTenantId() => "test-tenant";
    public bool IsAuthenticated() => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    public IEnumerable<string> GetUserRoles() => new[] { "User" };
    public bool HasRole(string role) => role == "User" || role == "Admin";
    public string? GetUserEmail() => "test@example.com";
    public string? GetClaim(string claimType) => claimType switch
    {
        "sub" => "test-user-id",
        "tenant" => "test-tenant",
        _ => null
    };
    public IEnumerable<(string Type, string Value)> GetAllClaims() => new[]
    {
        ("sub", "test-user-id"),
        ("tenant", "test-tenant"),
        ("role", "User")
    };
}

/// <summary>
/// Mock implementation of ITenantContextService for testing (Infrastructure namespace)
/// </summary>
public class MockTenantContextService : InfraITenantContextService
{
    private string _tenantId = "test-tenant";
    private InfraTenantProcessInfo? _processInfo;
    private GrpcChannel? _grpcChannel;

    public string? GetTenantId() => _tenantId;

    public string GetRequiredTenantId() => _tenantId ?? throw new UnauthorizedAccessException("Tenant ID not found");

    public InfraTenantProcessInfo? GetTenantProcessInfo() => _processInfo ?? new InfraTenantProcessInfo
    {
        TenantId = _tenantId,
        ProcessId = 1,
        GrpcEndpoint = "http://localhost:50000",
        IsHealthy = true,
        StartTime = DateTime.UtcNow,
        LastHealthCheck = DateTime.UtcNow
    };

    public GrpcChannel? GetGrpcChannel() => _grpcChannel;

    public void SetTenantContext(string tenantId, InfraTenantProcessInfo? processInfo = null, GrpcChannel? grpcChannel = null)
    {
        _tenantId = tenantId;
        _processInfo = processInfo;
        _grpcChannel = grpcChannel;
    }

    public void ClearTenantContext()
    {
        _tenantId = "test-tenant";
        _processInfo = null;
        _grpcChannel = null;
    }

    public Task<bool> ValidateTenantAccessAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
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

        // Add default tenant header for all requests
        Client.DefaultRequestHeaders.Add("X-Tenant-ID", "test-tenant");
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

        if (valueStart < 7 || valueEnd < valueStart) return string.Empty;

        return content.Substring(valueStart, valueEnd - valueStart);
    }

    protected Task<string> GetJwtTokenAsync(string username = "testuser", string role = "User")
    {
        // For security tests, we return an empty token since the demo endpoints
        // don't actually validate JWT tokens - they just return demo responses.
        // The tests are verifying behavior of endpoints that exist in demo mode.
        return Task.FromResult(string.Empty);
    }

    protected void SetAuthorizationHeader(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            Client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }

    public void Dispose()
    {
        Client?.Dispose();
        Factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}
