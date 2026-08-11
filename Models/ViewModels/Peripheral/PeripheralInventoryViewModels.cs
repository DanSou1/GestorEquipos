namespace GestorEquipos.Models.ViewModels.Peripheral
{
    public class PeripheralInventoryItem
    {
        public int Id { get; set; }
        public int PeripheralTypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Serial { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int DesktopId { get; set; }
        public string DesktopName { get; set; } = string.Empty;
        public string CurrentHolderName { get; set; } = "Sin asignar";
    }

    public class PeripheralInventoryStatsViewModel
    {
        public int Total { get; set; }
        public int Activos { get; set; }
        public int Disponibles { get; set; }
        public int Raes { get; set; }
        public List<PeripheralTypeCountItem> ByType { get; set; } = new();
    }

    public class PeripheralTypeCountItem
    {
        public string TypeName { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
