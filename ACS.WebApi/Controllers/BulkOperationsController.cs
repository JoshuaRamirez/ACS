using ACS.Service.Domain;
using ACS.Service.Domain.Events;
using ACS.Service.Domain.Validation;
using ACS.Service.Services;
using ACS.WebApi.Models;
using ACS.WebApi.Models.Requests;
using ACS.WebApi.Models.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for bulk operations on users, roles, permissions, and other entities
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Administrator,BulkOperator")]
[Produces("application/json")]
public class BulkOperationsController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IPermissionService _permissionService;
    private readonly IGroupService _groupService;
    private readonly IResourceService _resourceService;
    private readonly IBulkOperationService _bulkOperationService;
    private readonly IValidationService _validationService;
    private readonly IDomainEventPublisher _eventPublisher;
    private readonly ILogger<BulkOperationsController> _logger;

    public BulkOperationsController(
        IUserService userService,
        IRoleService roleService,
        IPermissionService permissionService,
        IGroupService groupService,
        IResourceService resourceService,
        IBulkOperationService bulkOperationService,
        IValidationService validationService,
        IDomainEventPublisher eventPublisher,
        ILogger<BulkOperationsController> logger)
    {
        _userService = userService;
        _roleService = roleService;
        _permissionService = permissionService;
        _groupService = groupService;
        _resourceService = resourceService;
        _bulkOperationService = bulkOperationService;
        _validationService = validationService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    #region User Bulk Operations

    /// <summary>
    /// Creates multiple users in a single operation
    /// </summary>
    /// <param name="request">Bulk user creation request</param>
    /// <returns>Bulk operation results</returns>
    [HttpPost("users/create")]
    [ProducesResponseType(typeof(BulkOperationResponse<UserResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkOperationResponse<UserResponse>>> BulkCreateUsersAsync(
        [FromBody] BulkCreateUsersRequest request)
    {
        try
        {
            _logger.LogInformation("Starting bulk user creation for {UserCount} users", request.Users.Count);

            var validationResults = new List<BulkValidationResult<CreateUserRequest>>();
            var validUsers = new List<CreateUserRequest>();

            // Pre-validate all users
            for (int i = 0; i < request.Users.Count; i++)
            {
                var user = request.Users[i];
                var validationResult = await ValidateCreateUserRequestAsync(user);
                
                validationResults.Add(new BulkValidationResult<CreateUserRequest>
                {
                    Index = i,
                    Item = user,
                    IsValid = validationResult.IsValid,
                    ValidationErrors = validationResult.AllErrors.Select(e => e.ErrorMessage ?? "").ToList()
                });

                if (validationResult.IsValid)
                {
                    validUsers.Add(user);
                }
            }

            if (!validUsers.Any() && !request.ContinueOnError)
            {
                return BadRequest("No valid users to create and ContinueOnError is false");
            }

            // Execute bulk creation
            var bulkResult = await _bulkOperationService.BulkCreateUsersAsync(validUsers);

            // Publish bulk domain event
            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                "BulkCreateUsers",
                validUsers.Count,
                bulkResult.SuccessCount,
                bulkResult.FailureCount,
                "Bulk user creation via API"));

            var response = new BulkOperationResponse<UserResponse>
            {
                TotalRequested = request.Users.Count,
                TotalProcessed = validUsers.Count,
                SuccessCount = bulkResult.SuccessCount,
                FailureCount = bulkResult.FailureCount + (request.Users.Count - validUsers.Count),
                Results = MapBulkUserResults(bulkResult.Results, validationResults),
                ValidationResults = validationResults.Where(v => !v.IsValid).ToList(),
                Status = DetermineBulkOperationStatus(bulkResult),
                Duration = bulkResult.Duration,
                Warnings = bulkResult.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk user creation");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during bulk user creation");
        }
    }

    /// <summary>
    /// Updates multiple users in a single operation
    /// </summary>
    /// <param name="request">Bulk user update request</param>
    /// <returns>Bulk operation results</returns>
    [HttpPut("users/update")]
    [ProducesResponseType(typeof(BulkOperationResponse<UserResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkOperationResponse<UserResponse>>> BulkUpdateUsersAsync(
        [FromBody] BulkUpdateUsersRequest request)
    {
        try
        {
            _logger.LogInformation("Starting bulk user update for {UserCount} users", request.Updates.Count);

            var bulkResult = await _bulkOperationService.BulkUpdateUsersAsync(
                request.Updates,
                request.ContinueOnError);

            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                "BulkUpdateUsers",
                request.Updates.Count,
                bulkResult.SuccessCount,
                bulkResult.FailureCount,
                "Bulk user update via API"));

            var response = new BulkOperationResponse<UserResponse>
            {
                TotalRequested = request.Updates.Count,
                TotalProcessed = request.Updates.Count,
                SuccessCount = bulkResult.SuccessCount,
                FailureCount = bulkResult.FailureCount,
                Results = MapBulkUserResults(bulkResult.Results),
                Status = DetermineBulkOperationStatus(bulkResult),
                Duration = bulkResult.Duration,
                Warnings = bulkResult.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk user update");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during bulk user update");
        }
    }

    /// <summary>
    /// Assigns roles to multiple users
    /// </summary>
    /// <param name="request">Bulk role assignment request</param>
    /// <returns>Bulk operation results</returns>
    [HttpPost("users/assign-roles")]
    [ProducesResponseType(typeof(BulkOperationResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkOperationResponse>> BulkAssignRolesToUsersAsync(
        [FromBody] BulkAssignRolesRequest request)
    {
        try
        {
            _logger.LogInformation("Starting bulk role assignment: {RoleCount} roles to {UserCount} users",
                request.RoleIds.Count, request.UserIds.Count);

            var bulkResult = await _bulkOperationService.BulkAssignRolesAsync(
                request.UserIds,
                request.RoleIds,
                request.ContinueOnError);

            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                "BulkAssignRoles",
                request.UserIds.Count * request.RoleIds.Count,
                bulkResult.SuccessCount,
                bulkResult.FailureCount,
                $"Bulk role assignment: {string.Join(",", request.RoleIds)} to users"));

            var response = new BulkOperationResponse
            {
                TotalRequested = request.UserIds.Count,
                TotalProcessed = request.UserIds.Count,
                SuccessCount = bulkResult.SuccessCount,
                FailureCount = bulkResult.FailureCount,
                Results = bulkResult.Results.Select(MapBulkOperationResult).ToList(),
                Status = DetermineBulkOperationStatus(bulkResult),
                Duration = bulkResult.Duration,
                Warnings = bulkResult.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk role assignment");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during bulk role assignment");
        }
    }

    /// <summary>
    /// Removes roles from multiple users
    /// </summary>
    /// <param name="request">Bulk role removal request</param>
    /// <returns>Bulk operation results</returns>
    [HttpPost("users/remove-roles")]
    [ProducesResponseType(typeof(BulkOperationResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkOperationResponse>> BulkRemoveRolesFromUsersAsync(
        [FromBody] BulkRemoveRolesRequest request)
    {
        try
        {
            _logger.LogInformation("Starting bulk role removal: {RoleCount} roles from {UserCount} users",
                request.RoleIds.Count, request.UserIds.Count);

            var bulkResult = await _bulkOperationService.BulkRemoveRolesAsync(
                request.UserIds,
                request.RoleIds,
                request.ContinueOnError);

            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                "BulkRemoveRoles",
                request.UserIds.Count * request.RoleIds.Count,
                bulkResult.SuccessCount,
                bulkResult.FailureCount,
                $"Bulk role removal: {string.Join(",", request.RoleIds)} from users"));

            var response = new BulkOperationResponse
            {
                TotalRequested = request.UserIds.Count,
                TotalProcessed = request.UserIds.Count,
                SuccessCount = bulkResult.SuccessCount,
                FailureCount = bulkResult.FailureCount,
                Results = bulkResult.Results.Select(MapBulkOperationResult).ToList(),
                Status = DetermineBulkOperationStatus(bulkResult),
                Duration = bulkResult.Duration,
                Warnings = bulkResult.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk role removal");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during bulk role removal");
        }
    }

    /// <summary>
    /// Activates or deactivates multiple users
    /// </summary>
    /// <param name="request">Bulk user status change request</param>
    /// <returns>Bulk operation results</returns>
    [HttpPost("users/change-status")]
    [ProducesResponseType(typeof(BulkOperationResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkOperationResponse>> BulkChangeUserStatusAsync(
        [FromBody] BulkChangeUserStatusRequest request)
    {
        try
        {
            _logger.LogInformation("Starting bulk user status change: {Status} for {UserCount} users",
                request.IsActive ? "Activate" : "Deactivate", request.UserIds.Count);

            var bulkResult = await _bulkOperationService.BulkChangeUserStatusAsync(
                request.UserIds,
                request.IsActive,
                request.Reason,
                request.ContinueOnError);

            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                "BulkChangeUserStatus",
                request.UserIds.Count,
                bulkResult.SuccessCount,
                bulkResult.FailureCount,
                $"Bulk user {(request.IsActive ? "activation" : "deactivation")}"));

            var response = new BulkOperationResponse
            {
                TotalRequested = request.UserIds.Count,
                TotalProcessed = request.UserIds.Count,
                SuccessCount = bulkResult.SuccessCount,
                FailureCount = bulkResult.FailureCount,
                Results = bulkResult.Results.Select(MapBulkOperationResult).ToList(),
                Status = DetermineBulkOperationStatus(bulkResult),
                Duration = bulkResult.Duration,
                Warnings = bulkResult.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk user status change");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during bulk user status change");
        }
    }

    #endregion

    #region Role Bulk Operations

    /// <summary>
    /// Creates multiple roles in a single operation
    /// </summary>
    /// <param name="request">Bulk role creation request</param>
    /// <returns>Bulk operation results</returns>
    [HttpPost("roles/create")]
    [ProducesResponseType(typeof(BulkOperationResponse<RoleResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkOperationResponse<RoleResponse>>> BulkCreateRolesAsync(
        [FromBody] BulkCreateRolesRequest request)
    {
        try
        {
            _logger.LogInformation("Starting bulk role creation for {RoleCount} roles", request.Roles.Count);

            var bulkResult = await _bulkOperationService.BulkCreateRolesAsync(
                request.Roles,
                request.ContinueOnError);

            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                "BulkCreateRoles",
                request.Roles.Count,
                bulkResult.SuccessCount,
                bulkResult.FailureCount,
                "Bulk role creation via API"));

            var response = new BulkOperationResponse<RoleResponse>
            {
                TotalRequested = request.Roles.Count,
                TotalProcessed = request.Roles.Count,
                SuccessCount = bulkResult.SuccessCount,
                FailureCount = bulkResult.FailureCount,
                Results = MapBulkRoleResults(bulkResult.Results),
                Status = DetermineBulkOperationStatus(bulkResult),
                Duration = bulkResult.Duration,
                Warnings = bulkResult.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk role creation");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during bulk role creation");
        }
    }

    /// <summary>
    /// Assigns permissions to multiple roles
    /// </summary>
    /// <param name="request">Bulk permission assignment request</param>
    /// <returns>Bulk operation results</returns>
    [HttpPost("roles/assign-permissions")]
    [ProducesResponseType(typeof(BulkOperationResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkOperationResponse>> BulkAssignPermissionsToRolesAsync(
        [FromBody] BulkAssignPermissionsRequest request)
    {
        try
        {
            _logger.LogInformation("Starting bulk permission assignment to {RoleCount} roles", request.RoleIds.Count);

            var bulkResult = await _bulkOperationService.BulkAssignPermissionsToRolesAsync(
                request.RoleIds,
                request.Permissions,
                request.ContinueOnError);

            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                "BulkAssignPermissions",
                request.RoleIds.Count * request.Permissions.Count,
                bulkResult.SuccessCount,
                bulkResult.FailureCount,
                "Bulk permission assignment to roles"));

            var response = new BulkOperationResponse
            {
                TotalRequested = request.RoleIds.Count,
                TotalProcessed = request.RoleIds.Count,
                SuccessCount = bulkResult.SuccessCount,
                FailureCount = bulkResult.FailureCount,
                Results = bulkResult.Results.Select(MapBulkOperationResult).ToList(),
                Status = DetermineBulkOperationStatus(bulkResult),
                Duration = bulkResult.Duration,
                Warnings = bulkResult.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk permission assignment");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during bulk permission assignment");
        }
    }

    #endregion

    #region Permission Bulk Operations

    /// <summary>
    /// Creates multiple permissions in a single operation
    /// </summary>
    /// <param name="request">Bulk permission creation request</param>
    /// <returns>Bulk operation results</returns>
    [HttpPost("permissions/create")]
    [ProducesResponseType(typeof(BulkOperationResponse<PermissionResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkOperationResponse<PermissionResponse>>> BulkCreatePermissionsAsync(
        [FromBody] BulkCreatePermissionsRequest request)
    {
        try
        {
            _logger.LogInformation("Starting bulk permission creation for {PermissionCount} permissions", request.Permissions.Count);

            var bulkResult = await _bulkOperationService.BulkCreatePermissionsAsync(
                request.Permissions,
                request.ContinueOnError);

            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                "BulkCreatePermissions",
                request.Permissions.Count,
                bulkResult.SuccessCount,
                bulkResult.FailureCount,
                "Bulk permission creation via API"));

            var response = new BulkOperationResponse<PermissionResponse>
            {
                TotalRequested = request.Permissions.Count,
                TotalProcessed = request.Permissions.Count,
                SuccessCount = bulkResult.SuccessCount,
                FailureCount = bulkResult.FailureCount,
                Results = MapBulkPermissionResults(bulkResult.Results),
                Status = DetermineBulkOperationStatus(bulkResult),
                Duration = bulkResult.Duration,
                Warnings = bulkResult.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk permission creation");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during bulk permission creation");
        }
    }

    /// <summary>
    /// Updates expiration dates for multiple permissions
    /// </summary>
    /// <param name="request">Bulk permission expiration update request</param>
    /// <returns>Bulk operation results</returns>
    [HttpPost("permissions/update-expiration")]
    [ProducesResponseType(typeof(BulkOperationResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkOperationResponse>> BulkUpdatePermissionExpirationAsync(
        [FromBody] BulkUpdatePermissionExpirationRequest request)
    {
        try
        {
            _logger.LogInformation("Starting bulk permission expiration update for {PermissionCount} permissions", 
                request.PermissionIds.Count);

            var bulkResult = await _bulkOperationService.BulkUpdatePermissionExpirationAsync(
                request.PermissionIds,
                request.ExpirationDate,
                request.ContinueOnError);

            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                "BulkUpdatePermissionExpiration",
                request.PermissionIds.Count,
                bulkResult.SuccessCount,
                bulkResult.FailureCount,
                $"Bulk permission expiration update to {request.ExpirationDate}"));

            var response = new BulkOperationResponse
            {
                TotalRequested = request.PermissionIds.Count,
                TotalProcessed = request.PermissionIds.Count,
                SuccessCount = bulkResult.SuccessCount,
                FailureCount = bulkResult.FailureCount,
                Results = bulkResult.Results.Select(MapBulkOperationResult).ToList(),
                Status = DetermineBulkOperationStatus(bulkResult),
                Duration = bulkResult.Duration,
                Warnings = bulkResult.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk permission expiration update");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during bulk permission expiration update");
        }
    }

    #endregion

    #region Group Bulk Operations

    /// <summary>
    /// Adds multiple users to groups in a single operation
    /// </summary>
    /// <param name="request">Bulk group membership request</param>
    /// <returns>Bulk operation results</returns>
    [HttpPost("groups/add-members")]
    [ProducesResponseType(typeof(BulkOperationResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkOperationResponse>> BulkAddUsersToGroupsAsync(
        [FromBody] BulkGroupMembershipRequest request)
    {
        try
        {
            _logger.LogInformation("Starting bulk group membership: {UserCount} users to {GroupCount} groups",
                request.UserIds.Count, request.GroupIds.Count);

            var bulkResult = await _bulkOperationService.BulkAddUsersToGroupsAsync(
                request.UserIds,
                request.GroupIds,
                request.ContinueOnError);

            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                "BulkAddUsersToGroups",
                request.UserIds.Count * request.GroupIds.Count,
                bulkResult.SuccessCount,
                bulkResult.FailureCount,
                "Bulk group membership assignment"));

            var response = new BulkOperationResponse
            {
                TotalRequested = request.UserIds.Count * request.GroupIds.Count,
                TotalProcessed = request.UserIds.Count * request.GroupIds.Count,
                SuccessCount = bulkResult.SuccessCount,
                FailureCount = bulkResult.FailureCount,
                Results = bulkResult.Results.Select(MapBulkOperationResult).ToList(),
                Status = DetermineBulkOperationStatus(bulkResult),
                Duration = bulkResult.Duration,
                Warnings = bulkResult.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk group membership assignment");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during bulk group membership assignment");
        }
    }

    #endregion

    #region Import/Export Operations

    /// <summary>
    /// Imports entities from CSV file
    /// </summary>
    /// <param name="file">CSV file containing entity data</param>
    /// <param name="entityType">Type of entities to import (User, Role, Permission)</param>
    /// <param name="continueOnError">Whether to continue processing when errors occur</param>
    /// <returns>Import operation results</returns>
    [HttpPost("import")]
    [ProducesResponseType(typeof(BulkImportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BulkImportResponse>> ImportEntitiesAsync(
        IFormFile file,
        [FromQuery] string entityType,
        [FromQuery] bool continueOnError = true)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided or file is empty");
            }

            if (!IsValidEntityType(entityType))
            {
                return BadRequest($"Invalid entity type: {entityType}. Supported types: User, Role, Permission, Group");
            }

            _logger.LogInformation("Starting import of {EntityType} entities from file: {FileName}", 
                entityType, file.FileName);

            using var stream = file.OpenReadStream();
            var importResult = await _bulkOperationService.ImportEntitiesAsync(
                stream,
                entityType,
                file.ContentType,
                continueOnError);

            await _eventPublisher.PublishAsync(new BulkOperationCompletedEvent(
                $"Import{entityType}s",
                importResult.TotalRecords,
                importResult.SuccessCount,
                importResult.FailureCount,
                $"Import from file: {file.FileName}"));

            var response = new BulkImportResponse
            {
                FileName = file.FileName,
                EntityType = entityType,
                TotalRecords = importResult.TotalRecords,
                SuccessCount = importResult.SuccessCount,
                FailureCount = importResult.FailureCount,
                ValidationErrors = importResult.ValidationErrors,
                ProcessingErrors = importResult.ProcessingErrors,
                ImportedIds = importResult.ImportedIds,
                Status = importResult.SuccessCount == importResult.TotalRecords ? "Completed" : 
                        importResult.SuccessCount > 0 ? "PartiallyCompleted" : "Failed",
                Duration = importResult.Duration,
                Summary = GenerateImportSummary(importResult)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during entity import");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during entity import");
        }
    }

    /// <summary>
    /// Exports entities to CSV format
    /// </summary>
    /// <param name="request">Export request parameters</param>
    /// <returns>CSV file containing exported entities</returns>
    [HttpPost("export")]
    [ProducesResponseType(typeof(FileResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> ExportEntitiesAsync([FromBody] BulkExportRequest request)
    {
        try
        {
            _logger.LogInformation("Starting export of {EntityType} entities", request.EntityType);

            var exportData = await _bulkOperationService.ExportEntitiesAsync(
                request.EntityType,
                request.IncludeInactive,
                request.Filters);

            var csvData = GenerateCsvData(exportData, request.EntityType);
            var fileName = $"{request.EntityType.ToLower()}_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            await _eventPublisher.PublishAsync(new EntityExportedEvent(
                request.EntityType,
                exportData.Count(),
                "CSV export via API"));

            return File(csvData, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during entity export");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during entity export");
        }
    }

    #endregion

    #region Operation Status and Monitoring

    /// <summary>
    /// Gets the status of a bulk operation
    /// </summary>
    /// <param name="operationId">Bulk operation ID</param>
    /// <returns>Operation status</returns>
    [HttpGet("operations/{operationId}/status")]
    [ProducesResponseType(typeof(BulkOperationStatusResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<BulkOperationStatusResponse>> GetBulkOperationStatusAsync(string operationId)
    {
        try
        {
            var status = await _bulkOperationService.GetOperationStatusAsync(operationId);
            if (status == null)
            {
                return NotFound($"Bulk operation {operationId} not found");
            }

            var response = new BulkOperationStatusResponse
            {
                OperationId = operationId,
                Status = status.Status,
                Progress = status.Progress,
                StartTime = status.StartTime,
                EndTime = status.EndTime,
                Duration = status.Duration,
                TotalItems = status.TotalItems,
                ProcessedItems = status.ProcessedItems,
                SuccessCount = status.SuccessCount,
                FailureCount = status.FailureCount,
                CurrentOperation = status.CurrentOperation,
                ErrorMessages = status.ErrorMessages,
                Warnings = status.Warnings
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bulk operation status for {OperationId}", operationId);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving operation status");
        }
    }

    /// <summary>
    /// Gets recent bulk operations
    /// </summary>
    /// <param name="limit">Maximum number of operations to return</param>
    /// <returns>List of recent bulk operations</returns>
    [HttpGet("operations/recent")]
    [ProducesResponseType(typeof(IEnumerable<BulkOperationSummaryResponse>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<BulkOperationSummaryResponse>>> GetRecentBulkOperationsAsync(
        [FromQuery] int limit = 20)
    {
        try
        {
            var operations = await _bulkOperationService.GetRecentOperationsAsync(limit);
            
            var response = operations.Select(op => new BulkOperationSummaryResponse
            {
                OperationId = op.OperationId,
                OperationType = op.OperationType,
                Status = op.Status,
                StartTime = op.StartTime,
                EndTime = op.EndTime,
                Duration = op.Duration,
                TotalItems = op.TotalItems,
                SuccessCount = op.SuccessCount,
                FailureCount = op.FailureCount,
                InitiatedBy = op.InitiatedBy
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent bulk operations");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving recent operations");
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<Domain.Validation.ValidationResult> ValidateCreateUserRequestAsync(CreateUserRequest request)
    {
        // This would integrate with the actual validation service
        // For now, return a simple validation result
        var validationResult = new Domain.Validation.ValidationResult();
        
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            validationResult.AddError("UserName is required", "UserName");
        }
        
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            validationResult.AddError("Email is required", "Email");
        }

        return validationResult;
    }

    private List<BulkOperationResult<UserResponse>> MapBulkUserResults(
        IEnumerable<BulkOperationResult<User>> results,
        List<BulkValidationResult<CreateUserRequest>>? validationResults = null)
    {
        return results.Select(r => new BulkOperationResult<UserResponse>
        {
            ItemId = r.ItemId,
            Success = r.Success,
            ErrorMessage = r.ErrorMessage,
            Data = r.Data != null ? MapUserToResponse(r.Data) : null,
            Details = r.Details
        }).ToList();
    }

    private List<BulkOperationResult<RoleResponse>> MapBulkRoleResults(
        IEnumerable<BulkOperationResult<Role>> results)
    {
        return results.Select(r => new BulkOperationResult<RoleResponse>
        {
            ItemId = r.ItemId,
            Success = r.Success,
            ErrorMessage = r.ErrorMessage,
            Data = r.Data != null ? MapRoleToResponse(r.Data) : null,
            Details = r.Details
        }).ToList();
    }

    private List<BulkOperationResult<PermissionResponse>> MapBulkPermissionResults(
        IEnumerable<BulkOperationResult<Permission>> results)
    {
        return results.Select(r => new BulkOperationResult<PermissionResponse>
        {
            ItemId = r.ItemId,
            Success = r.Success,
            ErrorMessage = r.ErrorMessage,
            Data = r.Data != null ? MapPermissionToResponse(r.Data) : null,
            Details = r.Details
        }).ToList();
    }

    private BulkOperationResult MapBulkOperationResult(BulkOperationResult<object> result)
    {
        return new BulkOperationResult
        {
            ItemId = result.ItemId,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            Details = result.Details
        };
    }

    private UserResponse MapUserToResponse(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            UserName = user.Name,
            Email = user.Email ?? "",
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            RoleCount = user.Children?.OfType<Role>().Count() ?? 0,
            GroupCount = user.Parents?.OfType<Group>().Count() ?? 0
        };
    }

    private RoleResponse MapRoleToResponse(Role role)
    {
        return new RoleResponse
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt,
            PermissionCount = role.Permissions?.Count ?? 0,
            UserCount = role.Parents?.OfType<User>().Count() ?? 0
        };
    }

    private PermissionResponse MapPermissionToResponse(Permission permission)
    {
        return new PermissionResponse
        {
            Id = permission.Id,
            EntityId = permission.EntityId,
            ResourceId = permission.ResourceId,
            HttpVerb = permission.HttpVerb,
            Grant = permission.Grant,
            Scheme = permission.Scheme,
            ExpirationDate = permission.ExpirationDate,
            CreatedAt = permission.CreatedAt,
            IsEffective = permission.Grant && !permission.Deny && (permission.ExpirationDate == null || permission.ExpirationDate > DateTime.UtcNow)
        };
    }

    private string DetermineBulkOperationStatus(BulkOperationResult result)
    {
        if (result.FailureCount == 0)
            return "Completed";
        if (result.SuccessCount > 0)
            return "PartiallyCompleted";
        return "Failed";
    }

    private bool IsValidEntityType(string entityType)
    {
        var validTypes = new[] { "User", "Role", "Permission", "Group", "Resource" };
        return validTypes.Contains(entityType, StringComparer.OrdinalIgnoreCase);
    }

    private string GenerateImportSummary(BulkImportResult result)
    {
        return $"Imported {result.SuccessCount} of {result.TotalRecords} records. " +
               $"{result.FailureCount} failed. " +
               $"Processing took {result.Duration.TotalSeconds:F2} seconds.";
    }

    private byte[] GenerateCsvData(IEnumerable<object> data, string entityType)
    {
        // This would generate CSV data based on entity type
        // Implementation would depend on the specific entity structure
        var csv = new System.Text.StringBuilder();
        
        // Add header based on entity type
        csv.AppendLine(GetCsvHeader(entityType));
        
        // Add data rows
        foreach (var item in data)
        {
            csv.AppendLine(ConvertToCsvRow(item, entityType));
        }
        
        return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
    }

    private string GetCsvHeader(string entityType)
    {
        return entityType.ToLower() switch
        {
            "user" => "Id,UserName,Email,IsActive,CreatedAt,UpdatedAt",
            "role" => "Id,Name,Description,IsActive,CreatedAt,UpdatedAt",
            "permission" => "Id,EntityId,ResourceId,HttpVerb,Grant,Deny,Scheme,ExpirationDate",
            "group" => "Id,Name,Description,IsActive,CreatedAt,UpdatedAt",
            _ => "Id,Name,Description"
        };
    }

    private string ConvertToCsvRow(object item, string entityType)
    {
        // Convert item to CSV row based on entity type
        // This is a simplified implementation
        var json = JsonSerializer.Serialize(item);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        
        return entityType.ToLower() switch
        {
            "user" => $"{dict.GetValueOrDefault("Id")},{dict.GetValueOrDefault("UserName")},{dict.GetValueOrDefault("Email")},{dict.GetValueOrDefault("IsActive")},{dict.GetValueOrDefault("CreatedAt")},{dict.GetValueOrDefault("UpdatedAt")}",
            "role" => $"{dict.GetValueOrDefault("Id")},{dict.GetValueOrDefault("Name")},{dict.GetValueOrDefault("Description")},{dict.GetValueOrDefault("IsActive")},{dict.GetValueOrDefault("CreatedAt")},{dict.GetValueOrDefault("UpdatedAt")}",
            _ => $"{dict.GetValueOrDefault("Id")},{dict.GetValueOrDefault("Name")},{dict.GetValueOrDefault("Description")}"
        };
    }

    #endregion
}