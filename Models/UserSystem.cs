using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public class UserSystem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [ForeignKey("Rol")]
        public int RolId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        public Rol Rol { get; set; } = null!;
        public Users User { get; set; } = null!;
    }
}
