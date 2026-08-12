using GestorEquipos.Models;

namespace Gestor_Equipos.Services
{
    public interface ILocationService
    {
        Task<List<Area>> GetAllAreasAsync();
        Task<Area?> GetAreaByIdAsync(int id);
        Task<int> CreateAreaAsync(string name);
        Task UpdateAreaAsync(int id, string name);
        Task DeleteAreaAsync(int id, int actingAdminUserSystemId, string? adminPassword);

        Task<List<Regional>> GetAllRegionalsAsync();
        Task<Regional?> GetRegionalByIdAsync(int id);
        Task<int> CreateRegionalAsync(string name);
        Task UpdateRegionalAsync(int id, string name);
        Task DeleteRegionalAsync(int id, int actingAdminUserSystemId, string? adminPassword);
    }
}
