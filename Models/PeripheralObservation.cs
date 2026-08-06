using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public enum PeripheralObservationType
    {
        Reparacion,
        Cambio,
        BajaRAES
    }

    public class PeripheralObservation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Peripheral")]
        public int PeripheralId { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        [Required]
        public PeripheralObservationType Type { get; set; }

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public Peripheral Peripheral { get; set; } = null!;
    }
}
