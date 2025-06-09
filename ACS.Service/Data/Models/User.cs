namespace ACS.Service.Data.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int EntityId { get; set; }
    public Entity Entity { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; }
    public int GroupId { get; set; }
    public Group Group { get; set; }
}
