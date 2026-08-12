using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public enum RamType
    {
        DDR2,
        DDR3,
        DDR4,
        DDR5
    }

    public class Desktop
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string NameDesktop { get; set; }
        [MaxLength(50)]
        [Required]
        public string SerialNumber { get; set; }
        [Required]
        [MaxLength(100)]
        public string Brand { get; set; }
        [Required]
        [MaxLength(100)]
        public string Model { get; set; }
        [Required]
        [MaxLength(200)]
        public string Processor { get; set; }
        [Required]
        [MaxLength(100)]
        public string Disk { get; set; }
        [Required]
        [ForeignKey("OSVersion")]
        public int OSVersionId { get; set; }
        [Required]
        [ForeignKey("Ram")]
        public int RamId { get; set; }
        [Required]
        public RamType RamType { get; set; }
        [ForeignKey("Remote")]
        public int? RemoteId { get; set; }
        [Required]
        public bool Estado { get; set; } = true;

        // Navigation properties
        public OSVersion OSVersion { get; set; }
        public Ram Ram { get; set; }
        public Remote Remote { get; set; }
        public ICollection<Asignation> Asignations { get; set; } = new List<Asignation>();
        public ICollection<Maintenance> Maintenances { get; set; } = new List<Maintenance>();
        public ICollection<Peripheral> Peripherals { get; set; } = new List<Peripheral>();
        public ICollection<License> Licenses { get; set; } = new List<License>();
        public ICollection<SpecChangeLog> SpecChangeLogs { get; set; } = new List<SpecChangeLog>();
    }
}