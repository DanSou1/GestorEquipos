using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.MaintenanceType;

namespace Gestor_Equipos.Services
{
    public interface IMaintenanceTypeService
    {
        Task<List<MaintenanceType>> GetAllAsync();
        Task<MaintenanceType?> GetByIdAsync(int id);
        Task<int> CreateAsync(MaintenanceTypeCreateViewModel vm);
        Task UpdateAsync(int id, MaintenanceTypeEditViewModel vm);
        Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword);
    }
}
