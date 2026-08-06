using GestorEquipos.Models;

namespace Gestor_Equipos.Services
{
    public interface IAsignationService
    {
        Task AssignAsync(int desktopId, int userId);
        Task<List<Asignation>> GetHistoryAsync(int desktopId);
    }
}
