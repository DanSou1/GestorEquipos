using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public class SpecChangeLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Desktop")]
        public int DesktopId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FieldName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? OldValue { get; set; }

        [MaxLength(300)]
        public string? NewValue { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        [Required]
        [ForeignKey("ChangedByUserSystem")]
        public int ChangedByUserSystemId { get; set; }

        public Desktop Desktop { get; set; } = null!;
        public UserSystem ChangedByUserSystem { get; set; } = null!;
    }
}
