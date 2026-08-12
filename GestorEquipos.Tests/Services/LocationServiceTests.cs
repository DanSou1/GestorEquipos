using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class LocationServiceTests
    {
        private static LocationService CreateService(Gestor_Equipos.Data.MyDbContext db)
        {
            return new LocationService(db, new PasswordHasher<UserSystem>());
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
        public async Task CreateAreaAsync_CreatesArea()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var id = await service.CreateAreaAsync("Contabilidad");

            var area = db.Areas.Single(a => a.Id == id);
            Assert.Equal("Contabilidad", area.Name);
        }

        [Fact]
        public async Task CreateAreaAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAreaAsync("Contabilidad");

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAreaAsync("contabilidad"));
        }

        [Fact]
        public async Task UpdateAreaAsync_RenamesArea()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id = await service.CreateAreaAsync("Contabilidad");

            await service.UpdateAreaAsync(id, "Finanzas");

            var area = db.Areas.Single(a => a.Id == id);
            Assert.Equal("Finanzas", area.Name);
        }

        [Fact]
        public async Task UpdateAreaAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id1 = await service.CreateAreaAsync("Contabilidad");
            await service.CreateAreaAsync("Finanzas");

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAreaAsync(id1, "finanzas"));
        }

        [Fact]
        public async Task UpdateAreaAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAreaAsync(999, "X"));
        }

        [Fact]
        public async Task DeleteAreaAsync_RemovesUnusedArea_WhenPasswordCorrect()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id = await service.CreateAreaAsync("Contabilidad");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-area-ok", "AdminPass1!");

            await service.DeleteAreaAsync(id, adminUserSystemId, "AdminPass1!");

            Assert.Empty(db.Areas.Where(a => a.Id == id));
        }

        [Fact]
        public async Task DeleteAreaAsync_UnassignsUsersAndRemovesArea_WhenReferenced()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var areaId = await service.CreateAreaAsync("Contabilidad");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-area-inuse", "AdminPass1!");
            var regional = new Regional { Name = "R" };
            db.Regionals.Add(regional);
            db.SaveChanges();
            var affectedUser = new Users { Name = "A", LastName = "B", Email = "a@x.com", EmailTeams = "a@x.com", AreaId = areaId, RegionalId = regional.Id };
            db.Users.Add(affectedUser);
            db.SaveChanges();

            await service.DeleteAreaAsync(areaId, adminUserSystemId, "AdminPass1!");

            Assert.Empty(db.Areas.Where(a => a.Id == areaId));
            var reloadedUser = db.Users.Single(u => u.Id == affectedUser.Id);
            Assert.Null(reloadedUser.AreaId);
        }

        [Fact]
        public async Task DeleteAreaAsync_ThrowsWhenAdminPasswordIncorrect_AndPersistsArea()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var areaId = await service.CreateAreaAsync("Contabilidad");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-area-wrongpw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAreaAsync(areaId, adminUserSystemId, "IncorrectPassword!"));

            Assert.NotNull(db.Areas.SingleOrDefault(a => a.Id == areaId));
        }

        [Fact]
        public async Task DeleteAreaAsync_ThrowsWhenAdminPasswordMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var areaId = await service.CreateAreaAsync("Contabilidad");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-area-nopw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAreaAsync(areaId, adminUserSystemId, null));
        }

        [Fact]
        public async Task DeleteAreaAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAreaAsync(999, 0, "AdminPass1!"));
        }

        [Fact]
        public async Task GetAllAreasAsync_ReturnsOrderedByName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAreaAsync("Zeta");
            await service.CreateAreaAsync("Alfa");

            var result = await service.GetAllAreasAsync();

            Assert.Equal("Alfa", result[0].Name);
            Assert.Equal("Zeta", result[1].Name);
        }

        [Fact]
        public async Task CreateRegionalAsync_CreatesRegional()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var id = await service.CreateRegionalAsync("Norte de Santander");

            var regional = db.Regionals.Single(r => r.Id == id);
            Assert.Equal("Norte de Santander", regional.Name);
        }

        [Fact]
        public async Task CreateRegionalAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateRegionalAsync("Bogota");

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRegionalAsync("bogota"));
        }

        [Fact]
        public async Task UpdateRegionalAsync_RenamesRegional()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id = await service.CreateRegionalAsync("Bogota");

            await service.UpdateRegionalAsync(id, "Bogota D.C.");

            var regional = db.Regionals.Single(r => r.Id == id);
            Assert.Equal("Bogota D.C.", regional.Name);
        }

        [Fact]
        public async Task DeleteRegionalAsync_RemovesUnusedRegional_WhenPasswordCorrect()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id = await service.CreateRegionalAsync("Bogota");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-regional-ok", "AdminPass1!");

            await service.DeleteRegionalAsync(id, adminUserSystemId, "AdminPass1!");

            Assert.Empty(db.Regionals.Where(r => r.Id == id));
        }

        [Fact]
        public async Task DeleteRegionalAsync_UnassignsUsersAndRemovesRegional_WhenReferenced()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var regionalId = await service.CreateRegionalAsync("Bogota");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-regional-inuse", "AdminPass1!");
            var area = new Area { Name = "A" };
            db.Areas.Add(area);
            db.SaveChanges();
            var affectedUser = new Users { Name = "A", LastName = "B", Email = "a2@x.com", EmailTeams = "a2@x.com", AreaId = area.Id, RegionalId = regionalId };
            db.Users.Add(affectedUser);
            db.SaveChanges();

            await service.DeleteRegionalAsync(regionalId, adminUserSystemId, "AdminPass1!");

            Assert.Empty(db.Regionals.Where(r => r.Id == regionalId));
            var reloadedUser = db.Users.Single(u => u.Id == affectedUser.Id);
            Assert.Null(reloadedUser.RegionalId);
        }

        [Fact]
        public async Task DeleteRegionalAsync_ThrowsWhenAdminPasswordIncorrect_AndPersistsRegional()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var regionalId = await service.CreateRegionalAsync("Bogota");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-regional-wrongpw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteRegionalAsync(regionalId, adminUserSystemId, "IncorrectPassword!"));

            Assert.NotNull(db.Regionals.SingleOrDefault(r => r.Id == regionalId));
        }

        [Fact]
        public async Task DeleteRegionalAsync_ThrowsWhenAdminPasswordMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var regionalId = await service.CreateRegionalAsync("Bogota");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-regional-nopw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteRegionalAsync(regionalId, adminUserSystemId, null));
        }
    }
}
