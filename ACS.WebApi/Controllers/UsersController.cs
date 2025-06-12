using ACS.Service.Domain;
using ACS.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _service;

    public UsersController(IUserService service)
    {
        _service = service;
    }

    [HttpGet]
    public ActionResult<IEnumerable<User>> Get()
    {
        return Ok(_service.GetAll());
    }

    [HttpGet("{id}")]
    public ActionResult<User?> Get(int id)
    {
        var user = _service.GetById(id);
        if (user == null) return NotFound();
        return Ok(user);
    }

    [HttpPost]
    public ActionResult<User> Post(User user)
    {
        var created = _service.Add(user);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }
}
