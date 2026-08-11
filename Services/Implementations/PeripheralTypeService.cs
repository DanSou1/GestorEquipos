using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.PeripheralType;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class PeripheralTypeService : IPeripheralTypeService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;

        public PeripheralTypeService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        public async Task<List<PeripheralType>> GetAllAsync()
        {
            return await _dbContext.PeripheralTypes.OrderBy(t => t.Name).ToListAsync();
        }

        public async Task<PeripheralType?> GetByIdAsync(int id)
        {
            return await _dbContext.PeripheralTypes.SingleOrDefaultAsync(t => t.Id == id);
        }

        public async Task<int> CreateAsync(PeripheralTypeCreateViewModel vm)
        {
            var trimmedName = vm.Name.Trim();

            if (await _dbContext.PeripheralTypes.AnyAsync(t => t.Name.ToLower() == trimmedName.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un tipo de periférico con ese nombre.");
            }

            var type = new PeripheralType { Name = trimmedName };
            _dbContext.PeripheralTypes.Add(type);
            await _dbContext.SaveChangesAsync();
            return type.Id;
        }

        public async Task UpdateAsync(int id, PeripheralTypeEditViewModel vm)
        {
            var type = await _dbContext.PeripheralTypes.SingleOrDefaultAsync(t => t.Id == id)
                ?? throw new InvalidOperationException("Tipo de periférico no encontrado.");

            var trimmedName = vm.Name.Trim();

            if (await _dbContext.PeripheralTypes.AnyAsync(t => t.Id != id && t.Name.ToLower() == trimmedName.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un tipo de periférico con ese nombre.");
            }

            type.Name = trimmedName;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword)
        {
            var type = await _dbContext.PeripheralTypes.SingleOrDefaultAsync(t => t.Id == id)
                ?? throw new InvalidOperationException("Tipo de periférico no encontrado.");

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

            if (await _dbContext.Peripherals.AnyAsync(p => p.PeripheralTypeId == id))
            {
                throw new InvalidOperationException("No se puede eliminar: hay periféricos registrados con este tipo.");
            }

            _dbContext.PeripheralTypes.Remove(type);
            await _dbContext.SaveChangesAsync();
        }
    }
}
