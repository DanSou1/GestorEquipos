using System.ComponentModel.DataAnnotations;

namespace Gestor_Equipos.Models
{
    public class UserLogin
    {
        [Required]
        [EmailAddress]
        public string email { get; set; }
        [Required]
        public string passwordHash { get; set; }
    }
}
