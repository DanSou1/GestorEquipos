using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.PeripheralType;

namespace Gestor_Equipos.Services
{
    public interface IPeripheralTypeService
    {
        Task<List<PeripheralType>> GetAllAsync();
        Task<PeripheralType?> GetByIdAsync(int id);
        Task<int> CreateAsync(PeripheralTypeCreateViewModel vm);
        Task UpdateAsync(int id, PeripheralTypeEditViewModel vm);
        Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword);
    }
}
