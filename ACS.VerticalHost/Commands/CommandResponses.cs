namespace ACS.VerticalHost.Commands;

/// <summary>
/// Response for user deletion operation
/// </summary>
public class DeleteUserResult
{
    public bool Success { get; set; } = true;
    public int UserId { get; set; }
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
    public string? Message { get; set; }
}

/// <summary>
/// Response for user-group operations (add/remove)
/// </summary>
public class UserGroupOperationResult
{
    public bool Success { get; set; } = true;
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public DateTime OperationAt { get; set; } = DateTime.UtcNow;
    public string? Operation { get; set; } // "Added" or "Removed"
    public string? Message { get; set; }
}