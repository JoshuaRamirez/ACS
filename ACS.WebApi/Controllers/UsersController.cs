using ACS.Service.Services;
using ACS.WebApi.Mapping;
using ACS.WebApi.Models.Users;
using Microsoft.AspNetCore.Mvc;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public ActionResult<UsersResponse> Get()
    {
        var users = _userService.GetAll()
            .Select(u => u.ToResource())
            .ToList();

        var response = new UsersResponse { Users = users };
        return Ok(response);
    }

    [HttpGet("{id}")]
    public ActionResult<UserResponse> Get(int id)
    {
        var user = _userService.GetById(id);
        if (user is null)
        {
            return NotFound();
        }

        var response = new UserResponse { User = user.ToResource() };
        return Ok(response);
    }

    [HttpPost]
    public ActionResult<UserResponse> Post([FromBody] CreateUserRequest request)
    {
        var user = request.ToDomain();
        var created = _userService.Add(user);
        var resource = created.ToResource();

        var response = new UserResponse { User = resource };
        return CreatedAtAction(nameof(Get), new { id = resource.Id }, response);
    }
}

