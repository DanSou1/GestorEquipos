using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.TipoCaso
{
    public class TipoCasoEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;
    }
}
