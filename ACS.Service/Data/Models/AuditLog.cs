using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACS.Service.Data.Models
{
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; }

        public int EntityId { get; set; }

        [Required]
        [MaxLength(10)]
        public string ChangeType { get; set; }

        [Required]
        [MaxLength(100)]
        public string ChangedBy { get; set; }

        [Required]
        public DateTime ChangeDate { get; set; } = DateTime.Now;

        [Required]
        public string ChangeDetails { get; set; }
    }
}
