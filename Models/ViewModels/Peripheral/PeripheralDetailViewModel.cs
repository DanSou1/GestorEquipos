namespace GestorEquipos.Models.ViewModels.Peripheral
{
    public class PeripheralDetailViewModel
    {
        public int Id { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Serial { get; set; }
        public string Estado { get; set; } = string.Empty;

        public int DesktopId { get; set; }
        public string DesktopName { get; set; } = string.Empty;

        public string CurrentUserName { get; set; } = "Sin asignar";
        public DateOnly? CurrentSinceDate { get; set; }
        public string CurrentAreaName { get; set; } = "-";
        public string CurrentRegionalName { get; set; } = "-";

        public List<PeripheralAssignmentHistoryItem> AssignmentHistory { get; set; } = new();
        public List<PeripheralMaintenanceHistoryItem> Maintenances { get; set; } = new();
    }

    public class PeripheralAssignmentHistoryItem
    {
        public string UserName { get; set; } = string.Empty;
        public DateOnly DateAsignation { get; set; }
    }

    public class PeripheralMaintenanceHistoryItem
    {
        public DateOnly Date { get; set; }
        public string MaintenanceTypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TechnicianName { get; set; } = string.Empty;
    }
}
