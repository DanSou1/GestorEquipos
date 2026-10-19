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
        Task<int> CompleteAndSendAsync(int campaignItemId, MaintenanceCreateViewModel maintenanceVm, Func<int, string> buildSignUrl);

        Task<MaintenanceSignViewModel?> GetSignPromptAsync(int campaignItemId, int actingUsersId, CancellationToken cancellationToken = default);

        Task SignAsync(
            int campaignItemId, int actingUsersId, int actingUserSystemId, string signatureData,
            MaintenanceSignatureMethod method, bool saveSignature, CancellationToken cancellationToken = default);

        Task<byte[]?> RegeneratePdfAsync(int campaignItemId, CancellationToken cancellationToken = default);
    }
}
