using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.MaintenanceType;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class MaintenanceTypeService : IMaintenanceTypeService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;

        public MaintenanceTypeService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        public async Task<List<MaintenanceType>> GetAllAsync()
        {
            return await _dbContext.MaintenanceTypes.OrderBy(t => t.Type).ToListAsync();
        }

        public async Task<MaintenanceType?> GetByIdAsync(int id)
        {
            return await _dbContext.MaintenanceTypes.SingleOrDefaultAsync(t => t.Id == id);
        }

        public async Task<int> CreateAsync(MaintenanceTypeCreateViewModel vm)
        {
            var trimmedType = vm.Type.Trim();

            if (await _dbContext.MaintenanceTypes.AnyAsync(t => t.Type.ToLower() == trimmedType.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un tipo de mantenimiento con ese nombre.");
            }

            var type = new MaintenanceType { Type = trimmedType };
            _dbContext.MaintenanceTypes.Add(type);
            await _dbContext.SaveChangesAsync();
            return type.Id;
        }

        public async Task UpdateAsync(int id, MaintenanceTypeEditViewModel vm)
        {
            var type = await _dbContext.MaintenanceTypes.SingleOrDefaultAsync(t => t.Id == id)
                ?? throw new InvalidOperationException("Tipo de mantenimiento no encontrado.");

            var trimmedType = vm.Type.Trim();

            if (await _dbContext.MaintenanceTypes.AnyAsync(t => t.Id != id && t.Type.ToLower() == trimmedType.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un tipo de mantenimiento con ese nombre.");
            }

            type.Type = trimmedType;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword)
        {
            var type = await _dbContext.MaintenanceTypes.SingleOrDefaultAsync(t => t.Id == id)
                ?? throw new InvalidOperationException("Tipo de mantenimiento no encontrado.");

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

            if (await _dbContext.PeripheralMaintenances.AnyAsync(m => m.MaintenanceTypeId == id))
            {
                throw new InvalidOperationException("No se puede eliminar: hay mantenimientos de periféricos registrados con este tipo.");
            }

            _dbContext.MaintenanceTypes.Remove(type);
            await _dbContext.SaveChangesAsync();
        }
    }
}
