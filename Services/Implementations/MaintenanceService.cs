using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;

namespace Gestor_Equipos.Services.Implementations
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly MyDbContext _dbContext;

        public MaintenanceService(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<int> CreateAsync(MaintenanceCreateViewModel vm)
        {
            var maintenance = new Maintenance
            {
                DesktopId = vm.DesktopId,
                MaintenanceTypeId = vm.MaintenanceTypeId,
                Category = vm.Category,
                Date = vm.Date,
                Description = vm.Description,
                TechnicianName = vm.TechnicianName
            };

            if (vm.Category == MaintenanceCategory.Preventivo)
            {
                maintenance.Checklist = new MaintenanceChecklist
                {
                    VerificacionSistemaMecanico = vm.ChecklistVerificacionSistemaMecanico,
                    VerificacionSistemaElectrico = vm.ChecklistVerificacionSistemaElectrico,
                    VerificacionSistemaSeguridad = vm.ChecklistVerificacionSistemaSeguridad,
                    SopladoInterno = vm.ChecklistSopladoInterno,
                    LavadoInterno = vm.ChecklistLavadoInterno,
                    Lubricacion = vm.ChecklistLubricacion,
                    AjusteTornilleria = vm.ChecklistAjusteTornilleria,
                    CambioAccesorios = vm.ChecklistCambioAccesorios
                };
            }

            _dbContext.Maintenances.Add(maintenance);
            await _dbContext.SaveChangesAsync();
            return maintenance.Id;
        }
    }
}
