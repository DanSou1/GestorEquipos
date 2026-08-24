namespace GestorEquipos.Models.ViewModels.Remote
{
    public class RemoteInventoryItem
    {
        public int Id { get; set; }
        public RemoteConnectionType ConnectionType { get; set; }
        public string? IPAddress { get; set; }
        public string? Port { get; set; }
        public string? Username { get; set; }
        public string? AppDescription { get; set; }
        public string Estado { get; set; } = string.Empty;
        public List<AssignedDesktopItem> AssignedDesktops { get; set; } = new();
    }

    public class AssignedDesktopItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class RemoteInventoryStatsViewModel
    {
        public int Total { get; set; }
        public int Asignados { get; set; }
        public int Disponibles { get; set; }
    }
}
