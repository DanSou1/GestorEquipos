using Gestor_Equipos.Data;
using GestorEquipos.Models;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class LocationService : ILocationService
    {
        private readonly MyDbContext _dbContext;

        public LocationService(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Area>> GetAllAreasAsync()
        {
            return await _dbContext.Areas.OrderBy(a => a.Name).ToListAsync();
        }

        public async Task<Area?> GetAreaByIdAsync(int id)
        {
            return await _dbContext.Areas.SingleOrDefaultAsync(a => a.Id == id);
        }

        public async Task<int> CreateAreaAsync(string name)
        {
            var trimmedName = name.Trim();

            if (await _dbContext.Areas.AnyAsync(a => a.Name.ToLower() == trimmedName.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un área con ese nombre.");
            }

            var area = new Area { Name = trimmedName };
            _dbContext.Areas.Add(area);
            await _dbContext.SaveChangesAsync();
            return area.Id;
        }

        public async Task UpdateAreaAsync(int id, string name)
        {
            var area = await _dbContext.Areas.SingleOrDefaultAsync(a => a.Id == id)
                ?? throw new InvalidOperationException("Área no encontrada.");

            var trimmedName = name.Trim();

            if (await _dbContext.Areas.AnyAsync(a => a.Id != id && a.Name.ToLower() == trimmedName.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un área con ese nombre.");
            }

            area.Name = trimmedName;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAreaAsync(int id)
        {
            var area = await _dbContext.Areas.SingleOrDefaultAsync(a => a.Id == id)
                ?? throw new InvalidOperationException("Área no encontrada.");

            if (await _dbContext.Users.AnyAsync(u => u.AreaId == id))
            {
                throw new InvalidOperationException("No se puede eliminar: hay usuarios asignados a esta área.");
            }

            _dbContext.Areas.Remove(area);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<Regional>> GetAllRegionalsAsync()
        {
            return await _dbContext.Regionals.OrderBy(r => r.Name).ToListAsync();
        }

        public async Task<Regional?> GetRegionalByIdAsync(int id)
        {
            return await _dbContext.Regionals.SingleOrDefaultAsync(r => r.Id == id);
        }

        public async Task<int> CreateRegionalAsync(string name)
        {
            var trimmedName = name.Trim();

            if (await _dbContext.Regionals.AnyAsync(r => r.Name.ToLower() == trimmedName.ToLower()))
            {
                throw new InvalidOperationException("Ya existe una regional con ese nombre.");
            }

            var regional = new Regional { Name = trimmedName };
            _dbContext.Regionals.Add(regional);
            await _dbContext.SaveChangesAsync();
            return regional.Id;
        }

        public async Task UpdateRegionalAsync(int id, string name)
        {
            var regional = await _dbContext.Regionals.SingleOrDefaultAsync(r => r.Id == id)
                ?? throw new InvalidOperationException("Regional no encontrada.");

            var trimmedName = name.Trim();

            if (await _dbContext.Regionals.AnyAsync(r => r.Id != id && r.Name.ToLower() == trimmedName.ToLower()))
            {
                throw new InvalidOperationException("Ya existe una regional con ese nombre.");
            }

            regional.Name = trimmedName;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteRegionalAsync(int id)
        {
            var regional = await _dbContext.Regionals.SingleOrDefaultAsync(r => r.Id == id)
                ?? throw new InvalidOperationException("Regional no encontrada.");

            if (await _dbContext.Users.AnyAsync(u => u.RegionalId == id))
            {
                throw new InvalidOperationException("No se puede eliminar: hay usuarios asignados a esta regional.");
            }

            _dbContext.Regionals.Remove(regional);
            await _dbContext.SaveChangesAsync();
        }
    }
}
