using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    // Firma guardada por una persona (no por una cuenta de login) para reutilizar sin tener que
    // dibujarla de nuevo en cada mantenimiento — una por persona, se sobrescribe al re-guardar.
    public class UserSavedSignature
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        public string SignatureData { get; set; } = string.Empty;

        [Required]
        public DateTime SavedAtUtc { get; set; }

        public Users User { get; set; } = null!;
    }
}
