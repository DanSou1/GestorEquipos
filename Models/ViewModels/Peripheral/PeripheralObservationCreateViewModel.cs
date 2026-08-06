using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.Peripheral
{
    public class PeripheralObservationCreateViewModel
    {
        [Required]
        public DateOnly Date { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de novedad.")]
        public PeripheralObservationType Type { get; set; }

        [Required(ErrorMessage = "La observación es obligatoria.")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        // Solo aplica cuando Type == Cambio, para indicar explícitamente el Estado resultante.
        // En Reparacion/BajaRAES el servicio decide el Estado automáticamente.
        public PeripheralEstado? ResultingEstado { get; set; }
    }
}
