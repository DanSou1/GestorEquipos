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
        [ForeignKey("Technician")]
        public int TechnicianUserSystemId { get; set; }

        // Navigation properties
        public MaintenanceType MaintenanceType { get; set; } = null!;
        public Peripheral Peripheral { get; set; } = null!;
        public UserSystem Technician { get; set; } = null!;
    }
}
