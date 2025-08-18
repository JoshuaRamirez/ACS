using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissionsController : ControllerBase
{
    private readonly TenantGrpcClientService _grpcClientService;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        TenantGrpcClientService grpcClientService,
        ILogger<PermissionsController> logger)
    {
        _grpcClientService = grpcClientService;
        _logger = logger;
    }

    /// <summary>
    /// Check if an entity has permission for a specific resource and HTTP verb
    /// </summary>
    [HttpPost("check")]
    public async Task<ActionResult<ApiResponse<CheckPermissionResponse>>> CheckPermission([FromBody] CheckPermissionRequest request)
    {
        try
        {
            if (request.EntityId <= 0)
            {
                return BadRequest(new ApiResponse<CheckPermissionResponse>(false, null, "Entity ID must be greater than 0"));
            }

            if (string.IsNullOrWhiteSpace(request.Uri))
            {
                return BadRequest(new ApiResponse<CheckPermissionResponse>(false, null, "URI is required"));
            }

            if (string.IsNullOrWhiteSpace(request.HttpVerb))
            {
                return BadRequest(new ApiResponse<CheckPermissionResponse>(false, null, "HTTP verb is required"));
            }

            var result = await _grpcClientService.CheckPermissionAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission for entity {EntityId} on {Uri}:{HttpVerb}", 
                request.EntityId, request.Uri, request.HttpVerb);
            return StatusCode(500, new ApiResponse<CheckPermissionResponse>(false, null, "Error checking permission"));
        }
    }

    /// <summary>
    /// Grant permission to an entity for a specific resource and HTTP verb
    /// </summary>
    [HttpPost("grant")]
    public async Task<ActionResult<ApiResponse<bool>>> GrantPermission([FromBody] GrantPermissionRequest request)
    {
        try
        {
            if (request.EntityId <= 0)
            {
                return BadRequest(new ApiResponse<bool>(false, false, "Entity ID must be greater than 0"));
            }

            if (string.IsNullOrWhiteSpace(request.Uri))
            {
                return BadRequest(new ApiResponse<bool>(false, false, "URI is required"));
            }

            if (string.IsNullOrWhiteSpace(request.HttpVerb))
            {
                return BadRequest(new ApiResponse<bool>(false, false, "HTTP verb is required"));
            }

            var result = await _grpcClientService.GrantPermissionAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting permission to entity {EntityId} for {Uri}:{HttpVerb}", 
                request.EntityId, request.Uri, request.HttpVerb);
            return StatusCode(500, new ApiResponse<bool>(false, false, "Error granting permission"));
        }
    }

    /// <summary>
    /// Deny permission to an entity for a specific resource and HTTP verb
    /// </summary>
    [HttpPost("deny")]
    public async Task<ActionResult<ApiResponse<bool>>> DenyPermission([FromBody] DenyPermissionRequest request)
    {
        try
        {
            if (request.EntityId <= 0)
            {
                return BadRequest(new ApiResponse<bool>(false, false, "Entity ID must be greater than 0"));
            }

            if (string.IsNullOrWhiteSpace(request.Uri))
            {
                return BadRequest(new ApiResponse<bool>(false, false, "URI is required"));
            }

            if (string.IsNullOrWhiteSpace(request.HttpVerb))
            {
                return BadRequest(new ApiResponse<bool>(false, false, "HTTP verb is required"));
            }

            var result = await _grpcClientService.DenyPermissionAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying permission to entity {EntityId} for {Uri}:{HttpVerb}", 
                request.EntityId, request.Uri, request.HttpVerb);
            return StatusCode(500, new ApiResponse<bool>(false, false, "Error denying permission"));
        }
    }

    /// <summary>
    /// Get all permissions for a specific entity
    /// </summary>
    [HttpGet("entity/{entityId:int}")]
    public Task<ActionResult<ApiResponse<PermissionListResponse>>> GetEntityPermissions(int entityId, [FromQuery] PagedRequest request)
    {
        try
        {
            if (entityId <= 0)
            {
                return Task.FromResult<ActionResult<ApiResponse<PermissionListResponse>>>(BadRequest(new ApiResponse<PermissionListResponse>(false, null, "Entity ID must be greater than 0")));
            }

            // For now, return a placeholder since we don't have a GetEntityPermissions gRPC method yet
            var permissions = new List<PermissionResponse>();
            var response = new PermissionListResponse(permissions, 0, request.Page, request.PageSize);
            
            return Task.FromResult<ActionResult<ApiResponse<PermissionListResponse>>>(Ok(new ApiResponse<PermissionListResponse>(true, response)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for entity {EntityId}", entityId);
            return Task.FromResult<ActionResult<ApiResponse<PermissionListResponse>>>(StatusCode(500, new ApiResponse<PermissionListResponse>(false, null, "Error retrieving permissions")));
        }
    }

    /// <summary>
    /// Remove a permission from an entity
    /// </summary>
    [HttpDelete("entity/{entityId:int}")]
    public Task<ActionResult<ApiResponse<bool>>> RemovePermission(int entityId, [FromBody] GrantPermissionRequest request)
    {
        try
        {
            if (entityId != request.EntityId)
            {
                return Task.FromResult<ActionResult<ApiResponse<bool>>>(BadRequest(new ApiResponse<bool>(false, false, "Entity ID in URL and body must match")));
            }

            if (string.IsNullOrWhiteSpace(request.Uri))
            {
                return Task.FromResult<ActionResult<ApiResponse<bool>>>(BadRequest(new ApiResponse<bool>(false, false, "URI is required")));
            }

            if (string.IsNullOrWhiteSpace(request.HttpVerb))
            {
                return Task.FromResult<ActionResult<ApiResponse<bool>>>(BadRequest(new ApiResponse<bool>(false, false, "HTTP verb is required")));
            }

            // For now, return a placeholder since we don't have a RemovePermission gRPC method yet
            return Task.FromResult<ActionResult<ApiResponse<bool>>>(StatusCode(501, new ApiResponse<bool>(false, false, "RemovePermission not implemented yet")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission from entity {EntityId} for {Uri}:{HttpVerb}", 
                entityId, request.Uri, request.HttpVerb);
            return Task.FromResult<ActionResult<ApiResponse<bool>>>(StatusCode(500, new ApiResponse<bool>(false, false, "Error removing permission")));
        }
    }
}