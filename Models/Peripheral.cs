using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public enum PeripheralEstado
    {
        Activo,
        Inactivo,
        Raes
    }

    public class Peripheral
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Desktop")]
        public int DesktopId { get; set; }

        [Required]
        [ForeignKey("PeripheralType")]
        public int PeripheralTypeId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Brand { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Model { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Serial { get; set; }

        [Required]
        public PeripheralEstado Estado { get; set; } = PeripheralEstado.Activo;

        public Desktop Desktop { get; set; } = null!;
        public PeripheralType PeripheralType { get; set; } = null!;
        public ICollection<PeripheralObservation> Observations { get; set; } = new List<PeripheralObservation>();
    }
}
