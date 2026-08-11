using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.PeripheralMaintenance;

namespace Gestor_Equipos.Services
{
    public interface IPeripheralMaintenanceService
    {
        Task<int> CreateAsync(PeripheralMaintenanceCreateViewModel vm);
        Task<List<PeripheralMaintenance>> GetByPeripheralAsync(int peripheralId);
    }
}
