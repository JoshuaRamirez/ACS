using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Entity Entity { get; set; } = null!;

    // Many-to-many relationships through junction tables
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<GroupRole> GroupRoles { get; set; } = new List<GroupRole>();
    
    // Navigation properties for convenience (read-only computed properties)
    public IEnumerable<User> Users => UserRoles.Select(ur => ur.User);
    public IEnumerable<Group> Groups => GroupRoles.Select(gr => gr.Group);
}