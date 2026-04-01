using System.ComponentModel.DataAnnotations;

namespace Gestor_Equipos.Models
{
    public class User
    {
        [Required]
        public int id { get; set; }
        [Required]
        [EmailAddress]
        public UserLogin email { get; set; }
        [Required]
        public UserLogin password_hash { get; set; }
        [Required]
        public bool is_active { get; set; }
        [Required]
        public Rol id_rol { get; set; }

    }
}
