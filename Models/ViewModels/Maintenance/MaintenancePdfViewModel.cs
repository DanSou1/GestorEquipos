namespace GestorEquipos.Models.ViewModels.Maintenance
{
    public class MaintenancePdfViewModel
    {
        public string DesktopName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public MaintenanceCategory Category { get; set; }
        public string MaintenanceTypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TechnicianName { get; set; } = string.Empty;

        public string CurrentUserName { get; set; } = "Sin asignar";
        public string CurrentAreaName { get; set; } = "-";

        public List<MaintenanceEquipmentRow> Equipment { get; set; } = new();
        public List<MaintenanceChecklistItem>? Checklist { get; set; }
    }

    public class MaintenanceEquipmentRow
    {
        public string Label { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Serial { get; set; }
    }

    public class MaintenanceChecklistItem
    {
        public string Label { get; set; } = string.Empty;
        public bool Checked { get; set; }
    }
}
