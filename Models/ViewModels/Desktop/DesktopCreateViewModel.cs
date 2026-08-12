using System.ComponentModel.DataAnnotations;
using GestorEquipos.Models;

namespace GestorEquipos.Models.ViewModels.Desktop
{
    public class DesktopCreateViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "El nombre del equipo es obligatorio.")]
        [MaxLength(50)]
        public string NameDesktop { get; set; } = string.Empty;

        [Required(ErrorMessage = "El serial es obligatorio.")]
        [MaxLength(50)]
        public string SerialNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "La marca es obligatoria.")]
        [MaxLength(100)]
        public string Brand { get; set; } = string.Empty;

        [Required(ErrorMessage = "El modelo es obligatorio.")]
        [MaxLength(100)]
        public string Model { get; set; } = string.Empty;

        [Required(ErrorMessage = "El procesador es obligatorio.")]
        [MaxLength(200)]
        public string Processor { get; set; } = string.Empty;

        [Required(ErrorMessage = "El disco es obligatorio.")]
        [MaxLength(100)]
        public string Disk { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona el sistema operativo.")]
        public int OSVersionId { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de RAM.")]
        public RamType RamType { get; set; }

        [Required(ErrorMessage = "Selecciona la RAM.")]
        public int RamId { get; set; }

        // "", "new", o el Id de un Remote existente como string.
        public string RemoteSelection { get; set; } = "";

        public RemoteConnectionType? NewRemoteConnectionType { get; set; }

        [MaxLength(50)]
        public string? NewRemoteIPAddress { get; set; }

        [MaxLength(10)]
        public string? NewRemotePort { get; set; }

        [MaxLength(150)]
        public string? NewRemoteUsername { get; set; }

        [MaxLength(200)]
        public string? NewRemotePassword { get; set; }

        [MaxLength(200)]
        public string? NewRemoteAppDescription { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (RemoteSelection != "new")
            {
                yield break;
            }

            if (NewRemoteConnectionType is null)
            {
                yield return new ValidationResult(
                    "Selecciona el tipo de conexión remota.",
                    new[] { nameof(NewRemoteConnectionType) });
                yield break;
            }

            if (NewRemoteConnectionType == RemoteConnectionType.Aplicativo)
            {
                if (string.IsNullOrWhiteSpace(NewRemoteAppDescription))
                {
                    yield return new ValidationResult(
                        "Ingresa el nombre del aplicativo o la URL.",
                        new[] { nameof(NewRemoteAppDescription) });
                }

                yield break;
            }

            // EscritorioRemotoWindows
            if (string.IsNullOrWhiteSpace(NewRemoteIPAddress))
            {
                yield return new ValidationResult("La IP es obligatoria.", new[] { nameof(NewRemoteIPAddress) });
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(NewRemoteIPAddress, @"^(\d{1,3}\.){3}\d{1,3}$"))
            {
                yield return new ValidationResult("Ingresa una IP válida (ej. 10.0.0.5).", new[] { nameof(NewRemoteIPAddress) });
            }

            if (string.IsNullOrWhiteSpace(NewRemotePort))
            {
                yield return new ValidationResult("El puerto es obligatorio.", new[] { nameof(NewRemotePort) });
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(NewRemotePort, @"^\d{1,5}$"))
            {
                yield return new ValidationResult("El puerto debe ser numérico.", new[] { nameof(NewRemotePort) });
            }

            if (string.IsNullOrWhiteSpace(NewRemoteUsername))
            {
                yield return new ValidationResult(
                    "El usuario es obligatorio (ej. NOMBREEQUIPO\\usuario).",
                    new[] { nameof(NewRemoteUsername) });
            }

            if (string.IsNullOrWhiteSpace(NewRemotePassword))
            {
                yield return new ValidationResult("La clave es obligatoria.", new[] { nameof(NewRemotePassword) });
            }
        }
    }
}
