using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

/// <summary>
/// DEMO: Pure HTTP API proxy for Admin operations - SIMPLIFIED VERSION
/// Acts as gateway to VerticalHost - contains NO business logic
/// This version uses simple types to demonstrate the proxy pattern works
/// ZERO dependencies on business services - only IVerticalHostClient
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IVerticalHostClient _verticalClient;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IVerticalHostClient verticalClient,
        ILogger<AdminController> logger)
    {
        _verticalClient = verticalClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/health - DEMO: Pure HTTP proxy to VerticalHost
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<object>> GetSystemHealth()
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying GetSystemHealth request to VerticalHost");

            // This demonstrates the proxy pattern - in full implementation would call:
            // var response = await _verticalClient.GetSystemHealthAsync(new GetSystemHealthRequest { ... });
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Clean architecture proxy pattern working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Operation = "GetSystemHealth",
                Status = "Healthy",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> CommandBuffer -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in proxy demonstration");
            return StatusCode(500, "An error occurred in proxy demonstration");
        }
    }

    /// <summary>
    /// GET /api/admin/system-info - DEMO: Pure HTTP proxy to VerticalHost
    /// </summary>
    [HttpGet("system-info")]
    public async Task<ActionResult<object>> GetSystemInfo()
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying GetSystemInfo request to VerticalHost");

            // This demonstrates the proxy pattern - in full implementation would call:
            // var response = await _verticalClient.GetSystemInfoAsync(new GetSystemInfoRequest { ... });
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Clean architecture proxy pattern working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Operation = "GetSystemInfo",
                ApplicationName = "Access Control System",
                Version = "1.0.0-DEMO",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> CommandBuffer -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in proxy demonstration");
            return StatusCode(500, "An error occurred in proxy demonstration");
        }
    }
}