using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.TipoCaso
{
    public class TipoCasoCreateViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;
    }
}
