using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public class Users
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MaxLength(200)]
        [EmailAddress]
        public string EmailTeams { get; set; } = string.Empty;

        [ForeignKey("Area")]
        public int? AreaId { get; set; }

        [ForeignKey("Regional")]
        public int? RegionalId { get; set; }

        public Area? Area { get; set; }
        public Regional? Regional { get; set; }
        public ICollection<Asignation> Asignations { get; set; } = new List<Asignation>();
        public ICollection<UserSystem> UserSystems { get; set; } = new List<UserSystem>();

        public bool Activo { get; set; } = true;
        public DateOnly? DeactivatedAt { get; set; }
    }
}
