using ACS.Infrastructure;
using ACS.Service.Data;
using ACS.Service.Infrastructure;
using ACS.Service.Services;
using ACS.VerticalHost.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Get tenant ID from command line arguments or environment
var tenantId = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("TENANT_ID");
if (string.IsNullOrEmpty(tenantId))
{
    throw new InvalidOperationException("Tenant ID must be provided as command line argument or TENANT_ID environment variable");
}

// Get gRPC port from command line arguments
var grpcPort = 50051; // default
if (args.Length > 1 && args[1].StartsWith("--grpc-port"))
{
    if (args.Length > 2 && int.TryParse(args[2], out var port))
    {
        grpcPort = port;
    }
    else if (args[1].Contains('=') && int.TryParse(args[1].Split('=')[1], out var portFromArg))
    {
        grpcPort = portFromArg;
    }
}

// Configure Kestrel to listen on the specified gRPC port
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(grpcPort, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

// Single-tenant service infrastructure
builder.Services.AddSingleton<TenantConfiguration>(provider => 
    new TenantConfiguration { TenantId = tenantId });

// Domain services
builder.Services.AddSingleton<InMemoryEntityGraph>();
builder.Services.AddSingleton<TenantDatabasePersistenceService>();
builder.Services.AddSingleton<AccessControlDomainService>();

// Background services
builder.Services.AddSingleton<TenantRingBuffer>();
builder.Services.AddHostedService<TenantAccessControlHostedService>();

// gRPC services
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
    options.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4MB
});

// Tenant-specific database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(GetTenantConnectionString(tenantId)));

// Build and configure the application
var app = builder.Build();

// Configure gRPC endpoint
app.MapGrpcService<VerticalGrpcService>();

// Initialize domain services
using (var scope = app.Services.CreateScope())
{
    var entityGraph = scope.ServiceProvider.GetRequiredService<InMemoryEntityGraph>();
    var domainService = scope.ServiceProvider.GetRequiredService<AccessControlDomainService>();
    
    // Load entity graph and hydrate normalizers
    await domainService.LoadEntityGraphAsync();
    
    Console.WriteLine($"Entity graph loaded for tenant: {tenantId}");
}

Console.WriteLine($"Starting VerticalHost for tenant: {tenantId} on gRPC port: {grpcPort}");
await app.RunAsync();

static string GetTenantConnectionString(string tenantId)
{
    var baseConnectionString = Environment.GetEnvironmentVariable("BASE_CONNECTION_STRING") 
        ?? "Server=(localdb)\\mssqllocaldb;Database=ACS_{TenantId};Trusted_Connection=true;MultipleActiveResultSets=true";
    
    return baseConnectionString.Replace("{TenantId}", tenantId);
}
