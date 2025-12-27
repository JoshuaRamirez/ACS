using ACS.Service.Domain;

namespace ACS.Service.Responses;

/// <summary>
/// Service response for a single permission operation
/// </summary>
public record PermissionResponse
{
    public Permission? Permission { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for multiple permissions with pagination
/// </summary>
public record PermissionsResponse
{
    public ICollection<Permission> Permissions { get; init; } = new List<Permission>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission creation operation
/// </summary>
public record CreatePermissionResponse
{
    public Permission? Permission { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission update operation
/// </summary>
public record UpdatePermissionResponse
{
    public Permission? Permission { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission deletion operation
/// </summary>
public record DeletePermissionResponse
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission grant/revoke operations
/// </summary>
public record PermissionGrantResponse
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission check operations
/// </summary>
public record PermissionCheckResponse
{
    public bool HasPermission { get; init; }
    public string? Reason { get; init; }
    public ICollection<Permission> GrantingPermissions { get; init; } = new List<Permission>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for entity permissions operations
/// </summary>
public record EntityPermissionsResponse
{
    public ICollection<Permission> DirectPermissions { get; init; } = new List<Permission>();
    public ICollection<Permission> InheritedPermissions { get; init; } = new List<Permission>();
    public ICollection<Permission> AllPermissions { get; init; } = new List<Permission>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for bulk permission operations
/// </summary>
public record BulkPermissionResponse<T>
{
    public int TotalRequested { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public ICollection<T> SuccessfulItems { get; init; } = new List<T>();
    public ICollection<BulkOperationError> Errors { get; init; } = new List<BulkOperationError>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for bulk permission operations (non-generic)
/// </summary>
public record BulkPermissionResponse
{
    public int TotalRequested { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public ICollection<string> SuccessfulItems { get; init; } = new List<string>();
    public ICollection<BulkOperationError> Errors { get; init; } = new List<BulkOperationError>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission grant operations
/// </summary>
public record GrantPermissionResponse
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime GrantedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission revoke operations
/// </summary>
public record RevokePermissionResponse
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime RevokedAt { get; init; } = DateTime.UtcNow;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission check operations with details
/// </summary>
public record CheckPermissionResponse
{
    public bool HasPermission { get; init; }
    public bool IsExpired { get; init; }
    public string? Reason { get; init; }
    public ICollection<Permission> GrantingPermissions { get; init; } = new List<Permission>();
    public DateTime? ExpiresAt { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for effective permissions operations
/// </summary>
public record EffectivePermissionsResponse
{
    public ICollection<Permission> EffectivePermissions { get; init; } = new List<Permission>();
    public ICollection<Permission> DirectPermissions { get; init; } = new List<Permission>();
    public ICollection<Permission> InheritedPermissions { get; init; } = new List<Permission>();
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission evaluation operations
/// </summary>
public record PermissionEvaluationResponse
{
    public bool HasPermission { get; init; }
    public string? EvaluationReason { get; init; }
    public ICollection<string> EvaluationSteps { get; init; } = new List<string>();
    public TimeSpan EvaluationTime { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for complex permission operations
/// </summary>
public record ComplexPermissionResponse
{
    public bool HasPermission { get; init; }
    public string? EvaluationResult { get; init; }
    public ICollection<string> EvaluationDetails { get; init; } = new List<string>();
    public Dictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission impact analysis
/// </summary>
public record PermissionImpactResponse
{
    public int AffectedEntities { get; init; }
    public ICollection<string> ImpactedResources { get; init; } = new List<string>();
    public ICollection<string> ImpactDetails { get; init; } = new List<string>();
    public string RiskLevel { get; init; } = "Low";
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for resource permissions operations
/// </summary>
public record ResourcePermissionsResponse
{
    public int ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public ICollection<Permission> Permissions { get; init; } = new List<Permission>();
    public ICollection<string> AllowedActions { get; init; } = new List<string>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission validation operations
/// </summary>
public record PermissionValidationResponse
{
    public bool IsValid { get; init; }
    public ICollection<string> ValidationErrors { get; init; } = new List<string>();
    public ICollection<string> ValidationWarnings { get; init; } = new List<string>();
    public ICollection<string> Suggestions { get; init; } = new List<string>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission usage analysis
/// </summary>
public record PermissionUsageResponse
{
    public int PermissionId { get; init; }
    public string PermissionName { get; init; } = string.Empty;
    public int UsageCount { get; init; }
    public ICollection<string> UsedBy { get; init; } = new List<string>();
    public DateTime LastUsed { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission cache statistics
/// </summary>
public record PermissionCacheStatsResponse
{
    public int TotalEntries { get; init; }
    public int HitCount { get; init; }
    public int MissCount { get; init; }
    public double HitRatio { get; init; }
    public long MemoryUsage { get; init; }
    public TimeSpan AverageAccessTime { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for direct permissions operations
/// </summary>
public record DirectPermissionsResponse
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public ICollection<Permission> DirectPermissions { get; init; } = new List<Permission>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Result of permission check operation - simple result type
/// </summary>
public record PermissionCheckResult
{
    public bool HasPermission { get; init; }
    public bool IsExpired { get; init; }
    public string? Reason { get; init; }
    public ICollection<Permission> GrantingPermissions { get; init; } = new List<Permission>();
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Service response for permission revoke operations (alias for compatibility)
/// </summary>
public record PermissionRevokeResponse
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public string? Error { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public ICollection<int> AffectedEntityIds { get; init; } = new List<int>();
    public DateTime RevokedAt { get; init; } = DateTime.UtcNow;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for detailed permission check operations
/// </summary>
public record PermissionCheckWithDetailsResponse
{
    public bool HasPermission { get; init; }
    public bool IsExpired { get; init; }
    public bool IsInherited { get; init; }
    public string? InheritedFrom { get; init; }
    public ICollection<string> InheritanceChain { get; init; } = new List<string>();
    public string? Reason { get; init; }
    public ICollection<string> Details { get; init; } = new List<string>();
    public ICollection<Permission> GrantingPermissions { get; init; } = new List<Permission>();
    public DateTime? ExpiresAt { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission structure validation
/// </summary>
public record ValidatePermissionStructureResponse
{
    public bool IsValid { get; init; }
    public ICollection<PermissionInconsistency> Inconsistencies { get; init; } = new List<PermissionInconsistency>();
    public ICollection<PermissionInconsistency> FixedInconsistencies { get; init; } = new List<PermissionInconsistency>();
    public ICollection<string> ValidationErrors { get; init; } = new List<string>();
    public ICollection<string> ValidationWarnings { get; init; } = new List<string>();
    public ICollection<string> Recommendations { get; init; } = new List<string>();
    public int ConflictCount { get; init; }
    public int RedundancyCount { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for getting entity permissions
/// </summary>
public record GetEntityPermissionsResponse
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public ICollection<string> Permissions { get; init; } = new List<string>();
    public ICollection<Permission> DirectPermissions { get; init; } = new List<Permission>();
    public ICollection<Permission> InheritedPermissions { get; init; } = new List<Permission>();
    public int TotalCount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission usage analysis
/// </summary>
public record GetPermissionUsageResponse
{
    public int PermissionId { get; init; }
    public string PermissionName { get; init; } = string.Empty;
    public int UsageCount { get; init; }
    public ICollection<string> UsedBy { get; init; } = new List<string>();
    public ICollection<PermissionUsageItem> Usage { get; init; } = new List<PermissionUsageItem>();
    public DateTime LastUsed { get; init; }
    public DateTime? FirstUsed { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for bulk permission update operations
/// </summary>
public record BulkPermissionUpdateResponse
{
    public bool Success { get; init; } = true;
    public int TotalRequested { get; init; }
    public int ProcessedCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public int SuccessfulOperations { get; init; }
    public int FailedOperations { get; init; }
    public ICollection<BulkOperationResult> OperationResults { get; init; } = new List<BulkOperationResult>();
    public ICollection<string> SuccessfulUpdates { get; init; } = new List<string>();
    public ICollection<string> FailedUpdates { get; init; } = new List<string>();
    public ICollection<string> Errors { get; init; } = new List<string>();
    public string? Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for complex permission evaluation
/// </summary>
public record EvaluateComplexPermissionResponse
{
    public bool HasAccess { get; init; }
    public bool HasPermission { get; init; }
    public string? DecisionReason { get; init; }
    public string? EvaluationResult { get; init; }
    public ICollection<PermissionEvaluationStep> ReasoningTrace { get; init; } = new List<PermissionEvaluationStep>();
    public ICollection<ConditionEvaluationResult> ConditionResults { get; init; } = new List<ConditionEvaluationResult>();
    public PermissionDecisionContext Context { get; init; } = new PermissionDecisionContext();
    public ICollection<string> EvaluationSteps { get; init; } = new List<string>();
    public ICollection<string> EvaluationDetails { get; init; } = new List<string>();
    public TimeSpan EvaluationTime { get; init; }
    public string? ConflictResolution { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for effective permissions retrieval
/// </summary>
public record GetEffectivePermissionsResponse
{
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public ICollection<EffectivePermission> Permissions { get; init; } = new List<EffectivePermission>();
    public ICollection<PermissionConflict> Conflicts { get; init; } = new List<PermissionConflict>();
    public PermissionsSummary Summary { get; init; } = new PermissionsSummary();
    public ICollection<string> EffectivePermissions { get; init; } = new List<string>();
    public ICollection<string> InheritedPermissions { get; init; } = new List<string>();
    public ICollection<string> DirectPermissions { get; init; } = new List<string>();
    public ICollection<Permission> PermissionDetails { get; init; } = new List<Permission>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for permission impact analysis
/// </summary>
public record PermissionImpactAnalysisResponse
{
    public string AnalysisId { get; init; } = string.Empty;
    public string AnalysisType { get; init; } = string.Empty;
    public ICollection<ImpactItem> DirectImpacts { get; init; } = new List<ImpactItem>();
    public ICollection<ImpactItem> IndirectImpacts { get; init; } = new List<ImpactItem>();
    public RiskAssessment RiskAssessment { get; init; } = new RiskAssessment();
    public ICollection<string> Recommendations { get; init; } = new List<string>();
    public Dictionary<string, object> AnalysisMetadata { get; init; } = new Dictionary<string, object>();
    public int PermissionId { get; init; }
    public string Operation { get; init; } = string.Empty;
    public int AffectedUsers { get; init; }
    public int AffectedGroups { get; init; }
    public int AffectedResources { get; init; }
    public ICollection<string> ImpactedEntities { get; init; } = new List<string>();
    public ICollection<string> ImpactDetails { get; init; } = new List<string>();
    public ICollection<string> DownstreamEffects { get; init; } = new List<string>();
    public string RiskLevel { get; init; } = "Low"; // Low, Medium, High, Critical
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for resource permissions retrieval
/// </summary>
public record GetResourcePermissionsResponse
{
    public int ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public ICollection<ResourcePermissionInfo> Permissions { get; init; } = new List<ResourcePermissionInfo>();
    public ICollection<string> AllowedActions { get; init; } = new List<string>();
    public ICollection<Permission> PermissionDetails { get; init; } = new List<Permission>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a bulk operation result
/// </summary>
public record BulkOperationResult
{
    public int Index { get; init; }
    public object? Operation { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a permission evaluation step
/// </summary>
public record PermissionEvaluationStep
{
    public int Step { get; init; }
    public string Description { get; init; } = string.Empty;
    public string DecisionPoint { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string? Reason { get; init; }
    public Dictionary<string, object> Context { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents a condition evaluation result
/// </summary>
public record ConditionEvaluationResult
{
    public PermissionCondition Condition { get; init; } = new PermissionCondition();
    public bool Satisfied { get; init; }
    public string? Reason { get; init; }
    public object? ActualValue { get; init; }
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a permission condition
/// </summary>
public record PermissionCondition
{
    public string Type { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public object? Value { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents permission decision context
/// </summary>
public record PermissionDecisionContext
{
    public int UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public int ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime RequestTimestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> AdditionalContext { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents a permission inconsistency
/// </summary>
public record PermissionInconsistency
{
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public int PermissionId { get; init; }
    public int? ResourceId { get; init; }
    public string Severity { get; init; } = string.Empty;
    public bool CanAutoFix { get; init; }
    public string? RecommendedAction { get; init; }
}

/// <summary>
/// Represents an effective permission
/// </summary>
public record EffectivePermission
{
    public int PermissionId { get; init; }
    public string PermissionName { get; init; } = string.Empty;
    public int ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string? InheritedFrom { get; init; }
    public ICollection<string> InheritanceChain { get; init; } = new List<string>();
    public DateTime GrantedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; }
    public bool HasConflicts { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents a permission conflict
/// </summary>
public record PermissionConflict
{
    public string ConflictType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ICollection<int> ConflictingPermissions { get; init; } = new List<int>();
    public string Resolution { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
}

/// <summary>
/// Represents permissions summary
/// </summary>
public record PermissionsSummary
{
    public int TotalPermissions { get; init; }
    public int DirectPermissions { get; init; }
    public int InheritedPermissions { get; init; }
    public int ActivePermissions { get; init; }
    public int ExpiredPermissions { get; init; }
    public int ConflictCount { get; init; }
    public ICollection<string> ResourceTypes { get; init; } = new List<string>();
    public DateTime? NextExpiration { get; init; }
}

/// <summary>
/// Represents an impact item
/// </summary>
public record ImpactItem
{
    public string ImpactType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int AffectedEntityId { get; init; }
    public string AffectedEntityName { get; init; } = string.Empty;
    public string AffectedEntityType { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public Dictionary<string, object> Details { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents a risk assessment
/// </summary>
public record RiskAssessment
{
    public string OverallRiskLevel { get; init; } = string.Empty;
    public double RiskScore { get; init; }
    public ICollection<RiskFactor> RiskFactors { get; init; } = new List<RiskFactor>();
    public ICollection<string> MitigationRecommendations { get; init; } = new List<string>();
    public string? RiskJustification { get; init; }
}

/// <summary>
/// Represents a risk factor
/// </summary>
public record RiskFactor
{
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
    public string Probability { get; init; } = string.Empty;
    public string? Mitigation { get; init; }
}

/// <summary>
/// Represents resource permission info
/// </summary>
public record ResourcePermissionInfo
{
    public int PermissionId { get; init; }
    public string PermissionName { get; init; } = string.Empty;
    public int EntityId { get; init; }
    public string EntityName { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public bool IsInherited { get; init; }
    public string? InheritedFrom { get; init; }
    public DateTime GrantedAt { get; init; } = DateTime.UtcNow;
    public string? GrantedBy { get; init; }
}

/// <summary>
/// Represents a permission usage item
/// </summary>
public record PermissionUsageItem
{
    public int EntityId { get; init; }
    public string EntityName { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public int? ResourceId { get; init; }
    public string? ResourceName { get; init; }
    public bool IsDirect { get; init; }
    public string? GrantedThrough { get; init; }
    public DateTime GrantedAt { get; init; } = DateTime.UtcNow;
    public string? GrantedBy { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsExpired { get; init; }
}
