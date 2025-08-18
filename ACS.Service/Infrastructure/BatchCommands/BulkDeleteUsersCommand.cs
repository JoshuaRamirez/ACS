namespace ACS.Service.Infrastructure.BatchCommands;

/// <summary>
/// Command for bulk deletion of users
/// </summary>
public class BulkDeleteUsersCommand : BatchCommand<bool>
{
    public List<int> UserIds { get; set; } = new();
    
    public override int TotalItems => UserIds.Count;
}