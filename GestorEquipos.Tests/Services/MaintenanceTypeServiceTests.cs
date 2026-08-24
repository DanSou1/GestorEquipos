using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.MaintenanceType;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class MaintenanceTypeServiceTests
    {
        private static MaintenanceTypeService CreateService(Gestor_Equipos.Data.MyDbContext db)
        {
            return new MaintenanceTypeService(db, new PasswordHasher<UserSystem>());
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

            var id = await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Preventivo" });

            var type = db.MaintenanceTypes.Single(t => t.Id == id);
            Assert.Equal("Preventivo", type.Type);
        }

        [Fact]
        public async Task CreateAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Correctivo" });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "correctivo" }));
        }

        [Fact]
        public async Task UpdateAsync_RenamesType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id = await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Preventivvo" });

            await service.UpdateAsync(id, new MaintenanceTypeEditViewModel { Id = id, Type = "Preventivo" });

            var type = db.MaintenanceTypes.Single(t => t.Id == id);
            Assert.Equal("Preventivo", type.Type);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id1 = await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Preventivo" });
            await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Correctivo" });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateAsync(id1, new MaintenanceTypeEditViewModel { Id = id1, Type = "correctivo" }));
        }

        [Fact]
        public async Task UpdateAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateAsync(999, new MaintenanceTypeEditViewModel { Id = 999, Type = "X" }));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenMaintenancesReferenceType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Correctivo" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-inuse", "AdminPass1!");

            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.SaveChanges();
            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();
            db.Maintenances.Add(new Maintenance { DesktopId = desktop.Id, MaintenanceTypeId = typeId, Date = new DateOnly(2026, 1, 1), Description = "D", TechnicianName = "T" });
            db.SaveChanges();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(typeId, adminUserSystemId, "AdminPass1!"));

            Assert.NotNull(db.MaintenanceTypes.SingleOrDefault(t => t.Id == typeId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenPeripheralMaintenancesReferenceType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Correctivo" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-inuse-periph", "AdminPass1!");

            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            var peripheralType = new PeripheralType { Name = "Mouse" };
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.PeripheralTypes.Add(peripheralType);
            db.SaveChanges();
            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = peripheralType.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();
            db.PeripheralMaintenances.Add(new PeripheralMaintenance { PeripheralId = peripheral.Id, MaintenanceTypeId = typeId, Date = new DateOnly(2026, 1, 1), Description = "D", TechnicianName = "T" });
            db.SaveChanges();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(typeId, adminUserSystemId, "AdminPass1!"));

            Assert.NotNull(db.MaintenanceTypes.SingleOrDefault(t => t.Id == typeId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordIncorrect_AndPersistsType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Garantia" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-wrongpw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(typeId, adminUserSystemId, "IncorrectPassword!"));

            Assert.NotNull(db.MaintenanceTypes.SingleOrDefault(t => t.Id == typeId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Instalacion" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-nopw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(typeId, adminUserSystemId, null));
        }

        [Fact]
        public async Task DeleteAsync_RemovesType_WhenPasswordCorrectAndUnused()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var typeId = await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Diagnostico" });
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-ok", "AdminPass1!");

            await service.DeleteAsync(typeId, adminUserSystemId, "AdminPass1!");

            Assert.Empty(db.MaintenanceTypes.Where(t => t.Id == typeId));
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOrderedByType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Zeta" });
            await service.CreateAsync(new MaintenanceTypeCreateViewModel { Type = "Alfa" });

            var result = await service.GetAllAsync();

            Assert.Equal("Alfa", result[0].Type);
            Assert.Equal("Zeta", result[1].Type);
        }
    }
}
