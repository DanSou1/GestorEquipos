using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Microsoft.EntityFrameworkCore;

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
                Date = vm.Date,
                Description = vm.Description,
                TechnicianName = vm.TechnicianName
            };

            _dbContext.Maintenances.Add(maintenance);
            await _dbContext.SaveChangesAsync();
            return maintenance.Id;
        }

        public async Task<List<Maintenance>> GetByDesktopAsync(int desktopId)
        {
            return await _dbContext.Maintenances
                .Where(m => m.DesktopId == desktopId)
                .Include(m => m.MaintenanceType)
                .OrderByDescending(m => m.Date)
                .ToListAsync();
        }
    }
}
