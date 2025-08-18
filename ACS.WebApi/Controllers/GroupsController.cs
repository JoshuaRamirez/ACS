using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;
using ACS.WebApi.Services;
using ACS.Service.Infrastructure;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly TenantGrpcClientService _grpcClientService;
    private readonly GrpcErrorMappingService _errorMapper;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(
        TenantGrpcClientService grpcClientService,
        GrpcErrorMappingService errorMapper,
        ILogger<GroupsController> logger)
    {
        _grpcClientService = grpcClientService;
        _errorMapper = errorMapper;
        _logger = logger;
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
                "current-user", // TODO: Get from authentication context
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
            return this.HandleGrpcException<GroupListResponse>(ex, _errorMapper, "Error retrieving groups");
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
                "current-user", // TODO: Get from authentication context
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
            return this.HandleGrpcException<GroupResponse>(ex, _errorMapper, "Error retrieving group");
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
                "current-user", // TODO: Get from authentication context
                request.Name,
                request.ParentGroupId);

            var result = await _grpcClientService.CreateGroupAsync(createGroupCommand);
            
            if (result.Success && result.Data != null)
            {
                return CreatedAtAction(nameof(GetGroup), new { id = result.Data.Id }, result);
            }
            
            return StatusCode(500, new ApiResponse<GroupResponse>(false, null, result.Message ?? "Error creating group"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group");
            return this.HandleGrpcException<GroupResponse>(ex, _errorMapper, "Error creating group");
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
                "current-user", // TODO: Get from authentication context
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
            return this.HandleGrpcException<GroupResponse>(ex, _errorMapper, "Error updating group");
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
                "current-user", // TODO: Get from authentication context
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
            return this.HandleGrpcException<bool>(ex, _errorMapper, "Error deleting group");
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
            return this.HandleGrpcException<bool>(ex, _errorMapper, "Error adding group to group");
        }
    }

    /// <summary>
    /// Add a role to a group
    /// </summary>
    [HttpPost("{groupId:int}/roles")]
    public async Task<ActionResult<ApiResponse<bool>>> AddRoleToGroup(int groupId, [FromBody] AddRoleToGroupRequest request)
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
            return this.HandleGrpcException<bool>(ex, _errorMapper, "Error adding role to group");
        }
    }
}