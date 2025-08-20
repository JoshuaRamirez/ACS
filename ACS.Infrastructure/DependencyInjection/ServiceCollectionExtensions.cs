using ACS.Infrastructure.Authentication;
using ACS.Infrastructure.Caching;
using ACS.Infrastructure.Compression;
using ACS.Infrastructure.Diagnostics;
using ACS.Infrastructure.Grpc;
using ACS.Infrastructure.Logging;
using ACS.Infrastructure.Monitoring;
using ACS.Infrastructure.Optimization;
using ACS.Infrastructure.Performance;
using ACS.Infrastructure.RateLimiting;
using ACS.Infrastructure.Security;
using ACS.Infrastructure.Security.KeyVault;
using ACS.Infrastructure.Services;
using ACS.Service.Compliance;
using ACS.Service.Data;
using ACS.Service.Data.Repositories;
using ACS.Service.Domain.Events;
using ACS.Service.Domain.Specifications;
using ACS.Service.Infrastructure;
using ACS.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Text;

namespace ACS.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for service registration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all core services for the ACS application
    /// </summary>
    public static IServiceCollection AddAcsCore(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        // Add basic services
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddLogging();
        
        // Add security services
        services.AddAcsSecurity(configuration, isDevelopment);
        
        // Add data access services
        services.AddAcsDataAccess(configuration);
        
        // Add domain services
        services.AddAcsDomainServices();
        
        // Add infrastructure services
        services.AddAcsInfrastructure(configuration);
        
        // Add logging services
        services.AddAcsLogging(configuration);
        
        // Add diagnostic services
        services.AddAcsDiagnostics();
        
        // Add caching services
        services.AddAcsCaching(configuration);
        
        // Add performance optimization services
        services.AddAcsPerformance(configuration);
        
        // Add compliance services
        services.AddAcsCompliance(configuration);
        
        return services;
    }

    /// <summary>
    /// Register security-related services
    /// </summary>
    public static IServiceCollection AddAcsSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        // JWT Authentication
        var jwtSecretKey = configuration.GetValue<string>("Authentication:Jwt:SecretKey") 
            ?? "your-super-secret-key-here-at-least-256-bits-long-for-production";
        var jwtIssuer = configuration.GetValue<string>("Authentication:Jwt:Issuer") ?? "ACS.WebApi";
        var jwtAudience = configuration.GetValue<string>("Authentication:Jwt:Audience") ?? "ACS.VerticalHost";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
            });

        services.AddAuthorization();

        // Password hashing service
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        
        // Authentication service
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        
        // JWT token service
        services.AddSingleton<JwtTokenService>();
        
        // Key Vault services (if not in development)
        if (!isDevelopment)
        {
            services.AddSecretManagement(configuration);
        }
        
        // Encryption services
        services.AddEncryptionServices(configuration);
        
        // gRPC authentication
        services.AddGrpcAuthentication();
        
        return services;
    }

    /// <summary>
    /// Register data access services
    /// </summary>
    public static IServiceCollection AddAcsDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext
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
            
            // Configure database performance
            options.ConfigureDatabasePerformance(configuration);
            
            // Add performance interceptor
            var performanceInterceptor = services.BuildServiceProvider().GetService<DatabasePerformanceInterceptor>();
            if (performanceInterceptor != null)
            {
                options.AddInterceptors(performanceInterceptor);
            }
            
            // Enable sensitive data logging only in development
            if (configuration.GetValue<bool>("Logging:EnableSensitiveDataLogging"))
            {
                options.EnableSensitiveDataLogging();
            }
        });
        
        // Register repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        
        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // Register data seeding service
        services.AddScoped<IDataSeeder, DataSeeder>();
        
        return services;
    }

    /// <summary>
    /// Register domain services
    /// </summary>
    public static IServiceCollection AddAcsDomainServices(this IServiceCollection services)
    {
        // Core domain services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<IPermissionEvaluationService, PermissionEvaluationService>();
        services.AddScoped<IAuditService, AuditService>();
        
        // Command processing
        services.AddScoped<ICommandProcessingService, CommandProcessingService>();
        services.AddSingleton<CommandTranslationService>();
        
        // Access control domain service
        services.AddScoped<AccessControlDomainService>();
        
        // In-memory entity graph
        services.AddScoped<InMemoryEntityGraph>();
        
        // Normalizer orchestration
        services.AddScoped<INormalizerOrchestrationService, NormalizerOrchestrationService>();
        
        // Domain event handling
        services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<UserCreatedEvent>, UserEventHandler>();
        services.AddScoped<IDomainEventHandler<GroupCreatedEvent>, GroupEventHandler>();
        services.AddScoped<IDomainEventHandler<RoleCreatedEvent>, RoleEventHandler>();
        services.AddScoped<IDomainEventHandler<PermissionGrantedEvent>, PermissionEventHandler>();
        
        // Specifications
        services.AddScoped<ISpecificationEvaluator, SpecificationEvaluator>();
        
        return services;
    }

    /// <summary>
    /// Register infrastructure services
    /// </summary>
    public static IServiceCollection AddAcsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Tenant and user context services
        services.AddScoped<ITenantContextService, Services.TenantContextService>();
        services.AddScoped<IUserContextService, Services.UserContextService>();
        services.AddSingleton<TenantProcessDiscoveryService>();
        
        // gRPC services
        services.AddScoped<TenantGrpcClientService>();
        services.AddScoped<GrpcErrorMappingService>();
        services.AddSingleton<CircuitBreakerService>();
        services.AddSingleton<IGrpcStreamingService, GrpcStreamingService>();
        services.AddSingleton<IGrpcErrorHandler, GrpcErrorHandler>();
        
        // Background services
        services.AddSingleton<TenantRingBuffer>();
        services.AddSingleton<EventPersistenceService>();
        services.AddSingleton<DeadLetterQueueService>();
        services.AddSingleton<ErrorRecoveryService>();
        services.AddSingleton<HealthMonitoringService>();
        services.AddSingleton<BatchProcessingService>();
        services.AddSingleton<TenantDatabasePersistenceService>();
        
        // Add as hosted services
        services.AddHostedService<DeadLetterQueueService>();
        services.AddHostedService<HealthMonitoringService>();
        
        // Tenant metrics
        services.AddTenantMetrics();
        
        // Rate limiting
        services.AddRateLimiting(configuration);
        
        // OpenTelemetry
        services.ConfigureOpenTelemetry(configuration);
        
        // Performance metrics
        services.AddPerformanceMetrics(configuration);
        
        // gRPC compression
        services.ConfigureGrpcCompression(configuration);
        
        return services;
    }

    /// <summary>
    /// Register compliance services
    /// </summary>
    public static IServiceCollection AddAcsCompliance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Compliance audit service
        services.Configure<ComplianceOptions>(configuration.GetSection("Compliance"));
        services.AddScoped<IComplianceAuditService, ComplianceAuditService>();
        
        return services;
    }

    /// <summary>
    /// Register services for WebAPI project
    /// </summary>
    public static IServiceCollection AddAcsWebApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add core services
        services.AddAcsCore(configuration);
        
        // Add API-specific services
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        
        // Add CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader());
        });
        
        return services;
    }

    /// <summary>
    /// Register services for VerticalHost project
    /// </summary>
    public static IServiceCollection AddAcsVerticalHost(
        this IServiceCollection services,
        IConfiguration configuration,
        string tenantId)
    {
        // Tenant configuration
        services.AddSingleton(new TenantConfiguration { TenantId = tenantId });
        
        // Add core services (without WebAPI-specific ones)
        services.AddAcsCore(configuration);
        
        // Add gRPC services
        services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = configuration.GetValue<bool>("Grpc:EnableDetailedErrors");
            options.MaxReceiveMessageSize = configuration.GetValue<int>("Grpc:MaxReceiveMessageSize", 16 * 1024 * 1024);
            options.MaxSendMessageSize = configuration.GetValue<int>("Grpc:MaxSendMessageSize", 16 * 1024 * 1024);
        });
        
        // Add response compression for gRPC
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.MimeTypes = new[]
            {
                "application/grpc",
                "application/grpc-web",
                "application/grpc-web-text"
            };
        });
        
        // Configure compression levels
        services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
        {
            options.Level = configuration.GetValue<System.IO.Compression.CompressionLevel>(
                "Compression:Gzip:Level", 
                System.IO.Compression.CompressionLevel.Optimal);
        });
        
        services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
        {
            options.Level = configuration.GetValue<System.IO.Compression.CompressionLevel>(
                "Compression:Brotli:Level", 
                System.IO.Compression.CompressionLevel.Optimal);
        });
        
        // Add health checks
        services.AddHealthChecks();
        
        return services;
    }

    /// <summary>
    /// Register services for Dashboard project
    /// </summary>
    public static IServiceCollection AddAcsDashboard(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add core services
        services.AddAcsCore(configuration, isDevelopment: true);
        
        // Add Blazor services
        services.AddRazorPages();
        services.AddServerSideBlazor();
        
        // Add SignalR for real-time updates
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = configuration.GetValue<bool>("SignalR:EnableDetailedErrors");
        });
        
        // Add dashboard-specific services
        services.AddConsoleDashboard(configuration);
        
        return services;
    }

    /// <summary>
    /// Configure all services with proper error handling
    /// </summary>
    public static IServiceCollection ConfigureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger logger,
        string projectType = "WebApi")
    {
        try
        {
            logger.LogInformation("Configuring services for {ProjectType}", projectType);
            
            switch (projectType.ToLowerInvariant())
            {
                case "webapi":
                    services.AddAcsWebApi(configuration);
                    break;
                    
                case "verticalhost":
                    var tenantId = configuration["TenantId"] ?? 
                                  Environment.GetEnvironmentVariable("TENANT_ID") ?? 
                                  throw new InvalidOperationException("Tenant ID not configured");
                    services.AddAcsVerticalHost(configuration, tenantId);
                    break;
                    
                case "dashboard":
                    services.AddAcsDashboard(configuration);
                    break;
                    
                default:
                    services.AddAcsCore(configuration);
                    break;
            }
            
            logger.LogInformation("Services configured successfully for {ProjectType}", projectType);
            return services;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to configure services for {ProjectType}", projectType);
            throw;
        }
    }
}

/// <summary>
/// Service registration validation
/// </summary>
public static class ServiceRegistrationValidator
{
    /// <summary>
    /// Validate that all required services are registered
    /// </summary>
    public static void ValidateServices(IServiceProvider serviceProvider, ILogger logger)
    {
        var requiredServices = new[]
        {
            typeof(IUserService),
            typeof(IGroupService),
            typeof(IRoleService),
            typeof(IResourceService),
            typeof(IPermissionEvaluationService),
            typeof(IAuditService),
            typeof(IComplianceAuditService),
            typeof(ApplicationDbContext),
            typeof(IUnitOfWork)
        };

        var missingServices = new List<Type>();

        foreach (var serviceType in requiredServices)
        {
            try
            {
                var service = serviceProvider.GetService(serviceType);
                if (service == null)
                {
                    missingServices.Add(serviceType);
                    logger.LogWarning("Required service {ServiceType} is not registered", serviceType.Name);
                }
                else
                {
                    logger.LogDebug("Service {ServiceType} is registered", serviceType.Name);
                }
            }
            catch (Exception ex)
            {
                missingServices.Add(serviceType);
                logger.LogError(ex, "Error resolving service {ServiceType}", serviceType.Name);
            }
        }

        if (missingServices.Any())
        {
            var message = $"Missing required services: {string.Join(", ", missingServices.Select(t => t.Name))}";
            logger.LogError(message);
            throw new InvalidOperationException(message);
        }

        logger.LogInformation("All required services are registered successfully");
    }

    /// <summary>
    /// Register comprehensive caching services
    /// </summary>
    public static IServiceCollection AddAcsCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Cache strategy
        services.AddSingleton<ICacheStrategy, DefaultCacheStrategy>();
        
        // Multi-level cache
        services.AddSingleton<IMultiLevelCache, MultiLevelCache>();
        
        // Cache-aside service
        services.AddScoped<ICacheAsideService, CacheAsideService>();
        
        // Cache invalidation service
        services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
        
        // Add Redis distributed cache if configured
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = configuration.GetValue<string>("Redis:InstanceName", "ACS");
            });
        }
        else
        {
            // Use SQL Server distributed cache as fallback
            var sqlConnectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                services.AddSqlServerCache(options =>
                {
                    options.ConnectionString = sqlConnectionString;
                    options.SchemaName = "dbo";
                    options.TableName = "DataCache";
                    options.DefaultSlidingExpiration = TimeSpan.FromMinutes(20);
                });
            }
        }
        
        // Cached service decorators (manually configure)
        // Note: In production, consider using Scrutor package for automatic decoration
        // For now, services will need to explicitly inject ICacheAsideService for caching
        
        // Legacy cache services for backward compatibility
        services.AddScoped<ACS.Service.Caching.IEntityCache, ACS.Service.Caching.MemoryEntityCache>();
        
        return services;
    }

    /// <summary>
    /// Register performance optimization services
    /// </summary>
    public static IServiceCollection AddAcsPerformance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Query optimizer
        services.AddScoped<IQueryOptimizer, QueryOptimizer>();
        
        // Performance configuration
        services.AddSingleton(provider => 
            LazyLoadingConfiguration.GetPerformanceConfiguration(configuration));
        
        // Register optimized repositories
        services.AddScoped<OptimizedUserRepository>();
        
        // Database performance monitoring
        services.AddSingleton<DatabasePerformanceInterceptor>();
        services.AddSingleton<IConnectionPoolMonitor, ConnectionPoolMonitor>();
        services.AddScoped<IBatchProcessor, BatchProcessor>();
        services.AddHostedService<DatabasePerformanceMonitorService>();
        
        // Monitoring and metrics
        services.AddSingleton<IMetricsCollector, MetricsCollector>();
        services.AddSingleton<IDashboardService, DashboardService>();
        
        // Compression services
        services.AddSingleton<ICompressionService, CompressionService>();
        
        // Minification services
        services.AddSingleton<IMinificationService, MinificationService>();
        
        // Bundling services
        services.AddSingleton<IBundlingService>(provider =>
        {
            var env = provider.GetRequiredService<IHostEnvironment>();
            var config = provider.GetRequiredService<IConfiguration>();
            var logger = provider.GetRequiredService<ILogger<BundlingService>>();
            var minification = provider.GetRequiredService<IMinificationService>();
            var cache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var fileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, "wwwroot"));
            
            return new BundlingService(env, config, logger, minification, cache, fileProvider);
        });
        
        return services;
    }
    
    /// <summary>
    /// Register logging services with correlation support
    /// </summary>
    public static IServiceCollection AddAcsLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Correlation services
        services.AddSingleton<ICorrelationService, CorrelationService>();
        
        // Configure structured logging options
        services.Configure<StructuredLoggingOptions>(
            configuration.GetSection("Logging:StructuredLogging"));
        
        // Add custom console formatter for structured logging
        services.Configure<Microsoft.Extensions.Logging.Console.ConsoleLoggerOptions>(options =>
        {
            options.FormatterName = "structured";
        });
        
        services.AddSingleton<Microsoft.Extensions.Logging.Console.ConsoleFormatter, StructuredLoggingFormatter>();
        
        return services;
    }
    
    /// <summary>
    /// Register diagnostic services
    /// </summary>
    public static IServiceCollection AddAcsDiagnostics(this IServiceCollection services)
    {
        // Diagnostic service
        services.AddSingleton<IDiagnosticService, DiagnosticService>();
        
        return services;
    }
    
}