namespace GestorEquipos.Models.ViewModels.Desktop
{
    public class DesktopListViewModel
    {
        public int Id { get; set; }
        public string NameDesktop { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string AreaName { get; set; } = string.Empty;
        public string RegionalName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
