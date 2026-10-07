using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Peripheral;

namespace Gestor_Equipos.Services
{
    public interface IPeripheralService
    {
        Task<int> AddAsync(PeripheralCreateViewModel vm);
        Task<Peripheral?> GetByIdAsync(int id);
        Task<PeripheralDetailViewModel?> GetDetailAsync(int id, CancellationToken cancellationToken = default);
        Task UpdateAsync(int id, PeripheralEditViewModel vm);
        Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword);
        Task RetireToRaesAsync(int id);
        Task<List<PeripheralInventoryItem>> GetInventoryAsync(CancellationToken cancellationToken = default);
        Task<PeripheralInventoryStatsViewModel> GetInventoryStatsAsync(CancellationToken cancellationToken = default);
    }
}
