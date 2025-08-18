using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class GroupHierarchy
{
    public int Id { get; set; }
    public int ParentGroupId { get; set; }
    public int ChildGroupId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public Group ParentGroup { get; set; } = null!;
    public Group ChildGroup { get; set; } = null!;
}