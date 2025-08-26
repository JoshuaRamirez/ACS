using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace ACS.WebApi.Configuration;

/// <summary>
/// Swagger/OpenAPI configuration for comprehensive API documentation
/// </summary>
public static class SwaggerConfiguration
{
    /// <summary>
    /// Configure Swagger services with comprehensive documentation
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            // API Information
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1.0",
                Title = "ACS (Access Control System) API",
                Description = @"
                    <h2>Comprehensive Access Control System REST API</h2>
                    <p>This API provides enterprise-grade access control and authorization management capabilities including:</p>
                    <ul>
                        <li><strong>User Management</strong>: Create, update, and manage user accounts with role-based access control</li>
                        <li><strong>Group Hierarchy</strong>: Organize users into hierarchical groups with inherited permissions</li>
                        <li><strong>Role-Based Access Control (RBAC)</strong>: Define roles with specific permissions and assign to users/groups</li>
                        <li><strong>Permission Evaluation</strong>: Real-time permission checking and authorization decisions</li>
                        <li><strong>Multi-Tenant Architecture</strong>: Isolated tenant data with tenant-specific configurations</li>
                        <li><strong>Audit & Compliance</strong>: Comprehensive audit logging and compliance reporting (GDPR, SOC2, HIPAA)</li>
                    </ul>
                    <p><strong>Authentication:</strong> Uses JWT Bearer tokens. Obtain tokens via the <code>/api/auth/login</code> endpoint.</p>
                    <p><strong>Rate Limiting:</strong> API endpoints have rate limiting applied. See individual endpoint documentation for specific limits.</p>
                ",
                Contact = new OpenApiContact
                {
                    Name = "ACS Development Team",
                    Email = "dev-team@acs.com",
                    Url = new Uri("https://acs.com/support")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                },
                TermsOfService = new Uri("https://acs.com/terms")
            });

            // Include XML documentation
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // JWT Authentication
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = @"
                    <h4>JWT Authorization header using the Bearer scheme</h4>
                    <p>Enter your JWT token in the text input below.</p>
                    <p><strong>Example:</strong> 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'</p>
                    <p><strong>How to obtain a token:</strong></p>
                    <ol>
                        <li>Use the <code>/api/auth/login</code> endpoint with valid credentials</li>
                        <li>Copy the 'token' value from the response</li>
                        <li>Paste it in the input field above (without 'Bearer ' prefix)</li>
                        <li>Click 'Authorize' to apply to all API requests</li>
                    </ol>
                ",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] { }
                }
            });

            // Group endpoints by tags
            options.TagActionsBy(api =>
            {
                if (api.GroupName != null)
                {
                    return new[] { api.GroupName };
                }

                var controllerName = api.ActionDescriptor.RouteValues["controller"];
                return new[] { controllerName ?? "Default" };
            });

            // Custom operation sorting
            options.OrderActionsBy(apiDesc => $"{apiDesc.ActionDescriptor.RouteValues["controller"]}_{apiDesc.HttpMethod}");

            // Add custom schema filters
            options.SchemaFilter<EnumSchemaFilter>();
            options.OperationFilter<AuthorizeOperationFilter>();
            options.OperationFilter<SwaggerDefaultValues>();

            // Document all HTTP status codes
            options.OperationFilter<SwaggerResponsesOperationFilter>();

            // Enable annotations
            options.EnableAnnotations();

            // Custom document filter for additional metadata
            options.DocumentFilter<SwaggerDocumentFilter>();
        });

        return services;
    }

    /// <summary>
    /// Configure Swagger UI with enhanced features
    /// </summary>
    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger(options =>
        {
            options.RouteTemplate = "api-docs/{documentName}/swagger.json";
        });

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/api-docs/v1/swagger.json", "ACS API v1.0");
            options.RoutePrefix = "api-docs";
            
            // Enhanced UI configuration
            options.DocumentTitle = "ACS API Documentation";
            options.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
            options.DefaultModelExpandDepth(2);
            options.DefaultModelsExpandDepth(1);
            options.DisplayOperationId();
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
            options.EnableValidator();
            options.ShowExtensions();
            options.EnableTryItOutByDefault();
            
            // Custom CSS for better appearance
            options.InjectStylesheet("/swagger-ui/custom.css");
            
            // Custom JavaScript for enhanced functionality
            options.InjectJavascript("/swagger-ui/custom.js");

            if (env.IsDevelopment())
            {
                options.EnablePersistAuthorization();
            }
        });

        return app;
    }
}

/// <summary>
/// Schema filter for better enum documentation
/// </summary>
public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            schema.Enum.Clear();
            Enum.GetNames(context.Type)
                .ToList()
                .ForEach(name => schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(name)));
        }
    }
}

/// <summary>
/// Operation filter to document authorization requirements
/// </summary>
public class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any() == true ||
                          context.MethodInfo.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any();

        if (hasAuthorize)
        {
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized - Invalid or missing JWT token" });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden - Insufficient permissions for this operation" });
        }
    }
}

/// <summary>
/// Operation filter for default parameter values
/// </summary>
public class SwaggerDefaultValues : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;

        // Check for deprecated attribute on the action method
        var deprecatedAttribute = apiDescription.ActionDescriptor?.EndpointMetadata?.OfType<ObsoleteAttribute>().FirstOrDefault();
        operation.Deprecated |= deprecatedAttribute != null;

        foreach (var responseType in context.ApiDescription.SupportedResponseTypes)
        {
            var responseKey = responseType.IsDefaultResponse ? "default" : responseType.StatusCode.ToString();
            var response = operation.Responses[responseKey];

            foreach (var contentType in response.Content.Keys)
            {
                if (responseType.ApiResponseFormats.All(x => x.MediaType != contentType))
                {
                    response.Content.Remove(contentType);
                }
            }
        }

        if (operation.Parameters == null) return;

        foreach (var parameter in operation.Parameters)
        {
            var description = apiDescription.ParameterDescriptions.First(p => p.Name == parameter.Name);

            parameter.Description ??= description.ModelMetadata?.Description;

            if (parameter.Schema.Default == null && description.DefaultValue != null)
            {
                parameter.Schema.Default = new Microsoft.OpenApi.Any.OpenApiString(description.DefaultValue.ToString());
            }

            parameter.Required |= description.IsRequired;
        }
    }
}

/// <summary>
/// Operation filter to add comprehensive response documentation
/// </summary>
public class SwaggerResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add common response codes if not already present
        operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Bad Request - Invalid input parameters or request format" });
        operation.Responses.TryAdd("500", new OpenApiResponse { Description = "Internal Server Error - An unexpected error occurred" });

        // Add rate limiting response
        operation.Responses.TryAdd("429", new OpenApiResponse { Description = "Too Many Requests - Rate limit exceeded" });

        // Add specific responses based on HTTP method
        var method = context.ApiDescription.HttpMethod?.ToUpperInvariant();
        switch (method)
        {
            case "POST":
                operation.Responses.TryAdd("201", new OpenApiResponse { Description = "Created - Resource successfully created" });
                operation.Responses.TryAdd("409", new OpenApiResponse { Description = "Conflict - Resource already exists or conflicts with existing data" });
                break;
            case "GET":
                operation.Responses.TryAdd("404", new OpenApiResponse { Description = "Not Found - Requested resource does not exist" });
                break;
            case "PUT":
                operation.Responses.TryAdd("404", new OpenApiResponse { Description = "Not Found - Resource to update does not exist" });
                break;
            case "DELETE":
                operation.Responses.TryAdd("404", new OpenApiResponse { Description = "Not Found - Resource to delete does not exist" });
                operation.Responses.TryAdd("204", new OpenApiResponse { Description = "No Content - Resource successfully deleted" });
                break;
        }
    }
}

/// <summary>
/// Document filter for additional API metadata and examples
/// </summary>
public class SwaggerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Add custom tags with descriptions
        swaggerDoc.Tags = new List<OpenApiTag>
        {
            new OpenApiTag { Name = "Authentication", Description = "User authentication and JWT token management" },
            new OpenApiTag { Name = "Users", Description = "User account management and profile operations" },
            new OpenApiTag { Name = "Groups", Description = "Group management and hierarchical organization" },
            new OpenApiTag { Name = "Roles", Description = "Role-based access control and permission management" },
            new OpenApiTag { Name = "Permissions", Description = "Permission evaluation and access control decisions" },
            new OpenApiTag { Name = "Health", Description = "System health checks and diagnostics" },
            new OpenApiTag { Name = "Admin", Description = "Administrative operations and system management" },
            new OpenApiTag { Name = "Audit", Description = "Audit logging and compliance reporting" },
            new OpenApiTag { Name = "Metrics", Description = "Performance metrics and monitoring data" }
        };

        // Add servers for different environments
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new OpenApiServer { Url = "https://api.acs.com", Description = "Production Server" },
            new OpenApiServer { Url = "https://staging-api.acs.com", Description = "Staging Server" },
            new OpenApiServer { Url = "https://localhost:5001", Description = "Development Server (HTTPS)" },
            new OpenApiServer { Url = "http://localhost:5000", Description = "Development Server (HTTP)" }
        };
    }
}