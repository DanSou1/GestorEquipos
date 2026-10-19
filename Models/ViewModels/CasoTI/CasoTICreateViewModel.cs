using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.CasoTI
{
    public class CasoTICreateViewModel
    {
        [Required(ErrorMessage = "La fecha es obligatoria.")]
        public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Now);

        [Required(ErrorMessage = "El asunto es obligatorio.")]
        [MaxLength(150)]
        public string Asunto { get; set; } = string.Empty;

        // Ignorado para un Colaborador: el servicio fuerza PersonaUserId al propio actor
        // (nunca confiar en lo que mande el cliente). Requerido cuando crea Admin/Técnico.
        public int? PersonaUserId { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de caso.")]
        public int TipoCasoId { get; set; }

        [MaxLength(4000)]
        public string? Notas { get; set; }
    }
}
