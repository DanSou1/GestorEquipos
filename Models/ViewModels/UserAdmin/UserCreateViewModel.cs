using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.UserAdmin
{
    public class UserCreateViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        [MaxLength(200)]
        public string? EmailTeams { get; set; }

        public int? AreaId { get; set; }

        public int? RegionalId { get; set; }
    }
}
