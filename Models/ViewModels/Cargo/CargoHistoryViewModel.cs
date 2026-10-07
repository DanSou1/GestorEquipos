namespace GestorEquipos.Models.ViewModels.Cargo
{
    public class CargoHistoryViewModel
    {
        public int CargoId { get; set; }
        public string CargoName { get; set; } = string.Empty;
        public List<CargoHistoryItem> Items { get; set; } = new();
    }

    public class CargoHistoryItem
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public bool IsCurrent => EndDate is null;
    }
}
