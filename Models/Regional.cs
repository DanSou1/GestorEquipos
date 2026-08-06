using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    public class Regional
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(15)]
        public string Name { get; set; }
    }
}
