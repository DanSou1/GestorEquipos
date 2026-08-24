using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace GestorEquipos.Models.ViewModels.Remote
{
    public class RemoteEditViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "Selecciona el tipo de conexión remota.")]
        public RemoteConnectionType ConnectionType { get; set; }

        [MaxLength(50)]
        public string? IPAddress { get; set; }

        [MaxLength(10)]
        public string? Port { get; set; }

        [MaxLength(150)]
        public string? Username { get; set; }

        [MaxLength(200)]
        public string? Password { get; set; }

        [MaxLength(200)]
        public string? AppDescription { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ConnectionType == RemoteConnectionType.Aplicativo)
            {
                if (string.IsNullOrWhiteSpace(AppDescription))
                {
                    yield return new ValidationResult(
                        "Ingresa el nombre del aplicativo o la URL.",
                        new[] { nameof(AppDescription) });
                }

                yield break;
            }

            // EscritorioRemotoWindows
            if (string.IsNullOrWhiteSpace(IPAddress))
            {
                yield return new ValidationResult("La IP es obligatoria.", new[] { nameof(IPAddress) });
            }
            else if (!Regex.IsMatch(IPAddress, @"^(\d{1,3}\.){3}\d{1,3}$"))
            {
                yield return new ValidationResult("Ingresa una IP válida (ej. 10.0.0.5).", new[] { nameof(IPAddress) });
            }

            if (string.IsNullOrWhiteSpace(Port))
            {
                yield return new ValidationResult("El puerto es obligatorio.", new[] { nameof(Port) });
            }
            else if (!Regex.IsMatch(Port, @"^\d{1,5}$"))
            {
                yield return new ValidationResult("El puerto debe ser numérico.", new[] { nameof(Port) });
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                yield return new ValidationResult(
                    "El usuario es obligatorio (ej. NOMBREEQUIPO\\usuario).",
                    new[] { nameof(Username) });
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                yield return new ValidationResult("La clave es obligatoria.", new[] { nameof(Password) });
            }
        }
    }
}
