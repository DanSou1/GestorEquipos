using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.MaintenanceType
{
    public class MaintenanceTypeEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(100)]
        public string Type { get; set; } = string.Empty;
    }
}
