using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    public class TipoCaso
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        public ICollection<CasoTI> Casos { get; set; } = new List<CasoTI>();
    }
}
