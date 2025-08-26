using Microsoft.AspNetCore.Mvc;
using ACS.Service.Services;
using ACS.Service.Requests;
using ACS.Service.Responses;
// Removed ambiguous import - using fully qualified names instead
using ACS.Infrastructure.Services;
using ACS.Service.Infrastructure;
using ACS.WebApi.DTOs;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IUserContextService _userContext;
    private readonly ILogger<RolesController> _logger;
    private readonly IGrpcClientService _grpcClientService;
    private readonly IErrorMapper _errorMapper;

    public RolesController(
        IRoleService roleService,
        IUserContextService userContext,
        ILogger<RolesController> logger,
        IGrpcClientService? grpcClientService = null,
        IErrorMapper? errorMapper = null)
    {
        _roleService = roleService;
        _userContext = userContext;
        _logger = logger;
        _grpcClientService = grpcClientService ?? new MockGrpcClientService();
        _errorMapper = errorMapper ?? new MockErrorMapper();
    }

    /// <summary>
    /// Get all roles for the current tenant
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ACS.WebApi.DTOs.ApiResponse<RoleListResponse>>> GetRoles([FromQuery] GetRolesRequest request)
    {
        try
        {
            var getRolesCommand = new ACS.Service.Infrastructure.GetRolesCommand(
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
            
            return StatusCode(500, new ACS.WebApi.DTOs.ApiResponse<RoleListResponse>(false, null, result.Message ?? "Error retrieving roles"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles");
            return this.HandleGrpcException<ACS.WebApi.DTOs.ApiResponse<RoleListResponse>>(ex, _errorMapper, "Error retrieving roles");
        }
    }

    /// <summary>
    /// Get a specific role by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>>> GetRole(int id)
    {
        try
        {
            var getRoleCommand = new ACS.Service.Infrastructure.GetRoleCommand(
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
                return NotFound(new ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>(false, null, $"Role {id} not found"));
            }
            
            return StatusCode(500, new ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>(false, null, result.Message ?? "Error retrieving role"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleId}", id);
            return this.HandleGrpcException<ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>>(ex, _errorMapper, "Error retrieving role");
        }
    }

    /// <summary>
    /// Create a new role
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>>> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>(false, null, "Role name is required"));
            }

            var createRoleCommand = new ACS.Service.Infrastructure.CreateRoleCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _userContext.GetCurrentUserId(),
                request.Name,
                request.GroupId);

            var result = await _grpcClientService.CreateRoleAsync(createRoleCommand);
            
            if (result.Success && result.Data != null)
            {
                return CreatedAtAction(nameof(GetRole), new { id = 1 }, result);
            }
            
            return StatusCode(500, new ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>(false, null, result.Message ?? "Error creating role"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role");
            return this.HandleGrpcException<ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>>(ex, _errorMapper, "Error creating role");
        }
    }

    /// <summary>
    /// Update an existing role
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>>> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>(false, null, "Role name is required"));
            }

            var updateRoleCommand = new ACS.Service.Infrastructure.UpdateRoleCommand(
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
                return NotFound(new ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>(false, null, $"Role {id} not found"));
            }
            
            return StatusCode(500, new ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>(false, null, result.Message ?? "Error updating role"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", id);
            return this.HandleGrpcException<ACS.WebApi.DTOs.ApiResponse<ACS.WebApi.DTOs.RoleResponse>>(ex, _errorMapper, "Error updating role");
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
            var deleteRoleCommand = new ACS.Service.Infrastructure.DeleteRoleCommand(
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
            return this.HandleGrpcException<ACS.WebApi.DTOs.ApiResponse<bool>>(ex, _errorMapper, "Error deleting role");
        }
    }
}