using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ACS.Infrastructure.RateLimiting;

/// <summary>
/// Extension methods for configuring rate limiting services
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Add rate limiting services to the service collection
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitConfig = configuration.GetSection("RateLimit").Get<RateLimitingConfiguration>() 
                             ?? new RateLimitingConfiguration();

        // Register configuration
        services.Configure<RateLimitingConfiguration>(configuration.GetSection("RateLimit"));

        // Register storage based on configuration
        switch (rateLimitConfig.Storage.Type)
        {
            case RateLimitStorageType.Redis:
                services.AddSingleton<IRateLimitStorage>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<RedisRateLimitStorage>>();
                    return new RedisRateLimitStorage(logger, configuration);
                });
                
                // Ensure Redis connection is available
                if (!string.IsNullOrEmpty(rateLimitConfig.Storage.ConnectionString))
                {
                    services.AddSingleton<IConnectionMultiplexer>(provider =>
                        ConnectionMultiplexer.Connect(rateLimitConfig.Storage.ConnectionString));
                }
                break;
                
            case RateLimitStorageType.InMemory:
            default:
                services.AddSingleton<IRateLimitStorage, InMemoryRateLimitStorage>();
                break;
        }

        // Register rate limiting service
        services.AddSingleton<IRateLimitingService, SlidingWindowRateLimiter>();

        // Register monitoring service
        services.AddSingleton<RateLimitingMonitoringService>();
        services.AddHostedService<RateLimitingMonitoringService>(provider => 
            provider.GetRequiredService<RateLimitingMonitoringService>());

        // Register metrics service if OpenTelemetry is available
        services.AddSingleton<RateLimitingMetricsService>();

        return services;
    }

    /// <summary>
    /// Add rate limiting middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }

    /// <summary>
    /// Configure default rate limiting policies
    /// </summary>
    public static IServiceCollection ConfigureRateLimitingPolicies(
        this IServiceCollection services, 
        Action<RateLimitingPolicyBuilder> configure)
    {
        var builder = new RateLimitingPolicyBuilder();
        configure(builder);
        
        services.Configure<RateLimitingConfiguration>(config =>
        {
            foreach (var policy in builder.TenantPolicies)
            {
                config.TenantPolicies[policy.Key] = policy.Value;
            }
            
            config.EndpointPolicies.AddRange(builder.EndpointPolicies);
        });

        return services;
    }
}

/// <summary>
/// Builder for configuring rate limiting policies
/// </summary>
public class RateLimitingPolicyBuilder
{
    internal Dictionary<string, RateLimitPolicyConfig> TenantPolicies { get; } = new();
    internal List<EndpointRateLimitPolicy> EndpointPolicies { get; } = new();

    /// <summary>
    /// Configure a rate limiting policy for a specific tenant
    /// </summary>
    public RateLimitingPolicyBuilder ForTenant(string tenantId, Action<RateLimitPolicyBuilder> configure)
    {
        var builder = new RateLimitPolicyBuilder();
        configure(builder);
        TenantPolicies[tenantId] = builder.Build();
        return this;
    }

    /// <summary>
    /// Configure a rate limiting policy for a specific endpoint
    /// </summary>
    public RateLimitingPolicyBuilder ForEndpoint(string pathPattern, Action<EndpointPolicyBuilder> configure)
    {
        var builder = new EndpointPolicyBuilder(pathPattern);
        configure(builder);
        EndpointPolicies.Add(builder.Build());
        return this;
    }
}

/// <summary>
/// Builder for individual rate limit policies
/// </summary>
public class RateLimitPolicyBuilder
{
    private readonly RateLimitPolicyConfig _policy = new();

    public RateLimitPolicyBuilder WithLimit(int requestLimit)
    {
        _policy.RequestLimit = requestLimit;
        return this;
    }

    public RateLimitPolicyBuilder WithWindow(int windowSizeSeconds)
    {
        _policy.WindowSizeSeconds = windowSizeSeconds;
        return this;
    }

    public RateLimitPolicyBuilder WithAlgorithm(RateLimitAlgorithm algorithm)
    {
        _policy.Algorithm = algorithm;
        return this;
    }

    public RateLimitPolicyBuilder WithDistributedStorage(bool useDistributed = true)
    {
        _policy.UseDistributedStorage = useDistributed;
        return this;
    }

    public RateLimitPolicyBuilder WithName(string policyName)
    {
        _policy.PolicyName = policyName;
        return this;
    }

    public RateLimitPolicyBuilder WithPriority(int priority)
    {
        _policy.Priority = priority;
        return this;
    }

    internal RateLimitPolicyConfig Build() => _policy;
}

/// <summary>
/// Builder for endpoint-specific rate limit policies
/// </summary>
public class EndpointPolicyBuilder
{
    private readonly EndpointRateLimitPolicy _policy;

    public EndpointPolicyBuilder(string pathPattern)
    {
        _policy = new EndpointRateLimitPolicy { PathPattern = pathPattern };
    }

    public EndpointPolicyBuilder WithLimit(int requestLimit)
    {
        _policy.RequestLimit = requestLimit;
        return this;
    }

    public EndpointPolicyBuilder WithWindow(int windowSizeSeconds)
    {
        _policy.WindowSizeSeconds = windowSizeSeconds;
        return this;
    }

    public EndpointPolicyBuilder WithMethods(params string[] httpMethods)
    {
        _policy.HttpMethods = httpMethods.ToList();
        return this;
    }

    public EndpointPolicyBuilder WithDescription(string description)
    {
        _policy.Description = description;
        return this;
    }

    public EndpointPolicyBuilder WithAlgorithm(RateLimitAlgorithm algorithm)
    {
        _policy.Algorithm = algorithm;
        return this;
    }

    public EndpointPolicyBuilder WithName(string policyName)
    {
        _policy.PolicyName = policyName;
        return this;
    }

    internal EndpointRateLimitPolicy Build() => _policy;
}