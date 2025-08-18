using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;
using ACS.WebApi.Services;
using ACS.Infrastructure;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ITenantContextService _tenantContextService;
    private readonly TenantProcessDiscoveryService _processDiscoveryService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ITenantContextService tenantContextService,
        TenantProcessDiscoveryService processDiscoveryService,
        ILogger<HealthController> logger)
    {
        _tenantContextService = tenantContextService;
        _processDiscoveryService = processDiscoveryService;
        _logger = logger;
    }

    /// <summary>
    /// Get health status of the WebApi and current tenant process
    /// </summary>
    [HttpGet]
    public Task<ActionResult<ApiResponse<HealthCheckResponse>>> GetHealth()
    {
        try
        {
            var details = new Dictionary<string, object>
            {
                ["webApi"] = "healthy",
                ["timestamp"] = DateTime.UtcNow
            };

            try
            {
                var tenantId = _tenantContextService.GetTenantId();
                var tenantProcessInfo = _tenantContextService.GetTenantProcessInfo();
                var grpcChannel = _tenantContextService.GetGrpcChannel();

                details["tenantId"] = tenantId;
                details["tenantProcess"] = tenantProcessInfo != null ? "healthy" : "unavailable";
                details["grpcChannel"] = grpcChannel != null ? "connected" : "disconnected";

                if (tenantProcessInfo != null)
                {
                    details["processId"] = tenantProcessInfo.ProcessId;
                    details["grpcEndpoint"] = tenantProcessInfo.GrpcEndpoint;
                }
            }
            catch (Exception ex)
            {
                details["tenantResolution"] = "failed";
                details["tenantError"] = ex.Message;
                _logger.LogWarning(ex, "Failed to resolve tenant information for health check");
            }

            var status = details.ContainsKey("tenantError") ? "degraded" : "healthy";
            var response = new HealthCheckResponse(status, details);

            return Task.FromResult<ActionResult<ApiResponse<HealthCheckResponse>>>(Ok(new ApiResponse<HealthCheckResponse>(true, response)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            
            var errorDetails = new Dictionary<string, object>
            {
                ["webApi"] = "unhealthy",
                ["error"] = ex.Message,
                ["timestamp"] = DateTime.UtcNow
            };

            var errorResponse = new HealthCheckResponse("unhealthy", errorDetails);
            return Task.FromResult<ActionResult<ApiResponse<HealthCheckResponse>>>(StatusCode(500, new ApiResponse<HealthCheckResponse>(false, errorResponse, "Health check failed")));
        }
    }

    /// <summary>
    /// Get health status of the current tenant process via gRPC
    /// </summary>
    [HttpGet("tenant")]
    public async Task<ActionResult<ApiResponse<TenantHealthResponse>>> GetTenantHealth()
    {
        try
        {
            var tenantId = _tenantContextService.GetTenantId();
            var channel = _tenantContextService.GetGrpcChannel();
            
            if (channel == null)
            {
                var unhealthyResponse = new TenantHealthResponse(
                    tenantId,
                    false,
                    DateTime.UtcNow,
                    0,
                    0,
                    0,
                    $"Tenant process not available for tenant {tenantId}"
                );
                
                return Ok(new ApiResponse<TenantHealthResponse>(true, unhealthyResponse));
            }

            var client = new ACS.Core.Grpc.VerticalService.VerticalServiceClient(channel);
            var request = new ACS.Core.Grpc.HealthRequest();
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var healthResponse = await client.HealthCheckAsync(request, cancellationToken: cts.Token);
            
            var response = new TenantHealthResponse(
                tenantId,
                healthResponse.Healthy,
                DateTime.UtcNow,
                healthResponse.UptimeSeconds,
                healthResponse.ActiveConnections,
                healthResponse.CommandsProcessed,
                healthResponse.Healthy ? "Healthy" : "Unhealthy"
            );

            return Ok(new ApiResponse<TenantHealthResponse>(true, response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking tenant health");
            
            var tenantId = _tenantContextService.GetTenantId();
            var unhealthyResponse = new TenantHealthResponse(
                tenantId,
                false,
                DateTime.UtcNow,
                0,
                0,
                0,
                ex.Message
            );
            
            return Ok(new ApiResponse<TenantHealthResponse>(true, unhealthyResponse));
        }
    }

    /// <summary>
    /// Get detailed health status of all tenant processes
    /// </summary>
    [HttpGet("processes")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object>>>> GetProcessesHealth()
    {
        try
        {
            var processStatuses = await _processDiscoveryService.GetAllTenantProcessesAsync();
            var processHealthChecks = new Dictionary<string, object>();
            
            // Perform actual health checks for each tenant process
            foreach (var processInfo in processStatuses)
            {
                try
                {
                    using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(processInfo.GrpcEndpoint);
                    var client = new ACS.Core.Grpc.VerticalService.VerticalServiceClient(channel);
                    
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var healthResponse = await client.HealthCheckAsync(new ACS.Core.Grpc.HealthRequest(), cancellationToken: cts.Token);
                    
                    processHealthChecks[processInfo.TenantId] = new
                    {
                        processId = processInfo.ProcessId,
                        grpcEndpoint = processInfo.GrpcEndpoint,
                        healthy = healthResponse.Healthy,
                        uptimeSeconds = healthResponse.UptimeSeconds,
                        activeConnections = healthResponse.ActiveConnections,
                        commandsProcessed = healthResponse.CommandsProcessed,
                        status = healthResponse.Healthy ? "healthy" : "unhealthy"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check health for tenant {TenantId}", processInfo.TenantId);
                    processHealthChecks[processInfo.TenantId] = new
                    {
                        processId = processInfo.ProcessId,
                        grpcEndpoint = processInfo.GrpcEndpoint,
                        healthy = false,
                        status = "unreachable",
                        error = ex.Message
                    };
                }
            }
            
            var details = new Dictionary<string, object>
            {
                ["totalProcesses"] = processStatuses.Count,
                ["healthyProcesses"] = processHealthChecks.Values.Count(p => 
                    p.GetType().GetProperty("healthy")?.GetValue(p) as bool? == true),
                ["timestamp"] = DateTime.UtcNow,
                ["processes"] = processHealthChecks
            };

            return Ok(new ApiResponse<Dictionary<string, object>>(true, details));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processes health status");
            return StatusCode(500, new ApiResponse<Dictionary<string, object>>(false, null, "Failed to get processes health"));
        }
    }

    /// <summary>
    /// Get comprehensive health status including WebApi and all tenant processes
    /// </summary>
    [HttpGet("detailed")]
    public async Task<ActionResult<ApiResponse<DetailedHealthResponse>>> GetDetailedHealth()
    {
        try
        {
            var webApiHealth = new HealthStatusResponse(
                "WebApi",
                true,
                DateTime.UtcNow,
                "Healthy",
                new Dictionary<string, object>
                {
                    { "version", "1.0.0" },
                    { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development" }
                }
            );

            var tenantHealths = new List<TenantHealthResponse>();
            
            try
            {
                var processStatuses = await _processDiscoveryService.GetAllTenantProcessesAsync();
                
                foreach (var processInfo in processStatuses)
                {
                    try
                    {
                        using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(processInfo.GrpcEndpoint);
                        var client = new ACS.Core.Grpc.VerticalService.VerticalServiceClient(channel);
                        
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        var healthResponse = await client.HealthCheckAsync(new ACS.Core.Grpc.HealthRequest(), cancellationToken: cts.Token);
                        
                        tenantHealths.Add(new TenantHealthResponse(
                            processInfo.TenantId,
                            healthResponse.Healthy,
                            DateTime.UtcNow,
                            healthResponse.UptimeSeconds,
                            healthResponse.ActiveConnections,
                            healthResponse.CommandsProcessed,
                            healthResponse.Healthy ? "Healthy" : "Unhealthy"
                        ));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check health for tenant {TenantId}", processInfo.TenantId);
                        tenantHealths.Add(new TenantHealthResponse(
                            processInfo.TenantId,
                            false,
                            DateTime.UtcNow,
                            0,
                            0,
                            0,
                            ex.Message
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get tenant processes for health check");
            }

            var overallHealthy = webApiHealth.IsHealthy && tenantHealths.All(t => t.IsHealthy);

            var detailedHealth = new DetailedHealthResponse(
                overallHealthy,
                DateTime.UtcNow,
                webApiHealth,
                tenantHealths
            );

            return Ok(new ApiResponse<DetailedHealthResponse>(true, detailedHealth));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing detailed health check");
            return StatusCode(500, new ApiResponse<DetailedHealthResponse>(false, null, "Detailed health check failed"));
        }
    }
}