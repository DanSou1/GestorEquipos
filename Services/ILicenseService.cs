using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.License;

namespace Gestor_Equipos.Services
{
    public interface ILicenseService
    {
        Task<int> AddAsync(LicenseCreateViewModel vm);
        Task<List<License>> GetByDesktopAsync(int desktopId);
    }
}
