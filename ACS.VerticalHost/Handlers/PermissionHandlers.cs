using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Domain;
using ACS.Service.Services;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;

namespace ACS.VerticalHost.Handlers;

// Permission Command Handlers
public class GrantPermissionCommandHandler : ICommandHandler<GrantPermissionCommand, PermissionGrantResult>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<GrantPermissionCommandHandler> _logger;

    public GrantPermissionCommandHandler(IPermissionService permissionService, ILogger<GrantPermissionCommandHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<PermissionGrantResult> HandleAsync(GrantPermissionCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GrantPermissionCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            EntityId = command.EntityId, 
            EntityType = command.EntityType, 
            PermissionId = command.PermissionId 
        }, correlationId);
        
        try
        {
            // Validate required fields
            if (command.EntityId <= 0)
                throw new ArgumentException("Valid entity ID is required");
            if (string.IsNullOrWhiteSpace(command.EntityType))
                throw new ArgumentException("Entity type is required");
            if (command.PermissionId <= 0)
                throw new ArgumentException("Valid permission ID is required");

            // Check if permission is already granted
            var existingPermission = await _permissionService.CheckPermissionAsync(
                command.EntityId, command.EntityType, command.PermissionId, command.ResourceId);

            var wasAlreadyGranted = existingPermission.HasPermission && !existingPermission.IsExpired;

            if (!wasAlreadyGranted)
            {
                // Grant the permission
                var request = new ACS.Service.Requests.GrantPermissionRequest
                {
                    EntityId = command.EntityId,
                    EntityType = command.EntityType,
                    PermissionId = command.PermissionId,
                    ResourceId = command.ResourceId,
                    ExpiresAt = command.ExpiresAt,
                    GrantedBy = command.GrantedBy ?? "system",
                    Reason = command.Reason
                };
                
                await _permissionService.GrantPermissionAsync(request);
            }
            
            var result = new PermissionGrantResult
            {
                Success = true,
                EntityId = command.EntityId,
                EntityType = command.EntityType,
                PermissionId = command.PermissionId,
                ResourceId = command.ResourceId,
                GrantedAt = DateTime.UtcNow,
                ExpiresAt = command.ExpiresAt,
                WasAlreadyGranted = wasAlreadyGranted,
                Message = wasAlreadyGranted ? "Permission was already granted" : "Permission granted successfully"
            };
            
            LogCommandSuccess(_logger, context, new 
            { 
                EntityId = command.EntityId, 
                PermissionId = command.PermissionId,
                WasAlreadyGranted = wasAlreadyGranted
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<PermissionGrantResult>(_logger, ex, context, correlationId);
        }
    }
}

public class RevokePermissionCommandHandler : ICommandHandler<RevokePermissionCommand, PermissionRevokeResult>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<RevokePermissionCommandHandler> _logger;

    public RevokePermissionCommandHandler(IPermissionService permissionService, ILogger<RevokePermissionCommandHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<PermissionRevokeResult> HandleAsync(RevokePermissionCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(RevokePermissionCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            EntityId = command.EntityId, 
            EntityType = command.EntityType, 
            PermissionId = command.PermissionId,
            CascadeToChildren = command.CascadeToChildren
        }, correlationId);
        
        try
        {
            // Validate required fields
            if (command.EntityId <= 0)
                throw new ArgumentException("Valid entity ID is required");
            if (string.IsNullOrWhiteSpace(command.EntityType))
                throw new ArgumentException("Entity type is required");
            if (command.PermissionId <= 0)
                throw new ArgumentException("Valid permission ID is required");

            var request = new ACS.Service.Requests.RevokePermissionRequest
            {
                EntityId = command.EntityId,
                EntityType = command.EntityType,
                PermissionId = command.PermissionId,
                ResourceId = command.ResourceId,
                CascadeToChildren = command.CascadeToChildren,
                RevokedBy = command.RevokedBy ?? "system",
                Reason = command.Reason
            };
            
            var response = await _permissionService.RevokePermissionAsync(request);
            
            var result = new PermissionRevokeResult
            {
                Success = true,
                EntityId = command.EntityId,
                EntityType = command.EntityType,
                PermissionId = command.PermissionId,
                ResourceId = command.ResourceId,
                RevokedAt = DateTime.UtcNow,
                Message = "Permission revoked successfully",
                CascadeRevokedEntities = response.AffectedEntityIds.ToList()
            };
            
            LogCommandSuccess(_logger, context, new 
            { 
                EntityId = command.EntityId, 
                PermissionId = command.PermissionId,
                CascadeCount = result.CascadeRevokedEntities.Count
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<PermissionRevokeResult>(_logger, ex, context, correlationId);
        }
    }
}

public class ValidatePermissionStructureCommandHandler : ICommandHandler<ValidatePermissionStructureCommand, PermissionValidationResult>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ValidatePermissionStructureCommandHandler> _logger;

    public ValidatePermissionStructureCommandHandler(IPermissionService permissionService, ILogger<ValidatePermissionStructureCommandHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<PermissionValidationResult> HandleAsync(ValidatePermissionStructureCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(ValidatePermissionStructureCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            EntityId = command.EntityId, 
            EntityType = command.EntityType,
            FixInconsistencies = command.FixInconsistencies
        }, correlationId);
        
        try
        {
            var request = new ACS.Service.Requests.ValidatePermissionStructureRequest
            {
                EntityId = command.EntityId,
                EntityType = command.EntityType,
                FixInconsistencies = command.FixInconsistencies,
                ValidatedBy = command.ValidatedBy ?? "system"
            };
            
            var response = await _permissionService.ValidatePermissionStructureAsync(request);
            
            var result = new PermissionValidationResult
            {
                IsValid = response.IsValid,
                ValidatedAt = DateTime.UtcNow,
                Inconsistencies = response.Inconsistencies.Select(i => new PermissionInconsistency
                {
                    Type = i.Type,
                    Description = i.Description,
                    EntityId = i.EntityId,
                    EntityType = i.EntityType,
                    PermissionId = i.PermissionId,
                    ResourceId = i.ResourceId,
                    Severity = i.Severity,
                    CanAutoFix = i.CanAutoFix,
                    RecommendedAction = i.RecommendedAction
                }).ToList(),
                FixedInconsistencies = response.FixedInconsistencies.Select(i => new PermissionInconsistency
                {
                    Type = i.Type,
                    Description = i.Description,
                    EntityId = i.EntityId,
                    EntityType = i.EntityType,
                    PermissionId = i.PermissionId,
                    ResourceId = i.ResourceId,
                    Severity = i.Severity,
                    CanAutoFix = i.CanAutoFix,
                    RecommendedAction = i.RecommendedAction
                }).ToList(),
                Message = $"Validation completed. Found {response.Inconsistencies.Count} inconsistencies, fixed {response.FixedInconsistencies.Count}"
            };
            
            LogCommandSuccess(_logger, context, new 
            { 
                IsValid = result.IsValid, 
                InconsistencyCount = result.Inconsistencies.Count,
                FixedCount = result.FixedInconsistencies.Count
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<PermissionValidationResult>(_logger, ex, context, correlationId);
        }
    }
}

// Permission Query Handlers
public class CheckPermissionQueryHandler : IQueryHandler<CheckPermissionQuery, PermissionCheckResult>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<CheckPermissionQueryHandler> _logger;

    public CheckPermissionQueryHandler(IPermissionService permissionService, ILogger<CheckPermissionQueryHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<PermissionCheckResult> HandleAsync(CheckPermissionQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(CheckPermissionQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            EntityId = query.EntityId, 
            EntityType = query.EntityType, 
            PermissionId = query.PermissionId 
        }, correlationId);
        
        try
        {
            if (query.EntityId <= 0)
                throw new ArgumentException("Valid entity ID is required");
            if (string.IsNullOrWhiteSpace(query.EntityType))
                throw new ArgumentException("Entity type is required");
            if (query.PermissionId <= 0)
                throw new ArgumentException("Valid permission ID is required");

            var request = new ACS.Service.Requests.CheckPermissionRequest
            {
                EntityId = query.EntityId,
                EntityType = query.EntityType,
                PermissionId = query.PermissionId,
                ResourceId = query.ResourceId,
                IncludeInheritance = query.IncludeInheritance,
                IncludeExpired = query.IncludeExpired,
                CheckAt = query.CheckAt ?? DateTime.UtcNow
            };
            
            var response = await _permissionService.CheckPermissionWithDetailsAsync(request);
            
            var result = new PermissionCheckResult
            {
                HasPermission = response.HasPermission,
                EntityId = query.EntityId,
                EntityType = query.EntityType,
                PermissionId = query.PermissionId,
                ResourceId = query.ResourceId,
                IsInherited = response.IsInherited,
                InheritedFrom = response.InheritedFrom,
                IsExpired = response.IsExpired,
                ExpiresAt = response.ExpiresAt,
                CheckedAt = DateTime.UtcNow,
                InheritanceChain = response.InheritanceChain.ToList()
            };
            
            LogQuerySuccess(_logger, context, new 
            { 
                EntityId = query.EntityId, 
                PermissionId = query.PermissionId,
                HasPermission = result.HasPermission,
                IsInherited = result.IsInherited
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<PermissionCheckResult>(_logger, ex, context, correlationId);
        }
    }
}

public class GetEntityPermissionsQueryHandler : IQueryHandler<GetEntityPermissionsQuery, List<EntityPermissionInfo>>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<GetEntityPermissionsQueryHandler> _logger;

    public GetEntityPermissionsQueryHandler(IPermissionService permissionService, ILogger<GetEntityPermissionsQueryHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<List<EntityPermissionInfo>> HandleAsync(GetEntityPermissionsQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetEntityPermissionsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            EntityId = query.EntityId, 
            EntityType = query.EntityType,
            Page = query.Page,
            PageSize = query.PageSize
        }, correlationId);
        
        try
        {
            if (query.EntityId <= 0)
                throw new ArgumentException("Valid entity ID is required");
            if (string.IsNullOrWhiteSpace(query.EntityType))
                throw new ArgumentException("Entity type is required");

            var request = new ACS.Service.Requests.GetEntityPermissionsRequest
            {
                EntityId = query.EntityId,
                EntityType = query.EntityType,
                IncludeInherited = query.IncludeInherited,
                IncludeExpired = query.IncludeExpired,
                ResourceId = query.ResourceId,
                PermissionFilter = query.PermissionFilter,
                Page = query.Page,
                PageSize = query.PageSize
            };
            
            var response = await _permissionService.GetEntityPermissionsAsync(request);
            
            var result = response.Permissions.Select(p => new EntityPermissionInfo
            {
                PermissionId = p.PermissionId,
                PermissionName = p.PermissionName,
                PermissionDescription = p.PermissionDescription,
                ResourceId = p.ResourceId,
                ResourceName = p.ResourceName,
                IsInherited = p.IsInherited,
                InheritedFrom = p.InheritedFrom,
                GrantedAt = p.GrantedAt,
                GrantedBy = p.GrantedBy,
                ExpiresAt = p.ExpiresAt,
                IsExpired = p.IsExpired
            }).ToList();
            
            LogQuerySuccess(_logger, context, new 
            { 
                EntityId = query.EntityId, 
                EntityType = query.EntityType,
                PermissionCount = result.Count
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<EntityPermissionInfo>>(_logger, ex, context, correlationId);
        }
    }
}

public class GetPermissionUsageQueryHandler : IQueryHandler<GetPermissionUsageQuery, List<PermissionUsageInfo>>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<GetPermissionUsageQueryHandler> _logger;

    public GetPermissionUsageQueryHandler(IPermissionService permissionService, ILogger<GetPermissionUsageQueryHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<List<PermissionUsageInfo>> HandleAsync(GetPermissionUsageQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetPermissionUsageQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            PermissionId = query.PermissionId,
            ResourceId = query.ResourceId,
            Page = query.Page,
            PageSize = query.PageSize
        }, correlationId);
        
        try
        {
            if (query.PermissionId <= 0)
                throw new ArgumentException("Valid permission ID is required");

            var request = new ACS.Service.Requests.GetPermissionUsageRequest
            {
                PermissionId = query.PermissionId,
                ResourceId = query.ResourceId,
                IncludeIndirect = query.IncludeIndirect,
                EntityTypeFilter = query.EntityTypeFilter,
                Page = query.Page,
                PageSize = query.PageSize
            };
            
            var response = await _permissionService.GetPermissionUsageAsync(request);
            
            var result = response.Usage.Select(u => new PermissionUsageInfo
            {
                EntityId = u.EntityId,
                EntityName = u.EntityName,
                EntityType = u.EntityType,
                ResourceId = u.ResourceId,
                ResourceName = u.ResourceName,
                IsDirect = u.IsDirect,
                GrantedThrough = u.GrantedThrough,
                GrantedAt = u.GrantedAt,
                GrantedBy = u.GrantedBy,
                ExpiresAt = u.ExpiresAt,
                IsExpired = u.IsExpired
            }).ToList();
            
            LogQuerySuccess(_logger, context, new 
            { 
                PermissionId = query.PermissionId,
                UsageCount = result.Count
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<PermissionUsageInfo>>(_logger, ex, context, correlationId);
        }
    }
}