using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    // LOG-08: one row per Desktop/peripheral captured in Maintenance.DownloadPdf at the moment a
    // Preventivo maintenance is created — so reassigning the desktop, renaming a holder, or
    // retiring a peripheral afterward doesn't change what a past maintenance record says it covered.
    public class MaintenanceEquipmentSnapshotItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Maintenance")]
        public int MaintenanceId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Label { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Brand { get; set; }

        [MaxLength(100)]
        public string? Model { get; set; }

        [MaxLength(100)]
        public string? Serial { get; set; }

        public Maintenance Maintenance { get; set; } = null!;
    }
}
