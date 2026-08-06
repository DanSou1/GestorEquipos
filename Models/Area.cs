using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    public class Area
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }
    }
}
