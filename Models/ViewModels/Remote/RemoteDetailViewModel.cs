namespace GestorEquipos.Models.ViewModels.Remote
{
    public class RemoteDetailViewModel
    {
        public int Id { get; set; }
        public RemoteConnectionType ConnectionType { get; set; }
        public string? IPAddress { get; set; }
        public string? Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? AppDescription { get; set; }
        public string Estado { get; set; } = string.Empty;
        public List<AssignedDesktopItem> AssignedDesktops { get; set; } = new();
    }
}
