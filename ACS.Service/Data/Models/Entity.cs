using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class Entity
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Group> Groups { get; set; } = new List<Group>();
    public ICollection<Role> Roles { get; set; } = new List<Role>();
    public ICollection<PermissionScheme> EntityPermissions { get; set; } = new List<PermissionScheme>();
}