using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;
using ACS.Infrastructure.Services;
using ACS.Service.Infrastructure;
using ACS.Service.Domain;
using ACS.Service.Requests;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissionsController : ControllerBase
{
    private readonly IUserContextService _userContext;
    private readonly ILogger<PermissionsController> _logger;
    private readonly IGrpcClientService _grpcClientService;
    private readonly IErrorMapper _errorMapper;

    public PermissionsController(
        IUserContextService userContext,
        ILogger<PermissionsController> logger,
        IGrpcClientService? grpcClientService = null,
        IErrorMapper? errorMapper = null)
    {
        _userContext = userContext;
        _logger = logger;
        _grpcClientService = grpcClientService ?? new MockGrpcClientService();
        _errorMapper = errorMapper ?? new MockErrorMapper();
    }

    /// <summary>
    /// Check if an entity has permission for a specific resource and HTTP verb
    /// </summary>
    [HttpPost("check")]
    public async Task<ActionResult<ApiResponse<CheckPermissionResponse>>> CheckPermission([FromBody] ACS.Service.Requests.CheckPermissionRequest request)
    {
        try
        {
            if (request.EntityId <= 0)
            {
                return BadRequest(new ApiResponse<CheckPermissionResponse>(false, null, "Entity ID must be greater than 0"));
            }

            if (string.IsNullOrWhiteSpace(request.Resource))
            {
                return BadRequest(new ApiResponse<CheckPermissionResponse>(false, null, "Resource is required"));
            }

            if (string.IsNullOrWhiteSpace(request.Action))
            {
                return BadRequest(new ApiResponse<CheckPermissionResponse>(false, null, "Action is required"));
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
            _logger.LogError(ex, "Error checking permission for entity {EntityId} on {Resource}:{Action}", 
                request.EntityId, request.Resource, request.Action);
            return this.HandleGrpcException<ApiResponse<CheckPermissionResponse>>(ex, _errorMapper, "Error checking permission");
        }
    }

    /// <summary>
    /// Grant permission to an entity for a specific resource and HTTP verb
    /// </summary>
    [HttpPost("grant")]
    public async Task<ActionResult<ApiResponse<bool>>> GrantPermission([FromBody] ACS.WebApi.DTOs.GrantPermissionRequest request)
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
            return this.HandleGrpcException<ApiResponse<bool>>(ex, _errorMapper, "Error granting permission");
        }
    }

    /// <summary>
    /// Deny permission to an entity for a specific resource and HTTP verb
    /// </summary>
    [HttpPost("deny")]
    public async Task<ActionResult<ApiResponse<bool>>> DenyPermission([FromBody] ACS.WebApi.DTOs.DenyPermissionRequest request)
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
            return this.HandleGrpcException<ApiResponse<bool>>(ex, _errorMapper, "Error denying permission");
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
                _userContext.GetCurrentUserId(),
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
            return this.HandleGrpcException<ACS.WebApi.DTOs.ApiResponse<PermissionListResponse>>(ex, _errorMapper, "Error retrieving permissions");
        }
    }

    /// <summary>
    /// Remove a permission from an entity
    /// </summary>
    [HttpDelete("entity/{entityId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> RemovePermission(int entityId, [FromBody] ACS.WebApi.DTOs.GrantPermissionRequest request)
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
                _userContext.GetCurrentUserId(),
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
            return this.HandleGrpcException<ACS.WebApi.DTOs.ApiResponse<bool>>(ex, _errorMapper, "Error removing permission");
        }
    }
}