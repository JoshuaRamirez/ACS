using ACS.Service.Domain;

namespace ACS.Service.Infrastructure.BatchCommands;

/// <summary>
/// Command for bulk creation of users
/// </summary>
public class BulkCreateUsersCommand : BatchCommand<User>
{
    public List<UserCreationData> Users { get; set; } = new();
    
    public override int TotalItems => Users.Count;
}

/// <summary>
/// Data for creating a single user in bulk operation
/// </summary>
public class UserCreationData
{
    public string Name { get; set; } = "";
    public int? GroupId { get; set; }
    public int? RoleId { get; set; }
    public string? ExternalId { get; set; } // For tracking in batch results
}