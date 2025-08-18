using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;
using ACS.WebApi.Services;
using ACS.Service.Infrastructure;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly TenantGrpcClientService _grpcClientService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        TenantGrpcClientService grpcClientService,
        ILogger<UsersController> logger)
    {
        _grpcClientService = grpcClientService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users for the current tenant
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<UserListResponse>>> GetUsers([FromQuery] PagedRequest request)
    {
        try
        {
            var getUsersCommand = new Service.Infrastructure.GetUsersCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                request.Page,
                request.PageSize);

            var result = await _grpcClientService.GetUsersAsync(getUsersCommand);
            
            if (result.Success && result.Data != null)
            {
                return Ok(result);
            }
            
            return StatusCode(500, new ApiResponse<UserListResponse>(false, null, result.Message ?? "Error retrieving users"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new ApiResponse<UserListResponse>(false, null, "Error retrieving users"));
        }
    }

    /// <summary>
    /// Get a specific user by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> GetUser(int id)
    {
        try
        {
            var getUserCommand = new Service.Infrastructure.GetUserCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                id);

            var result = await _grpcClientService.GetUserAsync(getUserCommand);
            
            if (result.Success && result.Data != null)
            {
                return Ok(result);
            }
            
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(new ApiResponse<UserResponse>(false, null, $"User {id} not found"));
            }
            
            return StatusCode(500, new ApiResponse<UserResponse>(false, null, result.Message ?? "Error retrieving user"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(500, new ApiResponse<UserResponse>(false, null, "Error retrieving user"));
        }
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserResponse>>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ApiResponse<UserResponse>(false, null, "User name is required"));
            }

            var createUserCommand = new Service.Infrastructure.CreateUserCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                request.Name);

            var result = await _grpcClientService.CreateUserAsync(createUserCommand);
            
            if (result.Success && result.Data != null)
            {
                return CreatedAtAction(nameof(GetUser), new { id = result.Data.Id }, result);
            }
            
            return StatusCode(500, new ApiResponse<UserResponse>(false, null, result.Message ?? "Error creating user"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new ApiResponse<UserResponse>(false, null, "Error creating user"));
        }
    }

    /// <summary>
    /// Update an existing user
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ApiResponse<UserResponse>(false, null, "User name is required"));
            }

            var updateUserCommand = new Service.Infrastructure.UpdateUserCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                id,
                request.Name);

            var result = await _grpcClientService.UpdateUserAsync(updateUserCommand);
            
            if (result.Success && result.Data != null)
            {
                return Ok(result);
            }
            
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(new ApiResponse<UserResponse>(false, null, $"User {id} not found"));
            }
            
            return StatusCode(500, new ApiResponse<UserResponse>(false, null, result.Message ?? "Error updating user"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(500, new ApiResponse<UserResponse>(false, null, "Error updating user"));
        }
    }

    /// <summary>
    /// Delete a user
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteUser(int id)
    {
        try
        {
            var deleteUserCommand = new Service.Infrastructure.DeleteUserCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                id);

            var result = await _grpcClientService.DeleteUserAsync(deleteUserCommand);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(new ApiResponse<bool>(false, false, $"User {id} not found"));
            }
            
            return StatusCode(500, new ApiResponse<bool>(false, false, result.Message ?? "Error deleting user"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, new ApiResponse<bool>(false, false, "Error deleting user"));
        }
    }

    /// <summary>
    /// Add user to a group
    /// </summary>
    [HttpPost("{userId:int}/groups")]
    public async Task<ActionResult<ApiResponse<bool>>> AddUserToGroup(int userId, [FromBody] AddUserToGroupRequest request)
    {
        try
        {
            if (request.UserId != userId)
            {
                return BadRequest(new ApiResponse<bool>(false, false, "User ID in URL and body must match"));
            }

            var result = await _grpcClientService.AddUserToGroupAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to group {GroupId}", userId, request.GroupId);
            return StatusCode(500, new ApiResponse<bool>(false, false, "Error adding user to group"));
        }
    }

    /// <summary>
    /// Assign user to a role
    /// </summary>
    [HttpPost("{userId:int}/roles")]
    public async Task<ActionResult<ApiResponse<bool>>> AssignUserToRole(int userId, [FromBody] AssignUserToRoleRequest request)
    {
        try
        {
            if (request.UserId != userId)
            {
                return BadRequest(new ApiResponse<bool>(false, false, "User ID in URL and body must match"));
            }

            var result = await _grpcClientService.AssignUserToRoleAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning user {UserId} to role {RoleId}", userId, request.RoleId);
            return StatusCode(500, new ApiResponse<bool>(false, false, "Error assigning user to role"));
        }
    }
}