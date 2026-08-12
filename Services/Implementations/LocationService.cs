using Gestor_Equipos.Data;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class LocationService : ILocationService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;

        public LocationService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        private async Task VerifyAdminPasswordAsync(int actingAdminUserSystemId, string? adminPassword)
        {
            if (string.IsNullOrEmpty(adminPassword))
            {
                throw new InvalidOperationException("Debes ingresar tu contraseña de administrador para eliminar.");
            }

            var adminUserSystem = await _dbContext.UserSystems.SingleOrDefaultAsync(us => us.Id == actingAdminUserSystemId)
                ?? throw new InvalidOperationException("No se pudo verificar la cuenta del administrador.");

            var verificationResult = _passwordHasher.VerifyHashedPassword(adminUserSystem, adminUserSystem.PasswordHash, adminPassword);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                throw new InvalidOperationException("Contraseña de administrador incorrecta.");
            }
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

        public async Task DeleteAreaAsync(int id, int actingAdminUserSystemId, string? adminPassword)
        {
            var area = await _dbContext.Areas.SingleOrDefaultAsync(a => a.Id == id)
                ?? throw new InvalidOperationException("Área no encontrada.");

            await VerifyAdminPasswordAsync(actingAdminUserSystemId, adminPassword);

            var affectedUsers = await _dbContext.Users.Where(u => u.AreaId == id).ToListAsync();
            foreach (var user in affectedUsers)
            {
                user.AreaId = null;
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

        public async Task DeleteRegionalAsync(int id, int actingAdminUserSystemId, string? adminPassword)
        {
            var regional = await _dbContext.Regionals.SingleOrDefaultAsync(r => r.Id == id)
                ?? throw new InvalidOperationException("Regional no encontrada.");

            await VerifyAdminPasswordAsync(actingAdminUserSystemId, adminPassword);

            var affectedUsers = await _dbContext.Users.Where(u => u.RegionalId == id).ToListAsync();
            foreach (var user in affectedUsers)
            {
                user.RegionalId = null;
            }

            _dbContext.Regionals.Remove(regional);
            await _dbContext.SaveChangesAsync();
        }
    }
}
