using ACS.Service.Services;
using ACS.WebApi.Mapping;
using ACS.WebApi.Models.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    [HttpGet]
    public ActionResult<PermissionsResponse> Get()
    {
        var permissions = _permissionService.GetAll()
            .Select(p => p.ToResource())
            .ToList();

        var response = new PermissionsResponse { Permissions = permissions };
        return Ok(response);
    }

    [HttpGet("{id}")]
    public ActionResult<PermissionResponse> Get(int id)
    {
        var permission = _permissionService.GetById(id);
        if (permission is null)
        {
            return NotFound();
        }

        var response = new PermissionResponse { Permission = permission.ToResource() };
        return Ok(response);
    }

    [HttpPost]
    public ActionResult<PermissionResponse> Post([FromBody] CreatePermissionRequest request)
    {
        var permission = request.ToDomain();
        var created = _permissionService.Add(permission);
        var resource = created.ToResource();

        var response = new PermissionResponse { Permission = resource };
        return CreatedAtAction(nameof(Get), new { id = resource.Id }, response);
    }
}
