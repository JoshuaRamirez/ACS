using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACS.Service.Data.Models;

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int EntityId { get; set; }
    public Entity Entity { get; set; }
    public int ParentGroupId { get; set; }
    public Group? ParentGroup { get; set; }
    public ICollection<Group> ChildGroups { get; set; } = new List<Group>();
    public ICollection<Role> Roles { get; set; }
    public ICollection<User> Users { get; set; }
}