using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.PeripheralType
{
    public class PeripheralTypeCreateViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
