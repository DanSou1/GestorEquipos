using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Desktop;
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
                Estado = PeripheralEstado.Activo
            };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            db.PeripheralObservations.Add(new PeripheralObservation
            {
                PeripheralId = peripheral.Id,
                Date = new DateOnly(2026, 2, 1),
                Type = PeripheralObservationType.Reparacion,
                Description = "Se limpio el sensor"
            });

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
                TechnicianUserSystemId = technicianSystem.Id
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
            Assert.Single(result.Peripherals[0].Observations);
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
            var remote = new Remote { IPAddress = "10.0.0.1", Port = "3389" };
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
                RemoteId = remote.Id
            };

            await service.UpdateSpecsAsync(desktop.Id, vm, adminSystem.Id);

            var logs = db.SpecChangeLogs.Where(l => l.DesktopId == desktop.Id).ToList();
            Assert.Contains(logs, l => l.FieldName == "Sistema Operativo" && l.NewValue == "Windows 10");
            Assert.Contains(logs, l => l.FieldName == "Remote");

            var updated = db.Desktops.Single(d => d.Id == desktop.Id);
            Assert.Equal(newOs.Id, updated.OSVersionId);
            Assert.Equal(remote.Id, updated.RemoteId);
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
    }
}
