using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    public class OSVersion
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]

        public string TypeSO { get; set; }
        [Required]
        [MaxLength(15)]
        public string Version { get; set; }
    }
}
