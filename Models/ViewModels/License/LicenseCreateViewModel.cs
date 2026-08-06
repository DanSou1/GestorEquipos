using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.License
{
    public class LicenseCreateViewModel
    {
        [Required]
        public int DesktopId { get; set; }

        [Required(ErrorMessage = "El tipo/nombre del software es obligatorio.")]
        [MaxLength(150)]
        public string SoftwareType { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? LicenseKey { get; set; }

        public bool NoLicense { get; set; }
    }
}
