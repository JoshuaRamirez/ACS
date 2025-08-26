using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    public string? Salt { get; set; }
    
    public DateTime? LastLoginAt { get; set; }
    
    public int FailedLoginAttempts { get; set; } = 0;
    
    public DateTime? LockedOutUntil { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public int EntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Entity Entity { get; set; } = null!;
    
    // Many-to-many relationships through junction tables
    public ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    
    // Navigation properties for convenience (read-only computed properties)
    public IEnumerable<Group> Groups => UserGroups.Select(ug => ug.Group);
    public IEnumerable<Role> Roles => UserRoles.Select(ur => ur.Role);
    
    // GDPR-related properties for compliance tracking
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? PseudonymId { get; set; }
    public bool IsPseudonymized { get; set; }
    public bool IsAnonymized { get; set; }
    public DateTime? AnonymizedAt { get; set; }
}