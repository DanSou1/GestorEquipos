using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class LocationServiceTests
    {
        [Fact]
        public async Task CreateAreaAsync_CreatesArea()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);

            var id = await service.CreateAreaAsync("Contabilidad");

            var area = db.Areas.Single(a => a.Id == id);
            Assert.Equal("Contabilidad", area.Name);
        }

        [Fact]
        public async Task CreateAreaAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);
            await service.CreateAreaAsync("Contabilidad");

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAreaAsync("contabilidad"));
        }

        [Fact]
        public async Task UpdateAreaAsync_RenamesArea()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);
            var id = await service.CreateAreaAsync("Contabilidad");

            await service.UpdateAreaAsync(id, "Finanzas");

            var area = db.Areas.Single(a => a.Id == id);
            Assert.Equal("Finanzas", area.Name);
        }

        [Fact]
        public async Task UpdateAreaAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);
            var id1 = await service.CreateAreaAsync("Contabilidad");
            await service.CreateAreaAsync("Finanzas");

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAreaAsync(id1, "finanzas"));
        }

        [Fact]
        public async Task UpdateAreaAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAreaAsync(999, "X"));
        }

        [Fact]
        public async Task DeleteAreaAsync_RemovesUnusedArea()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);
            var id = await service.CreateAreaAsync("Contabilidad");

            await service.DeleteAreaAsync(id);

            Assert.Empty(db.Areas.Where(a => a.Id == id));
        }

        [Fact]
        public async Task DeleteAreaAsync_ThrowsWhenReferencedByUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);
            var areaId = await service.CreateAreaAsync("Contabilidad");
            var regional = new Regional { Name = "R" };
            db.Regionals.Add(regional);
            db.SaveChanges();
            db.Users.Add(new Users { Name = "A", LastName = "B", Email = "a@x.com", EmailTeams = "a@x.com", AreaId = areaId, RegionalId = regional.Id });
            db.SaveChanges();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAreaAsync(areaId));
        }

        [Fact]
        public async Task DeleteAreaAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAreaAsync(999));
        }

        [Fact]
        public async Task GetAllAreasAsync_ReturnsOrderedByName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);
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
            var service = new LocationService(db);

            var id = await service.CreateRegionalAsync("Norte de Santander");

            var regional = db.Regionals.Single(r => r.Id == id);
            Assert.Equal("Norte de Santander", regional.Name);
        }

        [Fact]
        public async Task CreateRegionalAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);
            await service.CreateRegionalAsync("Bogota");

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRegionalAsync("bogota"));
        }

        [Fact]
        public async Task UpdateRegionalAsync_RenamesRegional()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);
            var id = await service.CreateRegionalAsync("Bogota");

            await service.UpdateRegionalAsync(id, "Bogota D.C.");

            var regional = db.Regionals.Single(r => r.Id == id);
            Assert.Equal("Bogota D.C.", regional.Name);
        }

        [Fact]
        public async Task DeleteRegionalAsync_RemovesUnusedRegional()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);
            var id = await service.CreateRegionalAsync("Bogota");

            await service.DeleteRegionalAsync(id);

            Assert.Empty(db.Regionals.Where(r => r.Id == id));
        }

        [Fact]
        public async Task DeleteRegionalAsync_ThrowsWhenReferencedByUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new LocationService(db);
            var regionalId = await service.CreateRegionalAsync("Bogota");
            var area = new Area { Name = "A" };
            db.Areas.Add(area);
            db.SaveChanges();
            db.Users.Add(new Users { Name = "A", LastName = "B", Email = "a2@x.com", EmailTeams = "a2@x.com", AreaId = area.Id, RegionalId = regionalId });
            db.SaveChanges();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRegionalAsync(regionalId));
        }
    }
}
