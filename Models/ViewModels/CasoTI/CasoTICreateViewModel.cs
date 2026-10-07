using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.CasoTI
{
    public class CasoTICreateViewModel
    {
        [Required(ErrorMessage = "La fecha es obligatoria.")]
        public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Now);

        // Solo relevante cuando el Administrador crea el caso; si lo crea un Técnico,
        // el servicio ignora este valor y usa siempre al técnico autenticado.
        public int? TecnicoUserSystemId { get; set; }

        [Required(ErrorMessage = "Selecciona la persona que recibió el soporte.")]
        public int PersonaUserId { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de caso.")]
        public int TipoCasoId { get; set; }

        [MaxLength(4000)]
        public string? Notas { get; set; }
    }
}
