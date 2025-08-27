using ACS.WebApi.Services;

namespace ACS.WebApi.Configuration;

/// <summary>
/// Enforces clean architecture boundaries for HTTP API layer
/// Only allows dependencies that support the HTTP proxy pattern
/// ZERO business service dependencies allowed
/// </summary>
public static class CleanArchitectureServiceCollectionExtensions
{
    /// <summary>
    /// Registers ONLY the services allowed in HTTP API layer
    /// Acts as architectural guardrail - explicitly excludes business services
    /// </summary>
    public static IServiceCollection AddHttpProxyServices(this IServiceCollection services)
    {
        // ALLOWED: Pure gRPC proxy client (only allowed business dependency)
        services.AddScoped<IVerticalHostClient, VerticalHostClient>();
        
        // ALLOWED: HTTP-specific infrastructure services - simplified for clean architecture
        // Context services handled by infrastructure layer
        // services.AddScoped<ITenantContextService, TenantContextService>(); // Removed - use infrastructure
        // services.AddScoped<IUserContextService, UserContextService>(); // Removed - use infrastructure 
        // services.AddScoped<CircuitBreakerService>(); // Removed - static class
        // services.AddScoped<TelemetryService>(); // Removed - static class
        
        // ALLOWED: HTTP mapping (contracts only, no business logic) - REMOVED
        // Resource mappers not needed since controllers are pure proxies
        // services.AddScoped<IResourceMapper, ResourceMapper>(); // Removed - pure proxy pattern
        
        // EXPLICITLY FORBIDDEN: Business services (will throw if registered)
        services.AddForbiddenServiceDetection();
        
        return services;
    }
    
    /// <summary>
    /// Adds detection for forbidden business service dependencies
    /// Throws at startup if any business services are accidentally registered
    /// </summary>
    private static IServiceCollection AddForbiddenServiceDetection(this IServiceCollection services)
    {
        // List of business services that should NOT be in HTTP API
        var forbiddenServices = new[]
        {
            "IUserService",
            "IGroupService", 
            "IRoleService",
            "IResourceService",
            "IAuditService",
            "IPermissionEvaluationService",
            "IComplianceAuditService",
            "INormalizerOrchestrationService",
            "ICommandProcessingService",
            "IBatchProcessingService"
        };
        
        // Add a service that validates architecture at startup
        services.AddSingleton<IHostedService>(provider => 
            new ArchitecturalBoundaryValidator(provider, forbiddenServices));
            
        return services;
    }
}

/// <summary>
/// Validates architectural boundaries at application startup
/// Ensures HTTP API doesn't accidentally depend on business services
/// </summary>
internal class ArchitecturalBoundaryValidator : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string[] _forbiddenServices;
    private readonly ILogger<ArchitecturalBoundaryValidator> _logger;

    public ArchitecturalBoundaryValidator(
        IServiceProvider serviceProvider, 
        string[] forbiddenServices)
    {
        _serviceProvider = serviceProvider;
        _forbiddenServices = forbiddenServices;
        _logger = serviceProvider.GetRequiredService<ILogger<ArchitecturalBoundaryValidator>>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating architectural boundaries for HTTP API");
        
        var violations = new List<string>();
        
        // Check for forbidden service registrations
        foreach (var forbiddenService in _forbiddenServices)
        {
            // Try to resolve the service
            var serviceType = Type.GetType($"ACS.Service.Services.{forbiddenService}, ACS.Service");
            if (serviceType != null)
            {
                try
                {
                    var service = _serviceProvider.GetService(serviceType);
                    if (service != null)
                    {
                        violations.Add($"Forbidden business service {forbiddenService} is registered in HTTP API layer");
                    }
                }
                catch
                {
                    // Service not registered - this is good
                }
            }
        }
        
        if (violations.Any())
        {
            var errorMessage = "ARCHITECTURAL BOUNDARY VIOLATION:\n" + 
                             string.Join("\n", violations) +
                             "\n\nHTTP API layer must only depend on IVerticalHostClient for business operations.";
            
            _logger.LogCritical(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
        
        _logger.LogInformation("✅ Architectural boundaries validated successfully - HTTP API is properly isolated");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Configuration for clean HTTP proxy pattern
/// Enforces that HTTP layer only acts as gateway to VerticalHost
/// </summary>
public class HttpProxyOptions
{
    /// <summary>
    /// Whether to enforce strict architectural boundaries
    /// Should be true in production to prevent boundary violations
    /// </summary>
    public bool EnforceStrictBoundaries { get; set; } = true;
    
    /// <summary>
    /// Default timeout for gRPC calls to VerticalHost
    /// </summary>
    public TimeSpan DefaultCommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Whether to enable detailed request/response logging for debugging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
    
    /// <summary>
    /// Maximum number of concurrent gRPC calls to VerticalHost
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 100;
}

/// <summary>
/// Extension methods for configuring clean HTTP proxy behavior
/// </summary>
public static class HttpProxyConfigurationExtensions
{
    public static IServiceCollection ConfigureHttpProxy(this IServiceCollection services, Action<HttpProxyOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}