using ACS.Service.Domain;
using ACS.Service.Domain.Events;
using ACS.Service.Domain.Specifications;
using ACS.Service.Domain.Validation;
using ACS.Service.Services;
using ACS.WebApi.Models;
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
    private readonly IValidationService _validationService;
    private readonly ISpecificationService _specificationService;
    private readonly IDomainEventPublisher _eventPublisher;
    private readonly ILogger<ResourcesController> _logger;

    public ResourcesController(
        IResourceService resourceService,
        IPermissionEvaluationService permissionService,
        IValidationService validationService,
        ISpecificationService specificationService,
        IDomainEventPublisher eventPublisher,
        ILogger<ResourcesController> logger)
    {
        _resourceService = resourceService;
        _permissionService = permissionService;
        _validationService = validationService;
        _specificationService = specificationService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Gets all resources with optional filtering and pagination
    /// </summary>
    /// <param name="request">Query parameters for filtering and pagination</param>
    /// <returns>Paged list of resources</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ResourceResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<PagedResponse<ResourceResponse>>> GetResourcesAsync(
        [FromQuery] GetResourcesRequest request)
    {
        try
        {
            _logger.LogInformation("Getting resources with filters: ResourceType={ResourceType}, IsActive={IsActive}, Page={Page}",
                request.ResourceType, request.IsActive, request.Page);

            // Build specification based on filters
            var specification = BuildResourceSpecification(request);
            
            // Execute paged query
            var pagedResult = await _specificationService.QueryPagedAsync(specification, request.Page, request.PageSize);
            
            // Convert to response models
            var resourceResponses = pagedResult.Items.Select(MapToResponse).ToList();
            
            var response = new PagedResponse<ResourceResponse>
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
    public async Task<ActionResult<ResourceResponse>> CreateResourceAsync([FromBody] CreateResourceRequest request)
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
            if (!validationResult.IsValid)
            {
                return BadRequest(CreateValidationProblemDetails(validationResult));
            }

            // Check for URI conflicts
            var existingResource = await _resourceService.FindBestMatchingResourceAsync(request.Uri);
            if (existingResource != null && existingResource.Uri.Equals(request.Uri, StringComparison.OrdinalIgnoreCase))
            {
                return Conflict($"A resource with URI '{request.Uri}' already exists");
            }

            // Create the resource
            var createdResource = await _resourceService.CreateResourceAsync(resource);

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
    public async Task<ActionResult<ResourceResponse>> UpdateResourceAsync(int id, [FromBody] UpdateResourceRequest request)
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
            if (!validationResult.IsValid)
            {
                return BadRequest(CreateValidationProblemDetails(validationResult));
            }

            // Update the resource
            var updatedResource = await _resourceService.UpdateResourceAsync(existingResource);

            // Publish domain event
            await _eventPublisher.PublishAsync(new EntityUpdatedEvent(updatedResource, previousResource, "Resource updated via API"));

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
            await _resourceService.DeleteResourceAsync(id);

            // Publish domain event
            await _eventPublisher.PublishAsync(new EntityDeletedEvent(resource, false, "Resource deleted via API"));

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
    /// <param name="basePath">Base path to discover resources from</param>
    /// <param name="includeInactive">Whether to include inactive resources</param>
    /// <returns>List of discovered resources</returns>
    [HttpPost("discover")]
    [ProducesResponseType(typeof(IEnumerable<ResourceResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<IEnumerable<ResourceResponse>>> DiscoverResourcesAsync(
        [FromBody] DiscoverResourcesRequest request)
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
        [FromBody] ValidateUriPatternRequest request)
    {
        try
        {
            _logger.LogInformation("Validating URI pattern: {Pattern}", request.Pattern);

            var result = await _resourceService.ValidateUriPatternAsync(request.Pattern);
            
            var response = new UriPatternValidationResponse
            {
                IsValid = result.IsValid,
                ValidationErrors = result.ValidationErrors.ToList(),
                SuggestedCorrections = result.SuggestedCorrections.ToList(),
                MatchExamples = result.MatchExamples.ToList(),
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
        [FromBody] TestUriPatternRequest request)
    {
        try
        {
            _logger.LogInformation("Testing URI pattern {Pattern} against {UriCount} URIs", 
                request.Pattern, request.TestUris.Count);

            var testResults = new List<UriTestResult>();
            
            foreach (var testUri in request.TestUris)
            {
                var matchResult = await _resourceService.TestUriPatternMatchAsync(request.Pattern, testUri);
                testResults.Add(new UriTestResult
                {
                    TestUri = testUri,
                    IsMatch = matchResult.IsMatch,
                    ExtractedParameters = matchResult.ExtractedParameters,
                    MatchConfidence = matchResult.MatchConfidence
                });
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

            var hierarchy = await _resourceService.GetResourceHierarchyAsync(rootId, maxDepth);
            var response = hierarchy.Select(MapToHierarchyResponse).ToList();
            
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
            var matchingResources = await _resourceService.FindMatchingResourcesAsync(uri);
            
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

    private ISpecification<Resource> BuildResourceSpecification(GetResourcesRequest request)
    {
        var specification = new TrueSpecification<Resource>();

        if (!string.IsNullOrWhiteSpace(request.ResourceType))
        {
            var typeSpec = new Specification<Resource>
            {
                ToExpression = () => r => r.ResourceType == request.ResourceType
            };
            specification = (TrueSpecification<Resource>)specification.And(typeSpec);
        }

        if (request.IsActive.HasValue)
        {
            var activeSpec = new Specification<Resource>
            {
                ToExpression = () => r => r.IsActive == request.IsActive.Value
            };
            specification = (TrueSpecification<Resource>)specification.And(activeSpec);
        }

        if (!string.IsNullOrWhiteSpace(request.UriPattern))
        {
            var uriSpec = new Specification<Resource>
            {
                ToExpression = () => r => r.Uri.Contains(request.UriPattern)
            };
            specification = (TrueSpecification<Resource>)specification.And(uriSpec);
        }

        if (!string.IsNullOrWhiteSpace(request.Version))
        {
            var versionSpec = new Specification<Resource>
            {
                ToExpression = () => r => r.Version == request.Version
            };
            specification = (TrueSpecification<Resource>)specification.And(versionSpec);
        }

        return specification;
    }

    private ResourceResponse MapToResponse(Resource resource)
    {
        return new ResourceResponse
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

    private ResourceHierarchyResponse MapToHierarchyResponse(ResourceHierarchy hierarchy)
    {
        return new ResourceHierarchyResponse
        {
            Resource = MapToResponse(hierarchy.Resource),
            Children = hierarchy.Children.Select(MapToHierarchyResponse).ToList(),
            Depth = hierarchy.Depth,
            HasChildren = hierarchy.Children.Any()
        };
    }

    private ValidationProblemDetails CreateValidationProblemDetails(Domain.Validation.ValidationResult validationResult)
    {
        var problemDetails = new ValidationProblemDetails();
        
        foreach (var error in validationResult.AllErrors)
        {
            var memberNames = error.MemberNames?.ToArray() ?? new[] { "General" };
            foreach (var memberName in memberNames)
            {
                if (!problemDetails.Errors.ContainsKey(memberName))
                {
                    problemDetails.Errors[memberName] = new List<string>();
                }
                ((List<string>)problemDetails.Errors[memberName]).Add(error.ErrorMessage ?? "Validation error");
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
            var matchingResources = await _resourceService.FindMatchingResourcesAsync(uri);
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