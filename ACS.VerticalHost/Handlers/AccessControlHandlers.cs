using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Domain;
using ACS.Service.Services;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;

namespace ACS.VerticalHost.Handlers;

// Advanced Access Control Command Handlers
public class BulkPermissionUpdateCommandHandler : ICommandHandler<BulkPermissionUpdateCommand, BulkPermissionUpdateResult>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<BulkPermissionUpdateCommandHandler> _logger;

    public BulkPermissionUpdateCommandHandler(IPermissionService permissionService, ILogger<BulkPermissionUpdateCommandHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<BulkPermissionUpdateResult> HandleAsync(BulkPermissionUpdateCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(BulkPermissionUpdateCommandHandler), nameof(HandleAsync));
        var startTime = DateTime.UtcNow;
        
        LogOperationStart(_logger, context, new 
        { 
            OperationCount = command.Operations.Count,
            ValidateBeforeExecution = command.ValidateBeforeExecution,
            ExecuteInTransaction = command.ExecuteInTransaction,
            StopOnFirstError = command.StopOnFirstError
        }, correlationId);
        
        try
        {
            if (!command.Operations.Any())
                throw new ArgumentException("At least one operation is required");

            // Validate all operations if requested
            if (command.ValidateBeforeExecution)
            {
                foreach (var operation in command.Operations)
                {
                    ValidateBulkOperation(operation);
                }
            }

            var request = new ACS.Service.Requests.BulkPermissionUpdateRequest
            {
                Operations = command.Operations.Select(op => new ACS.Service.Requests.BulkPermissionOperationRequest
                {
                    OperationType = op.OperationType,
                    EntityId = op.EntityId,
                    EntityType = op.EntityType,
                    PermissionId = op.PermissionId,
                    ResourceId = op.ResourceId,
                    ExpiresAt = op.ExpiresAt,
                    Metadata = op.Metadata,
                    Reason = op.Reason
                }).ToList(),
                ValidateBeforeExecution = command.ValidateBeforeExecution,
                StopOnFirstError = command.StopOnFirstError,
                ExecuteInTransaction = command.ExecuteInTransaction,
                RequestedBy = command.RequestedBy ?? "system",
                Reason = command.Reason
            };
            
            var response = await _permissionService.BulkUpdatePermissionsAsync(request);
            var endTime = DateTime.UtcNow;
            
            var result = new BulkPermissionUpdateResult
            {
                Success = response.Success,
                TotalOperations = command.Operations.Count,
                SuccessfulOperations = response.SuccessfulOperations,
                FailedOperations = response.FailedOperations,
                ExecutedAt = startTime,
                Duration = endTime - startTime,
                ExecutedInTransaction = command.ExecuteInTransaction,
                OperationResults = response.OperationResults.Select(r => new BulkOperationResult
                {
                    Index = r.Index,
                    Operation = command.Operations[r.Index],
                    Success = r.Success,
                    ErrorMessage = r.ErrorMessage,
                    CompletedAt = r.CompletedAt
                }).ToList(),
                Message = response.Success ? 
                    $"Bulk operation completed successfully. {response.SuccessfulOperations}/{command.Operations.Count} operations succeeded." :
                    $"Bulk operation completed with errors. {response.SuccessfulOperations}/{command.Operations.Count} operations succeeded."
            };
            
            LogCommandSuccess(_logger, context, new 
            { 
                TotalOperations = result.TotalOperations,
                SuccessfulOperations = result.SuccessfulOperations,
                FailedOperations = result.FailedOperations,
                Duration = result.Duration
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<BulkPermissionUpdateResult>(_logger, ex, context, correlationId);
        }
    }

    private static void ValidateBulkOperation(BulkPermissionOperation operation)
    {
        if (operation.EntityId <= 0)
            throw new ArgumentException($"Invalid entity ID: {operation.EntityId}");
        if (string.IsNullOrWhiteSpace(operation.EntityType))
            throw new ArgumentException("Entity type is required");
        if (operation.PermissionId <= 0)
            throw new ArgumentException($"Invalid permission ID: {operation.PermissionId}");
        if (!IsValidOperationType(operation.OperationType))
            throw new ArgumentException($"Invalid operation type: {operation.OperationType}");
    }

    private static bool IsValidOperationType(string operationType)
    {
        return operationType is "Grant" or "Revoke" or "Update";
    }
}

public class AccessViolationHandlerCommandHandler : ICommandHandler<AccessViolationHandlerCommand, AccessViolationHandlerResult>
{
    private readonly ISecurityService _securityService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AccessViolationHandlerCommandHandler> _logger;

    public AccessViolationHandlerCommandHandler(
        ISecurityService securityService, 
        IAuditService auditService,
        ILogger<AccessViolationHandlerCommandHandler> logger)
    {
        _securityService = securityService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AccessViolationHandlerResult> HandleAsync(AccessViolationHandlerCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(AccessViolationHandlerCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            ViolationType = command.ViolationType,
            UserId = command.UserId,
            Severity = command.Severity,
            Action = command.Action
        }, correlationId);
        
        try
        {
            if (string.IsNullOrWhiteSpace(command.ViolationType))
                throw new ArgumentException("Violation type is required");
            if (string.IsNullOrWhiteSpace(command.Action))
                throw new ArgumentException("Action is required");

            var violationId = Guid.NewGuid().ToString();
            var actionsExecuted = new List<string>();
            var userBlocked = false;
            var alertGenerated = false;
            DateTime? blockUntil = null;

            // Record the violation in audit log
            await _auditService.RecordEventAsync(new ACS.Service.Requests.RecordAuditEventRequest
            {
                EventType = "AccessViolation",
                EventCategory = "Security",
                UserId = command.UserId,
                ResourceId = command.ResourceId,
                Action = "ViolationDetected",
                Details = $"Violation Type: {command.ViolationType}, Severity: {command.Severity}",
                Severity = command.Severity,
                IpAddress = command.IpAddress,
                UserAgent = command.UserAgent,
                SessionId = command.SessionId,
                EventTimestamp = command.OccurredAt,
                Metadata = command.Context,
                CorrelationId = correlationId
            });
            actionsExecuted.Add("AuditLogged");

            // Handle different violation actions
            switch (command.Action.ToLower())
            {
                case "block":
                    if (command.UserId.HasValue)
                    {
                        blockUntil = await _securityService.BlockUserAsync(
                            command.UserId.Value, command.Severity, violationId);
                        userBlocked = true;
                        actionsExecuted.Add("UserBlocked");
                    }
                    break;

                case "quarantine":
                    if (command.UserId.HasValue)
                    {
                        await _securityService.QuarantineUserAsync(
                            command.UserId.Value, command.ViolationType, violationId);
                        userBlocked = true;
                        actionsExecuted.Add("UserQuarantined");
                    }
                    break;

                case "alert":
                    await _securityService.GenerateSecurityAlertAsync(
                        command.ViolationType, command.Severity, command.Context, violationId);
                    alertGenerated = true;
                    actionsExecuted.Add("AlertGenerated");
                    break;

                case "log":
                    // Already logged above
                    break;

                default:
                    throw new ArgumentException($"Unknown action: {command.Action}");
            }

            var result = new AccessViolationHandlerResult
            {
                Success = true,
                ViolationId = violationId,
                ViolationType = command.ViolationType,
                ActionTaken = command.Action,
                HandledAt = DateTime.UtcNow,
                ActionsExecuted = actionsExecuted,
                UserBlocked = userBlocked,
                AlertGenerated = alertGenerated,
                BlockUntil = blockUntil,
                Message = $"Access violation handled successfully. Actions taken: {string.Join(", ", actionsExecuted)}"
            };
            
            LogCommandSuccess(_logger, context, new 
            { 
                ViolationId = violationId,
                ViolationType = command.ViolationType,
                ActionsExecuted = actionsExecuted.Count,
                UserBlocked = userBlocked,
                AlertGenerated = alertGenerated
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<AccessViolationHandlerResult>(_logger, ex, context, correlationId);
        }
    }
}

// Advanced Access Control Query Handlers
public class EvaluateComplexPermissionQueryHandler : IQueryHandler<EvaluateComplexPermissionQuery, ComplexPermissionEvaluationResult>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<EvaluateComplexPermissionQueryHandler> _logger;

    public EvaluateComplexPermissionQueryHandler(IPermissionService permissionService, ILogger<EvaluateComplexPermissionQueryHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<ComplexPermissionEvaluationResult> HandleAsync(EvaluateComplexPermissionQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(EvaluateComplexPermissionQueryHandler), nameof(HandleAsync));
        var startTime = DateTime.UtcNow;
        
        LogOperationStart(_logger, context, new 
        { 
            UserId = query.UserId,
            ResourceId = query.ResourceId,
            Action = query.Action,
            ConditionCount = query.Conditions.Count,
            IncludeReasoningTrace = query.IncludeReasoningTrace
        }, correlationId);
        
        try
        {
            if (query.UserId <= 0)
                throw new ArgumentException("Valid user ID is required");
            if (query.ResourceId <= 0)
                throw new ArgumentException("Valid resource ID is required");
            if (string.IsNullOrWhiteSpace(query.Action))
                throw new ArgumentException("Action is required");

            var request = new ACS.Service.Requests.EvaluateComplexPermissionRequest
            {
                UserId = query.UserId,
                ResourceId = query.ResourceId,
                Action = query.Action,
                Context = query.Context,
                Conditions = query.Conditions.Select(c => new ACS.Service.Requests.PermissionConditionRequest
                {
                    Type = c.Type,
                    Operator = c.Operator,
                    Value = c.Value,
                    Parameters = c.Parameters
                }).ToList(),
                IncludeReasoningTrace = query.IncludeReasoningTrace,
                EvaluateAt = query.EvaluateAt ?? DateTime.UtcNow
            };
            
            var response = await _permissionService.EvaluateComplexPermissionAsync(request);
            var endTime = DateTime.UtcNow;
            
            var result = new ComplexPermissionEvaluationResult
            {
                HasAccess = response.HasAccess,
                DecisionReason = response.DecisionReason,
                ReasoningTrace = response.ReasoningTrace.Select(step => new PermissionEvaluationStep
                {
                    Step = step.Step,
                    Description = step.Description,
                    DecisionPoint = step.DecisionPoint,
                    Passed = step.Passed,
                    Reason = step.Reason,
                    Context = step.Context
                }).ToList(),
                ConditionResults = response.ConditionResults.Select(cr => new ConditionEvaluationResult
                {
                    Condition = new PermissionCondition
                    {
                        Type = cr.Condition.Type,
                        Operator = cr.Condition.Operator,
                        Value = cr.Condition.Value,
                        Parameters = cr.Condition.Parameters
                    },
                    Satisfied = cr.Satisfied,
                    Reason = cr.Reason,
                    ActualValue = cr.ActualValue,
                    EvaluatedAt = cr.EvaluatedAt
                }).ToList(),
                Context = new PermissionDecisionContext
                {
                    UserId = response.Context.UserId,
                    UserName = response.Context.UserName,
                    ResourceId = response.Context.ResourceId,
                    ResourceName = response.Context.ResourceName,
                    Action = response.Context.Action,
                    IpAddress = response.Context.IpAddress,
                    UserAgent = response.Context.UserAgent,
                    RequestTimestamp = response.Context.RequestTimestamp,
                    AdditionalContext = response.Context.AdditionalContext
                },
                EvaluatedAt = endTime,
                EvaluationDuration = endTime - startTime,
                ConflictResolution = response.ConflictResolution
            };
            
            LogQuerySuccess(_logger, context, new 
            { 
                UserId = query.UserId,
                ResourceId = query.ResourceId,
                HasAccess = result.HasAccess,
                ConditionCount = result.ConditionResults.Count,
                EvaluationDuration = result.EvaluationDuration.TotalMilliseconds
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<ComplexPermissionEvaluationResult>(_logger, ex, context, correlationId);
        }
    }
}

public class GetEffectivePermissionsQueryHandler : IQueryHandler<GetEffectivePermissionsQuery, EffectivePermissionsResult>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<GetEffectivePermissionsQueryHandler> _logger;

    public GetEffectivePermissionsQueryHandler(IPermissionService permissionService, ILogger<GetEffectivePermissionsQueryHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<EffectivePermissionsResult> HandleAsync(GetEffectivePermissionsQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetEffectivePermissionsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            EntityId = query.EntityId,
            EntityType = query.EntityType,
            ResourceCount = query.ResourceIds?.Count ?? 0,
            IncludeInheritanceChain = query.IncludeInheritanceChain,
            ResolveConflicts = query.ResolveConflicts
        }, correlationId);
        
        try
        {
            if (query.EntityId <= 0)
                throw new ArgumentException("Valid entity ID is required");
            if (string.IsNullOrWhiteSpace(query.EntityType))
                throw new ArgumentException("Entity type is required");

            var request = new ACS.Service.Requests.GetEffectivePermissionsRequest
            {
                EntityId = query.EntityId,
                EntityType = query.EntityType,
                ResourceIds = query.ResourceIds,
                IncludeInheritanceChain = query.IncludeInheritanceChain,
                IncludeExpiredPermissions = query.IncludeExpiredPermissions,
                ResolveConflicts = query.ResolveConflicts,
                EffectiveAt = query.EffectiveAt ?? DateTime.UtcNow,
                PermissionScope = query.PermissionScope
            };
            
            var response = await _permissionService.GetEffectivePermissionsAsync(request);
            
            var result = new EffectivePermissionsResult
            {
                EntityId = query.EntityId,
                EntityType = query.EntityType,
                Permissions = response.Permissions.Select(p => new EffectivePermission
                {
                    PermissionId = p.PermissionId,
                    PermissionName = p.PermissionName,
                    ResourceId = p.ResourceId,
                    ResourceName = p.ResourceName,
                    Source = p.Source,
                    InheritedFrom = p.InheritedFrom,
                    InheritanceChain = p.InheritanceChain.ToList(),
                    GrantedAt = p.GrantedAt,
                    ExpiresAt = p.ExpiresAt,
                    IsActive = p.IsActive,
                    HasConflicts = p.HasConflicts,
                    Metadata = p.Metadata
                }).ToList(),
                Conflicts = response.Conflicts.Select(c => new PermissionConflict
                {
                    ConflictType = c.ConflictType,
                    Description = c.Description,
                    ConflictingPermissions = c.ConflictingPermissions.ToList(),
                    Resolution = c.Resolution,
                    Severity = c.Severity
                }).ToList(),
                CalculatedAt = DateTime.UtcNow,
                EffectiveAt = query.EffectiveAt ?? DateTime.UtcNow,
                Summary = new PermissionsSummary
                {
                    TotalPermissions = response.Summary.TotalPermissions,
                    DirectPermissions = response.Summary.DirectPermissions,
                    InheritedPermissions = response.Summary.InheritedPermissions,
                    ActivePermissions = response.Summary.ActivePermissions,
                    ExpiredPermissions = response.Summary.ExpiredPermissions,
                    ConflictCount = response.Summary.ConflictCount,
                    ResourceTypes = response.Summary.ResourceTypes.ToList(),
                    NextExpiration = response.Summary.NextExpiration
                }
            };
            
            LogQuerySuccess(_logger, context, new 
            { 
                EntityId = query.EntityId,
                EntityType = query.EntityType,
                PermissionCount = result.Permissions.Count,
                ConflictCount = result.Conflicts.Count,
                ActivePermissions = result.Summary.ActivePermissions
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<EffectivePermissionsResult>(_logger, ex, context, correlationId);
        }
    }
}

public class PermissionImpactAnalysisQueryHandler : IQueryHandler<PermissionImpactAnalysisQuery, PermissionImpactAnalysisResult>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionImpactAnalysisQueryHandler> _logger;

    public PermissionImpactAnalysisQueryHandler(IPermissionService permissionService, ILogger<PermissionImpactAnalysisQueryHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<PermissionImpactAnalysisResult> HandleAsync(PermissionImpactAnalysisQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(PermissionImpactAnalysisQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            PermissionId = query.PermissionId,
            ResourceId = query.ResourceId,
            EntityId = query.EntityId,
            EntityType = query.EntityType,
            AnalysisType = query.AnalysisType,
            MaxDepth = query.MaxDepth
        }, correlationId);
        
        try
        {
            if (string.IsNullOrWhiteSpace(query.AnalysisType))
                throw new ArgumentException("Analysis type is required");

            var request = new ACS.Service.Requests.PermissionImpactAnalysisRequest
            {
                PermissionId = query.PermissionId,
                ResourceId = query.ResourceId,
                EntityId = query.EntityId,
                EntityType = query.EntityType,
                AnalysisType = query.AnalysisType,
                IncludeDownstreamEffects = query.IncludeDownstreamEffects,
                IncludeRiskAssessment = query.IncludeRiskAssessment,
                MaxDepth = query.MaxDepth
            };
            
            var response = await _permissionService.AnalyzePermissionImpactAsync(request);
            
            var result = new PermissionImpactAnalysisResult
            {
                AnalysisId = response.AnalysisId,
                AnalysisType = response.AnalysisType,
                AnalyzedAt = DateTime.UtcNow,
                DirectImpacts = response.DirectImpacts.Select(i => new ImpactItem
                {
                    ImpactType = i.ImpactType,
                    Description = i.Description,
                    AffectedEntityId = i.AffectedEntityId,
                    AffectedEntityName = i.AffectedEntityName,
                    AffectedEntityType = i.AffectedEntityType,
                    ChangeType = i.ChangeType,
                    Severity = i.Severity,
                    Details = i.Details
                }).ToList(),
                IndirectImpacts = response.IndirectImpacts.Select(i => new ImpactItem
                {
                    ImpactType = i.ImpactType,
                    Description = i.Description,
                    AffectedEntityId = i.AffectedEntityId,
                    AffectedEntityName = i.AffectedEntityName,
                    AffectedEntityType = i.AffectedEntityType,
                    ChangeType = i.ChangeType,
                    Severity = i.Severity,
                    Details = i.Details
                }).ToList(),
                RiskAssessment = new RiskAssessment
                {
                    OverallRiskLevel = response.RiskAssessment.OverallRiskLevel,
                    RiskScore = response.RiskAssessment.RiskScore,
                    RiskFactors = response.RiskAssessment.RiskFactors.Select(rf => new RiskFactor
                    {
                        Category = rf.Category,
                        Description = rf.Description,
                        Impact = rf.Impact,
                        Probability = rf.Probability,
                        Mitigation = rf.Mitigation
                    }).ToList(),
                    MitigationRecommendations = response.RiskAssessment.MitigationRecommendations.ToList(),
                    RiskJustification = response.RiskAssessment.RiskJustification
                },
                Recommendations = response.Recommendations.ToList(),
                AnalysisMetadata = response.AnalysisMetadata
            };
            
            LogQuerySuccess(_logger, context, new 
            { 
                AnalysisId = result.AnalysisId,
                AnalysisType = query.AnalysisType,
                DirectImpactCount = result.DirectImpacts.Count,
                IndirectImpactCount = result.IndirectImpacts.Count,
                OverallRiskLevel = result.RiskAssessment.OverallRiskLevel
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<PermissionImpactAnalysisResult>(_logger, ex, context, correlationId);
        }
    }
}