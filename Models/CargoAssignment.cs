using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public class CargoAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("Cargo")]
        public int CargoId { get; set; }

        [Required]
        public DateOnly DateAsignation { get; set; }

        // Navigation properties
        public Users User { get; set; } = null!;
        public Cargo Cargo { get; set; } = null!;
    }
}
