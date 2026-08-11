using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
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
        public bool Estado { get; set; } = true;

        public Desktop Desktop { get; set; } = null!;
        public PeripheralType PeripheralType { get; set; } = null!;
        public ICollection<PeripheralAssignment> Assignments { get; set; } = new List<PeripheralAssignment>();
        public ICollection<PeripheralMaintenance> Maintenances { get; set; } = new List<PeripheralMaintenance>();
    }
}
