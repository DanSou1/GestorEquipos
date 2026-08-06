using GestorEquipos.Models.ViewModels.Peripheral;

namespace Gestor_Equipos.Services
{
    public interface IPeripheralService
    {
        Task<int> AddAsync(PeripheralCreateViewModel vm);
        Task AddObservationAsync(int peripheralId, PeripheralObservationCreateViewModel vm);
    }
}
