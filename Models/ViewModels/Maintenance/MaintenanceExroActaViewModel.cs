namespace GestorEquipos.Models.ViewModels.Maintenance
{
    // Datos para el acta "E-FGA-07 Control Mantenimiento V04" del módulo de Mantenimientos
    // programados — formato oficial de la empresa, distinto del PDF genérico que ya genera
    // MaintenancePdfService.GenerateMaintenancePdf.
    public class MaintenanceExroActaViewModel
    {
        public DateOnly Date { get; set; }
        public string UsuarioNombre { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public List<MaintenanceEquipmentRow> Equipment { get; set; } = new();
        public string DescripcionFalla { get; set; } = string.Empty;
        public List<MaintenanceChecklistItem> Checklist { get; set; } = new();
        public string TechnicianName { get; set; } = string.Empty;
        public string FileNameSafeUserName { get; set; } = string.Empty;

        // Null hasta que la persona firme (ver MaintenanceCampaignService.SignAsync) — mientras
        // tanto, MaintenancePdfService.AddExroFirmas dibuja la línea en blanco de siempre.
        public string? SignatureData { get; set; }
        public string? SignerName { get; set; }
        public DateTime? SignedAtUtc { get; set; }
    }
}
