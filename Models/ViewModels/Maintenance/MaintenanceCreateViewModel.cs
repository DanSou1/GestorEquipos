using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.Maintenance
{
    public class MaintenanceCreateViewModel
    {
        [Required]
        public int DesktopId { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de mantenimiento.")]
        public int MaintenanceTypeId { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        [Required(ErrorMessage = "La descripción/observación es obligatoria.")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona el técnico.")]
        public int TechnicianUserSystemId { get; set; }
    }
}
