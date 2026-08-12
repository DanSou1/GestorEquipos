using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Desktop;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class DesktopServiceTests
    {
        private static (Area area, Regional regional, OSVersion os, Ram ram) SeedLookups(Gestor_Equipos.Data.MyDbContext db)
        {
            var area = new Area { Name = "Sistemas" };
            var regional = new Regional { Name = "Bogota" };
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.SaveChanges();
            return (area, regional, os, ram);
        }

        private static Users SeedUser(Gestor_Equipos.Data.MyDbContext db, Area area, Regional regional, string name = "Juan")
        {
            var user = new Users
            {
                Name = name,
                LastName = "Perez",
                Email = $"{name.ToLower()}@empresa.com",
                EmailTeams = $"{name.ToLower()}@empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id
            };
            db.Users.Add(user);
            db.SaveChanges();
            return user;
        }

        private static Desktop SeedDesktop(Gestor_Equipos.Data.MyDbContext db, OSVersion os, Ram ram, bool estado = true)
        {
            var desktop = new Desktop
            {
                NameDesktop = "PC-001",
                SerialNumber = "SN-001",
                Brand = "Dell",
                Model = "OptiPlex",
                Processor = "i5",
                Disk = "256GB SSD",
                OSVersionId = os.Id,
                RamId = ram.Id,
                RamType = RamType.DDR4,
                Estado = estado
            };
            db.Desktops.Add(desktop);
            db.SaveChanges();
            return desktop;
        }

        [Fact]
        public async Task GetAllAsync_ReturnsLatestAssignmentInfo()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var oldUser = SeedUser(db, area, regional, "Old");
            var newUser = SeedUser(db, area, regional, "New");
            var desktop = SeedDesktop(db, os, ram);

            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = oldUser.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = newUser.Id, DateAsignation = new DateOnly(2026, 6, 1) });
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            var result = await service.GetAllAsync();

            var item = Assert.Single(result);
            Assert.Equal("New Perez", item.UserName);
            Assert.Equal("Activo", item.Status);
        }

        [Fact]
        public async Task GetAllAsync_NoAssignment_ShowsSinAsignar()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            SeedDesktop(db, os, ram);

            var service = new DesktopService(db, new AsignationService(db));
            var result = await service.GetAllAsync();

            var item = Assert.Single(result);
            Assert.Equal("Sin asignar", item.UserName);
            Assert.Equal("-", item.AreaName);
            Assert.Equal("-", item.RegionalName);
        }

        [Fact]
        public async Task GetAllAsync_ShowsDisponibleWhenCurrentHolderIsInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var user = SeedUser(db, area, regional);
            var desktop = SeedDesktop(db, os, ram);

            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            user.Activo = false;
            user.DeactivatedAt = new DateOnly(2026, 2, 1);
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            var result = await service.GetAllAsync();

            var item = Assert.Single(result);
            Assert.Equal("Disponible", item.UserName);
            Assert.Equal(area.Name, item.AreaName);
            Assert.Equal(regional.Name, item.RegionalName);
        }

        [Fact]
        public async Task GetDetailAsync_ShowsDisponibleAndDeactivatedDate()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var user = SeedUser(db, area, regional);
            var desktop = SeedDesktop(db, os, ram);

            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            user.Activo = false;
            user.DeactivatedAt = new DateOnly(2026, 2, 1);
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            var result = await service.GetDetailAsync(desktop.Id);

            Assert.NotNull(result);
            Assert.Equal("Disponible", result!.CurrentUserName);
            Assert.Equal(new DateOnly(2026, 2, 1), result.CurrentSinceDate);
            Assert.Equal(area.Name, result.CurrentAreaName);
            Assert.Equal(regional.Name, result.CurrentRegionalName);
        }

        [Fact]
        public async Task GetEquipmentStatsAsync_CountsActivoWhenHolderIsActive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var user = SeedUser(db, area, regional);
            var desktop = SeedDesktop(db, os, ram);

            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            var stats = await service.GetEquipmentStatsAsync();

            Assert.Equal(1, stats.Total);
            Assert.Equal(1, stats.Activos);
            Assert.Equal(0, stats.Disponibles);
            Assert.Equal(0, stats.Raes);
        }

        [Fact]
        public async Task GetEquipmentStatsAsync_CountsDisponibleWhenHolderIsInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var user = SeedUser(db, area, regional);
            var desktop = SeedDesktop(db, os, ram);

            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            user.Activo = false;
            user.DeactivatedAt = new DateOnly(2026, 2, 1);
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            var stats = await service.GetEquipmentStatsAsync();

            Assert.Equal(1, stats.Total);
            Assert.Equal(0, stats.Activos);
            Assert.Equal(1, stats.Disponibles);
            Assert.Equal(0, stats.Raes);
        }

        [Fact]
        public async Task GetEquipmentStatsAsync_CountsDisponibleWhenNeverAssigned()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            SeedDesktop(db, os, ram);

            var service = new DesktopService(db, new AsignationService(db));
            var stats = await service.GetEquipmentStatsAsync();

            Assert.Equal(1, stats.Total);
            Assert.Equal(0, stats.Activos);
            Assert.Equal(1, stats.Disponibles);
            Assert.Equal(0, stats.Raes);
        }

        [Fact]
        public async Task GetEquipmentStatsAsync_CountsRaesRegardlessOfHolder()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var user = SeedUser(db, area, regional);
            var desktop = SeedDesktop(db, os, ram, estado: false);

            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            var stats = await service.GetEquipmentStatsAsync();

            Assert.Equal(1, stats.Total);
            Assert.Equal(0, stats.Activos);
            Assert.Equal(0, stats.Disponibles);
            Assert.Equal(1, stats.Raes);
        }

        [Fact]
        public async Task GetEquipmentStatsAsync_TotalsMatchSumOfCategories()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var activeUser = SeedUser(db, area, regional, "Active");
            var inactiveUser = SeedUser(db, area, regional, "Inactive");

            var activeDesktop = SeedDesktop(db, os, ram);
            activeDesktop.NameDesktop = "PC-Active";
            activeDesktop.SerialNumber = "SN-Active";

            var disponibleDesktop = SeedDesktop(db, os, ram);
            disponibleDesktop.NameDesktop = "PC-Disponible";
            disponibleDesktop.SerialNumber = "SN-Disponible";

            var raesDesktop = SeedDesktop(db, os, ram, estado: false);
            raesDesktop.NameDesktop = "PC-Raes";
            raesDesktop.SerialNumber = "SN-Raes";
            db.SaveChanges();

            db.Asignations.Add(new Asignation { DesktopId = activeDesktop.Id, UserId = activeUser.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.Asignations.Add(new Asignation { DesktopId = disponibleDesktop.Id, UserId = inactiveUser.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            inactiveUser.Activo = false;
            inactiveUser.DeactivatedAt = new DateOnly(2026, 2, 1);
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            var stats = await service.GetEquipmentStatsAsync();

            Assert.Equal(3, stats.Total);
            Assert.Equal(1, stats.Activos);
            Assert.Equal(1, stats.Disponibles);
            Assert.Equal(1, stats.Raes);
            Assert.Equal(stats.Total, stats.Activos + stats.Disponibles + stats.Raes);
        }

        [Fact]
        public async Task GetDetailAsync_ReturnsNull_WhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new DesktopService(db, new AsignationService(db));

            var result = await service.GetDetailAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetDetailAsync_ReturnsFullHojaDeVida()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var user = SeedUser(db, area, regional);
            var desktop = SeedDesktop(db, os, ram);

            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });

            var peripheralType = new PeripheralType { Name = "Mouse" };
            db.PeripheralTypes.Add(peripheralType);
            db.SaveChanges();

            var peripheral = new Peripheral
            {
                DesktopId = desktop.Id,
                PeripheralTypeId = peripheralType.Id,
                Brand = "Logitech",
                Model = "M100",
                Estado = true
            };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            var maintenanceType = new MaintenanceType { Type = "Preventivo" };
            db.MaintenanceTypes.Add(maintenanceType);

            var role = new Rol { Name = AuthBootstrapper.AdministradorRoleName };
            db.Rols.Add(role);
            db.SaveChanges();

            var technicianUser = SeedUser(db, area, regional, "Tecnico");
            var technicianSystem = new UserSystem { Username = "tecnico", PasswordHash = "x", UserId = technicianUser.Id, RolId = role.Id };
            db.UserSystems.Add(technicianSystem);
            db.SaveChanges();

            db.Maintenances.Add(new Maintenance
            {
                DesktopId = desktop.Id,
                MaintenanceTypeId = maintenanceType.Id,
                Date = new DateOnly(2026, 3, 1),
                Description = "Limpieza general",
                TechnicianName = "Tecnico Perez"
            });

            db.Licenses.Add(new License { DesktopId = desktop.Id, SoftwareType = "Windows 10", NoLicense = true });

            db.SpecChangeLogs.Add(new SpecChangeLog
            {
                DesktopId = desktop.Id,
                FieldName = "RAM",
                OldValue = "4GB",
                NewValue = "8GB",
                Date = new DateOnly(2026, 4, 1),
                ChangedByUserSystemId = technicianSystem.Id
            });
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            var result = await service.GetDetailAsync(desktop.Id);

            Assert.NotNull(result);
            Assert.Equal("Juan Perez", result!.CurrentUserName);
            Assert.Single(result.AsignationHistory);
            Assert.Single(result.Peripherals);
            Assert.Single(result.Maintenances);
            Assert.Equal("Tecnico Perez", result.Maintenances[0].TechnicianName);
            Assert.Single(result.Licenses);
            Assert.True(result.Licenses[0].NoLicense);
            Assert.Single(result.SpecChangeLogs);
        }

        [Fact]
        public async Task CreateAsync_AddsDesktopWithEstadoActivo()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            var service = new DesktopService(db, new AsignationService(db));

            var id = await service.CreateAsync(new DesktopCreateViewModel
            {
                NameDesktop = "PC-002",
                SerialNumber = "SN-002",
                Brand = "HP",
                Model = "Elite",
                Processor = "i7",
                Disk = "512GB SSD",
                OSVersionId = os.Id,
                RamId = ram.Id
            });

            var desktop = db.Desktops.Single(d => d.Id == id);
            Assert.True(desktop.Estado);
            Assert.Equal("PC-002", desktop.NameDesktop);
        }

        [Fact]
        public async Task UpdateSpecsAsync_LogsOnlyChangedFields()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            var newRam = new Ram { Especification = "16GB" };
            db.Rams.Add(newRam);

            var role = new Rol { Name = AuthBootstrapper.AdministradorRoleName };
            db.Rols.Add(role);
            db.SaveChanges();

            var (area, regional, _, _) = (new Area { Name = "A" }, new Regional { Name = "R" }, os, ram);
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.SaveChanges();

            var adminUser = SeedUser(db, area, regional, "Admin");
            var adminSystem = new UserSystem { Username = "admin", PasswordHash = "x", UserId = adminUser.Id, RolId = role.Id };
            db.UserSystems.Add(adminSystem);
            db.SaveChanges();

            var desktop = SeedDesktop(db, os, ram);
            var service = new DesktopService(db, new AsignationService(db));

            var vm = new DesktopEditViewModel
            {
                NameDesktop = desktop.NameDesktop,
                SerialNumber = desktop.SerialNumber,
                Brand = "NuevaMarca",
                Model = desktop.Model,
                Processor = desktop.Processor,
                Disk = desktop.Disk,
                OSVersionId = desktop.OSVersionId,
                RamType = desktop.RamType,
                RamId = newRam.Id
            };

            await service.UpdateSpecsAsync(desktop.Id, vm, adminSystem.Id);

            var logs = db.SpecChangeLogs.Where(l => l.DesktopId == desktop.Id).ToList();
            Assert.Equal(2, logs.Count);
            Assert.Contains(logs, l => l.FieldName == "Marca" && l.OldValue == "Dell" && l.NewValue == "NuevaMarca");
            Assert.Contains(logs, l => l.FieldName == "RAM" && l.NewValue == "16GB");

            var updated = db.Desktops.Single(d => d.Id == desktop.Id);
            Assert.Equal("NuevaMarca", updated.Brand);
            Assert.Equal(newRam.Id, updated.RamId);
        }

        [Fact]
        public async Task UpdateSpecsAsync_LogsRamTypeChangeIndependentlyFromCapacity()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);

            var role = new Rol { Name = AuthBootstrapper.AdministradorRoleName };
            db.Rols.Add(role);
            db.SaveChanges();

            var adminUser = SeedUser(db, area, regional, "Admin");
            var adminSystem = new UserSystem { Username = "admin", PasswordHash = "x", UserId = adminUser.Id, RolId = role.Id };
            db.UserSystems.Add(adminSystem);
            db.SaveChanges();

            var desktop = SeedDesktop(db, os, ram);
            var service = new DesktopService(db, new AsignationService(db));

            var vm = new DesktopEditViewModel
            {
                NameDesktop = desktop.NameDesktop,
                SerialNumber = desktop.SerialNumber,
                Brand = desktop.Brand,
                Model = desktop.Model,
                Processor = desktop.Processor,
                Disk = desktop.Disk,
                OSVersionId = desktop.OSVersionId,
                RamType = RamType.DDR5,
                RamId = desktop.RamId
            };

            await service.UpdateSpecsAsync(desktop.Id, vm, adminSystem.Id);

            var logs = db.SpecChangeLogs.Where(l => l.DesktopId == desktop.Id).ToList();
            Assert.Single(logs);
            Assert.Contains(logs, l => l.FieldName == "Tipo de RAM" && l.OldValue == "DDR4" && l.NewValue == "DDR5");

            var updated = db.Desktops.Single(d => d.Id == desktop.Id);
            Assert.Equal(RamType.DDR5, updated.RamType);
        }

        [Fact]
        public async Task DeactivateAsync_SetsEstadoFalseAndAssignsToRaes()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var desktop = SeedDesktop(db, os, ram);

            var raesUser = new Users
            {
                Name = "RAES",
                LastName = "Sistema",
                Email = AuthBootstrapper.RaesUserEmail,
                EmailTeams = AuthBootstrapper.RaesUserEmail,
                AreaId = area.Id,
                RegionalId = regional.Id
            };
            db.Users.Add(raesUser);
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            await service.DeactivateAsync(desktop.Id);

            var updated = db.Desktops.Single(d => d.Id == desktop.Id);
            Assert.False(updated.Estado);

            var lastAsignation = db.Asignations.Where(a => a.DesktopId == desktop.Id).Single();
            Assert.Equal(raesUser.Id, lastAsignation.UserId);
        }

        [Fact]
        public async Task UpdateSpecsAsync_LogsOSVersionAndRemoteChanges()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            var newOs = new OSVersion { TypeSO = "Windows", Version = "10" };
            var remote = new Remote { ConnectionType = RemoteConnectionType.EscritorioRemotoWindows, IPAddress = "10.0.0.1", Port = "3389", Username = "PC01\\jperez", Password = "clave123" };
            db.OSVersions.Add(newOs);
            db.Remotes.Add(remote);
            db.SaveChanges();

            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var role = new Rol { Name = AuthBootstrapper.AdministradorRoleName };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.Rols.Add(role);
            db.SaveChanges();

            var adminUser = SeedUser(db, area, regional, "Admin2");
            var adminSystem = new UserSystem { Username = "admin2", PasswordHash = "x", UserId = adminUser.Id, RolId = role.Id };
            db.UserSystems.Add(adminSystem);
            db.SaveChanges();

            var desktop = SeedDesktop(db, os, ram);
            var service = new DesktopService(db, new AsignationService(db));

            var vm = new DesktopEditViewModel
            {
                NameDesktop = desktop.NameDesktop,
                SerialNumber = desktop.SerialNumber,
                Brand = desktop.Brand,
                Model = desktop.Model,
                Processor = desktop.Processor,
                Disk = desktop.Disk,
                OSVersionId = newOs.Id,
                RamId = desktop.RamId,
                RemoteSelection = remote.Id.ToString()
            };

            await service.UpdateSpecsAsync(desktop.Id, vm, adminSystem.Id);

            var logs = db.SpecChangeLogs.Where(l => l.DesktopId == desktop.Id).ToList();
            Assert.Contains(logs, l => l.FieldName == "Sistema Operativo" && l.NewValue == "Windows 10");
            Assert.Contains(logs, l => l.FieldName == "Acceso remoto" && l.NewValue == "RDP 10.0.0.1:3389 (PC01\\jperez)");

            var updated = db.Desktops.Single(d => d.Id == desktop.Id);
            Assert.Equal(newOs.Id, updated.OSVersionId);
            Assert.Equal(remote.Id, updated.RemoteId);
        }

        [Fact]
        public async Task DeactivateAsync_SucceedsWhenDesktopHasActiveCurrentHolder()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var currentHolder = SeedUser(db, area, regional, "Held");
            var desktop = SeedDesktop(db, os, ram);
            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = currentHolder.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var raesUser = new Users
            {
                Name = "RAES",
                LastName = "Sistema",
                Email = AuthBootstrapper.RaesUserEmail,
                EmailTeams = AuthBootstrapper.RaesUserEmail,
                AreaId = area.Id,
                RegionalId = regional.Id
            };
            db.Users.Add(raesUser);
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            await service.DeactivateAsync(desktop.Id);

            var updated = db.Desktops.Single(d => d.Id == desktop.Id);
            Assert.False(updated.Estado);

            var lastAsignation = db.Asignations.Where(a => a.DesktopId == desktop.Id).OrderByDescending(a => a.Id).First();
            Assert.Equal(raesUser.Id, lastAsignation.UserId);
        }

        [Fact]
        public async Task DeactivateAsync_NoOpWhenAlreadyInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            var desktop = SeedDesktop(db, os, ram, estado: false);

            var service = new DesktopService(db, new AsignationService(db));
            await service.DeactivateAsync(desktop.Id);

            Assert.Empty(db.Asignations.Where(a => a.DesktopId == desktop.Id));
        }

        // Verifies the outcome only: rows for every dependent table are gone. It cannot
        // catch a real FK-ordering bug, since the InMemory provider doesn't enforce
        // Asignation.DesktopId's DeleteBehavior.Restrict the way SQL Server does.
        [Fact]
        public async Task DeleteAsync_RemovesDesktopAndAllDependentRows()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var user = SeedUser(db, area, regional);
            var desktop = SeedDesktop(db, os, ram);

            var role = new Rol { Name = AuthBootstrapper.AdministradorRoleName };
            db.Rols.Add(role);
            db.SaveChanges();
            var adminUser = SeedUser(db, area, regional, "Admin3");
            var adminSystem = new UserSystem { Username = "admin3", PasswordHash = "x", UserId = adminUser.Id, RolId = role.Id };
            db.UserSystems.Add(adminSystem);
            db.SaveChanges();

            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });

            var peripheralType = new PeripheralType { Name = "Mouse" };
            db.PeripheralTypes.Add(peripheralType);
            db.SaveChanges();
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = peripheralType.Id, Brand = "Logitech", Model = "M100" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            var maintenanceType = new MaintenanceType { Type = "Preventivo" };
            db.MaintenanceTypes.Add(maintenanceType);
            db.SaveChanges();
            db.Maintenances.Add(new Maintenance { DesktopId = desktop.Id, MaintenanceTypeId = maintenanceType.Id, Date = new DateOnly(2026, 1, 1), Description = "Test", TechnicianName = "Test" });
            db.PeripheralMaintenances.Add(new PeripheralMaintenance { PeripheralId = peripheral.Id, MaintenanceTypeId = maintenanceType.Id, Date = new DateOnly(2026, 1, 1), Description = "Test", TechnicianName = "Test" });
            db.PeripheralAssignments.Add(new PeripheralAssignment { PeripheralId = peripheral.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });

            db.Licenses.Add(new License { DesktopId = desktop.Id, SoftwareType = "Office", LicenseKey = "AAAAA-BBBBB", NoLicense = false });
            db.SpecChangeLogs.Add(new SpecChangeLog { DesktopId = desktop.Id, FieldName = "RAM", OldValue = "8GB", NewValue = "16GB", Date = new DateOnly(2026, 1, 1), ChangedByUserSystemId = adminSystem.Id });
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            await service.DeleteAsync(desktop.Id);

            Assert.Empty(db.Desktops.Where(d => d.Id == desktop.Id));
            Assert.Empty(db.Asignations.Where(a => a.DesktopId == desktop.Id));
            Assert.Empty(db.Peripherals.Where(p => p.DesktopId == desktop.Id));
            Assert.Empty(db.PeripheralMaintenances.Where(m => m.PeripheralId == peripheral.Id));
            Assert.Empty(db.PeripheralAssignments.Where(a => a.PeripheralId == peripheral.Id));
            Assert.Empty(db.Maintenances.Where(m => m.DesktopId == desktop.Id));
            Assert.Empty(db.Licenses.Where(l => l.DesktopId == desktop.Id));
            Assert.Empty(db.SpecChangeLogs.Where(l => l.DesktopId == desktop.Id));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenDesktopNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new DesktopService(db, new AsignationService(db));

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(999));
        }

        [Fact]
        public async Task CreateAsync_WithRemoteSelectionNewRdp_CreatesRemoteAndLinksIt()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            var service = new DesktopService(db, new AsignationService(db));

            var vm = new DesktopCreateViewModel
            {
                NameDesktop = "PC-002",
                SerialNumber = "SN-002",
                Brand = "Dell",
                Model = "OptiPlex",
                Processor = "i5",
                Disk = "256GB SSD",
                OSVersionId = os.Id,
                RamId = ram.Id,
                RemoteSelection = "new",
                NewRemoteConnectionType = RemoteConnectionType.EscritorioRemotoWindows,
                NewRemoteIPAddress = "10.0.0.5",
                NewRemotePort = "3389",
                NewRemoteUsername = "PC02\\jperez",
                NewRemotePassword = "clave123"
            };

            var id = await service.CreateAsync(vm);

            var desktop = db.Desktops.Include(d => d.Remote).Single(d => d.Id == id);
            Assert.NotNull(desktop.Remote);
            Assert.Equal(RemoteConnectionType.EscritorioRemotoWindows, desktop.Remote!.ConnectionType);
            Assert.Equal("10.0.0.5", desktop.Remote.IPAddress);
            Assert.Equal("3389", desktop.Remote.Port);
            Assert.Equal("PC02\\jperez", desktop.Remote.Username);
            Assert.Equal("clave123", desktop.Remote.Password);
        }

        [Fact]
        public async Task CreateAsync_WithRemoteSelectionNewAplicativo_CreatesRemoteWithAppDescription()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            var service = new DesktopService(db, new AsignationService(db));

            var vm = new DesktopCreateViewModel
            {
                NameDesktop = "PC-003",
                SerialNumber = "SN-003",
                Brand = "Dell",
                Model = "OptiPlex",
                Processor = "i5",
                Disk = "256GB SSD",
                OSVersionId = os.Id,
                RamId = ram.Id,
                RemoteSelection = "new",
                NewRemoteConnectionType = RemoteConnectionType.Aplicativo,
                NewRemoteAppDescription = "SAP GUI"
            };

            var id = await service.CreateAsync(vm);

            var desktop = db.Desktops.Include(d => d.Remote).Single(d => d.Id == id);
            Assert.NotNull(desktop.Remote);
            Assert.Equal(RemoteConnectionType.Aplicativo, desktop.Remote!.ConnectionType);
            Assert.Equal("SAP GUI", desktop.Remote.AppDescription);
            Assert.Null(desktop.Remote.IPAddress);
            Assert.Null(desktop.Remote.Password);
        }

        [Fact]
        public async Task CreateAsync_WithExistingRemoteSelection_ReusesRemote()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.EscritorioRemotoWindows, IPAddress = "10.0.0.9", Port = "3389", Username = "PC09\\ana", Password = "clave9" };
            db.Remotes.Add(remote);
            db.SaveChanges();

            var service = new DesktopService(db, new AsignationService(db));
            var vm = new DesktopCreateViewModel
            {
                NameDesktop = "PC-004",
                SerialNumber = "SN-004",
                Brand = "Dell",
                Model = "OptiPlex",
                Processor = "i5",
                Disk = "256GB SSD",
                OSVersionId = os.Id,
                RamId = ram.Id,
                RemoteSelection = remote.Id.ToString()
            };

            var id = await service.CreateAsync(vm);

            var desktop = db.Desktops.Single(d => d.Id == id);
            Assert.Equal(remote.Id, desktop.RemoteId);
            Assert.Single(db.Remotes);
        }

        [Fact]
        public async Task CreateAsync_WithEmptyRemoteSelection_LeavesRemoteIdNull()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            var service = new DesktopService(db, new AsignationService(db));

            var vm = new DesktopCreateViewModel
            {
                NameDesktop = "PC-005",
                SerialNumber = "SN-005",
                Brand = "Dell",
                Model = "OptiPlex",
                Processor = "i5",
                Disk = "256GB SSD",
                OSVersionId = os.Id,
                RamId = ram.Id,
                RemoteSelection = ""
            };

            var id = await service.CreateAsync(vm);

            var desktop = db.Desktops.Single(d => d.Id == id);
            Assert.Null(desktop.RemoteId);
        }

        [Fact]
        public async Task UpdateSpecsAsync_SwitchingFromNoneToNewRemote_CreatesRemoteAndLogsFriendlySummary()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var role = new Rol { Name = AuthBootstrapper.AdministradorRoleName };
            db.Rols.Add(role);
            db.SaveChanges();
            var adminUser = SeedUser(db, area, regional, "Admin4");
            var adminSystem = new UserSystem { Username = "admin4", PasswordHash = "x", UserId = adminUser.Id, RolId = role.Id };
            db.UserSystems.Add(adminSystem);
            db.SaveChanges();

            var desktop = SeedDesktop(db, os, ram);
            var service = new DesktopService(db, new AsignationService(db));

            var vm = new DesktopEditViewModel
            {
                NameDesktop = desktop.NameDesktop,
                SerialNumber = desktop.SerialNumber,
                Brand = desktop.Brand,
                Model = desktop.Model,
                Processor = desktop.Processor,
                Disk = desktop.Disk,
                OSVersionId = desktop.OSVersionId,
                RamId = desktop.RamId,
                RemoteSelection = "new",
                NewRemoteConnectionType = RemoteConnectionType.Aplicativo,
                NewRemoteAppDescription = "SAP GUI"
            };

            await service.UpdateSpecsAsync(desktop.Id, vm, adminSystem.Id);

            var updated = db.Desktops.Include(d => d.Remote).Single(d => d.Id == desktop.Id);
            Assert.NotNull(updated.Remote);
            Assert.Equal("SAP GUI", updated.Remote!.AppDescription);

            var log = db.SpecChangeLogs.Single(l => l.DesktopId == desktop.Id && l.FieldName == "Acceso remoto");
            Assert.Null(log.OldValue);
            Assert.Equal("Aplicativo: SAP GUI", log.NewValue);
        }
    }
}
