using GestorEquipos.Models;

namespace Gestor_Equipos.Services
{
    public interface IAsignationService
    {
        Task AssignAsync(int desktopId, int userId);
        Task ReturnAsync(int desktopId);
        Task<List<Asignation>> GetHistoryAsync(int desktopId, CancellationToken cancellationToken = default);
    }
}
