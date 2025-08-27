using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IVerticalHostClient _verticalClient;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IVerticalHostClient verticalClient,
        ILogger<AuthController> logger)
    {
        _verticalClient = verticalClient;
        _logger = logger;
    }

    /// <summary>
    /// DEMO: Login via VerticalHost proxy
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Login([FromBody] object request)
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying login request to VerticalHost");
            
            // In full implementation would call:
            // var response = await _verticalClient.AuthenticateAsync(request);
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Authentication proxy working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in authentication proxy");
            return StatusCode(500, "Error in proxy demonstration");
        }
    }

    /// <summary>
    /// DEMO: Refresh token via VerticalHost proxy
    /// </summary>
    [HttpPost("refresh")]
    [Authorize]
    public async Task<ActionResult<object>> RefreshToken()
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying token refresh to VerticalHost");
            
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Token refresh proxy working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in token refresh proxy");
            return StatusCode(500, "Error in proxy demonstration");
        }
    }
}
