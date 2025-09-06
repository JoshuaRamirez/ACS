using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// Permission Management Commands
public class GrantPermissionCommand : ICommand<PermissionGrantResult>
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty; // "User", "Group", "Role"
    public int PermissionId { get; set; }
    public int? ResourceId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? GrantedBy { get; set; }
    public string? Reason { get; set; }
}

public class RevokePermissionCommand : ICommand<PermissionRevokeResult>
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty; // "User", "Group", "Role"
    public int PermissionId { get; set; }
    public int? ResourceId { get; set; }
    public bool CascadeToChildren { get; set; } = true;
    public string? RevokedBy { get; set; }
    public string? Reason { get; set; }
}

public class ValidatePermissionStructureCommand : ICommand<PermissionValidationResult>
{
    public int? EntityId { get; set; } // null for all entities
    public string? EntityType { get; set; } // null for all types
    public bool FixInconsistencies { get; set; } = false;
    public string? ValidatedBy { get; set; }
}

// Permission Management Queries
public class CheckPermissionQuery : IQuery<PermissionCheckResult>
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int PermissionId { get; set; }
    public int? ResourceId { get; set; }
    public bool IncludeInheritance { get; set; } = true;
    public bool IncludeExpired { get; set; } = false;
    public DateTime? CheckAt { get; set; } // null for current time
}

public class GetEntityPermissionsQuery : IQuery<List<EntityPermissionInfo>>
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public bool IncludeInherited { get; set; } = true;
    public bool IncludeExpired { get; set; } = false;
    public int? ResourceId { get; set; } // null for all resources
    public string? PermissionFilter { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class GetPermissionUsageQuery : IQuery<List<PermissionUsageInfo>>
{
    public int PermissionId { get; set; }
    public int? ResourceId { get; set; } // null for all resources
    public bool IncludeIndirect { get; set; } = true;
    public string? EntityTypeFilter { get; set; } // null for all entity types
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// Permission Result Types
public class PermissionGrantResult
{
    public bool Success { get; set; } = true;
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int PermissionId { get; set; }
    public int? ResourceId { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public string? Message { get; set; }
    public bool WasAlreadyGranted { get; set; }
}

public class PermissionRevokeResult
{
    public bool Success { get; set; } = true;
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int PermissionId { get; set; }
    public int? ResourceId { get; set; }
    public DateTime RevokedAt { get; set; } = DateTime.UtcNow;
    public string? Message { get; set; }
    public List<int> CascadeRevokedEntities { get; set; } = new();
}

public class PermissionValidationResult
{
    public bool IsValid { get; set; } = true;
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    public List<PermissionInconsistency> Inconsistencies { get; set; } = new();
    public List<PermissionInconsistency> FixedInconsistencies { get; set; } = new();
    public string? Message { get; set; }
}

public class PermissionCheckResult
{
    public bool HasPermission { get; set; }
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int PermissionId { get; set; }
    public int? ResourceId { get; set; }
    public bool IsInherited { get; set; }
    public string? InheritedFrom { get; set; }
    public bool IsExpired { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public List<string> InheritanceChain { get; set; } = new();
}

public class EntityPermissionInfo
{
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public string PermissionDescription { get; set; } = string.Empty;
    public int? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public bool IsInherited { get; set; }
    public string? InheritedFrom { get; set; }
    public DateTime GrantedAt { get; set; }
    public string? GrantedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
}

public class PermissionUsageInfo
{
    public int EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public bool IsDirect { get; set; }
    public string? GrantedThrough { get; set; } // For indirect grants
    public DateTime GrantedAt { get; set; }
    public string? GrantedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
}

public class PermissionInconsistency
{
    public string Type { get; set; } = string.Empty; // "OrphanedPermission", "ExpiredPermission", "DuplicateGrant", etc.
    public string Description { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? EntityType { get; set; }
    public int? PermissionId { get; set; }
    public int? ResourceId { get; set; }
    public string Severity { get; set; } = "Medium"; // "Low", "Medium", "High", "Critical"
    public bool CanAutoFix { get; set; }
    public string? RecommendedAction { get; set; }
}