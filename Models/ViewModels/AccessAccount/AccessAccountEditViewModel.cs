using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.AccessAccount
{
    public class AccessAccountEditViewModel
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

        [Required(ErrorMessage = "Selecciona el área.")]
        public int AreaId { get; set; }

        [Required(ErrorMessage = "Selecciona la regional.")]
        public int RegionalId { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [MaxLength(200)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona el rol.")]
        public int RolId { get; set; }

        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "La confirmación no coincide con la nueva contraseña.")]
        public string? ConfirmPassword { get; set; }

        [DataType(DataType.Password)]
        public string? AdminPassword { get; set; }
    }
}
