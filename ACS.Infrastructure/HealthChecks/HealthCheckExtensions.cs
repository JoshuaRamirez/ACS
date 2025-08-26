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

        // Add database health check using custom implementation
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            healthChecksBuilder.AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, new[] { "database", "sql" });
        }

        // Add Redis health check if configured
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            // TODO: AddRedis extension method requires AspNetCore.HealthChecks.Redis package
            // healthChecksBuilder.AddRedis(
            //     redisConnectionString,
            //     name: "redis",
            //     failureStatus: HealthStatus.Degraded,
            //     tags: new[] { "cache", "redis" },
            //     timeout: TimeSpan.FromSeconds(3));
            healthChecksBuilder.AddCheck<RedisHealthCheck>("redis", HealthStatus.Degraded, new[] { "cache", "redis" });
        }

        // TODO: AddDiskStorageHealthCheck requires AspNetCore.HealthChecks.System package
        // Add disk space health check using custom implementation
        healthChecksBuilder.AddCheck<DiskSpaceHealthCheck>("disk_space", HealthStatus.Unhealthy, new[] { "infrastructure", "disk" });

        // TODO: AddPrivateMemoryHealthCheck requires AspNetCore.HealthChecks.System package
        // Add memory health check using custom implementation
        healthChecksBuilder.AddCheck<MemoryHealthCheck>("memory", HealthStatus.Degraded, new[] { "infrastructure", "memory" });

        // Add external service health checks
        var externalServices = configuration.GetSection("HealthChecks:ExternalServices").GetChildren();
        foreach (var service in externalServices)
        {
            var endpoint = service["Endpoint"];
            if (!string.IsNullOrEmpty(endpoint))
            {
                // TODO: AddUrlGroup requires AspNetCore.HealthChecks.Network package
                // healthChecksBuilder.AddUrlGroup(
                //     new Uri(endpoint),
                //     name: $"external_{service.Key.ToLower()}",
                //     failureStatus: HealthStatus.Degraded,
                //     tags: new[] { "external", service.Key.ToLower() },
                //     timeout: TimeSpan.FromSeconds(service.GetValue("TimeoutSeconds", 5)));
                healthChecksBuilder.AddCheck<ExternalServiceHealthCheck>($"external_{service.Key.ToLower()}", 
                    HealthStatus.Degraded, new[] { "external", service.Key.ToLower() });
            }
        }

        // Add advanced health checks using custom implementations
        // TODO: These extension methods are not available, using AddCheck instead
        healthChecksBuilder
            // TODO: BusinessLogicHealthCheck class is not accessible from Infrastructure layer
            // .AddCheck<BusinessLogicHealthCheck>("business_logic", HealthStatus.Unhealthy, new[] { "business", "domain", "services" })
            .AddCheck<TenantHealthCheck>("tenants", HealthStatus.Degraded, new[] { "tenants", "processes", "multi-tenant" })
            .AddCheck<IntegrationHealthCheck>("integration", HealthStatus.Degraded, new[] { "integration", "connectivity", "cross-component" });

        return services;
    }

    /// <summary>
    /// Adds health check UI
    /// </summary>
    public static IServiceCollection AddHealthChecksUI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO: AddHealthChecksUI requires AspNetCore.HealthChecks.UI package
        // Health check UI configuration would go here if package is available
        // For now, just return services without UI configuration
        /*
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
        */

        return services;
    }
}