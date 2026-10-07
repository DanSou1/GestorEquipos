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

        // LOG-09: see the matching comment on DesktopService.CreateAsync.
        public async Task<int> AddAsync(PeripheralCreateViewModel vm)
        {
            if (!await _dbContext.Desktops.AnyAsync(d => d.Id == vm.DesktopId))
            {
                throw new InvalidOperationException("El equipo seleccionado no existe.");
            }

            if (!await _dbContext.PeripheralTypes.AnyAsync(t => t.Id == vm.PeripheralTypeId))
            {
                throw new InvalidOperationException("El tipo de periférico seleccionado no existe.");
            }

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

        public async Task<PeripheralDetailViewModel?> GetDetailAsync(int id, CancellationToken cancellationToken = default)
        {
            var peripheral = await _dbContext.Peripherals
                .AsNoTracking()
                .Include(p => p.Desktop)
                .Include(p => p.PeripheralType)
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (peripheral is null)
            {
                return null;
            }

            var assignmentHistory = await _peripheralAssignmentService.GetHistoryAsync(id, cancellationToken);
            var maintenances = await _peripheralMaintenanceService.GetByPeripheralAsync(id, cancellationToken);
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
                    : (peripheral.Desktop is null || !peripheral.Desktop.Estado || currentAssignment is null || !currentAssignment.User.Activo)
                        ? "Disponible"
                        : "Activo",
                DesktopId = peripheral.DesktopId,
                DesktopName = peripheral.Desktop?.NameDesktop ?? "Sin equipo asignado",
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
                        TechnicianName = m.TechnicianName
                    })
                    .ToList()
            };
        }

        public async Task UpdateAsync(int id, PeripheralEditViewModel vm)
        {
            var peripheral = await _dbContext.Peripherals.SingleOrDefaultAsync(p => p.Id == id)
                ?? throw new InvalidOperationException("Periférico no encontrado.");

            if (!await _dbContext.PeripheralTypes.AnyAsync(t => t.Id == vm.PeripheralTypeId))
            {
                throw new InvalidOperationException("El tipo de periférico seleccionado no existe.");
            }

            // LOG-12: link an orphaned peripheral (DesktopId null — e.g. its Desktop was deleted)
            // back onto an active equipo. Only this null -> value transition is allowed here;
            // moving an already-linked peripheral to a different desktop is out of scope.
            var wasOrphaned = peripheral.DesktopId is null;
            if (wasOrphaned && vm.DesktopId.HasValue)
            {
                var targetDesktop = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == vm.DesktopId)
                    ?? throw new InvalidOperationException("El equipo seleccionado no existe.");
                if (!targetDesktop.Estado)
                {
                    throw new InvalidOperationException("No se puede vincular el periférico a un equipo dado de baja (RAES).");
                }
                peripheral.DesktopId = vm.DesktopId;
            }

            peripheral.PeripheralTypeId = vm.PeripheralTypeId;
            peripheral.Brand = vm.Brand;
            peripheral.Model = vm.Model;
            peripheral.Serial = vm.Serial;

            await _dbContext.SaveChangesAsync();

            if (wasOrphaned && peripheral.DesktopId.HasValue)
            {
                var currentDesktopHolder = await _dbContext.Asignations
                    .Where(a => a.DesktopId == peripheral.DesktopId)
                    .Include(a => a.User)
                    .OrderByDescending(a => a.DateAsignation)
                    .ThenByDescending(a => a.Id)
                    .FirstOrDefaultAsync();

                if (currentDesktopHolder is not null && currentDesktopHolder.User.Activo)
                {
                    await _peripheralAssignmentService.AssignAsync(peripheral.Id, currentDesktopHolder.UserId);
                }
            }
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

        // LOG-02: see the matching comment on DesktopService.DeactivateAsync — same bug,
        // same fix (resolve RAES user first, flip Estado + reassign in one transaction).
        public async Task RetireToRaesAsync(int id)
        {
            var peripheral = await _dbContext.Peripherals.SingleOrDefaultAsync(p => p.Id == id)
                ?? throw new InvalidOperationException("Periférico no encontrado.");

            if (!peripheral.Estado)
            {
                return;
            }

            var raesUser = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == AuthBootstrapper.RaesUserEmail)
                ?? throw new InvalidOperationException("No se encontró el usuario reservado RAES; no se puede dar de baja el periférico.");

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                peripheral.Estado = false;
                await _dbContext.SaveChangesAsync();
                await _peripheralAssignmentService.AssignAsync(id, raesUser.Id);
                await transaction.CommitAsync();
            });
        }

        // Reduce assignment history in SQL; transfer at most one holder per peripheral.
        public async Task<List<PeripheralInventoryItem>> GetInventoryAsync(CancellationToken cancellationToken = default)
        {
            var peripherals = await _dbContext.Peripherals
                .AsNoTracking()
                .Select(p => new
                {
                    p.Id,
                    p.PeripheralTypeId,
                    TypeName = p.PeripheralType.Name,
                    p.Brand,
                    p.Model,
                    p.Serial,
                    p.DesktopId,
                    DesktopName = p.Desktop != null ? p.Desktop.NameDesktop : null,
                    DesktopActivo = p.Desktop != null && p.Desktop.Estado,
                    p.Estado
                })
                .ToListAsync(cancellationToken);

            var latestAssignmentByPeripheral = (await _dbContext.PeripheralAssignments
                .AsNoTracking()
                .Where(a => !_dbContext.PeripheralAssignments.Any(newer => newer.PeripheralId == a.PeripheralId &&
                    (newer.DateAsignation > a.DateAsignation ||
                     (newer.DateAsignation == a.DateAsignation && newer.Id > a.Id))))
                .Select(a => new { a.PeripheralId, a.User.Activo, HolderName = a.User.Name + " " + a.User.LastName })
                .ToListAsync(cancellationToken))
                .ToDictionary(a => a.PeripheralId);

            return peripherals
                .Select(p =>
                {
                    latestAssignmentByPeripheral.TryGetValue(p.Id, out var lastAssignment);
                    var estado = !p.Estado
                        ? "Raes"
                        : (p.DesktopActivo && lastAssignment is not null && lastAssignment.Activo)
                            ? "Activo"
                            : "Disponible";

                    return new PeripheralInventoryItem
                    {
                        Id = p.Id,
                        PeripheralTypeId = p.PeripheralTypeId,
                        TypeName = p.TypeName,
                        Brand = p.Brand,
                        Model = p.Model,
                        Serial = p.Serial,
                        DesktopId = p.DesktopId,
                        DesktopName = p.DesktopName ?? "Sin equipo asignado",
                        Estado = estado,
                        CurrentHolderName = lastAssignment is null
                            ? "Sin asignar"
                            : lastAssignment.Activo ? lastAssignment.HolderName : "Disponible"
                    };
                })
                .ToList();
        }

        // PERF-01: derive from GetInventoryAsync() instead of re-querying — see its comment.
        public async Task<PeripheralInventoryStatsViewModel> GetInventoryStatsAsync(CancellationToken cancellationToken = default)
        {
            var items = await GetInventoryAsync(cancellationToken);

            return new PeripheralInventoryStatsViewModel
            {
                Total = items.Count,
                Activos = items.Count(i => i.Estado == "Activo"),
                Disponibles = items.Count(i => i.Estado == "Disponible"),
                Raes = items.Count(i => i.Estado == "Raes"),
                ByType = items
                    .GroupBy(i => i.TypeName)
                    .Select(g => new PeripheralTypeCountItem { TypeName = g.Key, Count = g.Count() })
                    .OrderByDescending(t => t.Count)
                    .ThenBy(t => t.TypeName)
                    .ToList()
            };
        }
    }
}
