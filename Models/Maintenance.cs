using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public class Maintenance
    {
        [Key]
        public int Id { get; set; }
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
        [Required]
        public MaintenanceCategory Category { get; set; }

        // LOG-08: captured once at creation time (Preventivo only — see MaintenanceService.CreateAsync)
        // so the PDF reflects who held the equipment and what it included back then, not today.
        [MaxLength(200)]
        public string? SnapshotHolderName { get; set; }
        [MaxLength(150)]
        public string? SnapshotHolderArea { get; set; }

        // Navigation properties
        public Desktop Desktop { get; set; }
        public MaintenanceChecklist? Checklist { get; set; }
        public ICollection<MaintenanceEquipmentSnapshotItem> EquipmentSnapshot { get; set; } = new List<MaintenanceEquipmentSnapshotItem>();
    }

    public enum MaintenanceCategory
    {
        Correctivo = 0,
        Preventivo = 1
    }

    public class MaintenanceType
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Type { get; set; }

        public ICollection<PeripheralMaintenance> PeripheralMaintenances { get; set; } = new List<PeripheralMaintenance>();
    }
}
