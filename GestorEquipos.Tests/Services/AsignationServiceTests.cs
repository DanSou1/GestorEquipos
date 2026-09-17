using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class AsignationServiceTests
    {
        private static (Area area, Regional regional, OSVersion os, Ram ram) SeedLookups(Gestor_Equipos.Data.MyDbContext db)
        {
            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.SaveChanges();
            return (area, regional, os, ram);
        }

        private static Desktop SeedDesktop(Gestor_Equipos.Data.MyDbContext db, OSVersion os, Ram ram, bool estado = true)
        {
            // SerialNumber is unique per LOG-06 — even the InMemory provider enforces it — so
            // each call needs a distinct value for tests that seed more than one desktop.
            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN-" + Guid.NewGuid().ToString("N"), Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id, Estado = estado };
            db.Desktops.Add(desktop);
            db.SaveChanges();
            return desktop;
        }

        [Fact]
        public async Task AssignAsync_AddsAsignationRow()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);

            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana@x.com", EmailTeams = "ana@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram);

            var service = new AsignationService(db);
            await service.AssignAsync(desktop.Id, user.Id);

            var asignation = Assert.Single(db.Asignations);
            Assert.Equal(desktop.Id, asignation.DesktopId);
            Assert.Equal(user.Id, asignation.UserId);
        }

        [Fact]
        public async Task AssignAsync_ThrowsWhenDesktopNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _, _) = SeedLookups(db);
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana5@x.com", EmailTeams = "ana5@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();

            var service = new AsignationService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(999, user.Id));
        }

        [Fact]
        public async Task AssignAsync_ThrowsWhenDesktopIsRetired()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana6@x.com", EmailTeams = "ana6@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram, estado: false);

            var service = new AsignationService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(desktop.Id, user.Id));
        }

        [Fact]
        public async Task AssignAsync_AllowsRaesAssignmentEvenWhenDesktopRetired()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var raesUser = new Users { Name = "RAES", LastName = "Sistema", Email = AuthBootstrapper.RaesUserEmail, EmailTeams = AuthBootstrapper.RaesUserEmail, AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(raesUser);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram, estado: false);

            var service = new AsignationService(db);
            await service.AssignAsync(desktop.Id, raesUser.Id);

            var asignation = Assert.Single(db.Asignations);
            Assert.Equal(raesUser.Id, asignation.UserId);
        }

        [Fact]
        public async Task AssignAsync_ThrowsWhenActivelyHeldByDifferentActiveUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var userA = new Users { Name = "A", LastName = "Uno", Email = "a1@x.com", EmailTeams = "a1@x.com", AreaId = area.Id, RegionalId = regional.Id };
            var userB = new Users { Name = "B", LastName = "Dos", Email = "b1@x.com", EmailTeams = "b1@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.AddRange(userA, userB);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram);

            var service = new AsignationService(db);
            await service.AssignAsync(desktop.Id, userA.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(desktop.Id, userB.Id));
        }

        [Fact]
        public async Task AssignAsync_AllowsReassignToSameCurrentUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana7@x.com", EmailTeams = "ana7@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram);

            var service = new AsignationService(db);
            await service.AssignAsync(desktop.Id, user.Id);
            await service.AssignAsync(desktop.Id, user.Id);

            Assert.Equal(2, db.Asignations.Count(a => a.DesktopId == desktop.Id));
        }

        [Fact]
        public async Task AssignAsync_AllowsAssignWhenCurrentHolderIsInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var userA = new Users { Name = "A", LastName = "Uno", Email = "a2@x.com", EmailTeams = "a2@x.com", AreaId = area.Id, RegionalId = regional.Id };
            var userB = new Users { Name = "B", LastName = "Dos", Email = "b2@x.com", EmailTeams = "b2@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.AddRange(userA, userB);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram);

            var service = new AsignationService(db);
            await service.AssignAsync(desktop.Id, userA.Id);

            userA.Activo = false;
            userA.DeactivatedAt = DateOnly.FromDateTime(DateTime.Now);
            db.SaveChanges();

            await service.AssignAsync(desktop.Id, userB.Id);

            var latest = db.Asignations.Where(a => a.DesktopId == desktop.Id).OrderByDescending(a => a.Id).First();
            Assert.Equal(userB.Id, latest.UserId);
        }

        [Fact]
        public async Task AssignAsync_ThrowsWhenTargetUserIsInactive()
        {
            // LOG-04: the dropdown only lists active people, but a manipulated request could
            // still name someone inactive — the server must reject that too.
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var inactiveUser = new Users { Name = "Inactivo", LastName = "Persona", Email = "inactivo@x.com", EmailTeams = "inactivo@x.com", AreaId = area.Id, RegionalId = regional.Id, Activo = false };
            db.Users.Add(inactiveUser);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram);

            var service = new AsignationService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(desktop.Id, inactiveUser.Id));
            Assert.Empty(db.Asignations);
        }

        [Fact]
        public async Task ReturnAsync_ReleasesActiveHolderWithoutDeactivatingThem()
        {
            // UX-06: returning one equipo must not touch the holder's own Activo flag or their
            // other assignments — only DesktopService.DeactivateAsync (RAES) may do that.
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            db.Users.Add(new Users { Name = "Disponible", LastName = "Sistema", Email = AuthBootstrapper.UnassignedUserEmail, EmailTeams = AuthBootstrapper.UnassignedUserEmail, Activo = false });
            var holder = new Users { Name = "Ana", LastName = "Gomez", Email = "ana-return@x.com", EmailTeams = "ana-return@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(holder);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram);
            var otherDesktop = SeedDesktop(db, os, ram);

            var service = new AsignationService(db);
            await service.AssignAsync(desktop.Id, holder.Id);
            await service.AssignAsync(otherDesktop.Id, holder.Id);

            await service.ReturnAsync(desktop.Id);

            var latestOnReturned = db.Asignations.Where(a => a.DesktopId == desktop.Id).OrderByDescending(a => a.Id).First();
            Assert.Equal(AuthBootstrapper.UnassignedUserEmail, db.Users.Single(u => u.Id == latestOnReturned.UserId).Email);

            var holderAfter = db.Users.Single(u => u.Id == holder.Id);
            Assert.True(holderAfter.Activo);

            var latestOnOther = db.Asignations.Where(a => a.DesktopId == otherDesktop.Id).OrderByDescending(a => a.Id).First();
            Assert.Equal(holder.Id, latestOnOther.UserId);
        }

        [Fact]
        public async Task ReturnAsync_ThrowsWhenAlreadyDisponible()
        {
            using var db = TestHelpers.CreateDbContext();
            var (_, _, os, ram) = SeedLookups(db);
            db.Users.Add(new Users { Name = "Disponible", LastName = "Sistema", Email = AuthBootstrapper.UnassignedUserEmail, EmailTeams = AuthBootstrapper.UnassignedUserEmail, Activo = false });
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram);

            var service = new AsignationService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReturnAsync(desktop.Id));
        }

        [Fact]
        public async Task GetHistoryAsync_ReturnsOrderedDescendingByDate()
        {
            using var db = TestHelpers.CreateDbContext();
            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.SaveChanges();

            var user1 = new Users { Name = "Ana", LastName = "Gomez", Email = "ana@x.com", EmailTeams = "ana@x.com", AreaId = area.Id, RegionalId = regional.Id };
            var user2 = new Users { Name = "Luis", LastName = "Diaz", Email = "luis@x.com", EmailTeams = "luis@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.AddRange(user1, user2);
            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();

            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user1.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user2.Id, DateAsignation = new DateOnly(2026, 5, 1) });
            db.SaveChanges();

            var service = new AsignationService(db);
            var history = await service.GetHistoryAsync(desktop.Id);

            Assert.Equal(2, history.Count);
            Assert.Equal(user2.Id, history[0].UserId);
            Assert.Equal(user1.Id, history[1].UserId);
        }

        [Fact]
        public async Task GetHistoryAsync_IncludesUserAreaAndRegional()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, os, ram) = SeedLookups(db);
            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana8@x.com", EmailTeams = "ana8@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram);
            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            var service = new AsignationService(db);
            var history = await service.GetHistoryAsync(desktop.Id);

            var current = Assert.Single(history);
            Assert.Equal(area.Name, current.User.Area.Name);
            Assert.Equal(regional.Name, current.User.Regional.Name);
        }
    }
}
