using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.TipoCaso;

namespace Gestor_Equipos.Services
{
    public interface ITipoCasoService
    {
        Task<List<TipoCaso>> GetAllAsync();
        Task<TipoCaso?> GetByIdAsync(int id);
        Task<int> CreateAsync(TipoCasoCreateViewModel vm);
        Task UpdateAsync(int id, TipoCasoEditViewModel vm);
        Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword);
    }
}
