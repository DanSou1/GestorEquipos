using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public class PeripheralAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Peripheral")]
        public int PeripheralId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        public DateOnly DateAsignation { get; set; }

        // Navigation properties
        public Peripheral Peripheral { get; set; } = null!;
        public Users User { get; set; } = null!;
    }
}
