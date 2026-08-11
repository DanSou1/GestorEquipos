using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class PeripheralAssignmentService : IPeripheralAssignmentService
    {
        private readonly MyDbContext _dbContext;

        public PeripheralAssignmentService(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AssignAsync(int peripheralId, int userId)
        {
            var peripheral = await _dbContext.Peripherals.SingleOrDefaultAsync(p => p.Id == peripheralId)
                ?? throw new InvalidOperationException("Periférico no encontrado.");

            // Mirrors DesktopService.DeactivateAsync's RAES reassignment: that internal call must
            // still succeed regardless of the peripheral's retired state or previous active holder.
            var raesUser = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == AuthBootstrapper.RaesUserEmail);
            var isRaesAssignment = raesUser is not null && userId == raesUser.Id;

            if (!peripheral.Estado && !isRaesAssignment)
            {
                throw new InvalidOperationException("No se puede asignar un periférico dado de baja (RAES).");
            }

            var currentAssignment = await _dbContext.PeripheralAssignments
                .Where(a => a.PeripheralId == peripheralId)
                .Include(a => a.User)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            if (!isRaesAssignment && currentAssignment is not null && currentAssignment.User.Activo && currentAssignment.UserId != userId)
            {
                throw new InvalidOperationException(
                    $"Este periférico ya está asignado a {currentAssignment.User.Name} {currentAssignment.User.LastName}. " +
                    "Debe quedar Disponible antes de asignarlo a otra persona.");
            }

            var assignment = new PeripheralAssignment
            {
                PeripheralId = peripheralId,
                UserId = userId,
                DateAsignation = DateOnly.FromDateTime(DateTime.Now)
            };

            _dbContext.PeripheralAssignments.Add(assignment);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<PeripheralAssignment>> GetHistoryAsync(int peripheralId)
        {
            return await _dbContext.PeripheralAssignments
                .Where(a => a.PeripheralId == peripheralId)
                .Include(a => a.User).ThenInclude(u => u.Area)
                .Include(a => a.User).ThenInclude(u => u.Regional)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .ToListAsync();
        }
    }
}
