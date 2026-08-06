using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace GestorEquipos.Models
{
    public class Asignation
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [ForeignKey("Desktop")]
        public int DesktopId { get; set; }
        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }
        [Required]
        public DateOnly DateAsignation { get; set; }

        // Navigation properties
        public Desktop Desktop { get; set; }
        public Users User { get; set; }
    }
}
