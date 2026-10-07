namespace GestorEquipos.Models.ViewModels.Maintenance
{
    public class MaintenanceCampaignIndexViewModel
    {
        public List<MaintenanceCampaignListItem> Pendientes { get; set; } = new();
        public List<MaintenanceCampaignListItem> CorreoEnviado { get; set; } = new();
        public List<MaintenanceCampaignListItem> Realizados { get; set; } = new();
    }

    public class MaintenanceCampaignListItem
    {
        public int Id { get; set; }
        public int DesktopId { get; set; }
        public string DesktopName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public string PeriodLabel => PeriodMonth == 6 ? $"Junio {PeriodYear}" : $"Diciembre {PeriodYear}";
        public bool EsAtrasado { get; set; }
        public string? HolderName { get; set; }
        public DateTime? EmailSentAtUtc { get; set; }
        public DateTime? SignedPdfUploadedAtUtc { get; set; }
    }
}
