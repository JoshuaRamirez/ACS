using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;
using ACS.Infrastructure.Services;
using ACS.Service.Infrastructure;
using ACS.Service.Requests;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly IUserContextService _userContext;
    private readonly ILogger<GroupsController> _logger;
    private readonly IGrpcClientService _grpcClientService;
    private readonly IErrorMapper _errorMapper;

    public GroupsController(
        IUserContextService userContext,
        ILogger<GroupsController> logger,
        IGrpcClientService? grpcClientService = null,
        IErrorMapper? errorMapper = null)
    {
        _userContext = userContext;
        _logger = logger;
        _grpcClientService = grpcClientService ?? new MockGrpcClientService();
        _errorMapper = errorMapper ?? new MockErrorMapper();
    }

    /// <summary>
    /// Get all groups for the current tenant
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<GroupListResponse>>> GetGroups([FromQuery] PagedRequest request)
    {
        try
        {
            var getGroupsCommand = new GetGroupsCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                request.Page,
                request.PageSize);

            var result = await _grpcClientService.GetGroupsAsync(getGroupsCommand);
            
            if (result.Success && result.Data != null)
            {
                return Ok(result);
            }
            
            return StatusCode(500, new ApiResponse<GroupListResponse>(false, null, result.Message ?? "Error retrieving groups"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving groups");
            return this.HandleGrpcException<ApiResponse<GroupListResponse>>(ex, _errorMapper, "Error retrieving groups");
        }
    }

    /// <summary>
    /// Get a specific group by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<GroupResponse>>> GetGroup(int id)
    {
        try
        {
            var getGroupCommand = new GetGroupCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                id);

            var result = await _grpcClientService.GetGroupAsync(getGroupCommand);
            
            if (result.Success && result.Data != null)
            {
                return Ok(result);
            }
            
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(new ApiResponse<GroupResponse>(false, null, $"Group {id} not found"));
            }
            
            return StatusCode(500, new ApiResponse<GroupResponse>(false, null, result.Message ?? "Error retrieving group"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group {GroupId}", id);
            return this.HandleGrpcException<ApiResponse<GroupResponse>>(ex, _errorMapper, "Error retrieving group");
        }
    }

    /// <summary>
    /// Create a new group
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<GroupResponse>>> CreateGroup([FromBody] CreateGroupRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ApiResponse<GroupResponse>(false, null, "Group name is required"));
            }

            var createGroupCommand = new CreateGroupCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                request.Name,
                request.ParentGroupId);

            var result = await _grpcClientService.CreateGroupAsync(createGroupCommand);
            
            if (result.Success && result.Data != null)
            {
                return CreatedAtAction(nameof(GetGroup), new { id = 1 }, result); // result.Data is object type, mock ID
            }
            
            return StatusCode(500, new ApiResponse<GroupResponse>(false, null, result.Message ?? "Error creating group"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group");
            return this.HandleGrpcException<ApiResponse<GroupResponse>>(ex, _errorMapper, "Error creating group");
        }
    }

    /// <summary>
    /// Update an existing group
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<GroupResponse>>> UpdateGroup(int id, [FromBody] UpdateGroupRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ApiResponse<GroupResponse>(false, null, "Group name is required"));
            }

            var updateGroupCommand = new UpdateGroupCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                id,
                request.Name);

            var result = await _grpcClientService.UpdateGroupAsync(updateGroupCommand);
            
            if (result.Success && result.Data != null)
            {
                return Ok(result);
            }
            
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(new ApiResponse<GroupResponse>(false, null, $"Group {id} not found"));
            }
            
            return StatusCode(500, new ApiResponse<GroupResponse>(false, null, result.Message ?? "Error updating group"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group {GroupId}", id);
            return this.HandleGrpcException<ApiResponse<GroupResponse>>(ex, _errorMapper, "Error updating group");
        }
    }

    /// <summary>
    /// Delete a group
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteGroup(int id)
    {
        try
        {
            var deleteGroupCommand = new DeleteGroupCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                id);

            var result = await _grpcClientService.DeleteGroupAsync(deleteGroupCommand);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(new ApiResponse<bool>(false, false, $"Group {id} not found"));
            }
            
            return StatusCode(500, new ApiResponse<bool>(false, false, result.Message ?? "Error deleting group"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting group {GroupId}", id);
            return this.HandleGrpcException<ACS.WebApi.DTOs.ApiResponse<bool>>(ex, _errorMapper, "Error deleting group");
        }
    }

    /// <summary>
    /// Add a child group to a parent group
    /// </summary>
    [HttpPost("{parentGroupId:int}/children")]
    public async Task<ActionResult<ApiResponse<bool>>> AddGroupToGroup(int parentGroupId, [FromBody] AddGroupToGroupRequest request)
    {
        try
        {
            if (request.ParentGroupId != parentGroupId)
            {
                return BadRequest(new ApiResponse<bool>(false, false, "Parent group ID in URL and body must match"));
            }

            var result = await _grpcClientService.AddGroupToGroupAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding group {ChildGroupId} to group {ParentGroupId}", request.ChildGroupId, parentGroupId);
            return this.HandleGrpcException<ACS.WebApi.DTOs.ApiResponse<bool>>(ex, _errorMapper, "Error adding group to group");
        }
    }

    /// <summary>
    /// Add a role to a group
    /// </summary>
    [HttpPost("{groupId:int}/roles")]
    public async Task<ActionResult<ApiResponse<bool>>> AddRoleToGroup(int groupId, [FromBody] ACS.Service.Requests.AddRoleToGroupRequest request)
    {
        try
        {
            if (request.GroupId != groupId)
            {
                return BadRequest(new ApiResponse<bool>(false, false, "Group ID in URL and body must match"));
            }

            var result = await _grpcClientService.AddRoleToGroupAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding role {RoleId} to group {GroupId}", request.RoleId, groupId);
            return this.HandleGrpcException<ACS.WebApi.DTOs.ApiResponse<bool>>(ex, _errorMapper, "Error adding role to group");
        }
    }
}

// Extension method for handling gRPC exceptions
public static class ControllerExtensions
{
    public static ActionResult<T> HandleGrpcException<T>(this ControllerBase controller, Exception ex, IErrorMapper errorMapper, string defaultMessage)
    {
        // Mock implementation - return a generic error response
        return controller.StatusCode(500, new ApiResponse<T>(false, default(T), ex.Message ?? defaultMessage));
    }
}

// Mock interfaces and implementations
public interface IGrpcClientService
{
    Task<ServiceResult<object>> GetGroupsAsync(object command);
    Task<ServiceResult<object>> GetGroupAsync(object command);
    Task<ServiceResult<object>> CreateGroupAsync(object command);
    Task<ServiceResult<object>> UpdateGroupAsync(object command);
    Task<ServiceResult<object>> DeleteGroupAsync(object command);
    Task<ServiceResult<object>> AddGroupToGroupAsync(object command);
    Task<ServiceResult<object>> AddRoleToGroupAsync(object command);
    Task<ServiceResult<object>> CheckPermissionAsync(object request);
    Task<ServiceResult<object>> GrantPermissionAsync(object request);
    Task<ServiceResult<object>> DenyPermissionAsync(object request);
    Task<ServiceResult<object>> GetRolesAsync(object command);
    Task<ServiceResult<object>> GetRoleAsync(object command);
    Task<ServiceResult<object>> CreateRoleAsync(object command);
    Task<ServiceResult<object>> UpdateRoleAsync(object command);
    Task<ServiceResult<object>> DeleteRoleAsync(object command);
    Task<ServiceResult<object>> GetEntityPermissionsAsync(object command);
    Task<ServiceResult<object>> RemovePermissionAsync(object command);
}

public interface IErrorMapper
{
    string MapError(Exception ex);
}

public class MockGrpcClientService : IGrpcClientService
{
    public Task<ServiceResult<object>> GetGroupsAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = new { Groups = new List<object>(), TotalCount = 0 },
            Message = "Groups retrieved successfully"
        });
    }

    public Task<ServiceResult<object>> GetGroupAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = new { Id = 1, Name = "Mock Group", Description = "Mock group description" },
            Message = "Group retrieved successfully"
        });
    }

    public Task<ServiceResult<object>> CreateGroupAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = new { Id = 1, Name = "New Group", Description = "Created group" },
            Message = "Group created successfully"
        });
    }

    public Task<ServiceResult<object>> UpdateGroupAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = new { Id = 1, Name = "Updated Group", Description = "Updated group" },
            Message = "Group updated successfully"
        });
    }

    public Task<ServiceResult<object>> DeleteGroupAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = true,
            Message = "Group deleted successfully"
        });
    }

    public Task<ServiceResult<object>> AddGroupToGroupAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = true,
            Message = "Group added to group successfully"
        });
    }

    public Task<ServiceResult<object>> AddRoleToGroupAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = true,
            Message = "Role added to group successfully"
        });
    }

    public Task<ServiceResult<object>> CheckPermissionAsync(object request)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = new { HasPermission = true, Uri = "/test", HttpVerb = "GET", EntityId = 1 },
            Message = "Permission checked successfully"
        });
    }

    public Task<ServiceResult<object>> GrantPermissionAsync(object request)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = true,
            Message = "Permission granted successfully"
        });
    }

    public Task<ServiceResult<object>> DenyPermissionAsync(object request)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = true,
            Message = "Permission denied successfully"
        });
    }

    public Task<ServiceResult<object>> GetRolesAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = new { Roles = new List<object>(), TotalCount = 0, Page = 1, PageSize = 10 },
            Message = "Roles retrieved successfully"
        });
    }

    public Task<ServiceResult<object>> GetRoleAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = new { Id = 1, Name = "Mock Role", Description = "Mock role description" },
            Message = "Role retrieved successfully"
        });
    }

    public Task<ServiceResult<object>> CreateRoleAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = new { Id = 1, Name = "New Role", Description = "Created role" },
            Message = "Role created successfully"
        });
    }

    public Task<ServiceResult<object>> UpdateRoleAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = new { Id = 1, Name = "Updated Role", Description = "Updated role description" },
            Message = "Role updated successfully"
        });
    }

    public Task<ServiceResult<object>> DeleteRoleAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = true,
            Message = "Role deleted successfully"
        });
    }

    public Task<ServiceResult<object>> GetEntityPermissionsAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = new List<object>(),
            Message = "Entity permissions retrieved successfully"
        });
    }

    public Task<ServiceResult<object>> RemovePermissionAsync(object command)
    {
        return Task.FromResult(new ServiceResult<object>
        {
            Success = true,
            Data = true,
            Message = "Permission removed successfully"
        });
    }
}

public class MockErrorMapper : IErrorMapper
{
    public string MapError(Exception ex)
    {
        return ex.Message ?? "An error occurred";
    }
}

public class ServiceResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
}
