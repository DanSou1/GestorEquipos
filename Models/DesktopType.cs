using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    public class DesktopType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Brand { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Model { get; set; } = string.Empty;

        public ICollection<Desktop> Desktops { get; set; } = new List<Desktop>();
    }
}
