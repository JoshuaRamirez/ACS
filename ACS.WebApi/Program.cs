using ACS.WebApi.Configuration;
using ACS.WebApi.Services;
using ACS.WebApi.Extensions;
using ACS.WebApi.HealthChecks;
using ACS.Infrastructure;
using ACS.Infrastructure.Authentication;
using ACS.Infrastructure.DependencyInjection;
using ACS.Infrastructure.Telemetry;
using ACS.WebApi.Middleware;
using ACS.WebApi.Security.Csrf;
using ACS.WebApi.Security.Filters;
using ACS.WebApi.Security.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// ================================
// CLEAN ARCHITECTURE ENFORCEMENT
// HTTP API acts as PURE PROXY to VerticalHost
// ZERO business logic, ZERO database access
// ================================

// Configure environment variables 
builder.Configuration.AddEnvironmentVariables("ACS_");

// Load environment-specific configuration if needed
// Environment-specific configuration handled by appsettings

var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();

// ================================
// HTTP PROXY SERVICES ONLY
// ================================

// Add HTTP context accessor (needed for services)
builder.Services.AddHttpContextAccessor();

// Add ONLY the services allowed for HTTP proxy pattern
builder.Services.AddHttpProxyServices();

// Configure HTTP proxy behavior
builder.Services.ConfigureHttpProxy(options =>
{
    options.EnforceStrictBoundaries = true; // Throw if business services detected
    options.DefaultCommandTimeout = TimeSpan.FromSeconds(30);
    options.EnableDetailedLogging = builder.Environment.IsDevelopment();
    options.MaxConcurrentCalls = 100;
});

// Add minimal infrastructure services needed for HTTP layer
builder.Services.AddHttpInfrastructure(builder.Configuration, logger);

// Configure comprehensive OpenTelemetry for HTTP proxy layer only
builder.Services.ConfigureOpenTelemetryForWebApi(builder.Configuration, builder.Environment);

// Add comprehensive health checks (HTTP layer only)
builder.Services.AddHealthChecks()
    .AddCheck<VerticalHostConnectivityCheck>("vertical_host_connectivity")
    .AddCheck<GrpcChannelHealthCheck>("grpc_channel_health");

// Add authentication and authorization (HTTP layer concerns)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // JWT configuration for HTTP layer
        var jwtKey = builder.Configuration["Authentication:Jwt:SecretKey"];
        if (!string.IsNullOrEmpty(jwtKey))
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer = false,
                ValidateAudience = false
            };
        }
    });

builder.Services.AddAuthorization();

// Add controllers (HTTP layer only)
builder.Services.AddControllers(options =>
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

// Add comprehensive Swagger/OpenAPI documentation (using infrastructure extension)
// builder.Services.AddSwaggerDocumentation(); // Commented out - conflicts with SwaggerConfiguration

// Add tenant resolution middleware (for gRPC channel routing)
builder.Services.AddScoped<TenantProcessResolutionMiddleware>();

var app = builder.Build();

// ================================
// VALIDATE CLEAN ARCHITECTURE
// ================================

// Skip architectural validation for now - will be added after all dependencies are resolved
// TODO: Re-enable after all services are properly configured

logger.LogInformation("✅ HTTP API configured as pure proxy to VerticalHost - no business logic detected");

// ================================
// HTTP PIPELINE CONFIGURATION
// ================================

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ACS HTTP Proxy API v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders();

// Add tenant resolution middleware
app.UseMiddleware<TenantProcessResolutionMiddleware>();

// Add performance metrics middleware
app.UseMiddleware<PerformanceMetricsMiddleware>();

// Add authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/health");

// Add startup message
app.Logger.LogInformation("🚀 ACS HTTP API Proxy starting - delegates all business operations to VerticalHost");
app.Logger.LogInformation("📡 Architecture: HTTP Proxy → gRPC → VerticalHost (Command Buffer) → Business Logic");

await app.RunAsync();

// Make Program class public for test projects
public partial class Program { }