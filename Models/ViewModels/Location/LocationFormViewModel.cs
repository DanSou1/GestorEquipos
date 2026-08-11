using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.Location
{
    public class LocationFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
