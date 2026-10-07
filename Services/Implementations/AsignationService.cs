using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class AsignationService : IAsignationService
    {
        private readonly MyDbContext _dbContext;

        public AsignationService(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AssignAsync(int desktopId, int userId)
        {
            var desktop = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == desktopId)
                ?? throw new InvalidOperationException("Equipo no encontrado.");

            // DesktopService.DeactivateAsync sets Estado=false and then reassigns to the RAES
            // pseudo-user as the terminal step of its existing retirement flow — that internal
            // call must still succeed regardless of the desktop's retired state or its previous
            // active holder. UX-06's "Devolver equipo" reuses the same bypass to reassign to the
            // "sin asignar" pseudo-user, ending the current active holder's hold without
            // deactivating them — the pseudo-user's Activo=false makes every existing "current
            // holder inactive => Disponible" query treat it correctly for free. Any other
            // assignment follows the normal rules below.
            var isSystemAssignment = await _dbContext.Users
                .AnyAsync(u => u.Id == userId && (u.Email == AuthBootstrapper.RaesUserEmail || u.Email == AuthBootstrapper.UnassignedUserEmail));

            if (!desktop.Estado && !isSystemAssignment)
            {
                throw new InvalidOperationException("No se puede asignar un equipo dado de baja (RAES).");
            }

            // LOG-04: the assignment dropdown only lists active people, but the server must not
            // trust that — a manipulated request (or a stale page) could still name someone the
            // UI would never have offered.
            if (!isSystemAssignment)
            {
                var targetUser = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId)
                    ?? throw new InvalidOperationException("La persona seleccionada no existe.");
                if (!targetUser.Activo)
                {
                    throw new InvalidOperationException("No se puede asignar el equipo a una persona inactiva.");
                }
            }

            // LOG-05: direct reassignment from one active holder to another is allowed —
            // Asignation is append-only (no IsCurrent/EndDate column), so a user's hold on a
            // desktop simply ends the moment a newer Asignation row exists for that desktop;
            // GetHistoryAsync always treats the latest row as the current holder. No separate
            // "Disponible" row is inserted for the previous holder — this single insert is the
            // whole operation. Still wrapped in Serializable isolation (via CreateExecutionStrategy)
            // so it can run inline inside DesktopService.DeactivateAsync's own transaction (the
            // RAES retirement path) without nesting a second BeginTransactionAsync on the same
            // context.
            async Task InsertAsync()
            {
                _dbContext.Asignations.Add(new Asignation
                {
                    DesktopId = desktopId,
                    UserId = userId,
                    DateAsignation = DateOnly.FromDateTime(DateTime.Now)
                });
                await _dbContext.SaveChangesAsync();
            }

            if (_dbContext.Database.CurrentTransaction is not null)
            {
                await InsertAsync();
                return;
            }

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                await InsertAsync();
                await transaction.CommitAsync();
            });
        }

        // UX-06: lets an active holder return a single equipo without the collateral damage of
        // deactivating them (which would also release every *other* equipo/periférico they hold —
        // see AsignationService.AssignAsync's own comment, and DesktopService.DeactivateAsync for
        // the unrelated RAES retirement flow this deliberately does not touch).
        public async Task ReturnAsync(int desktopId)
        {
            var currentAsignation = await _dbContext.Asignations
                .Where(a => a.DesktopId == desktopId)
                .Include(a => a.User)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            if (currentAsignation is null || !currentAsignation.User.Activo)
            {
                throw new InvalidOperationException("Este equipo ya está Disponible.");
            }

            var unassignedUser = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == AuthBootstrapper.UnassignedUserEmail)
                ?? throw new InvalidOperationException("No se encontró el usuario reservado 'Disponible'; no se puede devolver el equipo.");

            await AssignAsync(desktopId, unassignedUser.Id);
        }

        public async Task<List<Asignation>> GetHistoryAsync(int desktopId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Asignations
                .Where(a => a.DesktopId == desktopId)
                .Include(a => a.User).ThenInclude(u => u.Area)
                .Include(a => a.User).ThenInclude(u => u.Regional)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
