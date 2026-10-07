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

        // Reduce assignment history in SQL; transfer at most one holder per desktop.
        public async Task<List<DesktopListViewModel>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var desktops = await _dbContext.Desktops
                .AsNoTracking()
                .Select(d => new
                {
                    d.Id,
                    d.NameDesktop,
                    d.SerialNumber,
                    Brand = d.DesktopType.Brand,
                    Model = d.DesktopType.Model,
                    d.Estado
                })
                .ToListAsync(cancellationToken);

            var latestAsignationByDesktop = (await _dbContext.Asignations
                .AsNoTracking()
                .Where(a => !_dbContext.Asignations.Any(newer => newer.DesktopId == a.DesktopId &&
                    (newer.DateAsignation > a.DateAsignation ||
                     (newer.DateAsignation == a.DateAsignation && newer.Id > a.Id))))
                .Select(a => new
                {
                    a.DesktopId,
                    a.User.Activo,
                    UserName = a.User.Name + " " + a.User.LastName,
                    AreaName = a.User.Area != null ? a.User.Area.Name : null,
                    RegionalName = a.User.Regional != null ? a.User.Regional.Name : null
                })
                .ToListAsync(cancellationToken))
                .ToDictionary(a => a.DesktopId);

            return desktops
                .Select(d =>
                {
                    latestAsignationByDesktop.TryGetValue(d.Id, out var last);
                    return new DesktopListViewModel
                    {
                        Id = d.Id,
                        NameDesktop = d.NameDesktop,
                        SerialNumber = d.SerialNumber,
                        Brand = d.Brand,
                        Model = d.Model,
                        Status = d.Estado ? "Activo" : "Inactivo",
                        UserName = last is null
                            ? "Sin asignar"
                            : last.Activo ? last.UserName : "Disponible",
                        AreaName = last?.AreaName ?? "-",
                        RegionalName = last?.RegionalName ?? "-"
                    };
                })
                .ToList();
        }

        public async Task<DesktopDetailViewModel?> GetDetailAsync(int desktopId, CancellationToken cancellationToken = default)
        {
            var desktop = await _dbContext.Desktops
                .AsNoTracking()
                .Include(d => d.DesktopType)
                .Include(d => d.OSVersion)
                .Include(d => d.Ram)
                .Include(d => d.Remote)
                .Include(d => d.Peripherals).ThenInclude(p => p.PeripheralType)
                .Include(d => d.Peripherals).ThenInclude(p => p.Assignments).ThenInclude(a => a.User)
                .Include(d => d.Maintenances)
                .Include(d => d.Licenses)
                .Include(d => d.SpecChangeLogs).ThenInclude(s => s.ChangedByUserSystem).ThenInclude(u => u.User)
                .AsSplitQuery()
                .SingleOrDefaultAsync(d => d.Id == desktopId, cancellationToken);

            if (desktop is null)
            {
                return null;
            }

            var asignationHistory = await _asignationService.GetHistoryAsync(desktopId, cancellationToken);
            var currentAsignation = asignationHistory.FirstOrDefault();
            var isDisponible = currentAsignation is not null && !currentAsignation.User.Activo;

            return new DesktopDetailViewModel
            {
                Id = desktop.Id,
                NameDesktop = desktop.NameDesktop,
                SerialNumber = desktop.SerialNumber,
                Brand = desktop.DesktopType.Brand,
                Model = desktop.DesktopType.Model,
                Processor = desktop.Processor,
                Disk = desktop.Disk,
                OSVersionName = $"{desktop.OSVersion.TypeSO} {desktop.OSVersion.Version}",
                RamSpecification = $"{desktop.RamType} {desktop.Ram.Especification}",
                RemoteAccess = desktop.Remote is null ? null : new RemoteAccessItem
                {
                    Id = desktop.Remote.Id,
                    ConnectionType = desktop.Remote.ConnectionType,
                    IPAddress = desktop.Remote.IPAddress,
                    Port = desktop.Remote.Port,
                    Username = desktop.Remote.Username,
                    Password = desktop.Remote.Password,
                    AppDescription = desktop.Remote.AppDescription
                },
                Estado = desktop.Estado,
                CurrentUserName = currentAsignation is null
                    ? "Sin asignar"
                    : isDisponible
                        ? "Disponible"
                        : $"{currentAsignation.User.Name} {currentAsignation.User.LastName}",
                CurrentSinceDate = isDisponible ? currentAsignation!.User.DeactivatedAt : null,
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
                    .Select(p =>
                    {
                        var latestAssignment = p.Assignments
                            .OrderByDescending(a => a.DateAsignation)
                            .ThenByDescending(a => a.Id)
                            .FirstOrDefault();
                        var estado = !p.Estado
                            ? "Raes"
                            : (!desktop.Estado || latestAssignment is null || !latestAssignment.User.Activo)
                                ? "Disponible"
                                : "Activo";

                        return new PeripheralDetailItem
                        {
                            Id = p.Id,
                            TypeName = p.PeripheralType.Name,
                            Brand = p.Brand,
                            Model = p.Model,
                            Serial = p.Serial,
                            Estado = estado
                        };
                    })
                    .ToList(),
                Maintenances = desktop.Maintenances
                    .OrderByDescending(m => m.Date)
                    .Select(m => new MaintenanceHistoryItem
                    {
                        Id = m.Id,
                        Date = m.Date,
                        Category = m.Category,
                        Description = m.Description,
                        TechnicianName = m.TechnicianName
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

        // LOG-09: [Required] on an int only checks it was posted, not that the row exists —
        // a stale form, deleted catalog entry, or manipulated request used to reach SQL as a raw
        // FK violation (500). Validate existence up front and fail with a normal form error.
        public async Task<int> CreateAsync(DesktopCreateViewModel vm)
        {
            if (!await _dbContext.DesktopTypes.AnyAsync(t => t.Id == vm.DesktopTypeId))
            {
                throw new InvalidOperationException("El tipo de equipo seleccionado no existe.");
            }

            if (!await _dbContext.OSVersions.AnyAsync(o => o.Id == vm.OSVersionId))
            {
                throw new InvalidOperationException("El sistema operativo seleccionado no existe.");
            }

            if (!await _dbContext.Rams.AnyAsync(r => r.Id == vm.RamId))
            {
                throw new InvalidOperationException("La RAM seleccionada no existe.");
            }

            if (await _dbContext.Desktops.AnyAsync(d => d.SerialNumber == vm.SerialNumber))
            {
                throw new InvalidOperationException("Ya existe un equipo registrado con ese serial.");
            }

            var (remoteId, newRemote) = ResolveRemoteSelection(vm, currentRemoteId: null);

            var desktop = new Desktop
            {
                NameDesktop = vm.NameDesktop,
                SerialNumber = vm.SerialNumber,
                Processor = vm.Processor,
                Disk = vm.Disk,
                DesktopTypeId = vm.DesktopTypeId,
                OSVersionId = vm.OSVersionId,
                RamId = vm.RamId,
                RamType = vm.RamType,
                RemoteId = remoteId,
                Remote = newRemote,
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

            if (!await _dbContext.DesktopTypes.AnyAsync(t => t.Id == vm.DesktopTypeId))
            {
                throw new InvalidOperationException("El tipo de equipo seleccionado no existe.");
            }

            if (!await _dbContext.OSVersions.AnyAsync(o => o.Id == vm.OSVersionId))
            {
                throw new InvalidOperationException("El sistema operativo seleccionado no existe.");
            }

            if (!await _dbContext.Rams.AnyAsync(r => r.Id == vm.RamId))
            {
                throw new InvalidOperationException("La RAM seleccionada no existe.");
            }

            if (await _dbContext.Desktops.AnyAsync(d => d.Id != desktopId && d.SerialNumber == vm.SerialNumber))
            {
                throw new InvalidOperationException("Ya existe un equipo registrado con ese serial.");
            }

            var date = DateOnly.FromDateTime(DateTime.Now);

            LogIfChanged(desktop.Id, "Nombre de equipo", desktop.NameDesktop, vm.NameDesktop, changedByUserSystemId, date);
            LogIfChanged(desktop.Id, "Serial", desktop.SerialNumber, vm.SerialNumber, changedByUserSystemId, date);
            LogIfChanged(desktop.Id, "Procesador", desktop.Processor, vm.Processor, changedByUserSystemId, date);
            LogIfChanged(desktop.Id, "Disco", desktop.Disk, vm.Disk, changedByUserSystemId, date);

            if (desktop.DesktopTypeId != vm.DesktopTypeId)
            {
                var oldName = await GetDesktopTypeNameAsync(desktop.DesktopTypeId);
                var newName = await GetDesktopTypeNameAsync(vm.DesktopTypeId);
                LogIfChanged(desktop.Id, "Marca / Modelo", oldName, newName, changedByUserSystemId, date);
            }

            if (desktop.OSVersionId != vm.OSVersionId)
            {
                var oldName = await GetOSVersionNameAsync(desktop.OSVersionId);
                var newName = await GetOSVersionNameAsync(vm.OSVersionId);
                LogIfChanged(desktop.Id, "Sistema Operativo", oldName, newName, changedByUserSystemId, date);
            }

            if (desktop.RamType != vm.RamType)
            {
                LogIfChanged(desktop.Id, "Tipo de RAM", desktop.RamType.ToString(), vm.RamType.ToString(), changedByUserSystemId, date);
            }

            if (desktop.RamId != vm.RamId)
            {
                var oldName = await GetRamNameAsync(desktop.RamId);
                var newName = await GetRamNameAsync(vm.RamId);
                LogIfChanged(desktop.Id, "RAM", oldName, newName, changedByUserSystemId, date);
            }

            var (remoteId, newRemote) = ResolveRemoteSelection(vm, desktop.RemoteId);

            if (desktop.RemoteId != remoteId || newRemote is not null)
            {
                var oldSummary = await GetRemoteSummaryAsync(desktop.RemoteId);
                var newSummary = newRemote is not null ? BuildRemoteSummary(newRemote) : await GetRemoteSummaryAsync(remoteId);
                LogIfChanged(desktop.Id, "Acceso remoto", oldSummary, newSummary, changedByUserSystemId, date);
            }

            desktop.NameDesktop = vm.NameDesktop;
            desktop.SerialNumber = vm.SerialNumber;
            desktop.Processor = vm.Processor;
            desktop.Disk = vm.Disk;
            desktop.DesktopTypeId = vm.DesktopTypeId;
            desktop.OSVersionId = vm.OSVersionId;
            desktop.RamId = vm.RamId;
            desktop.RamType = vm.RamType;

            if (newRemote is not null)
            {
                desktop.Remote = newRemote;
            }
            else
            {
                desktop.RemoteId = remoteId;
            }

            await _dbContext.SaveChangesAsync();
        }

        // LOG-02: Estado=false used to be saved separately from the RAES reassignment. If the
        // RAES pseudo-user was missing (or the reassignment failed for any other reason), the
        // desktop was left inactive with no matching Asignation row — and retrying just no-op'd
        // (Estado was already false) without ever creating the missing history. Resolve the RAES
        // user before writing anything, and make the state flip + reassignment one transaction.
        public async Task DeactivateAsync(int desktopId)
        {
            var desktop = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == desktopId)
                ?? throw new InvalidOperationException("Equipo no encontrado.");

            if (!desktop.Estado)
            {
                return;
            }

            var raesUser = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == AuthBootstrapper.RaesUserEmail)
                ?? throw new InvalidOperationException("No se encontró el usuario reservado RAES; no se puede dar de baja el equipo.");

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                desktop.Estado = false;
                await _dbContext.SaveChangesAsync();
                await _asignationService.AssignAsync(desktopId, raesUser.Id);
                await transaction.CommitAsync();
            });
        }

        public async Task DeleteAsync(int desktopId)
        {
            var desktop = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == desktopId)
                ?? throw new InvalidOperationException("Equipo no encontrado.");

            // Asignation.DesktopId is DeleteBehavior.Restrict at the DB level — must be
            // removed before the Desktop, or SaveChangesAsync throws a DbUpdateException
            // from a real SQL Server FK violation. Maintenance/License/SpecChangeLog are
            // Cascade — removed automatically. Peripheral.DesktopId is SetNull — peripherals
            // survive the delete (orphaned, DesktopId becomes null) — do not remove them
            // manually either way. MaintenanceCampaignItem.DesktopId is also Restrict.
            var asignations = _dbContext.Asignations.Where(a => a.DesktopId == desktopId);
            _dbContext.Asignations.RemoveRange(asignations);

            var campaignItems = _dbContext.MaintenanceCampaignItems.Where(c => c.DesktopId == desktopId);
            _dbContext.MaintenanceCampaignItems.RemoveRange(campaignItems);

            _dbContext.Desktops.Remove(desktop);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<EquipmentStatsViewModel> GetEquipmentStatsAsync(CancellationToken cancellationToken = default)
        {
            var rows = await _dbContext.Desktops.AsNoTracking()
                .Select(d => new
                {
                    d.Estado,
                    HolderActivo = d.Asignations
                        .OrderByDescending(a => a.DateAsignation)
                        .ThenByDescending(a => a.Id)
                        .Select(a => (bool?)a.User.Activo)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return new EquipmentStatsViewModel
            {
                Total = rows.Count,
                Activos = rows.Count(r => r.Estado && r.HolderActivo == true),
                Disponibles = rows.Count(r => r.Estado && r.HolderActivo != true),
                Raes = rows.Count(r => !r.Estado)
            };
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

        private async Task<string?> GetDesktopTypeNameAsync(int desktopTypeId)
        {
            var desktopType = await _dbContext.DesktopTypes.FindAsync(desktopTypeId);
            return desktopType is null ? null : $"{desktopType.Brand}, {desktopType.Model}";
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

        private static (int? RemoteId, Remote? NewRemote) ResolveRemoteSelection(DesktopCreateViewModel vm, int? currentRemoteId)
        {
            if (vm.RemoteSelection == "keep")
            {
                return (currentRemoteId, null);
            }

            if (vm.RemoteSelection == "new")
            {
                var isRdp = vm.NewRemoteConnectionType == RemoteConnectionType.EscritorioRemotoWindows;
                var remote = new Remote
                {
                    ConnectionType = vm.NewRemoteConnectionType!.Value,
                    IPAddress = isRdp ? vm.NewRemoteIPAddress : null,
                    Port = isRdp ? vm.NewRemotePort : null,
                    Username = isRdp ? vm.NewRemoteUsername : null,
                    Password = isRdp ? vm.NewRemotePassword : null,
                    AppDescription = isRdp ? null : vm.NewRemoteAppDescription
                };
                return (null, remote);
            }

            return (null, null);
        }

        private static string BuildRemoteSummary(Remote remote)
        {
            return remote.ConnectionType == RemoteConnectionType.Aplicativo
                ? $"Aplicativo: {remote.AppDescription}"
                : $"RDP {remote.IPAddress}:{remote.Port} ({remote.Username})";
        }

        private async Task<string?> GetRemoteSummaryAsync(int? remoteId)
        {
            if (remoteId is null)
            {
                return null;
            }

            var remote = await _dbContext.Remotes.FindAsync(remoteId.Value);
            return remote is null ? null : BuildRemoteSummary(remote);
        }
    }
}
