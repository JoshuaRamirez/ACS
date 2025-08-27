using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

/// <summary>
/// DEMO: Pure HTTP API proxy for Migration operations
/// Acts as gateway to VerticalHost - contains NO business logic
/// ZERO dependencies on business services - only IVerticalHostClient
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
public class MigrationController : ControllerBase
{
    private readonly IVerticalHostClient _verticalClient;
    private readonly ILogger<MigrationController> _logger;

    public MigrationController(
        IVerticalHostClient verticalClient,
        ILogger<MigrationController> logger)
    {
        _verticalClient = verticalClient;
        _logger = logger;
    }

    /// <summary>
    /// DEMO: Get migration history via VerticalHost proxy
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<object>> GetMigrationHistory()
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying migration history request to VerticalHost");
            
            // In full implementation would call:
            // var response = await _verticalClient.GetMigrationHistoryAsync();
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Migration history proxy working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in migration history proxy");
            return StatusCode(500, "Error in proxy demonstration");
        }
    }

    /// <summary>
    /// DEMO: Validate migrations via VerticalHost proxy
    /// </summary>
    [HttpPost("validate")]
    public async Task<ActionResult<object>> ValidateMigrations()
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying migration validation request to VerticalHost");
            
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Migration validation proxy working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy", 
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in migration validation proxy");
            return StatusCode(500, "Error in proxy demonstration");
        }
    }

}
