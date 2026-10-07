using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Cargo;

namespace Gestor_Equipos.Services
{
    public interface ICargoService
    {
        Task<List<Cargo>> GetAllAsync();
        Task<Cargo?> GetByIdAsync(int id);
        Task<int> CreateAsync(string name);
        Task UpdateAsync(int id, string name);
        Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword);
        Task<CargoHistoryViewModel?> GetHistoryAsync(int cargoId);
    }
}
