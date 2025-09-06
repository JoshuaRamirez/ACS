/// <summary>
/// Service request for bulk permission updates
/// </summary>
public record BulkPermissionUpdateRequest
{
    public ICollection<BulkPermissionOperationRequest> Operations { get; init; } = new List<BulkPermissionOperationRequest>();
    public bool ValidateBeforeExecution { get; init; } = true;
    public bool StopOnFirstError { get; init; } = false;
    public bool ExecuteInTransaction { get; init; } = true;
    public string RequestedBy { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

/// <summary>
/// Individual permission operation in bulk update
/// </summary>
public record BulkPermissionOperationRequest
{
    public string OperationType { get; init; } = string.Empty; // "Grant", "Revoke", "Update"
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public int PermissionId { get; init; }
    public int? ResourceId { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Service request for checking permission with details
/// </summary>
public record CheckPermissionRequest
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public int PermissionId { get; init; }
    public int? ResourceId { get; init; }
    public bool IncludeInheritance { get; init; } = true;
    public bool IncludeExpired { get; init; } = false;
    public DateTime? CheckAt { get; init; }
}

/// <summary>
/// Service request for getting entity permissions with enhanced options
/// </summary>
public record GetEntityPermissionsRequest
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public bool IncludeInherited { get; init; } = true;
    public bool IncludeExpired { get; init; } = false;
    public int? ResourceId { get; init; }
    public string? PermissionFilter { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Service request for getting permission usage
/// </summary>
public record GetPermissionUsageRequest
{
    public int PermissionId { get; init; }
    public int? ResourceId { get; init; }
    public bool IncludeIndirect { get; init; } = true;
    public string? EntityTypeFilter { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Service request for validating permission structure
/// </summary>
public record ValidatePermissionStructureRequest
{
    public int? EntityId { get; init; }
    public string? EntityType { get; init; }
    public bool FixInconsistencies { get; init; } = false;
    public string ValidatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for revoking permission with enhanced options
/// </summary>
public record RevokePermissionRequest
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public int PermissionId { get; init; }
    public int? ResourceId { get; init; }
    public bool CascadeToChildren { get; init; } = false;
    public string RevokedBy { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

/// <summary>
/// Service request for granting permission with enhanced options
/// </summary>
public record GrantPermissionRequest
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public int PermissionId { get; init; }
    public int? ResourceId { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string GrantedBy { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

/// <summary>
/// Service request for evaluating complex permissions
/// </summary>
public record EvaluateComplexPermissionRequest
{
    public int UserId { get; init; }
    public int ResourceId { get; init; }
    public string Action { get; init; } = string.Empty;
    public Dictionary<string, object> Context { get; init; } = new();
    public ICollection<PermissionConditionRequest> Conditions { get; init; } = new List<PermissionConditionRequest>();
    public bool IncludeReasoningTrace { get; init; } = false;
    public DateTime EvaluateAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Permission condition for complex evaluations
/// </summary>
public record PermissionConditionRequest
{
    public string Type { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public object Value { get; init; } = new();
    public Dictionary<string, object>? Parameters { get; init; }
}

/// <summary>
/// Service request for getting effective permissions
/// </summary>
public record GetEffectivePermissionsRequest
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public ICollection<int>? ResourceIds { get; init; }
    public bool IncludeInheritanceChain { get; init; } = true;
    public bool IncludeExpiredPermissions { get; init; } = false;
    public bool ResolveConflicts { get; init; } = true;
    public DateTime EffectiveAt { get; init; } = DateTime.UtcNow;
    public string? PermissionScope { get; init; }
}

/// <summary>
/// Service request for permission impact analysis
/// </summary>
public record PermissionImpactAnalysisRequest
{
    public int? PermissionId { get; init; }
    public int? ResourceId { get; init; }
    public int? EntityId { get; init; }
    public string? EntityType { get; init; }
    public string AnalysisType { get; init; } = string.Empty;
    public bool IncludeDownstreamEffects { get; init; } = true;
    public bool IncludeRiskAssessment { get; init; } = true;
    public int MaxDepth { get; init; } = 5;
}

/// <summary>
/// Service request for getting resource permissions
/// </summary>
public record GetResourcePermissionsRequest
{
    public int ResourceId { get; init; }
    public bool IncludeInherited { get; init; } = true;
    public bool IncludeEffective { get; init; } = true;
    public string? EntityType { get; init; }
}
