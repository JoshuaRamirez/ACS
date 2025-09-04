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
using ACS.VerticalHost.Handlers;
using ACS.VerticalHost.Commands;
using ACS.VerticalHost.Extensions;
using CommandIndexAnalysisReport = ACS.VerticalHost.Commands.IndexAnalysisReport;
using CommandMissingIndexRecommendation = ACS.VerticalHost.Commands.MissingIndexRecommendation;
using CommandMetricsSnapshot = ACS.VerticalHost.Commands.MetricsSnapshot;
using CommandMetricDataPoint = ACS.VerticalHost.Commands.MetricDataPoint;
using CommandDashboardData = ACS.VerticalHost.Commands.DashboardData;
using CommandDashboardInfo = ACS.VerticalHost.Commands.DashboardInfo;
using CommandDashboardConfiguration = ACS.VerticalHost.Commands.DashboardConfiguration;
using CommandRateLimitStatus = ACS.VerticalHost.Commands.RateLimitStatus;
using CommandAggregatedRateLimitMetrics = ACS.VerticalHost.Commands.AggregatedRateLimitMetrics;
using ACS.Service.Domain;
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
        
        // ================================
        // VERTICAL HOST: BUSINESS LOGIC LAYER
        // Contains ALL business services and domain logic
        // Includes command buffer for sequential processing
        // ================================
        
        using var loggerFactory = LoggerFactory.Create(options => options.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        
        // Configure ALL business services (this is where they belong)
        builder.Services.ConfigureServices(builder.Configuration, logger, "VerticalHost");
        
        // Add service layer dependencies (includes InMemoryEntityGraph, IAuthenticationService, etc.)
        builder.Services.AddAcsServiceLayer(builder.Configuration);
        
        // ================================
        // COMMAND BUFFER SYSTEM
        // ================================
        
        // Register the command buffer as singleton (per tenant process)
        builder.Services.AddSingleton<ICommandBuffer, CommandBuffer>();
        
        // Register command/query handlers using auto-registration
        builder.Services.AddHandlersAutoRegistration();
        
        // Add VerticalHost-specific services
        builder.Services.AddHostedService<TenantAccessControlHostedService>();
        builder.Services.AddHostedService<CommandBufferHostedService>();
        
        // Configure comprehensive OpenTelemetry for distributed tracing, metrics, and logging
        builder.Services.ConfigureOpenTelemetryForVerticalHost(builder.Configuration, builder.Environment);
        
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
                maxPoolSizeThreshold: 80)
            .AddCheck<CommandBufferHealthCheck>("command_buffer")
            .AddCheck<InMemoryEntityGraphHealthCheck>("entity_graph");

        // Build and configure the application
        var app = builder.Build();

        // Validate service registration
        using (var scope = app.Services.CreateScope())
        {
            var serviceLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            ServiceRegistrationValidator.ValidateServices(scope.ServiceProvider, serviceLogger);
            
            // Validate that command buffer is properly configured
            var commandBuffer = scope.ServiceProvider.GetRequiredService<ICommandBuffer>();
            serviceLogger.LogInformation("✅ Command buffer initialized successfully");
        }

        // Enable response compression middleware
        app.UseResponseCompression();
        
        // Configure gRPC endpoint
        app.MapGrpcService<VerticalGrpcService>();
        
        // Map health check endpoint
        app.MapHealthChecks("/health");

        // Initialize entity graph and command buffer
        using (var scope = app.Services.CreateScope())
        {
            var entityGraph = scope.ServiceProvider.GetRequiredService<InMemoryEntityGraph>();
            var commandBuffer = scope.ServiceProvider.GetRequiredService<ICommandBuffer>();
            
            // Start command buffer processing
            await commandBuffer.StartAsync();
            
            // Entity graph will be loaded lazily by the services as needed
            Console.WriteLine($"✅ VerticalHost initialized for tenant: {tenantId}");
            Console.WriteLine($"📊 Command buffer active - sequential processing enabled");
            Console.WriteLine($"💾 In-memory entity graph ready");
        }

        Console.WriteLine($"🚀 Starting VerticalHost for tenant: {tenantId} on gRPC port: {grpcPort}");
        Console.WriteLine($"🏗️  Architecture: HTTP API → gRPC → Command Buffer → Business Logic → Database");
        
        await app.RunAsync();
    }

    public static string GetTenantConnectionString(string tenantId)
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("BASE_CONNECTION_STRING") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=ACS_{TenantId};Trusted_Connection=true;MultipleActiveResultSets=true";
        
        return baseConnectionString.Replace("{TenantId}", tenantId);
    }
}

/// <summary>
/// Extension methods for registering command/query handlers
/// </summary>
public static class CommandQueryHandlerRegistration
{
    public static IServiceCollection AddCommandQueryHandlers(this IServiceCollection services)
    {
        // 🎯 CONVENTION-BASED AUTO-REGISTRATION
        // Automatically discovers and registers all handlers in ACS.VerticalHost.Handlers namespace
        // Replaces 70+ lines of manual registration with reflection-based discovery
        return services.AddHandlersAutoRegistration();
    }
}

/// <summary>
/// Hosted service to manage command buffer lifecycle
/// </summary>
public class CommandBufferHostedService : IHostedService
{
    private readonly ICommandBuffer _commandBuffer;
    private readonly ILogger<CommandBufferHostedService> _logger;

    public CommandBufferHostedService(
        ICommandBuffer commandBuffer,
        ILogger<CommandBufferHostedService> logger)
    {
        _commandBuffer = commandBuffer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Command Buffer processing");
        await _commandBuffer.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Command Buffer processing");
        await _commandBuffer.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Health check for command buffer
/// </summary>
public class CommandBufferHealthCheck : IHealthCheck
{
    private readonly ICommandBuffer _commandBuffer;

    public CommandBufferHealthCheck(ICommandBuffer commandBuffer)
    {
        _commandBuffer = commandBuffer;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = _commandBuffer.GetStats();
            
            var data = new Dictionary<string, object>
            {
                ["uptime_seconds"] = stats.UptimeSeconds,
                ["commands_processed"] = stats.CommandsProcessed,
                ["queries_processed"] = stats.QueriesProcessed,
                ["commands_in_flight"] = stats.CommandsInFlight,
                ["commands_per_second"] = stats.CommandsPerSecond,
                ["queries_per_second"] = stats.QueriesPerSecond,
                ["channel_usage"] = stats.ChannelUsage,
                ["channel_capacity"] = stats.ChannelCapacity,
                ["recent_errors"] = stats.RecentErrors.Count
            };
            
            var isHealthy = stats.CommandsInFlight < stats.ChannelCapacity * 0.9; // Under 90% capacity
            
            return Task.FromResult(isHealthy 
                ? HealthCheckResult.Healthy("Command buffer is operating normally", data)
                : HealthCheckResult.Degraded("Command buffer is under high load", null, data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Command buffer health check failed", ex));
        }
    }
}

/// <summary>
/// Health check for in-memory entity graph
/// </summary>
public class InMemoryEntityGraphHealthCheck : IHealthCheck
{
    private readonly InMemoryEntityGraph _entityGraph;

    public InMemoryEntityGraphHealthCheck(InMemoryEntityGraph entityGraph)
    {
        _entityGraph = entityGraph;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                ["total_entities"] = _entityGraph.TotalEntityCount,
                ["users"] = _entityGraph.Users.Count,
                ["groups"] = _entityGraph.Groups.Count,
                ["roles"] = _entityGraph.Roles.Count,
                ["permissions"] = _entityGraph.Permissions.Count,
                ["last_load_time"] = _entityGraph.LastLoadTime,
                ["load_duration_ms"] = _entityGraph.LoadDuration.TotalMilliseconds,
                ["memory_usage_mb"] = _entityGraph.MemoryUsageBytes / 1024.0 / 1024.0
            };
            
            var isHealthy = _entityGraph.TotalEntityCount > 0;
            
            return Task.FromResult(isHealthy 
                ? HealthCheckResult.Healthy("Entity graph is loaded and operational", data)
                : HealthCheckResult.Unhealthy("Entity graph is not loaded", null, data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Entity graph health check failed", ex));
        }
    }
}