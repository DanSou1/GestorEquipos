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

    public enum MaintenanceSignatureMethod
    {
        Manuscrita = 0,
        FirmaGuardada = 1
    }

    // Un ítem por (Desktop activo, período programado). Se crea de forma perezosa en
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

        // Snapshot de quién recibió el correo con el link de firma (el titular asignado al
        // Desktop en el momento de CompleteAndSendAsync) — nunca se re-consulta: si el equipo
        // se reasigna después, quien tiene el link sigue siendo quien puede firmar. Nullable:
        // un ítem Pendiente todavía no tiene firmante asignado.
        [ForeignKey("AssignedSigner")]
        public int? AssignedSignerUsersId { get; set; }

        // Rellenado al firmar dentro de la plataforma (CorreoEnviado -> Realizado). La firma se
        // guarda como datos vectoriales de texto (JSON de trazos), no como imagen ni PDF — el
        // PDF se regenera a demanda (ver MaintenanceCampaignService.RegeneratePdfAsync).
        public string? SignatureData { get; set; }
        public MaintenanceSignatureMethod? SignatureMethod { get; set; }
        public DateTime? SignedAtUtc { get; set; }
        [ForeignKey("SignerUserSystem")]
        public int? SignerUserSystemId { get; set; }

        // Navigation properties
        public Desktop Desktop { get; set; } = null!;
        public Maintenance? Maintenance { get; set; }
        public Users? AssignedSigner { get; set; }
        public UserSystem? SignerUserSystem { get; set; }
    }
}
