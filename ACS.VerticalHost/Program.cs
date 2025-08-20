using ACS.Infrastructure;
using ACS.Infrastructure.Authentication;
using ACS.Infrastructure.DependencyInjection;
using ACS.Infrastructure.Extensions;
using ACS.Infrastructure.Security;
using ACS.Infrastructure.Services;
using ACS.Infrastructure.Telemetry;
using ACS.Service.Data;
using ACS.Service.Infrastructure;
using ACS.Service.Services;
using ACS.VerticalHost.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ACS.VerticalHost;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Parse command line arguments
        var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
        var grpcPort = 50051; // default

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--tenant" && i + 1 < args.Length)
            {
                tenantId = args[i + 1];
                i++; // Skip next arg
            }
            else if (args[i] == "--port" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var port))
                {
                    grpcPort = port;
                }
                i++; // Skip next arg
            }
        }

        // Also check environment variable for port if not set via args
        if (Environment.GetEnvironmentVariable("GRPC_PORT") is string portEnv && int.TryParse(portEnv, out var envPort))
        {
            grpcPort = envPort;
        }

        if (string.IsNullOrEmpty(tenantId))
        {
            throw new InvalidOperationException("Tenant ID must be provided via --tenant argument or TENANT_ID environment variable");
        }

        // Configure Kestrel to listen on the specified gRPC port
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(grpcPort, listenOptions =>
            {
                listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
            });
        });

        // Set tenant ID in configuration for service registration
        builder.Configuration["TenantId"] = tenantId;
        
        // Configure all services using centralized registration
        var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
        builder.Services.ConfigureServices(builder.Configuration, logger, "VerticalHost");
        
        // Configure comprehensive OpenTelemetry for distributed tracing, metrics, and logging
        builder.Services.ConfigureOpenTelemetryForVerticalHost(builder.Configuration, builder.Environment);
        
        // Add VerticalHost-specific services
        builder.Services.AddHostedService<TenantAccessControlHostedService>();
        
        // Add gRPC service with interceptors
        builder.Services.AddGrpc().AddServiceOptions<VerticalGrpcService>(options =>
        {
            options.Interceptors.Add<ACS.Infrastructure.Authentication.GrpcAuthenticationInterceptor>();
            options.Interceptors.Add<ACS.Infrastructure.Services.CompressionMetricsInterceptor>();
        });

        // Override DbContext configuration for tenant-specific connection
        var connectionString = GetTenantConnectionString(tenantId);
        builder.Services.AddDbContextPool<ApplicationDbContext>((serviceProvider, optionsBuilder) =>
        {
            ACS.Service.Data.DatabaseConnectionPooling.ConfigureConnectionPooling(
                optionsBuilder, 
                connectionString, 
                tenantId, 
                builder.Configuration);
                
            // Configure encryption interceptors
            optionsBuilder.ConfigureEncryptionInterceptors(serviceProvider);
        }, 
        poolSize: builder.Configuration.GetValue<int>("Database:DbContextPoolSize", 128));
        
        // Add tenant-specific health checks
        builder.Services.AddHealthChecks()
            .AddConnectionPoolHealthCheck(
                connectionString, 
                name: $"database_pool_{tenantId}",
                maxPoolSizeThreshold: 80);

        // Build and configure the application
        var app = builder.Build();

        // Validate service registration
        using (var scope = app.Services.CreateScope())
        {
            var serviceLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            ServiceRegistrationValidator.ValidateServices(scope.ServiceProvider, serviceLogger);
        }

        // Enable response compression middleware
        app.UseResponseCompression();
        
        // Configure gRPC endpoint
        app.MapGrpcService<VerticalGrpcService>();
        
        // Map health check endpoint
        app.MapHealthChecks("/health");

        // Initialize domain services
        using (var scope = app.Services.CreateScope())
        {
            var entityGraph = scope.ServiceProvider.GetRequiredService<InMemoryEntityGraph>();
            var domainService = scope.ServiceProvider.GetRequiredService<AccessControlDomainService>();
            
            // Load entity graph and hydrate normalizers
            await domainService.LoadEntityGraphAsync();
            
            Console.WriteLine($"Entity graph loaded for tenant: {tenantId}");
        }

        Console.WriteLine($"Starting VerticalHost for tenant: {tenantId} on gRPC port: {grpcPort}");
        await app.RunAsync();
    }

    public static string GetTenantConnectionString(string tenantId)
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("BASE_CONNECTION_STRING") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=ACS_{TenantId};Trusted_Connection=true;MultipleActiveResultSets=true";
        
        return baseConnectionString.Replace("{TenantId}", tenantId);
    }
}
