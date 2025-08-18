namespace ACS.Service.Infrastructure.BatchCommands;

/// <summary>
/// Command for bulk update of users
/// </summary>
public class BulkUpdateUsersCommand : BatchCommand<bool>
{
    public List<UserUpdateData> Updates { get; set; } = new();
    
    public override int TotalItems => Updates.Count;
}

/// <summary>
/// Data for updating a single user in bulk operation
/// </summary>
public class UserUpdateData
{
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public int? NewGroupId { get; set; }
    public int? NewRoleId { get; set; }
}