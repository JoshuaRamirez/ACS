using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class GroupRole
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int RoleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public Group Group { get; set; } = null!;
    public Role Role { get; set; } = null!;
}