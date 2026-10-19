namespace GestorEquipos.Models.ViewModels.Maintenance
{
    // Datos para la pantalla de firma (MaintenanceCampaignController.Sign) — accesible solo
    // para la persona asignada como firmante del ítem (ver AssignedSignerUsersId).
    public class MaintenanceSignViewModel
    {
        public int CampaignItemId { get; set; }
        public string DesktopName { get; set; } = string.Empty;
        public bool HasSavedSignature { get; set; }
        public string? SavedSignatureData { get; set; }
    }
}
