using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACS.Service.Data.Models;

/// <summary>
/// Data model representing many-to-many relationship between groups (parent-child groups)
/// </summary>
[Table("GroupGroup")]
public class GroupGroup
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int ParentGroupId { get; set; }
    
    [Required]
    public int ChildGroupId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? DeletedAt { get; set; }
    
    [MaxLength(100)]
    public string? DeletedBy { get; set; }
    
    // Navigation properties
    [ForeignKey(nameof(ParentGroupId))]
    public virtual Group? ParentGroup { get; set; }
    
    [ForeignKey(nameof(ChildGroupId))]
    public virtual Group? ChildGroup { get; set; }
}