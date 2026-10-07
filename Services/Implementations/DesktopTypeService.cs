using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.DesktopType;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class DesktopTypeService : IDesktopTypeService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;

        public DesktopTypeService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        public async Task<List<DesktopType>> GetAllAsync()
        {
            return await _dbContext.DesktopTypes
                .OrderBy(t => t.Brand)
                .ThenBy(t => t.Model)
                .ToListAsync();
        }

        public async Task<DesktopType?> GetByIdAsync(int id)
        {
            return await _dbContext.DesktopTypes.SingleOrDefaultAsync(t => t.Id == id);
        }

        public async Task<int> CreateAsync(DesktopTypeCreateViewModel vm)
        {
            var trimmedBrand = vm.Brand.Trim();
            var trimmedModel = vm.Model.Trim();

            if (await _dbContext.DesktopTypes.AnyAsync(t =>
                    t.Brand.ToLower() == trimmedBrand.ToLower() && t.Model.ToLower() == trimmedModel.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un tipo de equipo con esa marca y modelo.");
            }

            var type = new DesktopType { Brand = trimmedBrand, Model = trimmedModel };
            _dbContext.DesktopTypes.Add(type);
            await _dbContext.SaveChangesAsync();
            return type.Id;
        }

        public async Task UpdateAsync(int id, DesktopTypeEditViewModel vm)
        {
            var type = await _dbContext.DesktopTypes.SingleOrDefaultAsync(t => t.Id == id)
                ?? throw new InvalidOperationException("Tipo de equipo no encontrado.");

            var trimmedBrand = vm.Brand.Trim();
            var trimmedModel = vm.Model.Trim();

            if (await _dbContext.DesktopTypes.AnyAsync(t =>
                    t.Id != id && t.Brand.ToLower() == trimmedBrand.ToLower() && t.Model.ToLower() == trimmedModel.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un tipo de equipo con esa marca y modelo.");
            }

            type.Brand = trimmedBrand;
            type.Model = trimmedModel;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword)
        {
            var type = await _dbContext.DesktopTypes.SingleOrDefaultAsync(t => t.Id == id)
                ?? throw new InvalidOperationException("Tipo de equipo no encontrado.");

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

            if (await _dbContext.Desktops.AnyAsync(d => d.DesktopTypeId == id))
            {
                throw new InvalidOperationException("No se puede eliminar: hay equipos registrados con este tipo.");
            }

            _dbContext.DesktopTypes.Remove(type);
            await _dbContext.SaveChangesAsync();
        }
    }
}
