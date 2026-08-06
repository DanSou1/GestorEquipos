using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.Desktop
{
    public class DesktopCreateViewModel
    {
        [Required(ErrorMessage = "El nombre del equipo es obligatorio.")]
        [MaxLength(50)]
        public string NameDesktop { get; set; } = string.Empty;

        [Required(ErrorMessage = "El serial es obligatorio.")]
        [MaxLength(50)]
        public string SerialNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "La marca es obligatoria.")]
        [MaxLength(100)]
        public string Brand { get; set; } = string.Empty;

        [Required(ErrorMessage = "El modelo es obligatorio.")]
        [MaxLength(100)]
        public string Model { get; set; } = string.Empty;

        [Required(ErrorMessage = "El procesador es obligatorio.")]
        [MaxLength(200)]
        public string Processor { get; set; } = string.Empty;

        [Required(ErrorMessage = "El disco es obligatorio.")]
        [MaxLength(100)]
        public string Disk { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona el sistema operativo.")]
        public int OSVersionId { get; set; }

        [Required(ErrorMessage = "Selecciona la RAM.")]
        public int RamId { get; set; }

        public int? RemoteId { get; set; }
    }
}
