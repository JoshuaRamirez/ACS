using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

/// <summary>
/// DEMO: Pure HTTP API proxy for Dashboard operations
/// Acts as gateway to VerticalHost - contains NO business logic
/// ZERO dependencies on business services - only IVerticalHostClient
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IVerticalHostClient _verticalClient;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IVerticalHostClient verticalClient,
        ILogger<DashboardController> logger)
    {
        _verticalClient = verticalClient;
        _logger = logger;
    }

    /// <summary>
    /// DEMO: Get system overview via VerticalHost proxy
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<object>> GetSystemOverview()
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying dashboard overview request to VerticalHost");
            
            // In full implementation would call:
            // var response = await _verticalClient.GetDashboardOverviewAsync();
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Dashboard overview proxy working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in dashboard overview proxy");
            return StatusCode(500, "Error in proxy demonstration");
        }
    }

    /// <summary>
    /// DEMO: Get health status via VerticalHost proxy  
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<object>> GetHealthStatus()
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying health status request to VerticalHost");
            
            // In full implementation would call:
            // var response = await _verticalClient.GetHealthStatusAsync();
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Health status proxy working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in health status proxy");
            return StatusCode(500, "Error in proxy demonstration");
        }
    }
}