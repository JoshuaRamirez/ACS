using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// Advanced Access Control Commands
public class BulkPermissionUpdateCommand : ICommand<BulkPermissionUpdateResult>
{
    public List<BulkPermissionOperation> Operations { get; set; } = new();
    public bool ValidateBeforeExecution { get; set; } = true;
    public bool StopOnFirstError { get; set; } = false;
    public bool ExecuteInTransaction { get; set; } = true;
    public string? RequestedBy { get; set; }
    public string? Reason { get; set; }
}

public class AccessViolationHandlerCommand : ICommand<AccessViolationHandlerResult>
{
    public string ViolationType { get; set; } = string.Empty; // "UnauthorizedAccess", "PermissionEscalation", "SuspiciousActivity"
    public int? UserId { get; set; }
    public int? ResourceId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
    public string Severity { get; set; } = "Medium"; // "Low", "Medium", "High", "Critical"
    public string Action { get; set; } = "Log"; // "Log", "Block", "Quarantine", "Alert"
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

// Advanced Access Control Queries
public class EvaluateComplexPermissionQuery : IQuery<ComplexPermissionEvaluationResult>
{
    public int UserId { get; set; }
    public int ResourceId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
    public List<PermissionCondition> Conditions { get; set; } = new();
    public bool IncludeReasoningTrace { get; set; } = false;
    public DateTime? EvaluateAt { get; set; } // null for current time
}

public class GetEffectivePermissionsQuery : IQuery<EffectivePermissionsResult>
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty; // "User", "Group", "Role"
    public List<int>? ResourceIds { get; set; } // null for all resources
    public bool IncludeInheritanceChain { get; set; } = true;
    public bool IncludeExpiredPermissions { get; set; } = false;
    public bool ResolveConflicts { get; set; } = true;
    public DateTime? EffectiveAt { get; set; } // null for current time
    public string? PermissionScope { get; set; } // Additional filtering scope
}

public class PermissionImpactAnalysisQuery : IQuery<PermissionImpactAnalysisResult>
{
    public int? PermissionId { get; set; }
    public int? ResourceId { get; set; }
    public int? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string AnalysisType { get; set; } = "Grant"; // "Grant", "Revoke", "Modify"
    public bool IncludeDownstreamEffects { get; set; } = true;
    public bool IncludeRiskAssessment { get; set; } = true;
    public int MaxDepth { get; set; } = 5; // Maximum inheritance depth to analyze
}

// Supporting Types for Commands
public class BulkPermissionOperation
{
    public string OperationType { get; set; } = string.Empty; // "Grant", "Revoke", "Update"
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int PermissionId { get; set; }
    public int? ResourceId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? Reason { get; set; }
}

public class PermissionCondition
{
    public string Type { get; set; } = string.Empty; // "TimeWindow", "IpRange", "DeviceType", "Location", "Custom"
    public string Operator { get; set; } = "Equals"; // "Equals", "NotEquals", "GreaterThan", "LessThan", "Contains", "Matches"
    public object Value { get; set; } = new();
    public Dictionary<string, object>? Parameters { get; set; }
}

// Result Types
public class BulkPermissionUpdateResult
{
    public bool Success { get; set; } = true;
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public List<BulkOperationResult> OperationResults { get; set; } = new();
    public string? Message { get; set; }
    public bool ExecutedInTransaction { get; set; }
}

public class BulkOperationResult
{
    public int Index { get; set; }
    public BulkPermissionOperation Operation { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class AccessViolationHandlerResult
{
    public bool Success { get; set; } = true;
    public string ViolationId { get; set; } = Guid.NewGuid().ToString();
    public string ViolationType { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public DateTime HandledAt { get; set; } = DateTime.UtcNow;
    public List<string> ActionsExecuted { get; set; } = new();
    public bool UserBlocked { get; set; }
    public bool AlertGenerated { get; set; }
    public DateTime? BlockUntil { get; set; }
    public string? Message { get; set; }
}

public class ComplexPermissionEvaluationResult
{
    public bool HasAccess { get; set; }
    public string DecisionReason { get; set; } = string.Empty;
    public List<PermissionEvaluationStep> ReasoningTrace { get; set; } = new();
    public List<ConditionEvaluationResult> ConditionResults { get; set; } = new();
    public PermissionDecisionContext Context { get; set; } = new();
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan EvaluationDuration { get; set; }
    public string? ConflictResolution { get; set; }
}

public class EffectivePermissionsResult
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public List<EffectivePermission> Permissions { get; set; } = new();
    public List<PermissionConflict> Conflicts { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;
    public PermissionsSummary Summary { get; set; } = new();
}

public class PermissionImpactAnalysisResult
{
    public string AnalysisId { get; set; } = Guid.NewGuid().ToString();
    public string AnalysisType { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public List<ImpactItem> DirectImpacts { get; set; } = new();
    public List<ImpactItem> IndirectImpacts { get; set; } = new();
    public RiskAssessment RiskAssessment { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public Dictionary<string, object> AnalysisMetadata { get; set; } = new();
}

// Supporting Result Types
public class PermissionEvaluationStep
{
    public int Step { get; set; }
    public string Description { get; set; } = string.Empty;
    public string DecisionPoint { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? Reason { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

public class ConditionEvaluationResult
{
    public PermissionCondition Condition { get; set; } = new();
    public bool Satisfied { get; set; }
    public string? Reason { get; set; }
    public object? ActualValue { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}

public class PermissionDecisionContext
{
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public int ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> AdditionalContext { get; set; } = new();
}

public class EffectivePermission
{
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public int? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string Source { get; set; } = string.Empty; // "Direct", "Inherited"
    public string? InheritedFrom { get; set; }
    public List<string> InheritanceChain { get; set; } = new();
    public DateTime GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public bool HasConflicts { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class PermissionConflict
{
    public string ConflictType { get; set; } = string.Empty; // "Duplicate", "Contradictory", "Expired"
    public string Description { get; set; } = string.Empty;
    public List<int> ConflictingPermissions { get; set; } = new();
    public string? Resolution { get; set; }
    public string Severity { get; set; } = "Medium";
}

public class PermissionsSummary
{
    public int TotalPermissions { get; set; }
    public int DirectPermissions { get; set; }
    public int InheritedPermissions { get; set; }
    public int ActivePermissions { get; set; }
    public int ExpiredPermissions { get; set; }
    public int ConflictCount { get; set; }
    public List<string> ResourceTypes { get; set; } = new();
    public DateTime? NextExpiration { get; set; }
}

public class ImpactItem
{
    public string ImpactType { get; set; } = string.Empty; // "UserAccess", "GroupMembership", "RoleAssignment", "ResourceAccess"
    public string Description { get; set; } = string.Empty;
    public int? AffectedEntityId { get; set; }
    public string? AffectedEntityName { get; set; }
    public string? AffectedEntityType { get; set; }
    public string ChangeType { get; set; } = string.Empty; // "Gained", "Lost", "Modified"
    public string Severity { get; set; } = "Medium";
    public Dictionary<string, object>? Details { get; set; }
}

public class RiskAssessment
{
    public string OverallRiskLevel { get; set; } = "Low"; // "Low", "Medium", "High", "Critical"
    public double RiskScore { get; set; } // 0.0 to 1.0
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public List<string> MitigationRecommendations { get; set; } = new();
    public string? RiskJustification { get; set; }
}