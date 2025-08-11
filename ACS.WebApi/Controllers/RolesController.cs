using ACS.Service.Services;
using ACS.WebApi.Mapping;
using ACS.WebApi.Models.Roles;
using Microsoft.AspNetCore.Mvc;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public ActionResult<RolesResponse> Get()
    {
        var roles = _roleService.GetAll()
            .Select(r => r.ToResource())
            .ToList();

        var response = new RolesResponse { Roles = roles };
        return Ok(response);
    }

    [HttpGet("{id}")]
    public ActionResult<RoleResponse> Get(int id)
    {
        var role = _roleService.GetById(id);
        if (role is null)
        {
            return NotFound();
        }

        var response = new RoleResponse { Role = role.ToResource() };
        return Ok(response);
    }

    [HttpPost]
    public ActionResult<RoleResponse> Post([FromBody] CreateRoleRequest request)
    {
        var role = request.ToDomain();
        var created = _roleService.Add(role);
        var resource = created.ToResource();

        var response = new RoleResponse { Role = resource };
        return CreatedAtAction(nameof(Get), new { id = resource.Id }, response);
    }
}
