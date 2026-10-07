using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.DesktopType;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class DesktopTypeServiceTests
    {
        private static DesktopTypeService CreateService(Gestor_Equipos.Data.MyDbContext db)
        {
            return new DesktopTypeService(db, new PasswordHasher<UserSystem>());
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

            var id = await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Lenovo", Model = "ThinkCentre M720" });

            var type = db.DesktopTypes.Single(t => t.Id == id);
            Assert.Equal("Lenovo", type.Brand);
            Assert.Equal("ThinkCentre M720", type.Model);
        }

        [Fact]
        public async Task CreateAsync_TrimsBrandAndModel()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var id = await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "  Dell  ", Model = "  Optiplex 3020  " });

            var type = db.DesktopTypes.Single(t => t.Id == id);
            Assert.Equal("Dell", type.Brand);
            Assert.Equal("Optiplex 3020", type.Model);
        }

        [Fact]
        public async Task CreateAsync_ThrowsOnDuplicateBrandAndModel()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Hewlett-Packard", Model = "EliteDesk 800G2" });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "hewlett-packard", Model = "elitedesk 800g2" }));
        }

        [Fact]
        public async Task CreateAsync_AllowsSameBrandWithDifferentModel()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Hewlett-Packard", Model = "EliteDesk 800G2" });

            var id = await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Hewlett-Packard", Model = "ProDesk 400G3" });

            Assert.NotEqual(0, id);
        }

        [Fact]
        public async Task CreateAsync_AllowsSameModelWithDifferentBrand()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Dell", Model = "3020" });

            var id = await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Lenovo", Model = "3020" });

            Assert.NotEqual(0, id);
        }

        [Fact]
        public async Task UpdateAsync_RenamesType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id = await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Dell", Model = "Optiplex 302" });

            await service.UpdateAsync(id, new DesktopTypeEditViewModel { Id = id, Brand = "Dell", Model = "Optiplex 3020" });

            var type = db.DesktopTypes.Single(t => t.Id == id);
            Assert.Equal("Optiplex 3020", type.Model);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsOnDuplicateBrandAndModel()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id1 = await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Lenovo", Model = "ThinkCentre M720" });
            await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Dell", Model = "Optiplex 3020" });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateAsync(id1, new DesktopTypeEditViewModel { Id = id1, Brand = "dell", Model = "optiplex 3020" }));
        }

        [Fact]
        public async Task UpdateAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateAsync(999, new DesktopTypeEditViewModel { Id = 999, Brand = "X", Model = "Y" }));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenDesktopsReferenceType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-inuse", "AdminPass1!");
            var desktop = TestHelpers.CreateDesktop(db, "SN-INUSE");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(desktop.DesktopTypeId, adminUserSystemId, "AdminPass1!"));

            Assert.NotNull(db.DesktopTypes.SingleOrDefault(t => t.Id == desktop.DesktopTypeId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordIncorrect_AndPersistsType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Acer", Model = "Veriton" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-wrongpw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(typeId, adminUserSystemId, "IncorrectPassword!"));

            Assert.NotNull(db.DesktopTypes.SingleOrDefault(t => t.Id == typeId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Asus", Model = "ExpertCenter" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-nopw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(typeId, adminUserSystemId, null));
        }

        [Fact]
        public async Task DeleteAsync_RemovesType_WhenPasswordCorrectAndUnused()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Apple", Model = "Mac Mini" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-ok", "AdminPass1!");

            await service.DeleteAsync(typeId, adminUserSystemId, "AdminPass1!");

            Assert.Empty(db.DesktopTypes.Where(t => t.Id == typeId));
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOrderedByBrandThenModel()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Zeta Corp", Model = "Z1" });
            await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Alfa Corp", Model = "B1" });
            await service.CreateAsync(new DesktopTypeCreateViewModel { Brand = "Alfa Corp", Model = "A1" });

            var result = await service.GetAllAsync();

            Assert.Equal("Alfa Corp", result[0].Brand);
            Assert.Equal("A1", result[0].Model);
            Assert.Equal("Alfa Corp", result[1].Brand);
            Assert.Equal("B1", result[1].Model);
            Assert.Equal("Zeta Corp", result[2].Brand);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNullWhenMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var result = await service.GetByIdAsync(999);

            Assert.Null(result);
        }
    }
}
