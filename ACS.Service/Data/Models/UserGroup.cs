using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class UserGroup
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public User User { get; set; } = null!;
    public Group Group { get; set; } = null!;
}