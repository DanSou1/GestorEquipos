using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;

namespace Gestor_Equipos.Services
{
    public interface IMaintenanceCampaignService
    {
        // referenceDate solo existe para permitir tests deterministas de la lógica de
        // apertura de períodos; en producción siempre se llama sin argumento (usa hoy).
        Task SyncOpenPeriodsAsync(DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
        Task<MaintenanceCampaignIndexViewModel> GetIndexAsync(bool onlyCompleted, CancellationToken cancellationToken = default);
        Task<MaintenanceCampaignItem?> GetByIdAsync(int campaignItemId, CancellationToken cancellationToken = default);
        Task<int> CompleteAndSendAsync(int campaignItemId, MaintenanceCreateViewModel maintenanceVm);
        Task UploadSignedPdfAsync(int campaignItemId, Stream fileStream, string fileName, int uploadedByUserSystemId);
        Task<(byte[] Content, string FileName)?> GetSignedPdfAsync(int campaignItemId, CancellationToken cancellationToken = default);
    }
}
