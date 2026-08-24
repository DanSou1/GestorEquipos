using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.MaintenanceType
{
    public class MaintenanceTypeCreateViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(100)]
        public string Type { get; set; } = string.Empty;
    }
}
