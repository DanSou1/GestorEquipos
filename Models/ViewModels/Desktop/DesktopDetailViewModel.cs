namespace GestorEquipos.Models.ViewModels.Desktop
{
    public class DesktopDetailViewModel
    {
        public int Id { get; set; }
        public string NameDesktop { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Processor { get; set; } = string.Empty;
        public string Disk { get; set; } = string.Empty;
        public string OSVersionName { get; set; } = string.Empty;
        public string RamSpecification { get; set; } = string.Empty;
        public RemoteAccessItem? RemoteAccess { get; set; }
        public bool Estado { get; set; }

        public string CurrentUserName { get; set; } = "Sin asignar";
        public DateOnly? CurrentSinceDate { get; set; }
        public string CurrentAreaName { get; set; } = "-";
        public string CurrentRegionalName { get; set; } = "-";

        public List<AsignationHistoryItem> AsignationHistory { get; set; } = new();
        public List<PeripheralDetailItem> Peripherals { get; set; } = new();
        public List<MaintenanceHistoryItem> Maintenances { get; set; } = new();
        public List<LicenseItem> Licenses { get; set; } = new();
        public List<SpecChangeLogItem> SpecChangeLogs { get; set; } = new();
    }

    public class RemoteAccessItem
    {
        public RemoteConnectionType ConnectionType { get; set; }
        public string? IPAddress { get; set; }
        public string? Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? AppDescription { get; set; }
    }

    public class AsignationHistoryItem
    {
        public string UserName { get; set; } = string.Empty;
        public DateOnly DateAsignation { get; set; }
    }

    public class PeripheralDetailItem
    {
        public int Id { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Serial { get; set; }
        public string Estado { get; set; } = string.Empty;
    }

    public class MaintenanceHistoryItem
    {
        public DateOnly Date { get; set; }
        public string MaintenanceTypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TechnicianName { get; set; } = string.Empty;
    }

    public class LicenseItem
    {
        public string SoftwareType { get; set; } = string.Empty;
        public string? LicenseKey { get; set; }
        public bool NoLicense { get; set; }
    }

    public class SpecChangeLogItem
    {
        public DateOnly Date { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string ChangedByName { get; set; } = string.Empty;
    }
}
