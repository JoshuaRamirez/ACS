using ACS.Infrastructure.Services;
using ACS.WebApi.Services;

namespace ACS.WebApi.Extensions;

/// <summary>
/// Extension methods for HTTP infrastructure services only
/// NO business services allowed
/// </summary>
public static class InfrastructureExtensions
{
    /// <summary>
    /// Add minimal infrastructure services needed for HTTP proxy layer
    /// </summary>
    public static IServiceCollection AddHttpInfrastructure(this IServiceCollection services, IConfiguration configuration, ILogger logger)
    {
        // Use services from infrastructure layer - avoid duplicates
        // Context services already registered in infrastructure
        // Circuit breaker, telemetry, and gRPC compression are static or infrastructure-managed
        
        logger.LogInformation("✅ HTTP infrastructure services registered");
        
        return services;
    }
    
    /// <summary>
    /// Add Swagger documentation for HTTP proxy API
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "ACS HTTP Proxy API",
                Version = "v1",
                Description = "Pure HTTP proxy API that delegates all business operations to VerticalHost via gRPC"
            });
            
            // Add authentication to Swagger
            c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme.",
                Name = "Authorization",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            
            c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
        
        return services;
    }
}