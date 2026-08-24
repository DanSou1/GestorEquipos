using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorEquipos.Models
{
    public class MaintenanceChecklist
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [ForeignKey("Maintenance")]
        public int MaintenanceId { get; set; }

        public bool VerificacionSistemaMecanico { get; set; }
        public bool VerificacionSistemaElectrico { get; set; }
        public bool VerificacionSistemaSeguridad { get; set; }
        public bool SopladoInterno { get; set; }
        public bool LavadoInterno { get; set; }
        public bool Lubricacion { get; set; }
        public bool AjusteTornilleria { get; set; }
        public bool CambioAccesorios { get; set; }

        // Navigation property
        public Maintenance Maintenance { get; set; }
    }
}
