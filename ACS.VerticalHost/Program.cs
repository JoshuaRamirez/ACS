using ACS.Infrastructure;
using ACS.Infrastructure.Authentication;
using ACS.Infrastructure.Extensions;
using ACS.Infrastructure.Security;
using ACS.Infrastructure.Services;
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

        // Single-tenant service infrastructure
        builder.Services.AddSingleton<TenantConfiguration>(provider => 
            new TenantConfiguration { TenantId = tenantId });

        // Domain services
        builder.Services.AddScoped<InMemoryEntityGraph>();
        builder.Services.AddSingleton<TenantDatabasePersistenceService>();
        builder.Services.AddSingleton<EventPersistenceService>();
        builder.Services.AddSingleton<DeadLetterQueueService>();
        builder.Services.AddSingleton<ErrorRecoveryService>();
        builder.Services.AddSingleton<HealthMonitoringService>();
        
        // Caching services
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ACS.Service.Caching.IEntityCache, ACS.Service.Caching.MemoryEntityCache>();
        
        builder.Services.AddSingleton<AccessControlDomainService>();
        builder.Services.AddSingleton<CommandTranslationService>();
        
        // Batch processing service
        builder.Services.AddSingleton<BatchProcessingService>();

        // Background services
        builder.Services.AddSingleton<TenantRingBuffer>();
        builder.Services.AddHostedService<TenantAccessControlHostedService>();
        builder.Services.AddHostedService<DeadLetterQueueService>();
        builder.Services.AddHostedService<HealthMonitoringService>();
        
        // Add performance metrics service
        builder.Services.AddPerformanceMetrics(builder.Configuration);
        
        // Add console dashboard (optional)
        builder.Services.AddConsoleDashboard(builder.Configuration);

        // Configure OpenTelemetry for distributed tracing
        builder.Services.ConfigureOpenTelemetry(builder.Configuration);

        // Configure gRPC compression options
        builder.Services.ConfigureGrpcCompression(builder.Configuration);
        
        // Add response compression middleware for gRPC
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            
            // Configure MIME types for gRPC compression
            options.MimeTypes = new[]
            {
                "application/grpc",
                "application/grpc-web",
                "application/grpc-web-text"
            };
        });
        
        // Configure compression provider levels
        builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
        {
            options.Level = builder.Configuration.GetValue<System.IO.Compression.CompressionLevel>("Compression:Gzip:Level", System.IO.Compression.CompressionLevel.Optimal);
        });
        
        builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
        {
            options.Level = builder.Configuration.GetValue<System.IO.Compression.CompressionLevel>("Compression:Brotli:Level", System.IO.Compression.CompressionLevel.Optimal);
        });
        
        // Add gRPC authentication
        builder.Services.AddGrpcAuthentication();
        
        // Add encryption services for tenant data protection
        builder.Services.AddEncryptionServices(builder.Configuration);
        
        // Add compression metrics interceptor
        builder.Services.AddSingleton<ACS.Infrastructure.Services.CompressionMetricsInterceptor>();
        builder.Services.AddGrpc().AddServiceOptions<VerticalGrpcService>(options =>
        {
            options.Interceptors.Add<ACS.Infrastructure.Authentication.GrpcAuthenticationInterceptor>();
            options.Interceptors.Add<ACS.Infrastructure.Services.CompressionMetricsInterceptor>();
        });

        // Add encryption interceptors to Entity Framework
        builder.Services.AddSingleton<EncryptionInterceptor>();
        builder.Services.AddSingleton<DecryptionInterceptor>();
        
        // Tenant-specific database context with optimized connection pooling and encryption
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
        
        // Add health checks for database connection pooling
        builder.Services.AddHealthChecks()
            .AddConnectionPoolHealthCheck(
                connectionString, 
                name: $"database_pool_{tenantId}",
                maxPoolSizeThreshold: 80);

        // Build and configure the application
        var app = builder.Build();

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
