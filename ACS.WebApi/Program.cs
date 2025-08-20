using ACS.Infrastructure;
using ACS.Infrastructure.Authentication;
using ACS.Infrastructure.Compression;
using ACS.Infrastructure.DependencyInjection;
using ACS.Infrastructure.Monitoring;
using ACS.Infrastructure.Optimization;
using ACS.Infrastructure.RateLimiting;
using ACS.Infrastructure.Security.KeyVault;
using ACS.Infrastructure.Telemetry;
using static ACS.Infrastructure.DependencyInjection.ServiceCollectionExtensions;
using ACS.Service.Compliance;
using ACS.Service.Data;
using ACS.Service.Services;
using ACS.WebApi.Middleware;
using ACS.WebApi.Security.Csrf;
using ACS.WebApi.Security.Filters;
using ACS.WebApi.Security.Headers;
using ACS.WebApi.Security.Validation;
using ACS.WebApi.Services;
using ACS.WebApi.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Key Vault for secrets management
builder.Host.ConfigureKeyVault((context, options) =>
{
    if (!context.HostingEnvironment.IsDevelopment())
    {
        // Production settings
        options.LoadAllSecrets = true;
        options.Optional = false;
        options.ConnectionStringNames.Add("DefaultConnection");
        options.ApiKeyNames.Add("ExternalService");
        options.SecretMappings.Add(new SecretMapping 
        { 
            SecretName = "JwtSecretKey", 
            ConfigurationKey = "Authentication:Jwt:SecretKey" 
        });
    }
    else
    {
        // Development settings
        options.Optional = true;
        options.LoadAllSecrets = false;
    }
});

// Configure all services using centralized registration
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();
builder.Services.ConfigureServices(builder.Configuration, logger, "WebApi");

// Configure comprehensive OpenTelemetry for distributed tracing, metrics, and logging
builder.Services.ConfigureOpenTelemetryForWebApi(builder.Configuration, builder.Environment);

// Add comprehensive health checks
builder.Services.AddHealthChecks(builder.Configuration);

// Add WebApi-specific filters
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
{
    // Add global input validation filter
    options.Filters.Add<InputValidationActionFilter>();
    options.Filters.Add<ValidationExceptionFilter>();
    
    // Add CSRF protection filter
    options.Filters.Add<CsrfProtectionActionFilter>();
});

// WebApi-specific security headers configuration
builder.Services.AddSecurityHeaders(options =>
{
    options.UseHsts = true;
    options.HstsMaxAge = 31536000;
    options.ContentSecurityPolicy.ScriptSrc = "'self' 'unsafe-inline'";
    options.ContentSecurityPolicy.StyleSrc = "'self' 'unsafe-inline'";
});

// Configure response compression options
builder.Services.Configure<ResponseCompressionOptions>(options =>
{
    options.EnableCompression = true;
    options.EnableMinification = !builder.Environment.IsDevelopment();
    options.EnableBrotli = true;
    options.EnableGzip = true;
    options.IsDevelopment = builder.Environment.IsDevelopment();
});

// Configure rate limiting policies
builder.Services.ConfigureRateLimitingPolicies(policies =>
{
    policies.ForTenant("premium_tenant", policy => policy
        .WithLimit(500)
        .WithWindow(60)
        .WithName("premium")
        .WithPriority(3)
    );
    
    policies.ForEndpoint("/api/admin/*", endpoint => endpoint
        .WithLimit(10)
        .WithWindow(60)
        .WithMethods("POST", "PUT", "DELETE")
        .WithDescription("Admin operations rate limiting")
        .WithName("admin_operations")
    );
});

// Add comprehensive Swagger/OpenAPI documentation
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

// Validate service registration
using (var scope = app.Services.CreateScope())
{
    var serviceLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    ServiceRegistrationValidator.ValidateServices(scope.ServiceProvider, serviceLogger);
}

// Configure the HTTP request pipeline.
// 1. Exception handling and developer diagnostics
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    
    // Enable Swagger documentation in development
    app.UseSwaggerDocumentation(app.Environment);
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts(); // Only in production
}

// 2. HTTPS redirection (early in pipeline)
app.UseHttpsRedirection();

// 3. Security headers middleware (right after HTTPS)
app.UseSecurityHeaders();

// 3a. Static file compression and bundling (before routing)
if (app.Environment.IsProduction())
{
    var staticFileOptions = new StaticFileCompressionOptions
    {
        EnableCompression = true,
        EnableBrotli = true,
        EnableGzip = true
    };
    app.UseMiddleware<StaticFileCompressionMiddleware>(staticFileOptions);
}

// 3b. Response compression middleware
app.UseMiddleware<ResponseCompressionMiddleware>();

// 4. Routing (must be before CORS and auth)
app.UseRouting();

// 5. CORS (must be after UseRouting and before UseAuthentication)
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}

// 6. Rate limiting (before authentication to protect auth endpoints)
app.UseRateLimiting();

// 7. Performance metrics (before auth but after rate limiting)
app.UsePerformanceMetrics();

// 7a. Metrics collection middleware
app.UseMiddleware<MetricsMiddleware>();

// 8. Compliance audit middleware (before auth for comprehensive logging)
app.UseComplianceAudit(new ComplianceAuditConfiguration
{
    Enabled = true,
    CaptureRequestBody = true,
    CaptureResponseBody = false,
    EnabledFrameworks = new List<ComplianceFramework>
    {
        ComplianceFramework.GDPR,
        ComplianceFramework.SOC2,
        ComplianceFramework.HIPAA,
        ComplianceFramework.PCI_DSS
    }
});

// 9. CSRF protection (before authentication)
app.UseMiddleware<CsrfProtectionMiddleware>();

// 9a. Correlation middleware (before authentication for comprehensive tracking)
app.UseCorrelationId();

// 10. Authentication (DO NOT use both UseJwtAuthentication and UseAuthentication)
app.UseAuthentication();

// 11. Authorization (must be after authentication)
app.UseAuthorization();

// 12. Tenant process resolution middleware (after authorization for security)
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseMiddleware<TenantProcessResolutionMiddleware>();
}

app.MapControllers();

// Initialize bundling service
var bundlingService = app.Services.GetService<IBundlingService>();
if (bundlingService != null)
{
    await bundlingService.RegisterBundlesAsync();
}

// Map health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = System.Text.Json.JsonSerializer.Serialize(new
        {
            Status = report.Status.ToString(),
            Duration = report.TotalDuration,
            Entries = report.Entries.Select(e => new
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Duration = e.Value.Duration,
                Tags = e.Value.Tags,
                Data = e.Value.Data
            })
        });
        await context.Response.WriteAsync(response);
    }
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Don't run any checks, just return 200
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Add tenant process status endpoint
app.MapGet("/tenants/{tenantId}/status", async (string tenantId, TenantProcessDiscoveryService processDiscovery) =>
{
    try
    {
        var processInfo = await processDiscovery.GetOrStartTenantProcessAsync(tenantId);
        
        return Results.Ok(new 
        { 
            TenantId = tenantId,
            ProcessId = processInfo.ProcessId,
            GrpcEndpoint = processInfo.GrpcEndpoint,
            Status = processInfo.IsHealthy ? "Healthy" : "Unhealthy",
            StartTime = processInfo.StartTime,
            LastHealthCheck = processInfo.LastHealthCheck
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error getting tenant process status: {ex.Message}");
    }
});

app.Run();

public partial class Program {}
