using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.PeripheralType;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class PeripheralTypeServiceTests
    {
        private static PeripheralTypeService CreateService(Gestor_Equipos.Data.MyDbContext db)
        {
            return new PeripheralTypeService(db, new PasswordHasher<UserSystem>());
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
        public async Task CreateAsync_CreatesType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var id = await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Teclado" });

            var type = db.PeripheralTypes.Single(t => t.Id == id);
            Assert.Equal("Teclado", type.Name);
        }

        [Fact]
        public async Task CreateAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Mouse" });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "mouse" }));
        }

        [Fact]
        public async Task UpdateAsync_RenamesType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id = await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Monitr" });

            await service.UpdateAsync(id, new PeripheralTypeEditViewModel { Id = id, Name = "Monitor" });

            var type = db.PeripheralTypes.Single(t => t.Id == id);
            Assert.Equal("Monitor", type.Name);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id1 = await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Teclado" });
            await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Mouse" });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateAsync(id1, new PeripheralTypeEditViewModel { Id = id1, Name = "mouse" }));
        }

        [Fact]
        public async Task UpdateAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateAsync(999, new PeripheralTypeEditViewModel { Id = 999, Name = "X" }));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenPeripheralsReferenceType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Camara" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-inuse", "AdminPass1!");

            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.SaveChanges();
            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();
            db.Peripherals.Add(new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = typeId, Brand = "B", Model = "M" });
            db.SaveChanges();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(typeId, adminUserSystemId, "AdminPass1!"));

            Assert.NotNull(db.PeripheralTypes.SingleOrDefault(t => t.Id == typeId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordIncorrect_AndPersistsType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Diadema" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-wrongpw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(typeId, adminUserSystemId, "IncorrectPassword!"));

            Assert.NotNull(db.PeripheralTypes.SingleOrDefault(t => t.Id == typeId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Parlantes" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-nopw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(typeId, adminUserSystemId, null));
        }

        [Fact]
        public async Task DeleteAsync_RemovesType_WhenPasswordCorrectAndUnused()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Impresora" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-ok", "AdminPass1!");

            await service.DeleteAsync(typeId, adminUserSystemId, "AdminPass1!");

            Assert.Empty(db.PeripheralTypes.Where(t => t.Id == typeId));
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOrderedByName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Zeta" });
            await service.CreateAsync(new PeripheralTypeCreateViewModel { Name = "Alfa" });

            var result = await service.GetAllAsync();

            Assert.Equal("Alfa", result[0].Name);
            Assert.Equal("Zeta", result[1].Name);
        }
    }
}
