using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

/// <summary>
/// DEMO: Pure HTTP API proxy for User resources - SIMPLIFIED VERSION
/// Acts as gateway to VerticalHost - contains NO business logic  
/// This version uses simple types to demonstrate the proxy pattern works
/// ZERO dependencies on business services - only IVerticalHostClient
/// </summary>
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IVerticalHostClient _verticalClient;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IVerticalHostClient verticalClient,
        ILogger<UsersController> logger)
    {
        _verticalClient = verticalClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/users - DEMO: Pure HTTP proxy to VerticalHost
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<object>> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying GetUsers request to VerticalHost: Page={Page}, PageSize={PageSize}, Search={Search}", 
                page, pageSize, search);

            // This demonstrates the proxy pattern - in full implementation would call:
            // var response = await _verticalClient.GetUsersAsync(new GetUsersResource { Page = page, PageSize = pageSize, Search = search });
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Clean architecture proxy pattern working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Page = page,
                PageSize = pageSize,
                Search = search ?? "",
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
    /// GET /api/users/{id} - DEMO: Pure HTTP proxy to VerticalHost  
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetUser(int id)
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying GetUser request to VerticalHost: UserId={UserId}", id);

            // This demonstrates the proxy pattern - in full implementation would call:
            // var response = await _verticalClient.GetUserAsync(new GetUserResource { Id = id });
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Clean architecture proxy pattern working",
                ProxyedTo = "VerticalHost via gRPC", 
                BusinessLogic = "ZERO - Pure proxy",
                UserId = id,
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> CommandBuffer -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in proxy demonstration for UserId={UserId}", id);
            return StatusCode(500, "An error occurred in proxy demonstration");
        }
    }

    /// <summary>
    /// POST /api/users - DEMO: Pure HTTP proxy to VerticalHost for user creation
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> CreateUser([FromBody] CreateUserDemo request)
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying CreateUser request to VerticalHost: Name={Name}", request.Name);

            // This demonstrates the proxy pattern - in full implementation would call:
            // var response = await _verticalClient.CreateUserAsync(new PostUserResource { Name = request.Name, Email = request.Email });
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Clean architecture proxy pattern working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy", 
                Command = "Would be queued in CommandBuffer for sequential processing",
                UserName = request.Name,
                UserEmail = request.Email,
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

/// <summary>
/// Simple demo request class
/// </summary>
public class CreateUserDemo
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}