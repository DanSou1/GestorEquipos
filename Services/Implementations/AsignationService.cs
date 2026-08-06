using Gestor_Equipos.Data;
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
                .Include(a => a.User)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .ToListAsync();
        }
    }
}
