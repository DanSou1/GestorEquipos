using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public class License
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Desktop")]
        public int DesktopId { get; set; }

        [Required]
        [MaxLength(150)]
        public string SoftwareType { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? LicenseKey { get; set; }

        [Required]
        public bool NoLicense { get; set; } = false;

        public Desktop Desktop { get; set; } = null!;
    }
}
