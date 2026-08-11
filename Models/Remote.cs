using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    public enum RemoteConnectionType
    {
        Aplicativo,
        EscritorioRemotoWindows
    }

    public class Remote
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public RemoteConnectionType ConnectionType { get; set; }

        [MaxLength(50)]
        [RegularExpression(@"^(\d{1,3}\.){3}\d{1,3}$", ErrorMessage = "Ingresa una IP válida (ej. 10.0.0.5).")]
        public string? IPAddress { get; set; }

        [MaxLength(10)]
        [RegularExpression(@"^\d{1,5}$", ErrorMessage = "El puerto debe ser numérico.")]
        public string? Port { get; set; }

        [MaxLength(150)]
        public string? Username { get; set; }

        [MaxLength(200)]
        public string? Password { get; set; }

        [MaxLength(200)]
        public string? AppDescription { get; set; }
    }
}
