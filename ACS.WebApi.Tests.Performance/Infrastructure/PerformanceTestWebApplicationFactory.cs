using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ACS.Service.Data;
using Microsoft.AspNetCore.Routing;

namespace ACS.WebApi.Tests.Performance.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for performance testing with optimized configuration
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

        builder.UseEnvironment("Testing");
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