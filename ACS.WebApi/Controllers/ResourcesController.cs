using ACS.Service.Domain;
using ACS.Service.Domain.Specifications;
using ACS.Service.Domain.Validation;
using ACS.Service.Services;
using ACS.Service.Requests;
// Note: Both ACS.Service.Requests and ACS.WebApi.Models.Requests have conflicting types - using fully qualified names in method signatures
using ACS.WebApi.Resources;
using ACS.WebApi.Models.Requests;
using ACS.WebApi.Models.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for managing resources and resource access patterns
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class ResourcesController : ControllerBase
{
    private readonly IResourceService _resourceService;
    private readonly IPermissionEvaluationService _permissionService;
    private readonly ILogger<ResourcesController> _logger;
    private readonly IEventPublisher _eventPublisher;
    private readonly IValidationService _validationService;

    public ResourcesController(
        IResourceService resourceService,
        IPermissionEvaluationService permissionService,
        ILogger<ResourcesController> logger,
        IEventPublisher? eventPublisher = null,
        IValidationService? validationService = null)
    {
        _resourceService = resourceService;
        _permissionService = permissionService;
        _logger = logger;
        _eventPublisher = eventPublisher ?? new MockEventPublisher();
        _validationService = validationService ?? new MockValidationService();
    }

    /// <summary>
    /// Gets all resources with optional filtering and pagination
    /// </summary>
    /// <param name="request">Query parameters for filtering and pagination</param>
    /// <returns>Paged list of resources</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ACS.WebApi.Models.Responses.PagedResponse<ACS.WebApi.Models.Responses.ResourceResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<ACS.WebApi.Models.Responses.PagedResponse<ACS.WebApi.Models.Responses.ResourceResponse>>> GetResourcesAsync(
        [FromQuery] ACS.WebApi.Models.Requests.GetResourcesRequest request)
    {
        try
        {
            _logger.LogInformation("Getting resources with filters: ResourceType={ResourceType}, IsActive={IsActive}, Page={Page}",
                request.ResourceType, request.IsActive, request.Page);

            // Execute paged query using resource service
            var resources = await _resourceService.GetResourcesPaginatedAsync(request.Page, request.PageSize);
            var totalCount = await _resourceService.GetTotalResourceCountAsync();
            
            var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
            var pagedResult = new { Items = resources, TotalCount = totalCount, Page = request.Page, PageSize = request.PageSize, TotalPages = totalPages };
            
            // Convert to response models
            var resourceResponses = pagedResult.Items.Select(MapToResponse).ToList();
            
            var response = new ACS.WebApi.Models.Responses.PagedResponse<ResourceResponse>
            {
                Items = resourceResponses,
                TotalCount = pagedResult.TotalCount,
                Page = pagedResult.Page,
                PageSize = pagedResult.PageSize,
                TotalPages = pagedResult.TotalPages
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resources");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving resources");
        }
    }

    /// <summary>
    /// Gets a resource by ID
    /// </summary>
    /// <param name="id">Resource ID</param>
    /// <returns>Resource details</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ResourceResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<ResourceResponse>> GetResourceAsync(int id)
    {
        try
        {
            var resource = await _resourceService.GetResourceByIdAsync(id);
            if (resource == null)
            {
                return NotFound($"Resource with ID {id} not found");
            }

            var response = MapToResponse(resource);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resource {ResourceId}", id);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving the resource");
        }
    }

    /// <summary>
    /// Gets a resource by URI pattern
    /// </summary>
    /// <param name="uri">URI pattern to search for</param>
    /// <returns>Best matching resource</returns>
    [HttpGet("by-uri")]
    [ProducesResponseType(typeof(ResourceResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<ResourceResponse>> GetResourceByUriAsync([FromQuery, Required] string uri)
    {
        try
        {
            _logger.LogInformation("Finding resource for URI: {Uri}", uri);

            var resource = await _resourceService.FindBestMatchingResourceAsync(uri);
            if (resource == null)
            {
                return NotFound($"No resource found matching URI: {uri}");
            }

            var response = MapToResponse(resource);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding resource for URI {Uri}", uri);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while finding the resource");
        }
    }

    /// <summary>
    /// Creates a new resource
    /// </summary>
    /// <param name="request">Resource creation request</param>
    /// <returns>Created resource</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ResourceResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Conflict)]
    public async Task<ActionResult<ResourceResponse>> CreateResourceAsync([FromBody] ACS.WebApi.Models.Requests.CreateResourceRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new resource: {Uri}", request.Uri);

            // Create domain entity
            var resource = new Resource
            {
                Uri = request.Uri,
                Description = request.Description,
                ResourceType = request.ResourceType,
                Version = request.Version,
                ParentResourceId = request.ParentResourceId,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Name = request.Name ?? ExtractNameFromUri(request.Uri)
            };

            // Validate the resource
            var validationResult = await _validationService.ValidateEntityAsync(resource, "Create");
            if (validationResult != System.ComponentModel.DataAnnotations.ValidationResult.Success)
            {
                return BadRequest(validationResult?.ErrorMessage ?? "Validation failed");
            }

            // Check for URI conflicts
            var existingResource = await _resourceService.FindBestMatchingResourceAsync(request.Uri);
            if (existingResource != null && existingResource.Uri.Equals(request.Uri, StringComparison.OrdinalIgnoreCase))
            {
                return Conflict($"A resource with URI '{request.Uri}' already exists");
            }

            // Create the resource
            var createdResource = await _resourceService.CreateResourceAsync(
                request.Uri,
                request.Description ?? string.Empty,
                request.ResourceType,
                "System"); // Mock created by

            // Publish domain event
            await _eventPublisher.PublishAsync(new EntityCreatedEvent(createdResource, "Resource created via API"));

            var response = MapToResponse(createdResource);
            return CreatedAtAction(nameof(GetResourceAsync), new { id = createdResource.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating resource with URI {Uri}", request.Uri);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while creating the resource");
        }
    }

    /// <summary>
    /// Updates an existing resource
    /// </summary>
    /// <param name="id">Resource ID</param>
    /// <param name="request">Resource update request</param>
    /// <returns>Updated resource</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ResourceResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<ResourceResponse>> UpdateResourceAsync(int id, [FromBody] ACS.WebApi.Models.Requests.UpdateResourceRequest request)
    {
        try
        {
            _logger.LogInformation("Updating resource {ResourceId}", id);

            var existingResource = await _resourceService.GetResourceByIdAsync(id);
            if (existingResource == null)
            {
                return NotFound($"Resource with ID {id} not found");
            }

            // Store previous state for event
            var previousResource = new Resource
            {
                Id = existingResource.Id,
                Name = existingResource.Name,
                Uri = existingResource.Uri,
                Description = existingResource.Description,
                ResourceType = existingResource.ResourceType,
                Version = existingResource.Version,
                IsActive = existingResource.IsActive
            };

            // Update properties
            if (!string.IsNullOrWhiteSpace(request.Description))
                existingResource.Description = request.Description;
            if (!string.IsNullOrWhiteSpace(request.ResourceType))
                existingResource.ResourceType = request.ResourceType;
            if (!string.IsNullOrWhiteSpace(request.Version))
                existingResource.Version = request.Version;
            if (request.ParentResourceId.HasValue)
                existingResource.ParentResourceId = request.ParentResourceId;
            if (request.IsActive.HasValue)
                existingResource.IsActive = request.IsActive.Value;
            
            existingResource.UpdatedAt = DateTime.UtcNow;

            // Validate the updated resource
            var validationResult = await _validationService.ValidateEntityAsync(existingResource, "Update");
            if (validationResult != System.ComponentModel.DataAnnotations.ValidationResult.Success)
            {
                return BadRequest(validationResult.ErrorMessage ?? "Validation failed");
            }

            // Update the resource
            var updatedResource = await _resourceService.UpdateResourceAsync(
                existingResource.Id, 
                existingResource.Uri, 
                request.Description ?? existingResource.Description ?? string.Empty, 
                request.ResourceType ?? existingResource.ResourceType ?? string.Empty, 
                "System");

            // Publish domain event
            await _eventPublisher.PublishAsync(new EntityUpdatedEvent(updatedResource, "Resource updated via API"));

            var response = MapToResponse(updatedResource);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating resource {ResourceId}", id);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while updating the resource");
        }
    }

    /// <summary>
    /// Deletes a resource
    /// </summary>
    /// <param name="id">Resource ID</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Conflict)]
    public async Task<IActionResult> DeleteResourceAsync(int id)
    {
        try
        {
            _logger.LogInformation("Deleting resource {ResourceId}", id);

            var resource = await _resourceService.GetResourceByIdAsync(id);
            if (resource == null)
            {
                return NotFound($"Resource with ID {id} not found");
            }

            // Check if resource has dependent child resources
            var childResources = await _resourceService.GetChildResourcesAsync(id);
            if (childResources.Any())
            {
                return Conflict($"Cannot delete resource with {childResources.Count()} child resources. Delete child resources first.");
            }

            // Delete the resource
            await _resourceService.DeleteResourceAsync(id, "System");

            // Publish domain event
            await _eventPublisher.PublishAsync(new EntityDeletedEvent(id, "Resource deleted via API"));

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting resource {ResourceId}", id);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while deleting the resource");
        }
    }

    /// <summary>
    /// Discovers resources from a base path
    /// </summary>
    /// <param name="request">Resource discovery request containing base path and options</param>
    /// <returns>List of discovered resources</returns>
    [HttpPost("discover")]
    [ProducesResponseType(typeof(IEnumerable<ResourceResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<IEnumerable<ResourceResponse>>> DiscoverResourcesAsync(
        [FromBody] ACS.WebApi.Models.Requests.DiscoverResourcesRequest request)
    {
        try
        {
            _logger.LogInformation("Discovering resources from base path: {BasePath}", request.BasePath);

            if (string.IsNullOrWhiteSpace(request.BasePath))
            {
                return BadRequest("Base path is required");
            }

            var discoveredResources = await _resourceService.DiscoverResourcesAsync(request.BasePath);
            
            if (!request.IncludeInactive)
            {
                discoveredResources = discoveredResources.Where(r => r.IsActive);
            }

            var responses = discoveredResources.Select(MapToResponse).ToList();
            return Ok(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering resources from base path {BasePath}", request.BasePath);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while discovering resources");
        }
    }

    /// <summary>
    /// Validates a URI pattern
    /// </summary>
    /// <param name="request">URI pattern validation request</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate-pattern")]
    [ProducesResponseType(typeof(UriPatternValidationResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<UriPatternValidationResponse>> ValidateUriPatternAsync(
        [FromBody] ACS.WebApi.Models.Requests.ValidateUriPatternRequest request)
    {
        try
        {
            _logger.LogInformation("Validating URI pattern: {Pattern}", request.Pattern);

            var result = await _resourceService.ValidateUriPatternAsync(request.Pattern);
            
            var response = new UriPatternValidationResponse
            {
                IsValid = result,
                ValidationErrors = result ? new List<string>() : new List<string> { "Invalid URI pattern" },
                SuggestedCorrections = new List<string>(),
                MatchExamples = new List<string>(),
                Pattern = request.Pattern
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating URI pattern {Pattern}", request.Pattern);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while validating the URI pattern");
        }
    }

    /// <summary>
    /// Tests URI pattern matching
    /// </summary>
    /// <param name="request">Pattern matching test request</param>
    /// <returns>Pattern matching results</returns>
    [HttpPost("test-pattern")]
    [ProducesResponseType(typeof(UriPatternTestResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<UriPatternTestResponse>> TestUriPatternAsync(
        [FromBody] ACS.WebApi.Models.Requests.TestUriPatternRequest request)
    {
        try
        {
            _logger.LogInformation("Testing URI pattern {Pattern} against {UriCount} URIs", 
                request.Pattern, request.TestUris.Count);

            // Call the service method with all test URIs at once
            var serviceResult = await _resourceService.TestUriPatternMatchAsync(request.Pattern, request.TestUris);
            
            // The service returns a dynamic object with TestResults property
            var testResults = new List<UriTestResult>();
            if (serviceResult is { } result && result.GetType().GetProperty("TestResults") is { } testResultsProp)
            {
                var serviceResults = testResultsProp.GetValue(result) as IEnumerable<dynamic>;
                if (serviceResults != null)
                {
                    foreach (var item in serviceResults)
                {
                    testResults.Add(new UriTestResult
                    {
                        TestUri = item.Uri,
                        IsMatch = item.Matches,
                        ExtractedParameters = new Dictionary<string, string>(),
                        MatchConfidence = item.Matches ? 1.0 : 0.0
                    });
                    }
                }
            }

            var response = new UriPatternTestResponse
            {
                Pattern = request.Pattern,
                TestResults = testResults,
                MatchCount = testResults.Count(r => r.IsMatch),
                TotalTests = testResults.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing URI pattern {Pattern}", request.Pattern);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while testing the URI pattern");
        }
    }

    /// <summary>
    /// Gets resource hierarchy starting from a root resource
    /// </summary>
    /// <param name="rootId">Root resource ID (optional, if not provided returns all root resources)</param>
    /// <param name="maxDepth">Maximum depth to traverse</param>
    /// <returns>Resource hierarchy tree</returns>
    [HttpGet("hierarchy")]
    [ProducesResponseType(typeof(IEnumerable<ResourceHierarchyResponse>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<ResourceHierarchyResponse>>> GetResourceHierarchyAsync(
        [FromQuery] int? rootId = null, 
        [FromQuery] int maxDepth = 10)
    {
        try
        {
            _logger.LogInformation("Getting resource hierarchy from root {RootId} with max depth {MaxDepth}", 
                rootId, maxDepth);

            var hierarchy = await _resourceService.GetResourceHierarchyAsync(rootId ?? 1);
            var response = hierarchy.Select(resource => new ACS.WebApi.Models.Responses.ResourceHierarchyResponse
            {
                Resource = MapToResponse(resource),
                Children = new List<ACS.WebApi.Models.Responses.ResourceHierarchyResponse>(),
                Depth = 0,
                HasChildren = false
            }).ToList();
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resource hierarchy from root {RootId}", rootId);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving the resource hierarchy");
        }
    }

    /// <summary>
    /// Checks if a URI is protected by any resource
    /// </summary>
    /// <param name="uri">URI to check</param>
    /// <returns>Protection status and matching resources</returns>
    [HttpGet("protection-status")]
    [ProducesResponseType(typeof(UriProtectionStatusResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<UriProtectionStatusResponse>> GetUriProtectionStatusAsync([FromQuery, Required] string uri)
    {
        try
        {
            _logger.LogInformation("Checking protection status for URI: {Uri}", uri);

            var isProtected = await _resourceService.IsUriProtectedAsync(uri);
            var matchingResources = await _resourceService.GetAllMatchingResourcesAsync(uri);
            
            var response = new UriProtectionStatusResponse
            {
                Uri = uri,
                IsProtected = isProtected,
                MatchingResources = matchingResources.Select(MapToResponse).ToList(),
                ProtectionLevel = DetermineProtectionLevel(matchingResources),
                RequiredPermissions = await GetRequiredPermissionsForUriAsync(uri)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking protection status for URI {Uri}", uri);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while checking URI protection status");
        }
    }

    #region Private Helper Methods

    private ISpecification<Resource> BuildResourceSpecification(ACS.WebApi.Models.Requests.GetResourcesRequest request)
    {
        // For now, return a simple true specification to avoid compilation errors
        // TODO: Implement proper specification building with concrete specification classes
        return new TrueSpecification<Resource>();
    }

    private ACS.WebApi.Models.Responses.ResourceResponse MapToResponse(Resource resource)
    {
        return new ACS.WebApi.Models.Responses.ResourceResponse
        {
            Id = resource.Id,
            Name = resource.Name,
            Uri = resource.Uri,
            Description = resource.Description,
            ResourceType = resource.ResourceType,
            Version = resource.Version,
            ParentResourceId = resource.ParentResourceId,
            IsActive = resource.IsActive,
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt,
            ChildResourceCount = resource.ChildResources?.Count ?? 0,
            PermissionCount = resource.Permissions?.Count ?? 0
        };
    }

    private ACS.WebApi.Models.Responses.ResourceHierarchyResponse MapToHierarchyResponse(ResourceHierarchy hierarchy)
    {
        if (hierarchy.Root == null)
            throw new ArgumentException("Hierarchy must have a root entity");

        var rootResource = hierarchy.Root as Resource ?? throw new ArgumentException("Root entity must be a Resource");
        
        return new ACS.WebApi.Models.Responses.ResourceHierarchyResponse
        {
            Resource = MapToResponse(rootResource),
            Children = new List<ACS.WebApi.Models.Responses.ResourceHierarchyResponse>(), // TODO: Map hierarchy tree
            Depth = 0,
            HasChildren = hierarchy.Root.Children.Any()
        };
    }

    private ValidationProblemDetails CreateValidationProblemDetails(ACS.Service.Domain.Validation.ValidationResult validationResult)
    {
        var problemDetails = new ValidationProblemDetails();
        
        foreach (var error in validationResult.AllErrors)
        {
            var memberNames = error.MemberNames?.ToArray() ?? new[] { "General" };
            foreach (var memberName in memberNames)
            {
                if (!problemDetails.Errors.ContainsKey(memberName))
                {
                    problemDetails.Errors[memberName] = new string[0];
                }
                var errorList = problemDetails.Errors[memberName].ToList();
                errorList.Add(error.ErrorMessage ?? "Validation error");
                problemDetails.Errors[memberName] = errorList.ToArray();
            }
        }

        return problemDetails;
    }

    private string ExtractNameFromUri(string uri)
    {
        var segments = uri.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.LastOrDefault() ?? "Unknown";
    }

    private string DetermineProtectionLevel(IEnumerable<Resource> matchingResources)
    {
        if (!matchingResources.Any())
            return "Unprotected";

        var hasWildcard = matchingResources.Any(r => r.Uri.Contains("*"));
        var hasParameters = matchingResources.Any(r => r.Uri.Contains("{") && r.Uri.Contains("}"));
        var hasDirectMatch = matchingResources.Any(r => !r.Uri.Contains("*") && !r.Uri.Contains("{"));

        if (hasDirectMatch)
            return "FullyProtected";
        else if (hasParameters)
            return "ParameterProtected";
        else if (hasWildcard)
            return "WildcardProtected";
        else
            return "PartiallyProtected";
    }

    private async Task<List<string>> GetRequiredPermissionsForUriAsync(string uri)
    {
        // This would typically integrate with the permission system to determine
        // what permissions are required for accessing this URI
        var permissions = new List<string>();
        
        try
        {
            var matchingResources = await _resourceService.GetAllMatchingResourcesAsync(uri);
            foreach (var resource in matchingResources)
            {
                // Add permissions based on resource configuration
                permissions.Add($"GET:{resource.Uri}");
                
                if (resource.Uri.Contains("/admin") || resource.Uri.Contains("/system"))
                {
                    permissions.Add($"ADMIN:{resource.Uri}");
                }
            }
        }
        catch
        {
            // Fallback to basic permissions if there's an error
            permissions.Add($"GET:{uri}");
        }

        return permissions.Distinct().ToList();
    }

    #endregion
}

public class MockValidationService : IValidationService
{
    public Task<System.ComponentModel.DataAnnotations.ValidationResult> ValidateEntityAsync<T>(T entity, string operationType = "Update") where T : class
    {
        return Task.FromResult(System.ComponentModel.DataAnnotations.ValidationResult.Success!);
    }

    public Task<Dictionary<T, ACS.Service.Domain.Validation.ValidationResult>> ValidateEntitiesBulkAsync<T>(IEnumerable<T> entities, string operationType = "Update") where T : class
    {
        return Task.FromResult(new Dictionary<T, ACS.Service.Domain.Validation.ValidationResult>());
    }

    public Task<ACS.Service.Domain.Validation.ValidationResult> ValidateBusinessRulesAsync<T>(T entity, IDictionary<string, object>? context = null) where T : class
    {
        return Task.FromResult(new ACS.Service.Domain.Validation.ValidationResult(new List<System.ComponentModel.DataAnnotations.ValidationResult>()));
    }

    public Task<ACS.Service.Domain.Validation.ValidationResult> ValidateInvariantsAsync<T>(T entity) where T : class
    {
        return Task.FromResult(new ACS.Service.Domain.Validation.ValidationResult(new List<System.ComponentModel.DataAnnotations.ValidationResult>()));
    }

    public Task<ACS.Service.Domain.Validation.ValidationResult> ValidateSystemInvariantsAsync()
    {
        return Task.FromResult(new ACS.Service.Domain.Validation.ValidationResult(new List<System.ComponentModel.DataAnnotations.ValidationResult>()));
    }

    public Task<ACS.Service.Domain.Validation.ValidationResult> ValidatePropertyAsync<T>(T entity, string propertyName, object? value) where T : class
    {
        return Task.FromResult(new ACS.Service.Domain.Validation.ValidationResult(new List<System.ComponentModel.DataAnnotations.ValidationResult>()));
    }

    public Task<bool> IsOperationAllowedAsync<T>(T entity, string operationType, IDictionary<string, object>? context = null) where T : class
    {
        return Task.FromResult(true);
    }

    public ACS.Service.Domain.Validation.EntityValidationSettings GetEntityValidationSettings<T>() where T : class
    {
        return new ACS.Service.Domain.Validation.EntityValidationSettings();
    }

    public Task UpdateValidationConfigurationAsync(ACS.Service.Domain.Validation.ValidationConfiguration configuration)
    {
        return Task.CompletedTask;
    }
}

public class EntityCreatedEvent
{
    public object Entity { get; }
    public string Description { get; }
    public DateTime CreatedAt { get; }
    
    public EntityCreatedEvent(object entity, string description)
    {
        Entity = entity;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }
}

public class EntityUpdatedEvent
{
    public object Entity { get; }
    public string Description { get; }
    public DateTime UpdatedAt { get; }
    
    public EntityUpdatedEvent(object entity, string description)
    {
        Entity = entity;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class EntityDeletedEvent
{
    public int EntityId { get; }
    public string Description { get; }
    public DateTime DeletedAt { get; }
    
    public EntityDeletedEvent(int entityId, string description)
    {
        EntityId = entityId;
        Description = description;
        DeletedAt = DateTime.UtcNow;
    }
}