using ACS.Service.Domain;

namespace ACS.Service.Services;

public interface IPermissionEvaluationService
{
    // Core Permission Evaluation
    Task<bool> HasPermissionAsync(int entityId, string uri, string httpVerb);
    Task<bool> HasPermissionAsync(int entityId, string uri, HttpVerb httpVerb);
    Task<List<Permission>> GetEffectivePermissionsAsync(int entityId);
    Task<bool> CanUserAccessResourceAsync(int userId, string uri, HttpVerb httpVerb);
    Task<List<Permission>> GetUserPermissionsAsync(int userId);
    Task<List<Permission>> GetGroupPermissionsAsync(int groupId);
    Task<List<Permission>> GetRolePermissionsAsync(int roleId);
    
    // Enhanced Permission Evaluation with Inheritance
    Task<IEnumerable<Permission>> GetEntityPermissionsAsync(int entityId, bool includeInherited = true);
    Task<IEnumerable<Permission>> GetInheritedPermissionsAsync(int entityId);
    Task<IEnumerable<Permission>> GetDirectPermissionsAsync(int entityId);
    Task<PermissionEvaluationResult> EvaluatePermissionAsync(int entityId, string uri, HttpVerb httpVerb);
    Task<IEnumerable<int>> GetPermissionSourcesAsync(int entityId, string uri, HttpVerb httpVerb);
    
    // Conditional Permissions
    Task<bool> EvaluateConditionalPermissionAsync(int entityId, string uri, HttpVerb httpVerb, Dictionary<string, object> context);
    Task AddConditionalPermissionAsync(int entityId, ConditionalPermission permission);
    Task<IEnumerable<ConditionalPermission>> GetConditionalPermissionsAsync(int entityId);
    Task<bool> RemoveConditionalPermissionAsync(int entityId, int conditionId);
    Task<bool> ValidateConditionAsync(string condition, Dictionary<string, object> context);
    
    // Permission Caching
    Task<bool> HasCachedPermissionAsync(int entityId, string uri, HttpVerb httpVerb);
    Task InvalidatePermissionCacheAsync(int entityId);
    Task InvalidateAllPermissionCachesAsync();
    Task PreloadPermissionsAsync(int entityId);
    Task<CacheStatistics> GetCacheStatisticsAsync();
    
    // Permission Hierarchy and Inheritance
    Task<PermissionHierarchy> GetPermissionHierarchyAsync(int entityId);
    Task<IEnumerable<Permission>> GetPermissionsFromParentsAsync(int entityId);
    Task<IEnumerable<Permission>> GetPermissionsFromRolesAsync(int entityId);
    Task<IEnumerable<Permission>> GetPermissionsFromGroupsAsync(int entityId);
    Task<PermissionInheritanceChain> TracePermissionSourceAsync(int entityId, string uri, HttpVerb httpVerb);
    
    // Permission Conflicts and Resolution
    Task<IEnumerable<PermissionConflict>> DetectPermissionConflictsAsync(int entityId);
    Task<Permission> ResolvePermissionConflictAsync(IEnumerable<Permission> conflictingPermissions);
    Task<ConflictResolutionStrategy> GetConflictResolutionStrategyAsync();
    Task SetConflictResolutionStrategyAsync(ConflictResolutionStrategy strategy);
    Task<IEnumerable<Permission>> GetConflictingPermissionsAsync(int entityId, string uri, HttpVerb httpVerb);
    
    // Permission Templates and Presets
    Task<PermissionTemplate> GetPermissionTemplateAsync(string templateName);
    Task<IEnumerable<PermissionTemplate>> GetAllPermissionTemplatesAsync();
    Task ApplyPermissionTemplateAsync(int entityId, string templateName);
    Task<PermissionTemplate> CreatePermissionTemplateAsync(string name, IEnumerable<Permission> permissions);
    Task<bool> DeletePermissionTemplateAsync(string templateName);
    
    // Bulk Permission Operations
    Task<Dictionary<int, bool>> EvaluatePermissionsBulkAsync(IEnumerable<int> entityIds, string uri, HttpVerb httpVerb);
    Task<Dictionary<string, bool>> EvaluateMultipleResourcesAsync(int entityId, IEnumerable<string> uris, HttpVerb httpVerb);
    Task GrantPermissionsBulkAsync(int entityId, IEnumerable<Permission> permissions);
    Task RevokePermissionsBulkAsync(int entityId, IEnumerable<Permission> permissions);
    Task<IEnumerable<Permission>> GetPermissionsBulkAsync(IEnumerable<int> entityIds);
    
    // Permission Analysis and Reporting
    Task<PermissionMatrix> GeneratePermissionMatrixAsync(IEnumerable<int> entityIds, IEnumerable<string> resources);
    Task<EffectivePermissionReport> GenerateEffectivePermissionReportAsync(int entityId);
    Task<IEnumerable<PermissionGap>> IdentifyPermissionGapsAsync(int entityId, IEnumerable<string> requiredResources);
    Task<IEnumerable<ExcessivePermission>> IdentifyExcessivePermissionsAsync(int entityId);
    Task<PermissionAuditReport> AuditPermissionsAsync(int entityId, DateTime? since = null);
    
    // Permission Delegation
    Task<bool> CanDelegatePermissionAsync(int delegatorId, int delegateeId, Permission permission);
    Task DelegatePermissionAsync(int delegatorId, int delegateeId, Permission permission, DateTime? expiresAt = null);
    Task<IEnumerable<DelegatedPermission>> GetDelegatedPermissionsAsync(int entityId);
    Task<IEnumerable<DelegatedPermission>> GetPermissionsDelegatedByAsync(int entityId);
    Task RevokeDelegatedPermissionAsync(int delegationId);
    
    // Time-based Permissions
    Task<bool> HasTemporaryPermissionAsync(int entityId, string uri, HttpVerb httpVerb, DateTime? asOf = null);
    Task GrantTemporaryPermissionAsync(int entityId, Permission permission, DateTime expiresAt);
    Task<IEnumerable<TemporaryPermission>> GetTemporaryPermissionsAsync(int entityId);
    Task<IEnumerable<TemporaryPermission>> GetExpiredPermissionsAsync(int entityId);
    Task CleanupExpiredPermissionsAsync();
    
    // Permission Policies
    Task<bool> EvaluatePolicyAsync(int entityId, string policyName, Dictionary<string, object> context);
    Task<PermissionPolicy> GetPolicyAsync(string policyName);
    Task<IEnumerable<PermissionPolicy>> GetApplicablePoliciesAsync(int entityId);
    Task CreatePolicyAsync(PermissionPolicy policy);
    Task UpdatePolicyAsync(string policyName, PermissionPolicy policy);
    Task DeletePolicyAsync(string policyName);
    
    // Permission Optimization
    Task OptimizePermissionsAsync(int entityId);
    Task<IEnumerable<RedundantPermission>> FindRedundantPermissionsAsync(int entityId);
    Task RemoveRedundantPermissionsAsync(int entityId);
    Task<PermissionOptimizationReport> AnalyzePermissionEfficiencyAsync(int entityId);
    Task ConsolidatePermissionsAsync(int entityId);
}

// Supporting types
public class PermissionEvaluationResult
{
    public bool IsAllowed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<PermissionSource> Sources { get; set; } = new();
    public List<Permission> AppliedPermissions { get; set; } = new();
    public TimeSpan EvaluationTime { get; set; }
    public bool FromCache { get; set; }
}

public class PermissionSource
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty; // Direct, Role, Group, Inherited
    public Permission Permission { get; set; } = null!;
}

public class ConditionalPermission : Permission
{
    public string Condition { get; set; } = string.Empty;
    public Dictionary<string, object> RequiredContext { get; set; } = new();
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
}

public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int HitCount { get; set; }
    public int MissCount { get; set; }
    public double HitRate => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0;
    public int TotalRequests => HitCount + MissCount;
    public long CacheSizeBytes { get; set; }
    public DateTime LastReset { get; set; }
    public Dictionary<int, int> EntriesByEntity { get; set; } = new();
}

public class PermissionHierarchy
{
    public int EntityId { get; set; }
    public List<Permission> DirectPermissions { get; set; } = new();
    public Dictionary<int, List<Permission>> RolePermissions { get; set; } = new();
    public Dictionary<int, List<Permission>> GroupPermissions { get; set; } = new();
    public Dictionary<int, List<Permission>> InheritedPermissions { get; set; } = new();
    public List<Permission> EffectivePermissions { get; set; } = new();
}

public class PermissionInheritanceChain
{
    public int EntityId { get; set; }
    public string Resource { get; set; } = string.Empty;
    public HttpVerb Verb { get; set; }
    public bool IsAllowed { get; set; }
    public List<InheritanceLink> Chain { get; set; } = new();
}

public class InheritanceLink
{
    public int SourceEntityId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Permission Permission { get; set; } = null!;
    public int Level { get; set; }
}

public class PermissionConflict
{
    public string Resource { get; set; } = string.Empty;
    public HttpVerb Verb { get; set; }
    public List<Permission> ConflictingPermissions { get; set; } = new();
    public string ConflictType { get; set; } = string.Empty; // Grant/Deny, Multiple Sources
    public Permission? ResolvedPermission { get; set; }
}

public enum ConflictResolutionStrategy
{
    DenyOverrides,    // Deny always wins
    GrantOverrides,   // Grant always wins
    MostSpecific,     // Most specific permission wins
    MostRecent,       // Most recently added wins
    HighestPriority   // Based on priority value
}

public class PermissionTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Permission> Permissions { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PermissionMatrix
{
    public List<int> EntityIds { get; set; } = new();
    public List<string> Resources { get; set; } = new();
    public Dictionary<int, Dictionary<string, Dictionary<HttpVerb, bool>>> Matrix { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class EffectivePermissionReport
{
    public int EntityId { get; set; }
    public List<Permission> DirectPermissions { get; set; } = new();
    public List<Permission> InheritedPermissions { get; set; } = new();
    public List<Permission> ConditionalPermissions { get; set; } = new();
    public List<Permission> TemporaryPermissions { get; set; } = new();
    public List<Permission> DelegatedPermissions { get; set; } = new();
    public List<Permission> EffectivePermissions { get; set; } = new();
    public List<PermissionConflict> Conflicts { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class PermissionGap
{
    public string Resource { get; set; } = string.Empty;
    public HttpVerb Verb { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<Permission> SuggestedPermissions { get; set; } = new();
}

public class ExcessivePermission
{
    public Permission Permission { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
    public DateTime LastUsed { get; set; }
    public int UsageCount { get; set; }
}

public class PermissionAuditReport
{
    public int EntityId { get; set; }
    public DateTime AuditDate { get; set; }
    public List<PermissionChange> Changes { get; set; } = new();
    public List<PermissionAnomaly> Anomalies { get; set; } = new();
    public Dictionary<string, int> Statistics { get; set; } = new();
}

public class PermissionChange
{
    public DateTime Timestamp { get; set; }
    public string ChangeType { get; set; } = string.Empty; // Grant, Revoke, Modify
    public Permission OldPermission { get; set; } = null!;
    public Permission NewPermission { get; set; } = null!;
    public string ChangedBy { get; set; } = string.Empty;
}

public class PermissionAnomaly
{
    public string AnomalyType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Permission RelatedPermission { get; set; } = null!;
    public DateTime DetectedAt { get; set; }
}

public class DelegatedPermission : Permission
{
    public int DelegationId { get; set; }
    public int DelegatorId { get; set; }
    public int DelegateeId { get; set; }
    public DateTime DelegatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}

public class TemporaryPermission : Permission
{
    public DateTime GrantedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public string GrantedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class PermissionPolicy
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PolicyRule { get; set; } = string.Empty;
    public List<string> RequiredClaims { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public bool IsActive { get; set; }
    public int Priority { get; set; }
}

public class RedundantPermission
{
    public Permission Permission { get; set; } = null!;
    public Permission SupersededBy { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
}

public class PermissionOptimizationReport
{
    public int EntityId { get; set; }
    public int TotalPermissions { get; set; }
    public int RedundantPermissions { get; set; }
    public int ConflictingPermissions { get; set; }
    public int UnusedPermissions { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public double EfficiencyScore { get; set; }
    public DateTime GeneratedAt { get; set; }
}