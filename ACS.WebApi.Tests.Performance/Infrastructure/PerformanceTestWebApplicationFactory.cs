using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ACS.Service.Data;
using ACS.Infrastructure;
using ACS.Infrastructure.Services;
using ACS.WebApi.Security.Validation;
using Microsoft.AspNetCore.Routing;
using Grpc.Net.Client;

namespace ACS.WebApi.Tests.Performance.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for performance testing with optimized configuration.
/// Registers mock implementations of infrastructure services to allow the WebAPI
/// to start without actual gRPC backends or tenant processes.
/// </summary>
public class PerformanceTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool _enableDetailedLogging;
    private readonly bool _useInMemoryDatabase;

    public PerformanceTestWebApplicationFactory(bool enableDetailedLogging = false, bool useInMemoryDatabase = true)
    {
        _enableDetailedLogging = enableDetailedLogging;
        _useInMemoryDatabase = useInMemoryDatabase;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Register mock infrastructure services BEFORE the app builds
            // These are required by TenantProcessResolutionMiddleware

            // Remove any existing registrations for these services
            services.RemoveAll<TenantProcessDiscoveryService>();
            services.RemoveAll<ITenantContextService>();
            services.RemoveAll<IUserContextService>();

            // Add mock implementations
            // Use a factory to provide the mock that returns values without starting processes
            services.AddSingleton<TenantProcessDiscoveryService>(provider =>
                new MockTenantProcessDiscoveryService(
                    provider.GetRequiredService<ILogger<TenantProcessDiscoveryService>>()));
            services.AddScoped<ITenantContextService, MockTenantContextService>();
            services.AddScoped<IUserContextService, MockUserContextService>();

            // Register IInputValidator if not already registered
            services.TryAddScoped<IInputValidator, InputValidator>();
            services.TryAddSingleton<IOptions<InputValidationOptions>>(
                Options.Create(new InputValidationOptions()));

            // Remove existing database context
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var dbContextServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextServiceDescriptor != null)
            {
                services.Remove(dbContextServiceDescriptor);
            }

            if (_useInMemoryDatabase)
            {
                // Add in-memory database for performance tests
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase($"PerformanceTestDb_{Guid.NewGuid()}");
                    options.EnableSensitiveDataLogging(false);
                    options.EnableServiceProviderCaching(true);
                    options.EnableDetailedErrors(false);
                });
            }
            else
            {
                // Use SQL Server for more realistic performance testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ACS_PerformanceTest;Trusted_Connection=true;MultipleActiveResultSets=true");
                    options.EnableSensitiveDataLogging(false);
                    options.EnableServiceProviderCaching(true);
                    options.EnableDetailedErrors(false);
                });
            }

            // Optimize for performance testing
            services.Configure<RouteOptions>(options =>
            {
                options.LowercaseUrls = true;
                options.LowercaseQueryStrings = true;
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            if (_enableDetailedLogging)
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            }
            else
            {
                logging.SetMinimumLevel(LogLevel.Error);
            }
        });

        // Set testing environment - middleware will check for this
        builder.UseEnvironment("Testing");

        // Set configuration to skip tenant process resolution in tests
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:SkipTenantProcessResolution"] = "true"
            });
        });
    }

    /// <summary>
    /// Creates an HttpClient that automatically adds the X-Tenant-ID header
    /// required by the TenantProcessResolutionMiddleware.
    /// </summary>
    public HttpClient CreateClientWithTenantHeader(string tenantId = "test-tenant")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);
        return client;
    }

    public async Task SeedTestDataAsync(int userCount = 1000, int groupCount = 100, int roleCount = 50)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Clear existing data
        context.Users.RemoveRange(context.Users);
        context.Groups.RemoveRange(context.Groups);
        context.Roles.RemoveRange(context.Roles);
        await context.SaveChangesAsync();

        // Seed test data for performance testing
        var random = new Random(42); // Fixed seed for reproducible tests

        // Create roles
        var roles = new List<Service.Data.Models.Role>();
        for (int i = 1; i <= roleCount; i++)
        {
            roles.Add(new Service.Data.Models.Role
            {
                Id = i,
                Name = $"Role_{i}",
                EntityId = i,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(365)),
                UpdatedAt = DateTime.UtcNow.AddDays(-random.Next(30))
            });
        }
        context.Roles.AddRange(roles);

        // Create groups
        var groups = new List<Service.Data.Models.Group>();
        for (int i = 1; i <= groupCount; i++)
        {
            groups.Add(new Service.Data.Models.Group
            {
                Id = i,
                Name = $"Group_{i}",
                EntityId = i,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(365)),
                UpdatedAt = DateTime.UtcNow.AddDays(-random.Next(30))
            });
        }
        context.Groups.AddRange(groups);

        // Create users
        var users = new List<Service.Data.Models.User>();
        for (int i = 1; i <= userCount; i++)
        {
            users.Add(new Service.Data.Models.User
            {
                Id = i,
                Name = $"User_{i}",
                Email = $"user{i}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                EntityId = i,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(365)),
                UpdatedAt = DateTime.UtcNow.AddDays(-random.Next(30))
            });
        }
        context.Users.AddRange(users);

        await context.SaveChangesAsync();

        Console.WriteLine($"Seeded {userCount} users, {groupCount} groups, and {roleCount} roles for performance testing");
    }
}

/// <summary>
/// Mock TenantProcessDiscoveryService for testing.
/// Returns fake tenant process info without starting actual processes.
/// </summary>
internal class MockTenantProcessDiscoveryService : TenantProcessDiscoveryService
{
    public MockTenantProcessDiscoveryService(ILogger<TenantProcessDiscoveryService> logger) : base(logger)
    {
    }

    public new Task<TenantProcessInfo> GetOrStartTenantProcessAsync(string tenantId)
    {
        // Return a mock tenant process without actually starting anything
        return Task.FromResult(new TenantProcessInfo
        {
            TenantId = tenantId,
            ProcessId = 12345, // Fake process ID
            GrpcEndpoint = "http://localhost:50000",
            StartTime = DateTime.UtcNow,
            IsHealthy = true,
            LastHealthCheck = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Mock TenantContextService for testing.
/// Provides tenant context without requiring actual infrastructure.
/// </summary>
internal class MockTenantContextService : ITenantContextService
{
    private string _tenantId = "test-tenant";
    private TenantProcessInfo? _processInfo;

    public string? GetTenantId() => _tenantId;

    public string GetRequiredTenantId() => _tenantId;

    public TenantProcessInfo? GetTenantProcessInfo() => _processInfo;

    public GrpcChannel? GetGrpcChannel() => null;

    public void SetTenantContext(string tenantId, TenantProcessInfo? processInfo = null, GrpcChannel? grpcChannel = null)
    {
        _tenantId = tenantId;
        _processInfo = processInfo;
    }

    public void ClearTenantContext()
    {
        _tenantId = "test-tenant";
        _processInfo = null;
    }

    public Task<bool> ValidateTenantAccessAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true); // Always allow access in tests
    }
}

/// <summary>
/// Mock UserContextService for testing.
/// Provides user context without requiring actual authentication.
/// </summary>
internal class MockUserContextService : IUserContextService
{
    public string GetCurrentUserId() => "test-user-id";
    public string GetCurrentUserName() => "Test User";
    public string GetTenantId() => "test-tenant";
    public bool IsAuthenticated() => true;
    public IEnumerable<string> GetUserRoles() => new[] { "Admin", "User" };
    public bool HasRole(string role) => role == "Admin" || role == "User";
    public string? GetUserEmail() => "test@example.com";
    public string? GetClaim(string claimType) => claimType switch
    {
        "sub" => "test-user-id",
        "name" => "Test User",
        "email" => "test@example.com",
        _ => null
    };
    public IEnumerable<(string Type, string Value)> GetAllClaims() => new[]
    {
        ("sub", "test-user-id"),
        ("name", "Test User"),
        ("email", "test@example.com"),
        ("role", "Admin")
    };
}