using ACS.Infrastructure;
using ACS.Infrastructure.Authentication;
using ACS.Infrastructure.RateLimiting;
using ACS.Service.Data;
using ACS.Service.Services;
using ACS.WebApi.Middleware;
using ACS.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add HttpContextAccessor for tenant context service
builder.Services.AddHttpContextAccessor();

// Register tenant process infrastructure
builder.Services.AddSingleton<TenantProcessDiscoveryService>();

// Register tenant context and gRPC client services
builder.Services.AddScoped<ITenantContextService, TenantContextService>();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddSingleton<CircuitBreakerService>();
builder.Services.AddScoped<GrpcErrorMappingService>();
builder.Services.AddScoped<TenantGrpcClientService>();

// Add logging
builder.Services.AddLogging();

// Configure JWT Authentication
var jwtSecretKey = builder.Configuration.GetValue<string>("Authentication:Jwt:SecretKey") ?? "your-super-secret-key-here-at-least-256-bits-long-for-production";
var jwtIssuer = builder.Configuration.GetValue<string>("Authentication:Jwt:Issuer") ?? "ACS.WebApi";
var jwtAudience = builder.Configuration.GetValue<string>("Authentication:Jwt:Audience") ?? "ACS.VerticalHost";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

builder.Services.AddAuthorization();

// Add JWT token service
builder.Services.AddSingleton<JwtTokenService>();

// Add database context (configure connection string for multi-tenant scenarios)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // For development, use a default connection string
    // In production, this would be resolved per tenant
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=ACS_Development;Trusted_Connection=true;MultipleActiveResultSets=true";
    options.UseSqlServer(connectionString);
});

// Add authentication services
builder.Services.AddScoped<IPasswordHashService, PasswordHashService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<ICommandProcessingService, CommandProcessingService>();
builder.Services.AddScoped<IUserService, UserService>();

// Configure OpenTelemetry for distributed tracing and metrics
builder.Services.ConfigureOpenTelemetry(builder.Configuration);

// Add rate limiting services
builder.Services.AddRateLimiting(builder.Configuration);

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

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}

app.UseHttpsRedirection();

// Add performance metrics middleware
app.UsePerformanceMetrics();

// Add rate limiting middleware (before authentication)
app.UseRateLimiting();

// Add JWT authentication middleware
app.UseJwtAuthentication();

// Add built-in authentication/authorization
app.UseAuthentication();
app.UseAuthorization();

// Add tenant process resolution middleware AFTER authorization (skip in testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseMiddleware<TenantProcessResolutionMiddleware>();
}

app.MapControllers();

// Add health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

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
