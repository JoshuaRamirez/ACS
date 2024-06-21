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
    public SchemeType SchemeType { get; set; }
    public Entity Entity { get; set; }
}