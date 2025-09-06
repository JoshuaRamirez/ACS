using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Domain;
using ACS.Service.Services;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;

namespace ACS.VerticalHost.Handlers;

// Resource Command Handlers
public class CreateResourceCommandHandler : ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>
{
    private readonly IResourceService _resourceService;
    private readonly ILogger<CreateResourceCommandHandler> _logger;

    public CreateResourceCommandHandler(IResourceService resourceService, ILogger<CreateResourceCommandHandler> logger)
    {
        _resourceService = resourceService;
        _logger = logger;
    }

    public async Task<ACS.Service.Domain.Resource> HandleAsync(CreateResourceCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(CreateResourceCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { Name = command.Name, UriPattern = command.UriPattern }, correlationId);
        
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(command.Name))
                throw new ArgumentException("Resource name is required");
            if (string.IsNullOrWhiteSpace(command.UriPattern))
                throw new ArgumentException("URI pattern is required");
            if (command.HttpVerbs == null || !command.HttpVerbs.Any())
                throw new ArgumentException("At least one HTTP verb is required");

            // Call resource service to create the resource
            var request = new ACS.Service.Requests.CreateResourceRequest
            {
                Name = command.Name,
                Description = command.Description,
                UriPattern = command.UriPattern,
                HttpVerbs = command.HttpVerbs,
                IsActive = command.IsActive,
                CreatedBy = command.CreatedBy ?? "system"
            };
            
            var response = await _resourceService.CreateAsync(request);
            var resource = response.Resource;

            if (resource == null) 
                throw new InvalidOperationException("Resource creation failed - null resource returned");
            
            LogCommandSuccess(_logger, context, new { ResourceId = resource.Id, Name = resource.Name }, correlationId);
            return resource;
        }
        catch (Exception ex)
        {
            return HandleCommandError<ACS.Service.Domain.Resource>(_logger, ex, context, correlationId);
        }
    }
}

public class UpdateResourceCommandHandler : ICommandHandler<UpdateResourceCommand, ACS.Service.Domain.Resource>
{
    private readonly IResourceService _resourceService;
    private readonly ILogger<UpdateResourceCommandHandler> _logger;

    public UpdateResourceCommandHandler(IResourceService resourceService, ILogger<UpdateResourceCommandHandler> logger)
    {
        _resourceService = resourceService;
        _logger = logger;
    }

    public async Task<ACS.Service.Domain.Resource> HandleAsync(UpdateResourceCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(UpdateResourceCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { ResourceId = command.ResourceId, Name = command.Name }, correlationId);
        
        try
        {
            if (command.ResourceId <= 0)
                throw new ArgumentException("Valid resource ID is required");

            // Call resource service to update
            var updateRequest = new ACS.Service.Requests.UpdateResourceRequest
            {
                ResourceId = command.ResourceId,
                Name = command.Name,
                Description = command.Description,
                UriPattern = command.UriPattern,
                HttpVerbs = command.HttpVerbs,
                IsActive = command.IsActive,
                UpdatedBy = command.UpdatedBy ?? "system"
            };
            
            var updateResponse = await _resourceService.UpdateAsync(updateRequest);
            
            // Get the updated resource
            var getRequest = new ACS.Service.Requests.GetResourceRequest
            {
                ResourceId = command.ResourceId
            };
            
            var getResponse = await _resourceService.GetByIdAsync(getRequest);
            var updatedResource = getResponse.Resource;
            
            if (updatedResource == null) 
                throw new InvalidOperationException($"Updated resource {command.ResourceId} not found");
            
            LogCommandSuccess(_logger, context, new { ResourceId = command.ResourceId, Name = updatedResource.Name }, correlationId);
            return updatedResource;
        }
        catch (Exception ex)
        {
            return HandleCommandError<ACS.Service.Domain.Resource>(_logger, ex, context, correlationId);
        }
    }
}

public class DeleteResourceCommandHandler : ICommandHandler<DeleteResourceCommand, DeleteResourceResult>
{
    private readonly IResourceService _resourceService;
    private readonly ILogger<DeleteResourceCommandHandler> _logger;

    public DeleteResourceCommandHandler(IResourceService resourceService, ILogger<DeleteResourceCommandHandler> logger)
    {
        _resourceService = resourceService;
        _logger = logger;
    }

    public async Task<DeleteResourceResult> HandleAsync(DeleteResourceCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(DeleteResourceCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { ResourceId = command.ResourceId, ForceDelete = command.ForceDelete }, correlationId);
        
        try
        {
            if (command.ResourceId <= 0)
                throw new ArgumentException("Valid resource ID is required");

            // Check for dependencies before deletion
            var dependencyCheck = await _resourceService.CheckDependenciesAsync(command.ResourceId);
            var dependenciesRemoved = new List<string>();

            if (dependencyCheck.HasDependencies && !command.ForceDelete)
            {
                throw new InvalidOperationException($"Resource has dependencies: {string.Join(", ", dependencyCheck.Dependencies)}. Use ForceDelete to override.");
            }

            if (command.ForceDelete && dependencyCheck.HasDependencies)
            {
                dependenciesRemoved = dependencyCheck.Dependencies;
            }

            // Call resource service to delete
            var request = new ACS.Service.Requests.DeleteResourceRequest
            {
                ResourceId = command.ResourceId,
                ForceDelete = command.ForceDelete,
                DeletedBy = command.DeletedBy ?? "system"
            };
            
            await _resourceService.DeleteAsync(request);
            
            var result = new DeleteResourceResult
            {
                Success = true,
                ResourceId = command.ResourceId,
                DeletedAt = DateTime.UtcNow,
                Message = "Resource deleted successfully",
                DependenciesRemoved = dependenciesRemoved
            };
            
            LogCommandSuccess(_logger, context, new { ResourceId = command.ResourceId, DependenciesCount = dependenciesRemoved.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<DeleteResourceResult>(_logger, ex, context, correlationId);
        }
    }
}

// Resource Query Handlers
public class GetResourceQueryHandler : IQueryHandler<GetResourceQuery, ACS.Service.Domain.Resource>
{
    private readonly IResourceService _resourceService;
    private readonly ILogger<GetResourceQueryHandler> _logger;

    public GetResourceQueryHandler(IResourceService resourceService, ILogger<GetResourceQueryHandler> logger)
    {
        _resourceService = resourceService;
        _logger = logger;
    }

    public async Task<ACS.Service.Domain.Resource> HandleAsync(GetResourceQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetResourceQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { ResourceId = query.ResourceId }, correlationId);
        
        try
        {
            if (query.ResourceId <= 0)
                throw new ArgumentException("Valid resource ID is required");

            var request = new ACS.Service.Requests.GetResourceRequest
            {
                ResourceId = query.ResourceId,
                IncludePermissions = query.IncludePermissions,
                IncludeUsage = query.IncludeUsage
            };
            
            var response = await _resourceService.GetByIdAsync(request);
            var resource = response.Resource;
            
            if (resource == null)
            {
                _logger.LogWarning("Resource {ResourceId} not found. CorrelationId: {CorrelationId}", 
                    query.ResourceId, correlationId);
                throw new InvalidOperationException($"Resource with ID {query.ResourceId} not found");
            }
            
            LogQuerySuccess(_logger, context, new { ResourceId = query.ResourceId, Name = resource.Name }, correlationId);
            return resource;
        }
        catch (Exception ex)
        {
            return HandleQueryError<ACS.Service.Domain.Resource>(_logger, ex, context, correlationId);
        }
    }
}

public class GetResourcesQueryHandler : IQueryHandler<GetResourcesQuery, List<ACS.Service.Domain.Resource>>
{
    private readonly IResourceService _resourceService;
    private readonly ILogger<GetResourcesQueryHandler> _logger;

    public GetResourcesQueryHandler(IResourceService resourceService, ILogger<GetResourcesQueryHandler> logger)
    {
        _resourceService = resourceService;
        _logger = logger;
    }

    public async Task<List<ACS.Service.Domain.Resource>> HandleAsync(GetResourcesQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetResourcesQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, 
            new { Page = query.Page, PageSize = query.PageSize, Search = query.Search }, correlationId);
        
        try
        {
            var request = new ACS.Service.Requests.GetResourcesRequest
            {
                Page = query.Page,
                PageSize = query.PageSize,
                Search = query.Search,
                UriPatternFilter = query.UriPatternFilter,
                HttpVerbFilter = query.HttpVerbFilter,
                ActiveOnly = query.ActiveOnly,
                IncludePermissions = query.IncludePermissions,
                SortBy = query.SortBy,
                SortDescending = query.SortDescending
            };
            
            var response = await _resourceService.GetAllAsync(request);
            var resources = response.Resources;
            
            var result = resources.ToList();
            LogQuerySuccess(_logger, context, 
                new { Page = query.Page, Count = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<ACS.Service.Domain.Resource>>(_logger, ex, context, correlationId);
        }
    }
}

public class GetResourcePermissionsQueryHandler : IQueryHandler<GetResourcePermissionsQuery, List<ResourcePermissionInfo>>
{
    private readonly IResourceService _resourceService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<GetResourcePermissionsQueryHandler> _logger;

    public GetResourcePermissionsQueryHandler(
        IResourceService resourceService, 
        IPermissionService permissionService,
        ILogger<GetResourcePermissionsQueryHandler> logger)
    {
        _resourceService = resourceService;
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<List<ResourcePermissionInfo>> HandleAsync(GetResourcePermissionsQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetResourcePermissionsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { ResourceId = query.ResourceId }, correlationId);
        
        try
        {
            if (query.ResourceId <= 0)
                throw new ArgumentException("Valid resource ID is required");

            // Get permissions for the resource
            var request = new ACS.Service.Requests.GetResourcePermissionsRequest
            {
                ResourceId = query.ResourceId,
                IncludeInherited = query.IncludeInherited,
                IncludeEffective = query.IncludeEffective,
                EntityType = query.EntityType
            };
            
            var response = await _permissionService.GetResourcePermissionsAsync(request);
            
            var result = response.Permissions.Select(p => new ResourcePermissionInfo
            {
                PermissionId = p.PermissionId,
                PermissionName = p.PermissionName,
                EntityId = p.EntityId,
                EntityName = p.EntityName,
                EntityType = p.EntityType,
                IsInherited = p.IsInherited,
                InheritedFrom = p.InheritedFrom,
                GrantedAt = p.GrantedAt,
                GrantedBy = p.GrantedBy
            }).ToList();
            
            LogQuerySuccess(_logger, context, 
                new { ResourceId = query.ResourceId, PermissionCount = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<ResourcePermissionInfo>>(_logger, ex, context, correlationId);
        }
    }
}