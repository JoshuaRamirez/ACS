using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Data;
using ACS.Infrastructure.Services;
using ACS.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ACS.WebApi.Tests.Integration.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for integration tests with in-memory database and mock services
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public ApplicationDbContext DbContext { get; private set; } = null!;
    public string TestDatabaseName { get; } = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Set environment to Testing to avoid middleware conflicts
            context.HostingEnvironment.EnvironmentName = "Testing";

            // Add test-specific configuration
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source=:memory:",
                ["Authentication:Jwt:SecretKey"] = "TestSecretKeyForJwtThatIsLongEnoughForHS256",
                ["Authentication:Jwt:Issuer"] = "ACS.Tests",
                ["Authentication:Jwt:Audience"] = "ACS.Tests",
                ["Authentication:Jwt:ExpiryMinutes"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(TestDatabaseName);
                options.EnableSensitiveDataLogging();
                options.LogTo(Console.WriteLine, LogLevel.Information);
            });

            // Replace complex services with test implementations
            services.AddScoped<ACS.Infrastructure.Services.ITenantContextService, TestTenantContextService>();
            services.AddScoped<TenantGrpcClientService, TestTenantGrpcClientService>();
            services.AddScoped<ACS.Infrastructure.Services.IUserContextService, TestUserContextService>();

            // Configure test JWT authentication
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "ACS.Tests",
                    ValidAudience = "ACS.Tests",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestSecretKeyForJwtThatIsLongEnoughForHS256")),
                    ClockSkew = TimeSpan.Zero
                };
            });

            // Build service provider to get DbContext for seeding
            var serviceProvider = services.BuildServiceProvider();
            DbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Ensure database is created
            DbContext.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DbContext?.Database.EnsureDeleted();
            DbContext?.Dispose();
        }
        base.Dispose(disposing);
    }
}