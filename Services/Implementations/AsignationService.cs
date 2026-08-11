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
            // active holder. Any other assignment follows the normal rules below.
            var raesUser = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == AuthBootstrapper.RaesUserEmail);
            var isRaesAssignment = raesUser is not null && userId == raesUser.Id;

            if (!desktop.Estado && !isRaesAssignment)
            {
                throw new InvalidOperationException("No se puede asignar un equipo dado de baja (RAES).");
            }

            var currentAsignation = await _dbContext.Asignations
                .Where(a => a.DesktopId == desktopId)
                .Include(a => a.User)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            if (!isRaesAssignment && currentAsignation is not null && currentAsignation.User.Activo && currentAsignation.UserId != userId)
            {
                throw new InvalidOperationException(
                    $"Este equipo ya está asignado a {currentAsignation.User.Name} {currentAsignation.User.LastName}. " +
                    "Debe quedar Disponible antes de asignarlo a otra persona.");
            }

            var asignation = new Asignation
            {
                DesktopId = desktopId,
                UserId = userId,
                DateAsignation = DateOnly.FromDateTime(DateTime.Now)
            };

            _dbContext.Asignations.Add(asignation);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<Asignation>> GetHistoryAsync(int desktopId)
        {
            return await _dbContext.Asignations
                .Where(a => a.DesktopId == desktopId)
                .Include(a => a.User).ThenInclude(u => u.Area)
                .Include(a => a.User).ThenInclude(u => u.Regional)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .ToListAsync();
        }
    }
}
