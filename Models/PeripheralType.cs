using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    public class PeripheralType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public ICollection<Peripheral> Peripherals { get; set; } = new List<Peripheral>();
    }
}
