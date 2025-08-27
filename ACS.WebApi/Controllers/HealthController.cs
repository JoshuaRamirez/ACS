using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IVerticalHostClient _verticalClient;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IVerticalHostClient verticalClient,
        ILogger<HealthController> logger)
    {
        _verticalClient = verticalClient;
        _logger = logger;
    }

    /// <summary>
    /// DEMO: Get health status via VerticalHost proxy
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<object>> GetHealth()
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying health check to VerticalHost");
            
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Health check proxy working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in health check proxy");
            return StatusCode(500, "Error in proxy demonstration");
        }
    }
}
