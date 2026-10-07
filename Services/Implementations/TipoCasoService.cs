using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.TipoCaso;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class TipoCasoService : ITipoCasoService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;

        public TipoCasoService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        public async Task<List<TipoCaso>> GetAllAsync()
        {
            return await _dbContext.TiposCaso.OrderBy(t => t.Nombre).ToListAsync();
        }

        public async Task<TipoCaso?> GetByIdAsync(int id)
        {
            return await _dbContext.TiposCaso.SingleOrDefaultAsync(t => t.Id == id);
        }

        public async Task<int> CreateAsync(TipoCasoCreateViewModel vm)
        {
            var trimmedNombre = vm.Nombre.Trim();

            if (await _dbContext.TiposCaso.AnyAsync(t => t.Nombre.ToLower() == trimmedNombre.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un tipo de caso con ese nombre.");
            }

            var tipoCaso = new TipoCaso { Nombre = trimmedNombre };
            _dbContext.TiposCaso.Add(tipoCaso);
            await _dbContext.SaveChangesAsync();
            return tipoCaso.Id;
        }

        public async Task UpdateAsync(int id, TipoCasoEditViewModel vm)
        {
            var tipoCaso = await _dbContext.TiposCaso.SingleOrDefaultAsync(t => t.Id == id)
                ?? throw new InvalidOperationException("Tipo de caso no encontrado.");

            var trimmedNombre = vm.Nombre.Trim();

            if (await _dbContext.TiposCaso.AnyAsync(t => t.Id != id && t.Nombre.ToLower() == trimmedNombre.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un tipo de caso con ese nombre.");
            }

            tipoCaso.Nombre = trimmedNombre;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword)
        {
            var tipoCaso = await _dbContext.TiposCaso.SingleOrDefaultAsync(t => t.Id == id)
                ?? throw new InvalidOperationException("Tipo de caso no encontrado.");

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

            if (await _dbContext.CasosTI.AnyAsync(c => c.TipoCasoId == id))
            {
                throw new InvalidOperationException("No se puede eliminar: hay casos TI registrados con este tipo.");
            }

            _dbContext.TiposCaso.Remove(tipoCaso);
            await _dbContext.SaveChangesAsync();
        }
    }
}
