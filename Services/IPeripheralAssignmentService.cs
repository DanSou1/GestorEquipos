using GestorEquipos.Models;

namespace Gestor_Equipos.Services
{
    public interface IPeripheralAssignmentService
    {
        Task AssignAsync(int peripheralId, int userId);
        Task<List<PeripheralAssignment>> GetHistoryAsync(int peripheralId);
    }
}
