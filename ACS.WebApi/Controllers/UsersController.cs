using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ACS.WebApi.Resources;
using ACS.WebApi.Services;
using ACS.Service.Infrastructure;
using ACS.WebApi.Mapping;
using ACS.WebApi.DTOs;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Pure REST API controller for User resources
/// Supports full HTTP verb spectrum (GET, POST, PUT, PATCH, DELETE)
/// Routes all requests through gRPC to VerticalHost processes
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly TenantGrpcClientService _grpcClient;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        TenantGrpcClientService grpcClient,
        ILogger<UsersController> logger)
    {
        _grpcClient = grpcClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/users - Retrieve all users with pagination, filtering, and sorting
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ACS.WebApi.Resources.ApiResponse<UserCollectionResource>>> GetUsers(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection)
    {
        try
        {
            var currentUser = GetCurrentUserId();
            var query = GrpcToDtoExtensions.FromQueryParameters(page, pageSize, search, sortBy, sortDirection);
            var (queryPage, queryPageSize, querySearch, querySortBy, sortDescending) = query.ToQueryParameters();
            
            var grpcCommand = new GetUsersCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                currentUser,
                queryPage,
                queryPageSize
            );
            
            var grpcResponse = await _grpcClient.GetUsersAsync(grpcCommand);

            if (!grpcResponse.Success)
            {
                return BadRequest(grpcResponse.Errors.ToErrorApiResponse<UserCollectionResource>(grpcResponse.Message));
            }

            var resource = grpcResponse.Data?.ToCollectionResource() ?? new UserCollectionResource
            {
                Users = new List<UserResource>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 10
            };
            return Ok(resource.ToApiResponse(message: grpcResponse.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<UserCollectionResource>("Internal server error"));
        }
    }

    /// <summary>
    /// GET /api/users/{id} - Retrieve a specific user by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ACS.WebApi.Resources.ApiResponse<UserResource>>> GetUser(int id)
    {
        try
        {
            var currentUser = GetCurrentUserId();
            var grpcCommand = new GetUserCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                currentUser,
                id
            );
            
            var grpcResponse = await _grpcClient.GetUserAsync(grpcCommand);

            if (!grpcResponse.Success)
            {
                if (grpcResponse.Data == null)
                {
                    return NotFound(grpcResponse.Errors.ToErrorApiResponse<UserResource>(grpcResponse.Message));
                }
                return BadRequest(grpcResponse.Errors.ToErrorApiResponse<UserResource>(grpcResponse.Message));
            }

            var resource = grpcResponse.Data?.ToResource();
            if (resource == null)
            {
                return NotFound(new[] { "User not found" }.ToErrorApiResponse<UserResource>());
            }

            return Ok(resource.ToApiResponse(message: grpcResponse.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<UserResource>("Internal server error"));
        }
    }

    /// <summary>
    /// POST /api/users - Create a new user
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ACS.WebApi.Resources.ApiResponse<UserResource>>> CreateUser([FromBody] CreateUserResource resource)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToErrorApiResponse<UserResource>("Validation failed"));
            }

            var currentUser = GetCurrentUserId();
            var grpcCommand = new CreateUserCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                currentUser,
                resource.Name
            );
            
            var grpcResponse = await _grpcClient.CreateUserAsync(grpcCommand);

            if (!grpcResponse.Success)
            {
                return BadRequest(grpcResponse.Errors.ToErrorApiResponse<UserResource>(grpcResponse.Message));
            }

            var userResource = grpcResponse.Data?.ToResource();
            if (userResource == null)
            {
                return StatusCode(500, new[] { "Failed to create user resource" }.ToErrorApiResponse<UserResource>());
            }

            return CreatedAtAction(
                nameof(GetUser), 
                new { id = userResource.Id }, 
                userResource.ToApiResponse(message: grpcResponse.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<UserResource>("Internal server error"));
        }
    }

    /// <summary>
    /// PUT /api/users/{id} - Update a user (full replacement)
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ACS.WebApi.Resources.ApiResponse<UserResource>>> UpdateUser(int id, [FromBody] UpdateUserResource resource)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToErrorApiResponse<UserResource>("Validation failed"));
            }

            var currentUser = GetCurrentUserId();
            var grpcCommand = new UpdateUserCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                currentUser,
                id,
                resource.Name
            );
            
            var grpcResponse = await _grpcClient.UpdateUserAsync(grpcCommand);

            if (!grpcResponse.Success)
            {
                if (grpcResponse.Data == null && grpcResponse.Message?.Contains("not found") == true)
                {
                    return NotFound(grpcResponse.Errors.ToErrorApiResponse<UserResource>(grpcResponse.Message));
                }
                return BadRequest(grpcResponse.Errors.ToErrorApiResponse<UserResource>(grpcResponse.Message));
            }

            var userResource = grpcResponse.Data?.ToResource();
            if (userResource == null)
            {
                return StatusCode(500, new[] { "Failed to update user resource" }.ToErrorApiResponse<UserResource>());
            }

            return Ok(userResource.ToApiResponse(message: grpcResponse.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<UserResource>("Internal server error"));
        }
    }

    /// <summary>
    /// PATCH /api/users/{id} - Partially update a user
    /// </summary>
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<ACS.WebApi.Resources.ApiResponse<UserResource>>> PatchUser(int id, [FromBody] PatchUserResource resource)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToErrorApiResponse<UserResource>("Validation failed"));
            }

            var currentUser = GetCurrentUserId();
            // For PATCH operations, we need to get the current user first, then update only changed fields
            var getUserCommand = new GetUserCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                currentUser,
                id
            );
            
            var existingUserResponse = await _grpcClient.GetUserAsync(getUserCommand);
            if (!existingUserResponse.Success)
            {
                return NotFound(existingUserResponse.Errors.ToErrorApiResponse<UserResource>(existingUserResponse.Message));
            }

            var updateCommand = new UpdateUserCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                currentUser,
                id,
                resource.Name ?? existingUserResponse.Data?.Name ?? string.Empty
            );
            
            var grpcResponse = await _grpcClient.UpdateUserAsync(updateCommand);

            if (!grpcResponse.Success)
            {
                if (grpcResponse.Data == null && grpcResponse.Message?.Contains("not found") == true)
                {
                    return NotFound(grpcResponse.Errors.ToErrorApiResponse<UserResource>(grpcResponse.Message));
                }
                return BadRequest(grpcResponse.Errors.ToErrorApiResponse<UserResource>(grpcResponse.Message));
            }

            var userResource = grpcResponse.Data?.ToResource();
            if (userResource == null)
            {
                return StatusCode(500, new[] { "Failed to patch user resource" }.ToErrorApiResponse<UserResource>());
            }

            return Ok(userResource.ToApiResponse(message: grpcResponse.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error patching user {UserId}", id);
            return StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<UserResource>("Internal server error"));
        }
    }

    /// <summary>
    /// DELETE /api/users/{id} - Delete a user
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ACS.WebApi.Resources.ApiResponse<bool>>> DeleteUser(int id)
    {
        try
        {
            var currentUser = GetCurrentUserId();
            var grpcCommand = new DeleteUserCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                currentUser,
                id
            );
            
            var grpcResponse = await _grpcClient.DeleteUserAsync(grpcCommand);

            if (!grpcResponse.Success)
            {
                if (grpcResponse.Message?.Contains("not found") == true)
                {
                    return NotFound(grpcResponse.Errors.ToErrorApiResponse<bool>(grpcResponse.Message));
                }
                return BadRequest(grpcResponse.Errors.ToErrorApiResponse<bool>(grpcResponse.Message));
            }

            return Ok(true.ToApiResponse(message: grpcResponse.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<bool>("Internal server error"));
        }
    }

    /// <summary>
    /// POST /api/users/{userId}/groups - Add user to a group
    /// </summary>
    [HttpPost("{userId:int}/groups")]
    public async Task<ActionResult<ACS.WebApi.Resources.ApiResponse<bool>>> AddUserToGroup(int userId, [FromBody] AddUserToGroupResource resource)
    {
        try
        {
            if (resource.UserId != userId)
            {
                return BadRequest(new[] { "User ID in URL and body must match" }.ToErrorApiResponse<bool>());
            }

            var addUserToGroupRequest = new ACS.WebApi.DTOs.AddUserToGroupRequest
            {
                UserId = userId,
                GroupId = resource.GroupId
            };
            
            var grpcResponse = await _grpcClient.AddUserToGroupAsync(addUserToGroupRequest);

            if (!grpcResponse.Success)
            {
                return BadRequest(grpcResponse.Errors.ToErrorApiResponse<bool>(grpcResponse.Message));
            }

            return Ok(true.ToApiResponse(message: grpcResponse.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to group {GroupId}", userId, resource.GroupId);
            return StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<bool>("Internal server error"));
        }
    }

    /// <summary>
    /// DELETE /api/users/{userId}/groups/{groupId} - Remove user from group
    /// </summary>
    [HttpDelete("{userId:int}/groups/{groupId:int}")]
    public Task<ActionResult<ACS.WebApi.Resources.ApiResponse<bool>>> RemoveUserFromGroup(int userId, int groupId)
    {
        try
        {
            // Create remove user from group request - need to implement this method in TenantGrpcClientService
            // For now, log an error and return not implemented
            _logger.LogError("RemoveUserFromGroup not yet implemented in gRPC client");
            return Task.FromResult<ActionResult<ACS.WebApi.Resources.ApiResponse<bool>>>(StatusCode(501, new[] { "Remove user from group not yet implemented" }.ToErrorApiResponse<bool>("Not implemented")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from group {GroupId}", userId, groupId);
            return Task.FromResult<ActionResult<ACS.WebApi.Resources.ApiResponse<bool>>>(StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<bool>("Internal server error")));
        }
    }

    /// <summary>
    /// POST /api/users/{userId}/roles - Assign user to a role
    /// </summary>
    [HttpPost("{userId:int}/roles")]
    public async Task<ActionResult<ACS.WebApi.Resources.ApiResponse<bool>>> AssignUserToRole(int userId, [FromBody] AssignUserToRoleResource resource)
    {
        try
        {
            if (resource.UserId != userId)
            {
                return BadRequest(new[] { "User ID in URL and body must match" }.ToErrorApiResponse<bool>());
            }

            var assignUserToRoleRequest = new ACS.WebApi.DTOs.AssignUserToRoleRequest
            {
                UserId = userId,
                RoleId = resource.RoleId
            };
            
            var grpcResponse = await _grpcClient.AssignUserToRoleAsync(assignUserToRoleRequest);

            if (!grpcResponse.Success)
            {
                return BadRequest(grpcResponse.Errors.ToErrorApiResponse<bool>(grpcResponse.Message));
            }

            return Ok(true.ToApiResponse(message: grpcResponse.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning user {UserId} to role {RoleId}", userId, resource.RoleId);
            return StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<bool>("Internal server error"));
        }
    }

    /// <summary>
    /// DELETE /api/users/{userId}/roles/{roleId} - Unassign user from role
    /// </summary>
    [HttpDelete("{userId:int}/roles/{roleId:int}")]
    public Task<ActionResult<ACS.WebApi.Resources.ApiResponse<bool>>> UnassignUserFromRole(int userId, int roleId)
    {
        try
        {
            // Create unassign user from role request - need to implement this method in TenantGrpcClientService
            // For now, log an error and return not implemented
            _logger.LogError("UnassignUserFromRole not yet implemented in gRPC client");
            return Task.FromResult<ActionResult<ACS.WebApi.Resources.ApiResponse<bool>>>(StatusCode(501, new[] { "Unassign user from role not yet implemented" }.ToErrorApiResponse<bool>("Not implemented")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning user {UserId} from role {RoleId}", userId, roleId);
            return Task.FromResult<ActionResult<ACS.WebApi.Resources.ApiResponse<bool>>>(StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<bool>("Internal server error")));
        }
    }

    /// <summary>
    /// POST /api/users/bulk - Create multiple users in bulk
    /// </summary>
    [HttpPost("bulk")]
    public Task<ActionResult<ACS.WebApi.Resources.ApiResponse<ACS.WebApi.Resources.BulkOperationResultResource<UserResource>>>> CreateUsersBulk([FromBody] ACS.WebApi.Resources.BulkOperationResource<CreateUserResource> resource)
    {
        try
        {
            // Bulk operations not yet implemented in gRPC client
            _logger.LogError("Bulk user creation not yet implemented in gRPC client");
            return Task.FromResult<ActionResult<ACS.WebApi.Resources.ApiResponse<ACS.WebApi.Resources.BulkOperationResultResource<UserResource>>>>(StatusCode(501, new[] { "Bulk user creation not yet implemented" }.ToErrorApiResponse<ACS.WebApi.Resources.BulkOperationResultResource<UserResource>>("Not implemented")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating users in bulk");
            return Task.FromResult<ActionResult<ACS.WebApi.Resources.ApiResponse<ACS.WebApi.Resources.BulkOperationResultResource<UserResource>>>>(StatusCode(500, new[] { ex.Message }.ToErrorApiResponse<ACS.WebApi.Resources.BulkOperationResultResource<UserResource>>("Internal server error")));
        }
    }

    /// <summary>
    /// Helper method to get current user ID from context
    /// </summary>
    private string GetCurrentUserId()
    {
        // TODO: Implement proper user context extraction
        return User.Identity?.Name ?? "system";
    }
}