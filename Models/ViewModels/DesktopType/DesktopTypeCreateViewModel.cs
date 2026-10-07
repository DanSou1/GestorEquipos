using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.DesktopType
{
    public class DesktopTypeCreateViewModel
    {
        [Required(ErrorMessage = "La marca es obligatoria.")]
        [MaxLength(100)]
        public string Brand { get; set; } = string.Empty;

        [Required(ErrorMessage = "El modelo es obligatorio.")]
        [MaxLength(100)]
        public string Model { get; set; } = string.Empty;
    }
}
