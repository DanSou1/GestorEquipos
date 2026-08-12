using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public class PeripheralMaintenance
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("MaintenanceType")]
        public int MaintenanceTypeId { get; set; }

        [Required]
        [ForeignKey("Peripheral")]
        public int PeripheralId { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string TechnicianName { get; set; } = string.Empty;

        // Navigation properties
        public MaintenanceType MaintenanceType { get; set; } = null!;
        public Peripheral Peripheral { get; set; } = null!;
    }
}
