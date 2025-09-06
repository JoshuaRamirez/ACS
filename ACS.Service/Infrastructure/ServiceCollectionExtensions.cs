using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using ACS.Service.Data;
using ACS.Service.Services;
using ACS.Service.Infrastructure;
using ACS.Service.Domain;

namespace ACS.Service.Infrastructure;

/// <summary>
/// Extension methods for registering ACS.Service layer dependencies
/// This handles the service layer registrations that were moved from Infrastructure
/// to maintain proper layering and avoid circular dependencies
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all core service layer dependencies
    /// This includes domain services, data access, and infrastructure components
    /// </summary>
    public static IServiceCollection AddAcsServiceLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register data access
        services.AddAcsDataAccess(configuration);
        
        // Register domain services
        services.AddAcsDomainServices();
        
        // Register infrastructure components
        services.AddServiceInfrastructure();
        
        // Register distributed cache (required by caching services)
        services.AddDistributedCache(configuration);
        
        return services;
    }

    /// <summary>
    /// Register data access services including DbContext and repositories
    /// </summary>
    public static IServiceCollection AddAcsDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext with connection pooling
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ACS_Development;Trusted_Connection=true;MultipleActiveResultSets=true";
        
        services.AddDbContextPool<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                
                sqlOptions.CommandTimeout(30);
                
                // Enable command batching
                sqlOptions.MinBatchSize(2);
                sqlOptions.MaxBatchSize(100);
            });
            
            // Enable sensitive data logging only in development
            if (configuration.GetValue<bool>("Logging:EnableSensitiveDataLogging"))
            {
                options.EnableSensitiveDataLogging();
            }
        });

        return services;
    }

    /// <summary>
    /// Register all domain services
    /// </summary>
    public static IServiceCollection AddAcsDomainServices(this IServiceCollection services)
    {
        // Core domain services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISystemMetricsService, SystemMetricsService>();
        
        // Authentication services
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        
        // Infrastructure database services
        services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
        services.AddScoped<IIndexAnalyzer, ACS.Service.Data.IndexAnalyzer>();
        services.AddScoped<IMigrationValidationService, MigrationValidationService>();
        
        return services;
    }

    /// <summary>
    /// Register service layer infrastructure components
    /// </summary>
    public static IServiceCollection AddServiceInfrastructure(this IServiceCollection services)
    {
        // In-memory entity graph (scoped to match DbContext scope)
        services.AddScoped<InMemoryEntityGraph>();
        
        // Command processing infrastructure
        services.AddScoped<ICommandProcessingService, CommandProcessingService>();
        
        // Unit of Work pattern
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // Tenant configuration services
        services.AddSingleton<ITenantConfigurationProvider, TenantConfigurationProvider>();
        services.AddSingleton(provider => 
        {
            // Create default tenant configuration for testing
            return new TenantConfiguration 
            { 
                TenantId = "default", 
                DatabaseConnectionString = "Server=localhost;Database=ACS_Default;Trusted_Connection=true;",
                DisplayName = "Default Tenant",
                CreatedAt = DateTime.UtcNow,
                IsActive = true 
            };
        });
        
        // Hosted services for background processing
        services.AddHostedService<TenantAccessControlHostedService>();
        
        return services;
    }

    /// <summary>
    /// Register distributed caching with fallback options
    /// </summary>
    public static IServiceCollection AddDistributedCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Check for Redis configuration first
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            // TODO: Uncomment when Redis package is available
            // services.AddStackExchangeRedisCache(options =>
            // {
            //     options.Configuration = redisConnectionString;
            //     options.InstanceName = configuration.GetValue<string>("Redis:InstanceName", "ACS");
            // });
            
            // Fallback to memory cache for now
            services.AddDistributedMemoryCache();
        }
        else
        {
            // Check for SQL Server cache configuration
            var sqlConnectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                // TODO: Uncomment when SQL Server cache package is available
                // services.AddSqlServerCache(options =>
                // {
                //     options.ConnectionString = sqlConnectionString;
                //     options.SchemaName = "dbo";
                //     options.TableName = "DataCache";
                //     options.DefaultSlidingExpiration = TimeSpan.FromMinutes(20);
                // });
                
                // Fallback to memory cache for now
                services.AddDistributedMemoryCache();
            }
            else
            {
                // Final fallback to in-memory distributed cache
                services.AddDistributedMemoryCache();
            }
        }

        return services;
    }
}