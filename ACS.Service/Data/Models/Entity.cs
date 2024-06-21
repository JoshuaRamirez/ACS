using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class Entity
{
    public int Id { get; set; }
    public ICollection<Group> Groups { get; set; }
    public ICollection<User> Users { get; set; }
    public ICollection<Role> Roles { get; set; }
    public ICollection<PermissionScheme> EntityPermissions { get; set; }
}