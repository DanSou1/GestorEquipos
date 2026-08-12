using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    public class Ram
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Especification { get; set; }
    }
}
