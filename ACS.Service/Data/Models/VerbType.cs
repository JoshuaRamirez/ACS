using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class VerbType
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    public string VerbName { get; set; } = string.Empty;
}