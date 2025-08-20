using ACS.Infrastructure;
using ACS.Infrastructure.Services;
using Grpc.Net.Client;
using System.Collections.Concurrent;

namespace ACS.WebApi.Middleware;

public class TenantProcessResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TenantProcessDiscoveryService _processDiscovery;
    private readonly ITenantContextService _tenantContextService;
    private readonly ILogger<TenantProcessResolutionMiddleware> _logger;
    private readonly ConcurrentDictionary<string, GrpcChannel> _grpcChannels = new();

    public TenantProcessResolutionMiddleware(
        RequestDelegate next, 
        TenantProcessDiscoveryService processDiscovery,
        ITenantContextService tenantContextService,
        ILogger<TenantProcessResolutionMiddleware> logger)
    {
        _next = next;
        _processDiscovery = processDiscovery;
        _tenantContextService = tenantContextService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = ExtractTenantId(context);
        
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("No tenant ID found in request {RequestPath}", context.Request.Path);
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Tenant ID is required");
            return;
        }

        try
        {
            // Set initial tenant context for downstream services
            _tenantContextService.SetTenantContext(tenantId);
            
            // Validate tenant access if user is authenticated
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var hasAccess = await _tenantContextService.ValidateTenantAccessAsync();
                if (!hasAccess)
                {
                    _logger.LogWarning("User denied access to tenant {TenantId} for request {RequestPath}", 
                        tenantId, context.Request.Path);
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Access denied to tenant");
                    return;
                }
            }
            
            // Get or start the tenant process
            var tenantProcess = await _processDiscovery.GetOrStartTenantProcessAsync(tenantId);
            
            // Convert to Infrastructure.Services.TenantProcessInfo
            var tenantProcessInfo = new TenantProcessInfo
            {
                TenantId = tenantId,
                ProcessId = tenantProcess.ProcessId,
                GrpcEndpoint = tenantProcess.GrpcEndpoint,
                IsHealthy = tenantProcess.IsHealthy,
                StartTime = tenantProcess.StartTime,
                LastHealthCheck = tenantProcess.LastHealthCheck
            };
            
            // Get or create gRPC channel for the tenant process
            var grpcChannel = GetOrCreateGrpcChannel(tenantProcess.GrpcEndpoint);
            
            // Update tenant context with process information
            _tenantContextService.SetTenantContext(tenantId, tenantProcessInfo, grpcChannel);
            
            _logger.LogDebug("Resolved tenant {TenantId} to process {ProcessId} at {Endpoint} for request {RequestPath}", 
                tenantId, tenantProcess.ProcessId, tenantProcess.GrpcEndpoint, context.Request.Path);
            
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving tenant process for {TenantId}", tenantId);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Error resolving tenant process");
        }
        finally
        {
            // Clear tenant context to prevent leakage
            _tenantContextService.ClearTenantContext();
        }
    }

    private GrpcChannel GetOrCreateGrpcChannel(string endpoint)
    {
        return _grpcChannels.GetOrAdd(endpoint, ep =>
        {
            _logger.LogDebug("Creating new gRPC channel for endpoint {Endpoint}", ep);
            return GrpcChannel.ForAddress(ep);
        });
    }

    private string ExtractTenantId(HttpContext context)
    {
        // Strategy 1: Header-based tenant resolution
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var headerTenantId))
        {
            return headerTenantId.ToString();
        }

        // Strategy 2: Subdomain-based tenant resolution
        var host = context.Request.Host.Host;
        if (host.Contains('.'))
        {
            var subdomain = host.Split('.')[0];
            if (subdomain != "www" && subdomain != "api")
            {
                return subdomain;
            }
        }

        // Strategy 3: URL path-based tenant resolution
        var pathSegments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments?.Length > 1 && pathSegments[0] == "tenants")
        {
            return pathSegments[1];
        }

        // Strategy 4: Query parameter-based tenant resolution
        if (context.Request.Query.TryGetValue("tenantId", out var queryTenantId))
        {
            return queryTenantId.ToString();
        }

        // Strategy 5: Default tenant for development
        return "tenant-a"; // Default for testing
    }
}