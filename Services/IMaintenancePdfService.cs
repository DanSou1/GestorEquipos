using GestorEquipos.Models.ViewModels.Maintenance;

namespace Gestor_Equipos.Services
{
    public interface IMaintenancePdfService
    {
        byte[] GenerateMaintenancePdf(MaintenancePdfViewModel detail);

        // Acta en el formato oficial de la empresa ("E-FGA-07 Control Mantenimiento V04"),
        // usada por el módulo de Mantenimientos Programados (junio/diciembre).
        byte[] GenerateExroActaPdf(MaintenanceExroActaViewModel detail);
    }
}
