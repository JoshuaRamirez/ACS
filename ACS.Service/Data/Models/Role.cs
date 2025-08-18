using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACS.Service.Data.Models;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public int EntityId { get; set; }
    public Entity Entity { get; set; } = null!;
    public ICollection<User> Users { get; set; } = new List<User>();
}