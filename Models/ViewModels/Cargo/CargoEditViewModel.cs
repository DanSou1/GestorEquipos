using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.Cargo
{
    public class CargoEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del cargo es obligatorio.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
