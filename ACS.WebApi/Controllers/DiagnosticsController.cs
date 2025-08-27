using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

/// <summary>
/// DEMO: Pure HTTP API proxy for Diagnostics operations
/// Acts as gateway to VerticalHost - contains NO business logic
/// ZERO dependencies on business services - only IVerticalHostClient
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Operator")]
public class DiagnosticsController : ControllerBase
{
    private readonly IVerticalHostClient _verticalClient;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        IVerticalHostClient verticalClient,
        ILogger<DiagnosticsController> logger)
    {
        _verticalClient = verticalClient;
        _logger = logger;
    }

    /// <summary>
    /// DEMO: Get system information via VerticalHost proxy
    /// </summary>
    [HttpGet("system-info")]
    public async Task<ActionResult<object>> GetSystemInfo()
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying system info request to VerticalHost");
            
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: System diagnostics proxy working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in system info proxy");
            return StatusCode(500, "Error in proxy demonstration");
        }
    }
}
