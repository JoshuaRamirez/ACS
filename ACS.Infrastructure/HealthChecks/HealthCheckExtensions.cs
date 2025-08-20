using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ACS.Infrastructure.HealthChecks;

/// <summary>
/// Extension methods for registering health checks
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds comprehensive health checks to the service collection
    /// </summary>
    public static IServiceCollection AddHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register health check services
        services.AddSingleton<DatabaseHealthCheck>();
        services.AddSingleton<RedisHealthCheck>();
        services.AddSingleton<DiskSpaceHealthCheck>();
        services.AddSingleton<MemoryHealthCheck>();
        services.AddSingleton<ExternalServiceHealthCheckFactory>();
        
        // Register the main health check service
        services.AddSingleton<IHealthCheckService, HealthCheckService>();
        services.AddHostedService<HealthCheckService>(provider => 
            (HealthCheckService)provider.GetRequiredService<IHealthCheckService>());
        
        // Configure health check options
        services.Configure<HealthCheckServiceOptions>(
            configuration.GetSection("HealthChecks"));
        
        services.AddSingleton(provider =>
        {
            var config = configuration.GetSection("HealthChecks");
            return new HealthCheckServiceOptions
            {
                EnablePeriodicChecks = config.GetValue("EnablePeriodicChecks", true),
                CheckInterval = TimeSpan.FromSeconds(config.GetValue("CheckIntervalSeconds", 60)),
                InitialDelay = TimeSpan.FromSeconds(config.GetValue("InitialDelaySeconds", 10)),
                MaxHistoryEntries = config.GetValue("MaxHistoryEntries", 100),
                GrpcServices = config.GetSection("GrpcServices").Get<string[]>() ?? 
                    new[] { "AccessControl", "TenantService" }
            };
        });

        // Add ASP.NET Core health checks integration
        var healthChecksBuilder = services.AddHealthChecks();

        // Add database health check
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            healthChecksBuilder.AddSqlServer(
                connectionString,
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "database", "sql" },
                timeout: TimeSpan.FromSeconds(5));
        }

        // Add Redis health check if configured
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            healthChecksBuilder.AddRedis(
                redisConnectionString,
                name: "redis",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "cache", "redis" },
                timeout: TimeSpan.FromSeconds(3));
        }

        // Add disk space health check
        healthChecksBuilder.AddDiskStorageHealthCheck(
            setup =>
            {
                setup.AddDrive("C:\\", configuration.GetValue<long>("HealthChecks:DiskSpace:MinimumFreeMB", 1024));
            },
            name: "disk_space",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "infrastructure", "disk" });

        // Add memory health check
        healthChecksBuilder.AddPrivateMemoryHealthCheck(
            configuration.GetValue<long>("HealthChecks:Memory:MaxMemoryMB", 2048) * 1024 * 1024,
            name: "memory",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "infrastructure", "memory" });

        // Add external service health checks
        var externalServices = configuration.GetSection("HealthChecks:ExternalServices").GetChildren();
        foreach (var service in externalServices)
        {
            var endpoint = service["Endpoint"];
            if (!string.IsNullOrEmpty(endpoint))
            {
                healthChecksBuilder.AddUrlGroup(
                    new Uri(endpoint),
                    name: $"external_{service.Key.ToLower()}",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "external", service.Key.ToLower() },
                    timeout: TimeSpan.FromSeconds(service.GetValue("TimeoutSeconds", 5)));
            }
        }

        // Add advanced health checks
        healthChecksBuilder
            .AddBusinessLogicHealthCheck(
                name: "business_logic",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "business", "domain", "services" })
            .AddTenantHealthCheck(
                name: "tenants",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "tenants", "processes", "multi-tenant" })
            .AddIntegrationHealthCheck(
                name: "integration",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "integration", "connectivity", "cross-component" });

        return services;
    }

    /// <summary>
    /// Adds health check UI
    /// </summary>
    public static IServiceCollection AddHealthChecksUI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecksUI(options =>
        {
            options.SetEvaluationTimeInSeconds(30);
            options.MaximumHistoryEntriesPerEndpoint(50);
            options.SetApiMaxActiveRequests(1);
            
            // Add health check endpoints to monitor
            options.AddHealthCheckEndpoint("ACS API", "/api/health");
            
            // Add additional endpoints from configuration
            var endpoints = configuration.GetSection("HealthChecksUI:Endpoints").GetChildren();
            foreach (var endpoint in endpoints)
            {
                var name = endpoint["Name"];
                var uri = endpoint["Uri"];
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(uri))
                {
                    options.AddHealthCheckEndpoint(name, uri);
                }
            }
        })
        .AddInMemoryStorage();

        return services;
    }
}