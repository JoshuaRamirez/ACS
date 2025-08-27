using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

/// <summary>
/// DEMO: Pure HTTP API proxy for Groups - SIMPLIFIED VERSION  
/// This demonstrates the clean architecture proxy pattern
/// ZERO dependencies on business services - only IVerticalHostClient
/// </summary>
[ApiController]
[Route("api/groups")]
public class GroupsController : ControllerBase
{
    private readonly IVerticalHostClient _verticalClient;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(
        IVerticalHostClient verticalClient,
        ILogger<GroupsController> logger)
    {
        _verticalClient = verticalClient;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<object> GetGroups([FromQuery] string? search = null)
    {
        _logger.LogInformation("DEMO: Pure proxy to VerticalHost for groups");
        
        return Ok(new
        {
            Message = "DEMO: Groups proxy pattern working",
            Architecture = "Pure proxy - no business logic",
            Search = search ?? ""
        });
    }

    [HttpGet("{id}")]
    public ActionResult<object> GetGroup(int id)
    {
        _logger.LogInformation("DEMO: Pure proxy to VerticalHost for group {GroupId}", id);
        
        return Ok(new
        {
            Message = "DEMO: Group proxy pattern working", 
            GroupId = id,
            Architecture = "Pure proxy - no business logic"
        });
    }
}