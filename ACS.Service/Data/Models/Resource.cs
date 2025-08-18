using System.ComponentModel.DataAnnotations;

namespace ACS.Service.Data.Models;

public class Resource
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Uri { get; set; } = string.Empty;
}