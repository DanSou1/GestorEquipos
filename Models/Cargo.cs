using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    public class Cargo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public ICollection<CargoAssignment> Assignments { get; set; } = new List<CargoAssignment>();
    }
}
