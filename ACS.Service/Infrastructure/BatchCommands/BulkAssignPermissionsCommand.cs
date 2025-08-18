using ACS.Service.Domain;

namespace ACS.Service.Infrastructure.BatchCommands;

/// <summary>
/// Command for bulk assignment of permissions to entities
/// </summary>
public class BulkAssignPermissionsCommand : BatchCommand<bool>
{
    public List<PermissionAssignment> Assignments { get; set; } = new();
    
    public override int TotalItems => Assignments.Count;
}

/// <summary>
/// Single permission assignment in a bulk operation
/// </summary>
public class PermissionAssignment
{
    public int EntityId { get; set; }
    public Permission Permission { get; set; } = new();
    public bool IsRevoke { get; set; } = false; // True to remove, false to add
}