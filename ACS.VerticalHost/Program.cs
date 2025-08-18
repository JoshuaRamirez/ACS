using ACS.Infrastructure;
using ACS.Service.Data;
using ACS.Service.Infrastructure;
using ACS.Service.Services;
using ACS.VerticalHost.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Parse command line arguments
var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
var grpcPort = 50051; // default

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--tenant" && i + 1 < args.Length)
    {
        tenantId = args[i + 1];
        i++; // Skip next arg
    }
    else if (args[i] == "--port" && i + 1 < args.Length)
    {
        if (int.TryParse(args[i + 1], out var port))
        {
            grpcPort = port;
        }
        i++; // Skip next arg
    }
}

// Also check environment variable for port if not set via args
if (Environment.GetEnvironmentVariable("GRPC_PORT") is string portEnv && int.TryParse(portEnv, out var envPort))
{
    grpcPort = envPort;
}

if (string.IsNullOrEmpty(tenantId))
{
    throw new InvalidOperationException("Tenant ID must be provided via --tenant argument or TENANT_ID environment variable");
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
builder.Services.AddSingleton<EventPersistenceService>();
builder.Services.AddSingleton<AccessControlDomainService>();
builder.Services.AddSingleton<CommandTranslationService>();

// Background services
builder.Services.AddSingleton<TenantRingBuffer>();
builder.Services.AddHostedService<TenantAccessControlHostedService>();

// Configure OpenTelemetry for distributed tracing
builder.Services.ConfigureOpenTelemetry(builder.Configuration);

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
