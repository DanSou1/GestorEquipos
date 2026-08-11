using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.Peripheral
{
    public class PeripheralEditViewModel
    {
        public int Id { get; set; }

        public int DesktopId { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de periférico.")]
        public int PeripheralTypeId { get; set; }

        [Required(ErrorMessage = "La marca es obligatoria.")]
        [MaxLength(100)]
        public string Brand { get; set; } = string.Empty;

        [Required(ErrorMessage = "El modelo es obligatorio.")]
        [MaxLength(100)]
        public string Model { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Serial { get; set; }
    }
}
