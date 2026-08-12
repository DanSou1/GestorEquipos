using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public class Maintenance
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [ForeignKey("MaintenanceType")]
        public int MaintenanceTypeId { get; set; }
        [Required]
        [ForeignKey("Desktop")]
        public int DesktopId { get; set; }
        [Required]
        public DateOnly Date { get; set; }
        [Required]
        [MaxLength(500)]
        public string Description { get; set; }
        [Required]
        [MaxLength(200)]
        public string TechnicianName { get; set; }

        // Navigation properties
        public MaintenanceType MaintenanceType { get; set; }
        public Desktop Desktop { get; set; }
    }

    public class MaintenanceType
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Type { get; set; }

        public ICollection<Maintenance> Maintenances { get; set; } = new List<Maintenance>();
        public ICollection<PeripheralMaintenance> PeripheralMaintenances { get; set; } = new List<PeripheralMaintenance>();
    }
}
