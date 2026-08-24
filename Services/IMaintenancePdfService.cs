using GestorEquipos.Models.ViewModels.Maintenance;

namespace Gestor_Equipos.Services
{
    public interface IMaintenancePdfService
    {
        byte[] GenerateMaintenancePdf(MaintenancePdfViewModel detail);
    }
}
