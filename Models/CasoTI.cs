using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public enum CasoTIEstado
    {
        Abierto = 0,
        Resuelto = 1
    }

    // Caso de soporte TI atendido por un técnico. AreaSnapshot se copia de Users.Area?.Name
    // al crear el caso (nunca se vuelve a consultar) — mismo criterio que
    // Maintenance.SnapshotHolderArea, para que un cambio posterior de área de la persona no
    // reescriba en silencio el historial de casos ya registrados.
    public class CasoTI
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateOnly Fecha { get; set; }

        // Nullable: todo caso nace sin técnico asignado (sin importar quién lo crea) y solo se
        // llena cuando alguien lo resuelve (ver CasoTIService.ResolveAsync) — así "cualquier
        // técnico puede entrar y resolverlo" no depende de quién lo haya registrado.
        [ForeignKey("Tecnico")]
        public int? TecnicoUserSystemId { get; set; }

        [Required]
        [ForeignKey("Persona")]
        public int PersonaUserId { get; set; }

        [MaxLength(150)]
        public string? AreaSnapshot { get; set; }

        // Nullable en BD para no inventar datos falsos en casos ya existentes antes de este
        // campo; CasoTICreateViewModel.Asunto sí lo exige para casos nuevos.
        [MaxLength(150)]
        public string? Asunto { get; set; }

        [Required]
        [ForeignKey("TipoCaso")]
        public int TipoCasoId { get; set; }

        [MaxLength(4000)]
        public string? Notas { get; set; }

        [Required]
        public CasoTIEstado Estado { get; set; } = CasoTIEstado.Abierto;

        [Required]
        public DateTime CreatedAtUtc { get; set; }

        public DateTime? ResolvedAtUtc { get; set; }

        // Generado al resolver; se "gasta" (RatingSubmittedAtUtc) con el primer envío del
        // formulario público de calificación — ver CasoTIRatingController.
        public Guid? RatingToken { get; set; }
        public DateTime? RatingSubmittedAtUtc { get; set; }

        [Range(1, 5)]
        public int? RatingValue { get; set; }

        [MaxLength(1000)]
        public string? RatingComment { get; set; }

        // Navigation properties
        public UserSystem? Tecnico { get; set; }
        public Users Persona { get; set; } = null!;
        public TipoCaso TipoCaso { get; set; } = null!;
    }
}
