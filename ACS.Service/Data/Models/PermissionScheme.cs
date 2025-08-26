using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACS.Service.Data.Models;

public class PermissionScheme
{
    [Key]
    public int Id { get; set; }

    [ForeignKey("Entity")]
    public int? EntityId { get; set; }
    public int? SchemeTypeId { get; set; }
    public SchemeType SchemeType { get; set; } = null!;
    public Entity Entity { get; set; } = null!;

    // Additional properties needed by tests and permission evaluation
    [ForeignKey("UriAccess")]
    public int? UriAccessId { get; set; }
    public UriAccess? UriAccess { get; set; }
    public bool Grant { get; set; } = false;
}