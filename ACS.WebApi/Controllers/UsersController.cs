using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;
using ACS.WebApi.Services;

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
    public Task<ActionResult<ApiResponse<UserListResponse>>> GetUsers([FromQuery] PagedRequest request)
    {
        try
        {
            // For now, return a placeholder since we don't have a GetUsers gRPC method yet
            // This would need to be implemented in the gRPC service
            var users = new List<UserResponse>();
            var response = new UserListResponse(users, 0, request.Page, request.PageSize);
            
            return Task.FromResult<ActionResult<ApiResponse<UserListResponse>>>(Ok(new ApiResponse<UserListResponse>(true, response)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return Task.FromResult<ActionResult<ApiResponse<UserListResponse>>>(StatusCode(500, new ApiResponse<UserListResponse>(false, null, "Error retrieving users")));
        }
    }

    /// <summary>
    /// Get a specific user by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public Task<ActionResult<ApiResponse<UserResponse>>> GetUser(int id)
    {
        try
        {
            // For now, return a placeholder since we don't have a GetUser gRPC method yet
            // This would need to be implemented in the gRPC service
            return Task.FromResult<ActionResult<ApiResponse<UserResponse>>>(NotFound(new ApiResponse<UserResponse>(false, null, $"User {id} not found")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return Task.FromResult<ActionResult<ApiResponse<UserResponse>>>(StatusCode(500, new ApiResponse<UserResponse>(false, null, "Error retrieving user")));
        }
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost]
    public Task<ActionResult<ApiResponse<UserResponse>>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Task.FromResult<ActionResult<ApiResponse<UserResponse>>>(BadRequest(new ApiResponse<UserResponse>(false, null, "User name is required")));
            }

            // For now, return a placeholder since we don't have a CreateUser gRPC method yet
            // This would need to be implemented in the gRPC service
            return Task.FromResult<ActionResult<ApiResponse<UserResponse>>>(StatusCode(501, new ApiResponse<UserResponse>(false, null, "CreateUser not implemented yet")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return Task.FromResult<ActionResult<ApiResponse<UserResponse>>>(StatusCode(500, new ApiResponse<UserResponse>(false, null, "Error creating user")));
        }
    }

    /// <summary>
    /// Update an existing user
    /// </summary>
    [HttpPut("{id:int}")]
    public Task<ActionResult<ApiResponse<UserResponse>>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Task.FromResult<ActionResult<ApiResponse<UserResponse>>>(BadRequest(new ApiResponse<UserResponse>(false, null, "User name is required")));
            }

            // For now, return a placeholder since we don't have an UpdateUser gRPC method yet
            return Task.FromResult<ActionResult<ApiResponse<UserResponse>>>(StatusCode(501, new ApiResponse<UserResponse>(false, null, "UpdateUser not implemented yet")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return Task.FromResult<ActionResult<ApiResponse<UserResponse>>>(StatusCode(500, new ApiResponse<UserResponse>(false, null, "Error updating user")));
        }
    }

    /// <summary>
    /// Delete a user
    /// </summary>
    [HttpDelete("{id:int}")]
    public Task<ActionResult<ApiResponse<bool>>> DeleteUser(int id)
    {
        try
        {
            // For now, return a placeholder since we don't have a DeleteUser gRPC method yet
            return Task.FromResult<ActionResult<ApiResponse<bool>>>(StatusCode(501, new ApiResponse<bool>(false, false, "DeleteUser not implemented yet")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return Task.FromResult<ActionResult<ApiResponse<bool>>>(StatusCode(500, new ApiResponse<bool>(false, false, "Error deleting user")));
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