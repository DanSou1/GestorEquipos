using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    public class Remote
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string IPAddress { get; set; }
        [Required]
        [MaxLength(50)]
        public string Port { get; set; }
    }
}
