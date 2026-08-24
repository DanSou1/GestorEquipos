using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Remote;

namespace Gestor_Equipos.Services
{
    public interface IRemoteService
    {
        Task<List<RemoteInventoryItem>> GetInventoryAsync();
        Task<RemoteInventoryStatsViewModel> GetInventoryStatsAsync();
        Task<RemoteDetailViewModel?> GetDetailAsync(int id);
        Task<Remote?> GetByIdAsync(int id);
        Task UpdateAsync(int id, RemoteEditViewModel vm);
        Task ReleaseAsync(int id);
        Task AssignToDesktopAsync(int remoteId, int desktopId);
    }
}
