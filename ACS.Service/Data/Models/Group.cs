using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Entity Entity { get; set; } = null!;

    // Many-to-many relationships through junction tables
    public ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
    public ICollection<GroupRole> GroupRoles { get; set; } = new List<GroupRole>();
    
    // Group hierarchy relationships
    public ICollection<GroupHierarchy> ParentGroupRelations { get; set; } = new List<GroupHierarchy>();
    public ICollection<GroupHierarchy> ChildGroupRelations { get; set; } = new List<GroupHierarchy>();
    
    // Navigation properties for convenience (read-only computed properties)
    public IEnumerable<User> Users => UserGroups.Select(ug => ug.User);
    public IEnumerable<Role> Roles => GroupRoles.Select(gr => gr.Role);
    public IEnumerable<Group> ParentGroups => ParentGroupRelations.Select(pgh => pgh.ParentGroup);
    public IEnumerable<Group> ChildGroups => ChildGroupRelations.Select(cgh => cgh.ChildGroup);
}