using GestorEquipos.Models.ViewModels.Desktop;

namespace Gestor_Equipos.Services
{
    public interface IDesktopService
    {
        Task<List<DesktopListViewModel>> GetAllAsync();
        Task<DesktopDetailViewModel?> GetDetailAsync(int desktopId);
        Task<int> CreateAsync(DesktopCreateViewModel vm);
        Task UpdateSpecsAsync(int desktopId, DesktopEditViewModel vm, int changedByUserSystemId);
        Task DeactivateAsync(int desktopId);
    }
}
