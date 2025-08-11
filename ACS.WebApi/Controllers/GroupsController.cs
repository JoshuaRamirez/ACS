using ACS.Service.Services;
using ACS.WebApi.Mapping;
using ACS.WebApi.Models.Groups;
using Microsoft.AspNetCore.Mvc;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly IGroupService _groupService;

    public GroupsController(IGroupService groupService)
    {
        _groupService = groupService;
    }

    [HttpGet]
    public ActionResult<GroupsResponse> Get()
    {
        var groups = _groupService.GetAll()
            .Select(g => g.ToResource())
            .ToList();

        var response = new GroupsResponse { Groups = groups };
        return Ok(response);
    }

    [HttpGet("{id}")]
    public ActionResult<GroupResponse> Get(int id)
    {
        var group = _groupService.GetById(id);
        if (group is null)
        {
            return NotFound();
        }

        var response = new GroupResponse { Group = group.ToResource() };
        return Ok(response);
    }

    [HttpPost]
    public ActionResult<GroupResponse> Post([FromBody] CreateGroupRequest request)
    {
        var group = request.ToDomain();
        var created = _groupService.Add(group);
        var resource = created.ToResource();

        var response = new GroupResponse { Group = resource };
        return CreatedAtAction(nameof(Get), new { id = resource.Id }, response);
    }
}
