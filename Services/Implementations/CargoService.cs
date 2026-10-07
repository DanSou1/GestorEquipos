using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Cargo;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class CargoService : ICargoService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;

        public CargoService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        public async Task<List<Cargo>> GetAllAsync()
        {
            return await _dbContext.Cargos.OrderBy(c => c.Name).ToListAsync();
        }

        public async Task<Cargo?> GetByIdAsync(int id)
        {
            return await _dbContext.Cargos.SingleOrDefaultAsync(c => c.Id == id);
        }

        public async Task<int> CreateAsync(string name)
        {
            var trimmedName = name.Trim();

            if (await _dbContext.Cargos.AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un cargo con ese nombre.");
            }

            var cargo = new Cargo { Name = trimmedName };
            _dbContext.Cargos.Add(cargo);
            await _dbContext.SaveChangesAsync();
            return cargo.Id;
        }

        public async Task UpdateAsync(int id, string name)
        {
            var cargo = await _dbContext.Cargos.SingleOrDefaultAsync(c => c.Id == id)
                ?? throw new InvalidOperationException("Cargo no encontrado.");

            var trimmedName = name.Trim();

            if (await _dbContext.Cargos.AnyAsync(c => c.Id != id && c.Name.ToLower() == trimmedName.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un cargo con ese nombre.");
            }

            cargo.Name = trimmedName;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword)
        {
            var cargo = await _dbContext.Cargos.SingleOrDefaultAsync(c => c.Id == id)
                ?? throw new InvalidOperationException("Cargo no encontrado.");

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

            if (await _dbContext.CargoAssignments.AnyAsync(a => a.CargoId == id))
            {
                throw new InvalidOperationException("No se puede eliminar: hay personas con este cargo en su historial.");
            }

            _dbContext.Cargos.Remove(cargo);
            await _dbContext.SaveChangesAsync();
        }

        // No hay flag "IsCurrent" en CargoAssignment (mismo criterio que Asignation/PeripheralAssignment):
        // el "fin" del período de una persona en un cargo es la fecha de su SIGUIENTE registro de
        // cargo (sea cual sea), no la del siguiente registro de este cargo en particular — un cargo
        // puede tener varios titulares a la vez. Mirror de latestAssignationIdByDesktop en
        // UserAdminService.GetDetailAsync, pero calculando "siguiente" en vez de solo "el último".
        public async Task<CargoHistoryViewModel?> GetHistoryAsync(int cargoId)
        {
            var cargo = await _dbContext.Cargos.SingleOrDefaultAsync(c => c.Id == cargoId);
            if (cargo is null)
            {
                return null;
            }

            var allAssignments = await _dbContext.CargoAssignments
                .Include(a => a.User)
                .OrderBy(a => a.UserId)
                .ThenBy(a => a.DateAsignation)
                .ThenBy(a => a.Id)
                .ToListAsync();

            var items = new List<CargoHistoryItem>();
            foreach (var group in allAssignments.GroupBy(a => a.UserId))
            {
                var ordered = group.OrderBy(a => a.DateAsignation).ThenBy(a => a.Id).ToList();
                for (var i = 0; i < ordered.Count; i++)
                {
                    var record = ordered[i];
                    if (record.CargoId != cargoId)
                    {
                        continue;
                    }

                    var endDate = i + 1 < ordered.Count ? ordered[i + 1].DateAsignation : (DateOnly?)null;
                    items.Add(new CargoHistoryItem
                    {
                        UserId = record.UserId,
                        UserName = $"{record.User.Name} {record.User.LastName}",
                        StartDate = record.DateAsignation,
                        EndDate = endDate
                    });
                }
            }

            return new CargoHistoryViewModel
            {
                CargoId = cargo.Id,
                CargoName = cargo.Name,
                Items = items.OrderBy(i => i.StartDate).ToList()
            };
        }
    }
}
