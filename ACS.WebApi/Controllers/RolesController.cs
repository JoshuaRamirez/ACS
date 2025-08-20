using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;
using ACS.Infrastructure.Services;
using ACS.Service.Infrastructure;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly TenantGrpcClientService _grpcClientService;
    private readonly GrpcErrorMappingService _errorMapper;
    private readonly IUserContextService _userContext;
    private readonly ILogger<RolesController> _logger;

    public RolesController(
        TenantGrpcClientService grpcClientService,
        GrpcErrorMappingService errorMapper,
        IUserContextService userContext,
        ILogger<RolesController> logger)
    {
        _grpcClientService = grpcClientService;
        _errorMapper = errorMapper;
        _userContext = userContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all roles for the current tenant
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<RoleListResponse>>> GetRoles([FromQuery] PagedRequest request)
    {
        try
        {
            var getRolesCommand = new GetRolesCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                request.Page,
                request.PageSize);

            var result = await _grpcClientService.GetRolesAsync(getRolesCommand);
            
            if (result.Success && result.Data != null)
            {
                return Ok(result);
            }
            
            return StatusCode(500, new ApiResponse<RoleListResponse>(false, null, result.Message ?? "Error retrieving roles"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles");
            return this.HandleGrpcException<RoleListResponse>(ex, _errorMapper, "Error retrieving roles");
        }
    }

    /// <summary>
    /// Get a specific role by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<RoleResponse>>> GetRole(int id)
    {
        try
        {
            var getRoleCommand = new GetRoleCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                id);

            var result = await _grpcClientService.GetRoleAsync(getRoleCommand);
            
            if (result.Success && result.Data != null)
            {
                return Ok(result);
            }
            
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(new ApiResponse<RoleResponse>(false, null, $"Role {id} not found"));
            }
            
            return StatusCode(500, new ApiResponse<RoleResponse>(false, null, result.Message ?? "Error retrieving role"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleId}", id);
            return this.HandleGrpcException<RoleResponse>(ex, _errorMapper, "Error retrieving role");
        }
    }

    /// <summary>
    /// Create a new role
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<RoleResponse>>> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ApiResponse<RoleResponse>(false, null, "Role name is required"));
            }

            var createRoleCommand = new CreateRoleCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                request.Name,
                request.GroupId);

            var result = await _grpcClientService.CreateRoleAsync(createRoleCommand);
            
            if (result.Success && result.Data != null)
            {
                return CreatedAtAction(nameof(GetRole), new { id = result.Data.Id }, result);
            }
            
            return StatusCode(500, new ApiResponse<RoleResponse>(false, null, result.Message ?? "Error creating role"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role");
            return this.HandleGrpcException<RoleResponse>(ex, _errorMapper, "Error creating role");
        }
    }

    /// <summary>
    /// Update an existing role
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<RoleResponse>>> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ApiResponse<RoleResponse>(false, null, "Role name is required"));
            }

            var updateRoleCommand = new UpdateRoleCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                id,
                request.Name);

            var result = await _grpcClientService.UpdateRoleAsync(updateRoleCommand);
            
            if (result.Success && result.Data != null)
            {
                return Ok(result);
            }
            
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(new ApiResponse<RoleResponse>(false, null, $"Role {id} not found"));
            }
            
            return StatusCode(500, new ApiResponse<RoleResponse>(false, null, result.Message ?? "Error updating role"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", id);
            return this.HandleGrpcException<RoleResponse>(ex, _errorMapper, "Error updating role");
        }
    }

    /// <summary>
    /// Delete a role
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteRole(int id)
    {
        try
        {
            var deleteRoleCommand = new DeleteRoleCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                id);

            var result = await _grpcClientService.DeleteRoleAsync(deleteRoleCommand);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(new ApiResponse<bool>(false, false, $"Role {id} not found"));
            }
            
            return StatusCode(500, new ApiResponse<bool>(false, false, result.Message ?? "Error deleting role"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", id);
            return this.HandleGrpcException<bool>(ex, _errorMapper, "Error deleting role");
        }
    }
}