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
    public bool HasPermission { get; init; }
    public string? EvaluationResult { get; init; }
    public ICollection<string> EvaluationSteps { get; init; } = new List<string>();
    public ICollection<string> EvaluationDetails { get; init; } = new List<string>();
    public Dictionary<string, object> Context { get; init; } = new Dictionary<string, object>();
    public TimeSpan EvaluationTime { get; init; }
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
    public ICollection<string> Permissions { get; init; } = new List<string>();
    public ICollection<string> AllowedActions { get; init; } = new List<string>();
    public ICollection<Permission> PermissionDetails { get; init; } = new List<Permission>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
