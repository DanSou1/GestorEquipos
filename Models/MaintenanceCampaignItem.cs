using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public enum MaintenanceCampaignStatus
    {
        Pendiente = 0,
        CorreoEnviado = 1,
        Realizado = 2
    }

    // Un ítem por (Desktop activo, período obligatorio). Se crea de forma perezosa en
    // MaintenanceCampaignService.SyncOpenPeriodsAsync la primera vez que alguien visita el
    // módulo tras abrirse el período (día 1 de junio/diciembre) — no hay background job.
    // Una fila nunca se borra ni se duplica para el mismo (Desktop, Año, Mes): así un ítem
    // que no se completa en su mes sigue apareciendo "atrasado" en los meses siguientes.
    public class MaintenanceCampaignItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Desktop")]
        public int DesktopId { get; set; }

        [Required]
        public int PeriodYear { get; set; }

        [Required]
        public int PeriodMonth { get; set; } // 6 o 12 únicamente

        [Required]
        public MaintenanceCampaignStatus Status { get; set; } = MaintenanceCampaignStatus.Pendiente;

        [Required]
        public DateTime CreatedAtUtc { get; set; }

        // Rellenado al completar el formulario (Pendiente -> CorreoEnviado)
        [ForeignKey("Maintenance")]
        public int? MaintenanceId { get; set; }
        public DateTime? EmailSentAtUtc { get; set; }
        [MaxLength(200)]
        public string? EmailSentToAddress { get; set; }

        // Rellenado al subir el PDF firmado (CorreoEnviado -> Realizado)
        [MaxLength(260)]
        public string? SignedPdfFileName { get; set; }
        public byte[]? SignedPdfContent { get; set; }
        public DateTime? SignedPdfUploadedAtUtc { get; set; }
        [ForeignKey("SignedPdfUploadedByUserSystem")]
        public int? SignedPdfUploadedByUserSystemId { get; set; }

        // Navigation properties
        public Desktop Desktop { get; set; } = null!;
        public Maintenance? Maintenance { get; set; }
        public UserSystem? SignedPdfUploadedByUserSystem { get; set; }
    }
}
