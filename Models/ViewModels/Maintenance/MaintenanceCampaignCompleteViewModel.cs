using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.Maintenance
{
    // Formulario de "Completar y enviar al correo" del módulo de Mantenimientos Programados.
    // A propósito NO incluye DesktopId ni Category: el controller/servicio los fija a partir
    // del MaintenanceCampaignItem server-side, nunca desde lo que mande el cliente.
    public class MaintenanceCampaignCompleteViewModel
    {
        [Required]
        public int CampaignItemId { get; set; }

        public string DesktopName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;

        [Required]
        public DateOnly Date { get; set; }

        [Required(ErrorMessage = "La descripción/observación es obligatoria.")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el nombre del técnico.")]
        [MaxLength(200)]
        public string TechnicianName { get; set; } = string.Empty;

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
