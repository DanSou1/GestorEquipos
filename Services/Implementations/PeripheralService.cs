using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Peripheral;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class PeripheralService : IPeripheralService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;
        private readonly IPeripheralAssignmentService _peripheralAssignmentService;
        private readonly IPeripheralMaintenanceService _peripheralMaintenanceService;

        public PeripheralService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher, IPeripheralAssignmentService peripheralAssignmentService, IPeripheralMaintenanceService peripheralMaintenanceService)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _peripheralAssignmentService = peripheralAssignmentService;
            _peripheralMaintenanceService = peripheralMaintenanceService;
        }

        public async Task<int> AddAsync(PeripheralCreateViewModel vm)
        {
            var peripheral = new Peripheral
            {
                DesktopId = vm.DesktopId,
                PeripheralTypeId = vm.PeripheralTypeId,
                Brand = vm.Brand,
                Model = vm.Model,
                Serial = vm.Serial,
                Estado = true
            };

            _dbContext.Peripherals.Add(peripheral);
            await _dbContext.SaveChangesAsync();

            // A newly registered peripheral inherits its parent Desktop's current active
            // holder (if any) — otherwise it starts out "Disponible" (no assignment row).
            var currentDesktopHolder = await _dbContext.Asignations
                .Where(a => a.DesktopId == vm.DesktopId)
                .Include(a => a.User)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            if (currentDesktopHolder is not null && currentDesktopHolder.User.Activo)
            {
                await _peripheralAssignmentService.AssignAsync(peripheral.Id, currentDesktopHolder.UserId);
            }

            return peripheral.Id;
        }

        public async Task<Peripheral?> GetByIdAsync(int id)
        {
            return await _dbContext.Peripherals.SingleOrDefaultAsync(p => p.Id == id);
        }

        public async Task<PeripheralDetailViewModel?> GetDetailAsync(int id)
        {
            var peripheral = await _dbContext.Peripherals
                .AsNoTracking()
                .Include(p => p.Desktop)
                .Include(p => p.PeripheralType)
                .SingleOrDefaultAsync(p => p.Id == id);

            if (peripheral is null)
            {
                return null;
            }

            var assignmentHistory = await _peripheralAssignmentService.GetHistoryAsync(id);
            var maintenances = await _peripheralMaintenanceService.GetByPeripheralAsync(id);
            var currentAssignment = assignmentHistory.FirstOrDefault();
            var isDisponible = currentAssignment is not null && !currentAssignment.User.Activo;

            return new PeripheralDetailViewModel
            {
                Id = peripheral.Id,
                TypeName = peripheral.PeripheralType.Name,
                Brand = peripheral.Brand,
                Model = peripheral.Model,
                Serial = peripheral.Serial,
                Estado = !peripheral.Estado
                    ? "Raes"
                    : (currentAssignment is null || !currentAssignment.User.Activo)
                        ? "Disponible"
                        : "Activo",
                DesktopId = peripheral.DesktopId,
                DesktopName = peripheral.Desktop.NameDesktop,
                CurrentUserName = currentAssignment is null
                    ? "Sin asignar"
                    : isDisponible
                        ? "Disponible"
                        : $"{currentAssignment.User.Name} {currentAssignment.User.LastName}",
                CurrentSinceDate = isDisponible ? currentAssignment!.User.DeactivatedAt : null,
                CurrentAreaName = currentAssignment?.User.Area?.Name ?? "-",
                CurrentRegionalName = currentAssignment?.User.Regional?.Name ?? "-",
                AssignmentHistory = assignmentHistory
                    .Select(a => new PeripheralAssignmentHistoryItem
                    {
                        UserName = $"{a.User.Name} {a.User.LastName}",
                        DateAsignation = a.DateAsignation
                    })
                    .ToList(),
                Maintenances = maintenances
                    .Select(m => new PeripheralMaintenanceHistoryItem
                    {
                        Date = m.Date,
                        MaintenanceTypeName = m.MaintenanceType.Type,
                        Description = m.Description,
                        TechnicianName = $"{m.Technician.User.Name} {m.Technician.User.LastName}"
                    })
                    .ToList()
            };
        }

        public async Task UpdateAsync(int id, PeripheralEditViewModel vm)
        {
            var peripheral = await _dbContext.Peripherals.SingleOrDefaultAsync(p => p.Id == id)
                ?? throw new InvalidOperationException("Periférico no encontrado.");

            peripheral.PeripheralTypeId = vm.PeripheralTypeId;
            peripheral.Brand = vm.Brand;
            peripheral.Model = vm.Model;
            peripheral.Serial = vm.Serial;

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword)
        {
            var peripheral = await _dbContext.Peripherals.SingleOrDefaultAsync(p => p.Id == id)
                ?? throw new InvalidOperationException("Periférico no encontrado.");

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

            _dbContext.Peripherals.Remove(peripheral);
            await _dbContext.SaveChangesAsync();
        }

        public async Task RetireToRaesAsync(int id)
        {
            var peripheral = await _dbContext.Peripherals.SingleOrDefaultAsync(p => p.Id == id)
                ?? throw new InvalidOperationException("Periférico no encontrado.");

            if (!peripheral.Estado)
            {
                return;
            }

            peripheral.Estado = false;
            await _dbContext.SaveChangesAsync();

            var raesUser = await _dbContext.Users.SingleAsync(u => u.Email == AuthBootstrapper.RaesUserEmail);
            await _peripheralAssignmentService.AssignAsync(id, raesUser.Id);
        }

        public async Task<List<PeripheralInventoryItem>> GetInventoryAsync()
        {
            var query =
                from p in _dbContext.Peripherals.AsNoTracking()
                select new PeripheralInventoryItem
                {
                    Id = p.Id,
                    PeripheralTypeId = p.PeripheralTypeId,
                    TypeName = p.PeripheralType.Name,
                    Brand = p.Brand,
                    Model = p.Model,
                    Serial = p.Serial,
                    DesktopId = p.DesktopId,
                    DesktopName = p.Desktop.NameDesktop,
                    Estado = !p.Estado
                        ? "Raes"
                        : (p.Assignments
                            .OrderByDescending(a => a.DateAsignation)
                            .ThenByDescending(a => a.Id)
                            .Select(a => (bool?)a.User.Activo)
                            .FirstOrDefault() == true)
                            ? "Activo"
                            : "Disponible",
                    CurrentHolderName = p.Assignments
                        .OrderByDescending(a => a.DateAsignation)
                        .ThenByDescending(a => a.Id)
                        .Select(a => a.User.Activo ? a.User.Name + " " + a.User.LastName : "Disponible")
                        .FirstOrDefault() ?? "Sin asignar"
                };

            return await query.ToListAsync();
        }

        public async Task<PeripheralInventoryStatsViewModel> GetInventoryStatsAsync()
        {
            var rows = await _dbContext.Peripherals.AsNoTracking()
                .Select(p => new
                {
                    p.Estado,
                    TypeName = p.PeripheralType.Name,
                    HolderActivo = p.Assignments
                        .OrderByDescending(a => a.DateAsignation)
                        .ThenByDescending(a => a.Id)
                        .Select(a => (bool?)a.User.Activo)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return new PeripheralInventoryStatsViewModel
            {
                Total = rows.Count,
                Activos = rows.Count(r => r.Estado && r.HolderActivo == true),
                Disponibles = rows.Count(r => r.Estado && r.HolderActivo != true),
                Raes = rows.Count(r => !r.Estado),
                ByType = rows
                    .GroupBy(r => r.TypeName)
                    .Select(g => new PeripheralTypeCountItem { TypeName = g.Key, Count = g.Count() })
                    .OrderByDescending(t => t.Count)
                    .ThenBy(t => t.TypeName)
                    .ToList()
            };
        }
    }
}
