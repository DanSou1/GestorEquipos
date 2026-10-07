using GestorEquipos.Models.ViewModels.Desktop;

namespace Gestor_Equipos.Services
{
    public interface IDesktopService
    {
        Task<List<DesktopListViewModel>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<DesktopDetailViewModel?> GetDetailAsync(int desktopId, CancellationToken cancellationToken = default);
        Task<int> CreateAsync(DesktopCreateViewModel vm);
        Task UpdateSpecsAsync(int desktopId, DesktopEditViewModel vm, int changedByUserSystemId);
        Task DeactivateAsync(int desktopId);
        Task DeleteAsync(int desktopId);
        Task<EquipmentStatsViewModel> GetEquipmentStatsAsync(CancellationToken cancellationToken = default);
    }
}
