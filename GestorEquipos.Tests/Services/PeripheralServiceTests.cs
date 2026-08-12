using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Peripheral;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class PeripheralServiceTests
    {
        private static PeripheralService CreateService(Gestor_Equipos.Data.MyDbContext db)
        {
            return new PeripheralService(db, new PasswordHasher<UserSystem>(), new PeripheralAssignmentService(db), new PeripheralMaintenanceService(db));
        }

        private static (Desktop desktop, PeripheralType type) SeedDesktopAndType(Gestor_Equipos.Data.MyDbContext db)
        {
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            var type = new PeripheralType { Name = "Teclado" };
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.PeripheralTypes.Add(type);
            db.SaveChanges();

            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();

            return (desktop, type);
        }

        private static int SeedAdminUserSystem(Gestor_Equipos.Data.MyDbContext db, string username, string password)
        {
            var area = new Area { Name = $"A-{username}" };
            var regional = new Regional { Name = $"R-{username}" };
            var role = new Rol { Name = "Administrador" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.Rols.Add(role);
            db.SaveChanges();

            var user = new Users { Name = "Admin", LastName = "User", Email = $"{username}@empresa.com", EmailTeams = $"{username}@empresa.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();

            var hasher = new PasswordHasher<UserSystem>();
            var userSystem = new UserSystem { Username = username, UserId = user.Id, RolId = role.Id, PasswordHash = string.Empty };
            userSystem.PasswordHash = hasher.HashPassword(userSystem, password);
            db.UserSystems.Add(userSystem);
            db.SaveChanges();

            return userSystem.Id;
        }

        [Fact]
        public async Task AddAsync_CreatesPeripheralWithEstadoActivo()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var service = CreateService(db);

            var id = await service.AddAsync(new PeripheralCreateViewModel
            {
                DesktopId = desktop.Id,
                PeripheralTypeId = type.Id,
                Brand = "Logitech",
                Model = "K120"
            });

            var peripheral = db.Peripherals.Single(p => p.Id == id);
            Assert.True(peripheral.Estado);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesBrandModelSerialAndType()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var otherType = new PeripheralType { Name = "Mouse" };
            db.PeripheralTypes.Add(otherType);
            db.SaveChanges();
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M", Serial = "S1" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            var service = CreateService(db);
            await service.UpdateAsync(peripheral.Id, new PeripheralEditViewModel
            {
                DesktopId = desktop.Id,
                PeripheralTypeId = otherType.Id,
                Brand = "Logitech",
                Model = "M170",
                Serial = "S2"
            });

            var updated = db.Peripherals.Single(p => p.Id == peripheral.Id);
            Assert.Equal(otherType.Id, updated.PeripheralTypeId);
            Assert.Equal("Logitech", updated.Brand);
            Assert.Equal("M170", updated.Model);
            Assert.Equal("S2", updated.Serial);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateAsync(999, new PeripheralEditViewModel { PeripheralTypeId = 1, Brand = "B", Model = "M" }));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordIncorrect_AndPersistsPeripheral()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-periph-wrongpw", "AdminPass1!");

            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(peripheral.Id, adminUserSystemId, "IncorrectPassword!"));

            Assert.NotNull(db.Peripherals.SingleOrDefault(p => p.Id == peripheral.Id));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-periph-nopw", "AdminPass1!");

            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(peripheral.Id, adminUserSystemId, null));
        }

        [Fact]
        public async Task DeleteAsync_RemovesPeripheral_WhenPasswordCorrect()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-periph-ok", "AdminPass1!");

            var service = CreateService(db);
            await service.DeleteAsync(peripheral.Id, adminUserSystemId, "AdminPass1!");

            Assert.Empty(db.Peripherals.Where(p => p.Id == peripheral.Id));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-periph-notfound", "AdminPass1!");
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(999, adminUserSystemId, "AdminPass1!"));
        }

        [Fact]
        public async Task AddAsync_AutoAssignsToDesktopsCurrentActiveHolder()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var area = new Area { Name = "A2" };
            var regional = new Regional { Name = "R2" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.SaveChanges();
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana-holder@x.com", EmailTeams = "ana-holder@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var service = CreateService(db);
            var id = await service.AddAsync(new PeripheralCreateViewModel { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" });

            var assignment = Assert.Single(db.PeripheralAssignments.Where(a => a.PeripheralId == id));
            Assert.Equal(user.Id, assignment.UserId);
        }

        [Fact]
        public async Task AddAsync_LeavesNoAssignmentWhenDesktopUnassigned()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);

            var service = CreateService(db);
            var id = await service.AddAsync(new PeripheralCreateViewModel { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" });

            Assert.Empty(db.PeripheralAssignments.Where(a => a.PeripheralId == id));
        }

        [Fact]
        public async Task AddAsync_LeavesNoAssignmentWhenDesktopHolderInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var area = new Area { Name = "A3" };
            var regional = new Regional { Name = "R3" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.SaveChanges();
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana-inactive@x.com", EmailTeams = "ana-inactive@x.com", AreaId = area.Id, RegionalId = regional.Id, Activo = false };
            db.Users.Add(user);
            db.SaveChanges();
            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var service = CreateService(db);
            var id = await service.AddAsync(new PeripheralCreateViewModel { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" });

            Assert.Empty(db.PeripheralAssignments.Where(a => a.PeripheralId == id));
        }

        [Fact]
        public async Task GetDetailAsync_ReturnsNullWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var detail = await service.GetDetailAsync(999);

            Assert.Null(detail);
        }

        [Fact]
        public async Task GetDetailAsync_ComputesCurrentHolderFromLatestAssignment()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var area = new Area { Name = "A4" };
            var regional = new Regional { Name = "R4" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.SaveChanges();
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana-detail@x.com", EmailTeams = "ana-detail@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();
            db.PeripheralAssignments.Add(new PeripheralAssignment { PeripheralId = peripheral.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var service = CreateService(db);
            var detail = await service.GetDetailAsync(peripheral.Id);

            Assert.NotNull(detail);
            Assert.Equal("Ana Gomez", detail!.CurrentUserName);
            Assert.Equal(desktop.Id, detail.DesktopId);
            Assert.Single(detail.AssignmentHistory);
        }

        [Fact]
        public async Task GetDetailAsync_ShowsDisponibleWhenHolderInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var area = new Area { Name = "A5" };
            var regional = new Regional { Name = "R5" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.SaveChanges();
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana-disp@x.com", EmailTeams = "ana-disp@x.com", AreaId = area.Id, RegionalId = regional.Id, Activo = false };
            db.Users.Add(user);
            db.SaveChanges();
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();
            db.PeripheralAssignments.Add(new PeripheralAssignment { PeripheralId = peripheral.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var service = CreateService(db);
            var detail = await service.GetDetailAsync(peripheral.Id);

            Assert.Equal("Disponible", detail!.CurrentUserName);
        }

        [Fact]
        public async Task GetDetailAsync_ShowsEstadoDerivedFromAssignmentAndBool()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var peripheralActivo = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M1", Estado = true };
            var peripheralRaes = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M2", Estado = false };
            db.Peripherals.AddRange(peripheralActivo, peripheralRaes);
            db.SaveChanges();

            var service = CreateService(db);
            var detailActivo = await service.GetDetailAsync(peripheralActivo.Id);
            var detailRaes = await service.GetDetailAsync(peripheralRaes.Id);

            Assert.Equal("Disponible", detailActivo!.Estado);
            Assert.Equal("Raes", detailRaes!.Estado);
        }

        [Fact]
        public async Task RetireToRaesAsync_SetsEstadoFalseAndAssignsToRaes()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var area = new Area { Name = "A6" };
            var regional = new Regional { Name = "R6" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.SaveChanges();
            var raesUser = new Users { Name = "RAES", LastName = "Sistema", Email = AuthBootstrapper.RaesUserEmail, EmailTeams = AuthBootstrapper.RaesUserEmail, AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(raesUser);
            db.SaveChanges();
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            var service = CreateService(db);
            await service.RetireToRaesAsync(peripheral.Id);

            var updated = db.Peripherals.Single(p => p.Id == peripheral.Id);
            Assert.False(updated.Estado);

            var lastAssignment = db.PeripheralAssignments.Where(a => a.PeripheralId == peripheral.Id).Single();
            Assert.Equal(raesUser.Id, lastAssignment.UserId);
        }

        [Fact]
        public async Task RetireToRaesAsync_NoOpWhenAlreadyRaes()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var area = new Area { Name = "A7" };
            var regional = new Regional { Name = "R7" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.SaveChanges();
            var raesUser = new Users { Name = "RAES", LastName = "Sistema", Email = AuthBootstrapper.RaesUserEmail, EmailTeams = AuthBootstrapper.RaesUserEmail, AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(raesUser);
            db.SaveChanges();
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M", Estado = false };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            var service = CreateService(db);
            await service.RetireToRaesAsync(peripheral.Id);

            Assert.Empty(db.PeripheralAssignments.Where(a => a.PeripheralId == peripheral.Id));
        }

        [Fact]
        public async Task RetireToRaesAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetireToRaesAsync(999));
        }

        [Fact]
        public async Task GetDetailAsync_IncludesMaintenanceHistory()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var maintenanceType = new MaintenanceType { Type = "Correctivo" };
            db.MaintenanceTypes.Add(maintenanceType);
            db.SaveChanges();

            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();
            db.PeripheralMaintenances.Add(new PeripheralMaintenance
            {
                PeripheralId = peripheral.Id,
                MaintenanceTypeId = maintenanceType.Id,
                Date = new DateOnly(2026, 1, 1),
                Description = "Cambio de teclas pegajosas",
                TechnicianName = "Carlos Ruiz"
            });
            db.SaveChanges();

            var service = CreateService(db);
            var detail = await service.GetDetailAsync(peripheral.Id);

            var maintenance = Assert.Single(detail!.Maintenances);
            Assert.Equal("Correctivo", maintenance.MaintenanceTypeName);
            Assert.Equal("Carlos Ruiz", maintenance.TechnicianName);
        }

        [Fact]
        public async Task GetInventoryStatsAsync_CountsActivoWhenHolderIsActive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var area = new Area { Name = "A9" };
            var regional = new Regional { Name = "R9" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.SaveChanges();
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana-inv1@x.com", EmailTeams = "ana-inv1@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();
            db.PeripheralAssignments.Add(new PeripheralAssignment { PeripheralId = peripheral.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var service = CreateService(db);
            var stats = await service.GetInventoryStatsAsync();

            Assert.Equal(1, stats.Total);
            Assert.Equal(1, stats.Activos);
            Assert.Equal(0, stats.Disponibles);
            Assert.Equal(0, stats.Raes);
        }

        [Fact]
        public async Task GetInventoryStatsAsync_CountsDisponibleWhenNeverAssigned()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            db.Peripherals.Add(new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" });
            db.SaveChanges();

            var service = CreateService(db);
            var stats = await service.GetInventoryStatsAsync();

            Assert.Equal(1, stats.Total);
            Assert.Equal(0, stats.Activos);
            Assert.Equal(1, stats.Disponibles);
            Assert.Equal(0, stats.Raes);
        }

        [Fact]
        public async Task GetInventoryStatsAsync_CountsRaesRegardlessOfHolder()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            db.Peripherals.Add(new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M", Estado = false });
            db.SaveChanges();

            var service = CreateService(db);
            var stats = await service.GetInventoryStatsAsync();

            Assert.Equal(1, stats.Total);
            Assert.Equal(0, stats.Activos);
            Assert.Equal(0, stats.Disponibles);
            Assert.Equal(1, stats.Raes);
        }

        [Fact]
        public async Task GetInventoryStatsAsync_TotalsMatchSumOfCategoriesAndGroupsByType()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, teclado) = SeedDesktopAndType(db);
            var monitor = new PeripheralType { Name = "Monitor" };
            db.PeripheralTypes.Add(monitor);
            db.SaveChanges();

            db.Peripherals.Add(new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = teclado.Id, Brand = "B", Model = "M1" });
            db.Peripherals.Add(new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = teclado.Id, Brand = "B", Model = "M2" });
            db.Peripherals.Add(new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = monitor.Id, Brand = "B", Model = "M3", Estado = false });
            db.SaveChanges();

            var service = CreateService(db);
            var stats = await service.GetInventoryStatsAsync();

            Assert.Equal(3, stats.Total);
            Assert.Equal(stats.Total, stats.Activos + stats.Disponibles + stats.Raes);
            Assert.Equal(2, stats.ByType.Count);
            Assert.Equal(2, stats.ByType.Single(t => t.TypeName == teclado.Name).Count);
            Assert.Equal(1, stats.ByType.Single(t => t.TypeName == monitor.Name).Count);
        }

        [Fact]
        public async Task GetInventoryAsync_ReturnsAllPeripheralsAcrossDesktops()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            db.Peripherals.Add(new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B1", Model = "M1" });
            db.Peripherals.Add(new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B2", Model = "M2" });
            db.SaveChanges();

            var service = CreateService(db);
            var result = await service.GetInventoryAsync();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetInventoryAsync_ComputesCurrentHolderFromLatestAssignment()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var area = new Area { Name = "A10" };
            var regional = new Regional { Name = "R10" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.SaveChanges();
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana-inv2@x.com", EmailTeams = "ana-inv2@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();
            db.PeripheralAssignments.Add(new PeripheralAssignment { PeripheralId = peripheral.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var service = CreateService(db);
            var result = await service.GetInventoryAsync();

            var item = Assert.Single(result);
            Assert.Equal("Ana Gomez", item.CurrentHolderName);
            Assert.Equal("Activo", item.Estado);
        }
    }
}
