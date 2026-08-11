using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class PeripheralAssignmentServiceTests
    {
        private static (Area area, Regional regional, OSVersion os, Ram ram, PeripheralType type) SeedLookups(Gestor_Equipos.Data.MyDbContext db)
        {
            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            var type = new PeripheralType { Name = "Mouse" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.PeripheralTypes.Add(type);
            db.SaveChanges();
            return (area, regional, os, ram, type);
        }

        private static Peripheral SeedPeripheral(Gestor_Equipos.Data.MyDbContext db, OSVersion os, Ram ram, PeripheralType type, bool estado = true)
        {
            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();

            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M", Estado = estado };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();
            return peripheral;
        }

        [Fact]
        public async Task AssignAsync_AddsAssignmentRow()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram, type) = SeedLookups(db);
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana@x.com", EmailTeams = "ana@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var peripheral = SeedPeripheral(db, os, ram, type);

            var service = new PeripheralAssignmentService(db);
            await service.AssignAsync(peripheral.Id, user.Id);

            var assignment = Assert.Single(db.PeripheralAssignments);
            Assert.Equal(peripheral.Id, assignment.PeripheralId);
            Assert.Equal(user.Id, assignment.UserId);
        }

        [Fact]
        public async Task AssignAsync_ThrowsWhenPeripheralNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _, _, _) = SeedLookups(db);
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana5@x.com", EmailTeams = "ana5@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();

            var service = new PeripheralAssignmentService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(999, user.Id));
        }

        [Fact]
        public async Task AssignAsync_ThrowsWhenPeripheralIsRetired()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram, type) = SeedLookups(db);
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana6@x.com", EmailTeams = "ana6@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var peripheral = SeedPeripheral(db, os, ram, type, estado: false);

            var service = new PeripheralAssignmentService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(peripheral.Id, user.Id));
        }

        [Fact]
        public async Task AssignAsync_AllowsRaesAssignmentEvenWhenRetired()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram, type) = SeedLookups(db);
            var raesUser = new Users { Name = "RAES", LastName = "Sistema", Email = AuthBootstrapper.RaesUserEmail, EmailTeams = AuthBootstrapper.RaesUserEmail, AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(raesUser);
            db.SaveChanges();
            var peripheral = SeedPeripheral(db, os, ram, type, estado: false);

            var service = new PeripheralAssignmentService(db);
            await service.AssignAsync(peripheral.Id, raesUser.Id);

            var assignment = Assert.Single(db.PeripheralAssignments);
            Assert.Equal(raesUser.Id, assignment.UserId);
        }

        [Fact]
        public async Task AssignAsync_ThrowsWhenActivelyHeldByDifferentActiveUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram, type) = SeedLookups(db);
            var userA = new Users { Name = "A", LastName = "Uno", Email = "a1@x.com", EmailTeams = "a1@x.com", AreaId = area.Id, RegionalId = regional.Id };
            var userB = new Users { Name = "B", LastName = "Dos", Email = "b1@x.com", EmailTeams = "b1@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.AddRange(userA, userB);
            db.SaveChanges();
            var peripheral = SeedPeripheral(db, os, ram, type);

            var service = new PeripheralAssignmentService(db);
            await service.AssignAsync(peripheral.Id, userA.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(peripheral.Id, userB.Id));
        }

        [Fact]
        public async Task AssignAsync_AllowsReassignToSameCurrentUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram, type) = SeedLookups(db);
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana7@x.com", EmailTeams = "ana7@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var peripheral = SeedPeripheral(db, os, ram, type);

            var service = new PeripheralAssignmentService(db);
            await service.AssignAsync(peripheral.Id, user.Id);
            await service.AssignAsync(peripheral.Id, user.Id);

            Assert.Equal(2, db.PeripheralAssignments.Count(a => a.PeripheralId == peripheral.Id));
        }

        [Fact]
        public async Task AssignAsync_AllowsAssignWhenCurrentHolderIsInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram, type) = SeedLookups(db);
            var userA = new Users { Name = "A", LastName = "Uno", Email = "a2@x.com", EmailTeams = "a2@x.com", AreaId = area.Id, RegionalId = regional.Id };
            var userB = new Users { Name = "B", LastName = "Dos", Email = "b2@x.com", EmailTeams = "b2@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.AddRange(userA, userB);
            db.SaveChanges();
            var peripheral = SeedPeripheral(db, os, ram, type);

            var service = new PeripheralAssignmentService(db);
            await service.AssignAsync(peripheral.Id, userA.Id);

            userA.Activo = false;
            userA.DeactivatedAt = DateOnly.FromDateTime(DateTime.Now);
            db.SaveChanges();

            await service.AssignAsync(peripheral.Id, userB.Id);

            var latest = db.PeripheralAssignments.Where(a => a.PeripheralId == peripheral.Id).OrderByDescending(a => a.Id).First();
            Assert.Equal(userB.Id, latest.UserId);
        }

        [Fact]
        public async Task GetHistoryAsync_ReturnsOrderedDescendingByDate()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram, type) = SeedLookups(db);
            var user1 = new Users { Name = "Ana", LastName = "Gomez", Email = "ana9@x.com", EmailTeams = "ana9@x.com", AreaId = area.Id, RegionalId = regional.Id };
            var user2 = new Users { Name = "Luis", LastName = "Diaz", Email = "luis9@x.com", EmailTeams = "luis9@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.AddRange(user1, user2);
            db.SaveChanges();
            var peripheral = SeedPeripheral(db, os, ram, type);

            db.PeripheralAssignments.Add(new PeripheralAssignment { PeripheralId = peripheral.Id, UserId = user1.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.PeripheralAssignments.Add(new PeripheralAssignment { PeripheralId = peripheral.Id, UserId = user2.Id, DateAsignation = new DateOnly(2026, 5, 1) });
            db.SaveChanges();

            var service = new PeripheralAssignmentService(db);
            var history = await service.GetHistoryAsync(peripheral.Id);

            Assert.Equal(2, history.Count);
            Assert.Equal(user2.Id, history[0].UserId);
            Assert.Equal(user1.Id, history[1].UserId);
        }

        [Fact]
        public async Task GetHistoryAsync_IncludesUserAreaAndRegional()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram, type) = SeedLookups(db);
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana10@x.com", EmailTeams = "ana10@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var peripheral = SeedPeripheral(db, os, ram, type);
            db.PeripheralAssignments.Add(new PeripheralAssignment { PeripheralId = peripheral.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var service = new PeripheralAssignmentService(db);
            var history = await service.GetHistoryAsync(peripheral.Id);

            var current = Assert.Single(history);
            Assert.Equal(area.Name, current.User.Area.Name);
            Assert.Equal(regional.Name, current.User.Regional.Name);
        }
    }
}
