using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.PeripheralMaintenance;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class PeripheralMaintenanceService : IPeripheralMaintenanceService
    {
        private readonly MyDbContext _dbContext;

        public PeripheralMaintenanceService(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<int> CreateAsync(PeripheralMaintenanceCreateViewModel vm)
        {
            var maintenance = new PeripheralMaintenance
            {
                PeripheralId = vm.PeripheralId,
                MaintenanceTypeId = vm.MaintenanceTypeId,
                Date = vm.Date,
                Description = vm.Description,
                TechnicianName = vm.TechnicianName
            };

            _dbContext.PeripheralMaintenances.Add(maintenance);
            await _dbContext.SaveChangesAsync();
            return maintenance.Id;
        }

        public async Task<List<PeripheralMaintenance>> GetByPeripheralAsync(int peripheralId)
        {
            return await _dbContext.PeripheralMaintenances
                .Where(m => m.PeripheralId == peripheralId)
                .Include(m => m.MaintenanceType)
                .OrderByDescending(m => m.Date)
                .ToListAsync();
        }
    }
}
