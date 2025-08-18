using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly TenantGrpcClientService _grpcClientService;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(
        TenantGrpcClientService grpcClientService,
        ILogger<GroupsController> logger)
    {
        _grpcClientService = grpcClientService;
        _logger = logger;
    }

    /// <summary>
    /// Get all groups for the current tenant
    /// </summary>
    [HttpGet]
    public Task<ActionResult<ApiResponse<GroupListResponse>>> GetGroups([FromQuery] PagedRequest request)
    {
        try
        {
            // For now, return a placeholder since we don't have a GetGroups gRPC method yet
            var groups = new List<GroupResponse>();
            var response = new GroupListResponse(groups, 0, request.Page, request.PageSize);
            
            return Task.FromResult<ActionResult<ApiResponse<GroupListResponse>>>(Ok(new ApiResponse<GroupListResponse>(true, response)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving groups");
            return Task.FromResult<ActionResult<ApiResponse<GroupListResponse>>>(StatusCode(500, new ApiResponse<GroupListResponse>(false, null, "Error retrieving groups")));
        }
    }

    /// <summary>
    /// Get a specific group by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public Task<ActionResult<ApiResponse<GroupResponse>>> GetGroup(int id)
    {
        try
        {
            // For now, return a placeholder since we don't have a GetGroup gRPC method yet
            return Task.FromResult<ActionResult<ApiResponse<GroupResponse>>>(NotFound(new ApiResponse<GroupResponse>(false, null, $"Group {id} not found")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group {GroupId}", id);
            return Task.FromResult<ActionResult<ApiResponse<GroupResponse>>>(StatusCode(500, new ApiResponse<GroupResponse>(false, null, "Error retrieving group")));
        }
    }

    /// <summary>
    /// Create a new group
    /// </summary>
    [HttpPost]
    public Task<ActionResult<ApiResponse<GroupResponse>>> CreateGroup([FromBody] CreateGroupRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Task.FromResult<ActionResult<ApiResponse<GroupResponse>>>(BadRequest(new ApiResponse<GroupResponse>(false, null, "Group name is required")));
            }

            // For now, return a placeholder since we don't have a CreateGroup gRPC method yet
            return Task.FromResult<ActionResult<ApiResponse<GroupResponse>>>(StatusCode(501, new ApiResponse<GroupResponse>(false, null, "CreateGroup not implemented yet")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group");
            return Task.FromResult<ActionResult<ApiResponse<GroupResponse>>>(StatusCode(500, new ApiResponse<GroupResponse>(false, null, "Error creating group")));
        }
    }

    /// <summary>
    /// Update an existing group
    /// </summary>
    [HttpPut("{id:int}")]
    public Task<ActionResult<ApiResponse<GroupResponse>>> UpdateGroup(int id, [FromBody] UpdateGroupRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Task.FromResult<ActionResult<ApiResponse<GroupResponse>>>(BadRequest(new ApiResponse<GroupResponse>(false, null, "Group name is required")));
            }

            // For now, return a placeholder since we don't have an UpdateGroup gRPC method yet
            return Task.FromResult<ActionResult<ApiResponse<GroupResponse>>>(StatusCode(501, new ApiResponse<GroupResponse>(false, null, "UpdateGroup not implemented yet")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group {GroupId}", id);
            return Task.FromResult<ActionResult<ApiResponse<GroupResponse>>>(StatusCode(500, new ApiResponse<GroupResponse>(false, null, "Error updating group")));
        }
    }

    /// <summary>
    /// Delete a group
    /// </summary>
    [HttpDelete("{id:int}")]
    public Task<ActionResult<ApiResponse<bool>>> DeleteGroup(int id)
    {
        try
        {
            // For now, return a placeholder since we don't have a DeleteGroup gRPC method yet
            return Task.FromResult<ActionResult<ApiResponse<bool>>>(StatusCode(501, new ApiResponse<bool>(false, false, "DeleteGroup not implemented yet")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting group {GroupId}", id);
            return Task.FromResult<ActionResult<ApiResponse<bool>>>(StatusCode(500, new ApiResponse<bool>(false, false, "Error deleting group")));
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
            return StatusCode(500, new ApiResponse<bool>(false, false, "Error adding group to group"));
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
            return StatusCode(500, new ApiResponse<bool>(false, false, "Error adding role to group"));
        }
    }
}