using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Desktop;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class DesktopService : IDesktopService
    {
        private readonly MyDbContext _dbContext;
        private readonly IAsignationService _asignationService;

        public DesktopService(MyDbContext dbContext, IAsignationService asignationService)
        {
            _dbContext = dbContext;
            _asignationService = asignationService;
        }

        public async Task<List<DesktopListViewModel>> GetAllAsync()
        {
            var query =
                from d in _dbContext.Desktops.AsNoTracking()
                select new DesktopListViewModel
                {
                    Id = d.Id,
                    NameDesktop = d.NameDesktop,
                    SerialNumber = d.SerialNumber,
                    Brand = d.Brand,
                    Model = d.Model,
                    Status = d.Estado ? "Activo" : "Inactivo",
                    UserName = d.Asignations
                        .OrderByDescending(a => a.DateAsignation)
                        .ThenByDescending(a => a.Id)
                        .Select(a => a.User.Name + " " + a.User.LastName)
                        .FirstOrDefault() ?? "Sin asignar",
                    AreaName = d.Asignations
                        .OrderByDescending(a => a.DateAsignation)
                        .ThenByDescending(a => a.Id)
                        .Select(a => a.User.Area.Name)
                        .FirstOrDefault() ?? "-",
                    RegionalName = d.Asignations
                        .OrderByDescending(a => a.DateAsignation)
                        .ThenByDescending(a => a.Id)
                        .Select(a => a.User.Regional.Name)
                        .FirstOrDefault() ?? "-"
                };

            return await query.ToListAsync();
        }

        public async Task<DesktopDetailViewModel?> GetDetailAsync(int desktopId)
        {
            var desktop = await _dbContext.Desktops
                .AsNoTracking()
                .Include(d => d.OSVersion)
                .Include(d => d.Ram)
                .Include(d => d.Remote)
                .Include(d => d.Peripherals).ThenInclude(p => p.PeripheralType)
                .Include(d => d.Peripherals).ThenInclude(p => p.Observations)
                .Include(d => d.Maintenances).ThenInclude(m => m.MaintenanceType)
                .Include(d => d.Maintenances).ThenInclude(m => m.Technician).ThenInclude(t => t.User)
                .Include(d => d.Licenses)
                .Include(d => d.SpecChangeLogs).ThenInclude(s => s.ChangedByUserSystem).ThenInclude(u => u.User)
                .SingleOrDefaultAsync(d => d.Id == desktopId);

            if (desktop is null)
            {
                return null;
            }

            var asignationHistory = await _asignationService.GetHistoryAsync(desktopId);
            var currentAsignation = asignationHistory.FirstOrDefault();

            return new DesktopDetailViewModel
            {
                Id = desktop.Id,
                NameDesktop = desktop.NameDesktop,
                SerialNumber = desktop.SerialNumber,
                Brand = desktop.Brand,
                Model = desktop.Model,
                Processor = desktop.Processor,
                Disk = desktop.Disk,
                OSVersionName = $"{desktop.OSVersion.TypeSO} {desktop.OSVersion.Version}",
                RamSpecification = desktop.Ram.Especification,
                RemoteInfo = desktop.Remote is null ? null : $"{desktop.Remote.IPAddress}:{desktop.Remote.Port}",
                Estado = desktop.Estado,
                CurrentUserName = currentAsignation is null ? "Sin asignar" : $"{currentAsignation.User.Name} {currentAsignation.User.LastName}",
                CurrentAreaName = currentAsignation?.User.Area?.Name ?? "-",
                CurrentRegionalName = currentAsignation?.User.Regional?.Name ?? "-",
                AsignationHistory = asignationHistory
                    .Select(a => new AsignationHistoryItem
                    {
                        UserName = $"{a.User.Name} {a.User.LastName}",
                        DateAsignation = a.DateAsignation
                    })
                    .ToList(),
                Peripherals = desktop.Peripherals
                    .Select(p => new PeripheralDetailItem
                    {
                        Id = p.Id,
                        TypeName = p.PeripheralType.Name,
                        Brand = p.Brand,
                        Model = p.Model,
                        Serial = p.Serial,
                        Estado = p.Estado.ToString(),
                        Observations = p.Observations
                            .OrderByDescending(o => o.Date)
                            .ThenByDescending(o => o.Id)
                            .Select(o => new PeripheralObservationItem
                            {
                                Date = o.Date,
                                Type = o.Type.ToString(),
                                Description = o.Description
                            })
                            .ToList()
                    })
                    .ToList(),
                Maintenances = desktop.Maintenances
                    .OrderByDescending(m => m.Date)
                    .Select(m => new MaintenanceHistoryItem
                    {
                        Date = m.Date,
                        MaintenanceTypeName = m.MaintenanceType.Type,
                        Description = m.Description,
                        TechnicianName = $"{m.Technician.User.Name} {m.Technician.User.LastName}"
                    })
                    .ToList(),
                Licenses = desktop.Licenses
                    .Select(l => new LicenseItem
                    {
                        SoftwareType = l.SoftwareType,
                        LicenseKey = l.LicenseKey,
                        NoLicense = l.NoLicense
                    })
                    .ToList(),
                SpecChangeLogs = desktop.SpecChangeLogs
                    .OrderByDescending(s => s.Date)
                    .ThenByDescending(s => s.Id)
                    .Select(s => new SpecChangeLogItem
                    {
                        Date = s.Date,
                        FieldName = s.FieldName,
                        OldValue = s.OldValue,
                        NewValue = s.NewValue,
                        ChangedByName = $"{s.ChangedByUserSystem.User.Name} {s.ChangedByUserSystem.User.LastName}"
                    })
                    .ToList()
            };
        }

        public async Task<int> CreateAsync(DesktopCreateViewModel vm)
        {
            var desktop = new Desktop
            {
                NameDesktop = vm.NameDesktop,
                SerialNumber = vm.SerialNumber,
                Brand = vm.Brand,
                Model = vm.Model,
                Processor = vm.Processor,
                Disk = vm.Disk,
                OSVersionId = vm.OSVersionId,
                RamId = vm.RamId,
                RemoteId = vm.RemoteId,
                Estado = true
            };

            _dbContext.Desktops.Add(desktop);
            await _dbContext.SaveChangesAsync();
            return desktop.Id;
        }

        public async Task UpdateSpecsAsync(int desktopId, DesktopEditViewModel vm, int changedByUserSystemId)
        {
            var desktop = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == desktopId)
                ?? throw new InvalidOperationException("Equipo no encontrado.");

            var date = DateOnly.FromDateTime(DateTime.Now);

            LogIfChanged(desktop.Id, "Nombre de equipo", desktop.NameDesktop, vm.NameDesktop, changedByUserSystemId, date);
            LogIfChanged(desktop.Id, "Serial", desktop.SerialNumber, vm.SerialNumber, changedByUserSystemId, date);
            LogIfChanged(desktop.Id, "Marca", desktop.Brand, vm.Brand, changedByUserSystemId, date);
            LogIfChanged(desktop.Id, "Modelo", desktop.Model, vm.Model, changedByUserSystemId, date);
            LogIfChanged(desktop.Id, "Procesador", desktop.Processor, vm.Processor, changedByUserSystemId, date);
            LogIfChanged(desktop.Id, "Disco", desktop.Disk, vm.Disk, changedByUserSystemId, date);

            if (desktop.OSVersionId != vm.OSVersionId)
            {
                var oldName = await GetOSVersionNameAsync(desktop.OSVersionId);
                var newName = await GetOSVersionNameAsync(vm.OSVersionId);
                LogIfChanged(desktop.Id, "Sistema Operativo", oldName, newName, changedByUserSystemId, date);
            }

            if (desktop.RamId != vm.RamId)
            {
                var oldName = await GetRamNameAsync(desktop.RamId);
                var newName = await GetRamNameAsync(vm.RamId);
                LogIfChanged(desktop.Id, "RAM", oldName, newName, changedByUserSystemId, date);
            }

            if (desktop.RemoteId != vm.RemoteId)
            {
                LogIfChanged(desktop.Id, "Remote", desktop.RemoteId?.ToString(), vm.RemoteId?.ToString(), changedByUserSystemId, date);
            }

            desktop.NameDesktop = vm.NameDesktop;
            desktop.SerialNumber = vm.SerialNumber;
            desktop.Brand = vm.Brand;
            desktop.Model = vm.Model;
            desktop.Processor = vm.Processor;
            desktop.Disk = vm.Disk;
            desktop.OSVersionId = vm.OSVersionId;
            desktop.RamId = vm.RamId;
            desktop.RemoteId = vm.RemoteId;

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeactivateAsync(int desktopId)
        {
            var desktop = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == desktopId)
                ?? throw new InvalidOperationException("Equipo no encontrado.");

            if (!desktop.Estado)
            {
                return;
            }

            desktop.Estado = false;
            await _dbContext.SaveChangesAsync();

            var raesUser = await _dbContext.Users.SingleAsync(u => u.Email == AuthBootstrapper.RaesUserEmail);
            await _asignationService.AssignAsync(desktopId, raesUser.Id);
        }

        private void LogIfChanged(int desktopId, string fieldName, string? oldValue, string? newValue, int changedByUserSystemId, DateOnly date)
        {
            if (oldValue == newValue)
            {
                return;
            }

            _dbContext.SpecChangeLogs.Add(new SpecChangeLog
            {
                DesktopId = desktopId,
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                Date = date,
                ChangedByUserSystemId = changedByUserSystemId
            });
        }

        private async Task<string?> GetOSVersionNameAsync(int osVersionId)
        {
            var osVersion = await _dbContext.OSVersions.FindAsync(osVersionId);
            return osVersion is null ? null : $"{osVersion.TypeSO} {osVersion.Version}";
        }

        private async Task<string?> GetRamNameAsync(int ramId)
        {
            var ram = await _dbContext.Rams.FindAsync(ramId);
            return ram?.Especification;
        }
    }
}
