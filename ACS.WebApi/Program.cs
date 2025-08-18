using ACS.Infrastructure;
using ACS.WebApi.Middleware;
using ACS.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add HttpContextAccessor for tenant context service
builder.Services.AddHttpContextAccessor();

// Register tenant process infrastructure
builder.Services.AddSingleton<TenantProcessDiscoveryService>();

// Register tenant context and gRPC client services
builder.Services.AddScoped<ITenantContextService, TenantContextService>();
builder.Services.AddScoped<TenantGrpcClientService>();

// Add logging
builder.Services.AddLogging();

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

// Add tenant process resolution middleware BEFORE authorization (skip in testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseMiddleware<TenantProcessResolutionMiddleware>();
}

app.UseAuthorization();

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
