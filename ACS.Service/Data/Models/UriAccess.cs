using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACS.Service.Data.Models;

public class UriAccess
{
    [Key]
    public int Id { get; set; }

    [ForeignKey("Resource")]
    public int ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;

    [ForeignKey("VerbType")]
    public int VerbTypeId { get; set; }
    public VerbType VerbType { get; set; } = null!;

    [ForeignKey("PermissionScheme")]
    public int EntityPermissionId { get; set; }
    public PermissionScheme PermissionScheme { get; set; } = null!;

    public bool Grant { get; set; }
    public bool Deny { get; set; }
}