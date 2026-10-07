using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.CasoTI
{
    public class CasoTIRatingSubmitViewModel
    {
        [Required]
        public Guid Token { get; set; }

        [Required(ErrorMessage = "Selecciona una calificación.")]
        [Range(1, 5, ErrorMessage = "La calificación debe estar entre 1 y 5.")]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }
}
