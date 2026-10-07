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

            // LOG-04: see the matching comment on AsignationService.AssignAsync — the dropdown
            // only offers active people, but the server must validate it too.
            if (!isRaesAssignment)
            {
                var targetUser = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId)
                    ?? throw new InvalidOperationException("La persona seleccionada no existe.");
                if (!targetUser.Activo)
                {
                    throw new InvalidOperationException("No se puede asignar el periférico a una persona inactiva.");
                }
            }

            // LOG-05: see the matching comment on AsignationService.AssignAsync.
            async Task CheckAndInsertAsync()
            {
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

                _dbContext.PeripheralAssignments.Add(new PeripheralAssignment
                {
                    PeripheralId = peripheralId,
                    UserId = userId,
                    DateAsignation = DateOnly.FromDateTime(DateTime.Now)
                });
                await _dbContext.SaveChangesAsync();
            }

            if (_dbContext.Database.CurrentTransaction is not null)
            {
                await CheckAndInsertAsync();
                return;
            }

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                await CheckAndInsertAsync();
                await transaction.CommitAsync();
            });
        }

        public async Task<List<PeripheralAssignment>> GetHistoryAsync(int peripheralId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.PeripheralAssignments
                .Where(a => a.PeripheralId == peripheralId)
                .Include(a => a.User).ThenInclude(u => u.Area)
                .Include(a => a.User).ThenInclude(u => u.Regional)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
