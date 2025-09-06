using ACS.Service.Domain;

namespace ACS.Service.Requests;

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
