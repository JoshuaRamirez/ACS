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
    /// Get detailed health status of all tenant processes
    /// </summary>
    [HttpGet("processes")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object>>>> GetProcessesHealth()
    {
        try
        {
            var processStatuses = await _processDiscoveryService.GetAllTenantProcessesAsync();
            
            var details = new Dictionary<string, object>
            {
                ["totalProcesses"] = processStatuses.Count,
                ["timestamp"] = DateTime.UtcNow,
                ["processes"] = processStatuses.ToDictionary(
                    p => p.TenantId, 
                    p => new
                    {
                        processId = p.ProcessId,
                        grpcEndpoint = p.GrpcEndpoint,
                        status = "running" // Could be enhanced with actual health checks
                    }
                )
            };

            return Ok(new ApiResponse<Dictionary<string, object>>(true, details));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processes health status");
            return StatusCode(500, new ApiResponse<Dictionary<string, object>>(false, null, "Failed to get processes health"));
        }
    }
}