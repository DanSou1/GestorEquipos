using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.DesktopType;

namespace Gestor_Equipos.Services
{
    public interface IDesktopTypeService
    {
        Task<List<DesktopType>> GetAllAsync();
        Task<DesktopType?> GetByIdAsync(int id);
        Task<int> CreateAsync(DesktopTypeCreateViewModel vm);
        Task UpdateAsync(int id, DesktopTypeEditViewModel vm);
        Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword);
    }
}
