using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;
using ACS.Infrastructure.Services;
using ACS.Service.Infrastructure;
using ACS.Service.Domain;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissionsController : ControllerBase
{
    private readonly TenantGrpcClientService _grpcClientService;
    private readonly GrpcErrorMappingService _errorMapper;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        TenantGrpcClientService grpcClientService,
        GrpcErrorMappingService errorMapper,
        ILogger<PermissionsController> logger)
    {
        _grpcClientService = grpcClientService;
        _errorMapper = errorMapper;
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
            return this.HandleGrpcException<CheckPermissionResponse>(ex, _errorMapper, "Error checking permission");
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
            return this.HandleGrpcException<bool>(ex, _errorMapper, "Error granting permission");
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
            return this.HandleGrpcException<bool>(ex, _errorMapper, "Error denying permission");
        }
    }

    /// <summary>
    /// Get all permissions for a specific entity
    /// </summary>
    [HttpGet("entity/{entityId:int}")]
    public async Task<ActionResult<ApiResponse<PermissionListResponse>>> GetEntityPermissions(int entityId, [FromQuery] PagedRequest request)
    {
        try
        {
            if (entityId <= 0)
            {
                return BadRequest(new ApiResponse<PermissionListResponse>(false, null, "Entity ID must be greater than 0"));
            }

            var getPermissionsCommand = new GetEntityPermissionsCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                entityId,
                request.Page,
                request.PageSize);

            var result = await _grpcClientService.GetEntityPermissionsAsync(getPermissionsCommand);
            
            if (result.Success && result.Data != null)
            {
                return Ok(result);
            }
            
            return StatusCode(500, new ApiResponse<PermissionListResponse>(false, null, result.Message ?? "Error retrieving permissions"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for entity {EntityId}", entityId);
            return this.HandleGrpcException<PermissionListResponse>(ex, _errorMapper, "Error retrieving permissions");
        }
    }

    /// <summary>
    /// Remove a permission from an entity
    /// </summary>
    [HttpDelete("entity/{entityId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> RemovePermission(int entityId, [FromBody] GrantPermissionRequest request)
    {
        try
        {
            if (entityId != request.EntityId)
            {
                return BadRequest(new ApiResponse<bool>(false, false, "Entity ID in URL and body must match"));
            }

            if (string.IsNullOrWhiteSpace(request.Uri))
            {
                return BadRequest(new ApiResponse<bool>(false, false, "URI is required"));
            }

            if (string.IsNullOrWhiteSpace(request.HttpVerb))
            {
                return BadRequest(new ApiResponse<bool>(false, false, "HTTP verb is required"));
            }

            // Parse the HTTP verb
            if (!Enum.TryParse<HttpVerb>(request.HttpVerb, true, out var httpVerb))
            {
                return BadRequest(new ApiResponse<bool>(false, false, "Invalid HTTP verb"));
            }

            var removePermissionCommand = new RemovePermissionCommand(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                "current-user", // TODO: Get from authentication context
                entityId,
                request.Uri,
                httpVerb);

            var result = await _grpcClientService.RemovePermissionAsync(removePermissionCommand);
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            return StatusCode(500, new ApiResponse<bool>(false, false, result.Message ?? "Error removing permission"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission from entity {EntityId} for {Uri}:{HttpVerb}", 
                entityId, request.Uri, request.HttpVerb);
            return this.HandleGrpcException<bool>(ex, _errorMapper, "Error removing permission");
        }
    }
}