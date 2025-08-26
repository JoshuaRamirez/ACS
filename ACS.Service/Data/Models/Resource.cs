using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class Resource
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Uri { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ResourceType { get; set; }

    [MaxLength(50)]
    public string? Version { get; set; }

    public int? ParentResourceId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<UriAccess> UriAccesses { get; set; } = new List<UriAccess>();
    public virtual Resource? ParentResource { get; set; }
    public virtual ICollection<Resource> ChildResources { get; set; } = new List<Resource>();
}