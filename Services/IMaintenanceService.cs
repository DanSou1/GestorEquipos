using GestorEquipos.Models.ViewModels.Maintenance;

namespace Gestor_Equipos.Services
{
    public interface IMaintenanceService
    {
        Task<int> CreateAsync(MaintenanceCreateViewModel vm);
    }
}
