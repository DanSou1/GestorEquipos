using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.Maintenance
{
    public class MaintenanceCreateViewModel
    {
        [Required]
        public int DesktopId { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de mantenimiento.")]
        public int MaintenanceTypeId { get; set; }

        [Required(ErrorMessage = "Selecciona la categoría del mantenimiento.")]
        public MaintenanceCategory Category { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        [Required(ErrorMessage = "La descripción/observación es obligatoria.")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el nombre del técnico.")]
        [MaxLength(200)]
        public string TechnicianName { get; set; } = string.Empty;

        // Checklist de mantenimiento preventivo — todos opcionales, solo se persisten si Category == Preventivo.
        public bool ChecklistVerificacionSistemaMecanico { get; set; }
        public bool ChecklistVerificacionSistemaElectrico { get; set; }
        public bool ChecklistVerificacionSistemaSeguridad { get; set; }
        public bool ChecklistSopladoInterno { get; set; }
        public bool ChecklistLavadoInterno { get; set; }
        public bool ChecklistLubricacion { get; set; }
        public bool ChecklistAjusteTornilleria { get; set; }
        public bool ChecklistCambioAccesorios { get; set; }
    }
}
