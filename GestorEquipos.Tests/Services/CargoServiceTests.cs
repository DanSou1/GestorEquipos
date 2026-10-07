using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class CargoServiceTests
    {
        private static CargoService CreateService(Gestor_Equipos.Data.MyDbContext db)
        {
            return new CargoService(db, new PasswordHasher<UserSystem>());
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
        public async Task CreateAsync_CreatesCargo()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var id = await service.CreateAsync("Técnico en Sistemas");

            var cargo = db.Cargos.Single(c => c.Id == id);
            Assert.Equal("Técnico en Sistemas", cargo.Name);
        }

        [Fact]
        public async Task CreateAsync_TrimsName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var id = await service.CreateAsync("  Auxiliar Administrativo  ");

            var cargo = db.Cargos.Single(c => c.Id == id);
            Assert.Equal("Auxiliar Administrativo", cargo.Name);
        }

        [Fact]
        public async Task CreateAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync("Técnico en Sistemas");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAsync("técnico en sistemas"));
        }

        [Fact]
        public async Task UpdateAsync_RenamesCargo()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id = await service.CreateAsync("Tecnico");

            await service.UpdateAsync(id, "Técnico en Sistemas");

            var cargo = db.Cargos.Single(c => c.Id == id);
            Assert.Equal("Técnico en Sistemas", cargo.Name);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsOnDuplicateName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id1 = await service.CreateAsync("Técnico en Sistemas");
            await service.CreateAsync("Auxiliar Administrativo");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateAsync(id1, "auxiliar administrativo"));
        }

        [Fact]
        public async Task UpdateAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(999, "X"));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenCargoAssignmentsReferenceIt()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-inuse", "AdminPass1!");
            var cargoId = await service.CreateAsync("Técnico en Sistemas");
            var daniel = TestHelpers.CreateUser(db, "Daniel");
            db.CargoAssignments.Add(new CargoAssignment { UserId = daniel.Id, CargoId = cargoId, DateAsignation = new DateOnly(2024, 1, 1) });
            db.SaveChanges();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(cargoId, adminUserSystemId, "AdminPass1!"));

            Assert.NotNull(db.Cargos.SingleOrDefault(c => c.Id == cargoId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordIncorrect_AndPersistsCargo()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var cargoId = await service.CreateAsync("Auxiliar Administrativo");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-wrongpw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(cargoId, adminUserSystemId, "IncorrectPassword!"));

            Assert.NotNull(db.Cargos.SingleOrDefault(c => c.Id == cargoId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var cargoId = await service.CreateAsync("Jefe de Área");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-nopw", "AdminPass1!");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(cargoId, adminUserSystemId, null));
        }

        [Fact]
        public async Task DeleteAsync_RemovesCargo_WhenPasswordCorrectAndUnused()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var cargoId = await service.CreateAsync("Pasante");
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-ok", "AdminPass1!");

            await service.DeleteAsync(cargoId, adminUserSystemId, "AdminPass1!");

            Assert.Empty(db.Cargos.Where(c => c.Id == cargoId));
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOrderedByName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync("Zeta");
            await service.CreateAsync("Alfa");

            var result = await service.GetAllAsync();

            Assert.Equal("Alfa", result[0].Name);
            Assert.Equal("Zeta", result[1].Name);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNullWhenMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            Assert.Null(await service.GetByIdAsync(999));
        }

        [Fact]
        public async Task GetHistoryAsync_ReturnsNullWhenCargoMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            Assert.Null(await service.GetHistoryAsync(999));
        }

        // El tramo de una persona en un cargo termina en la fecha de su SIGUIENTE registro de
        // cargo (sea cual sea el cargo nuevo), no en el siguiente registro de ESTE cargo — dos
        // personas pueden tener el mismo cargo al mismo tiempo.
        [Fact]
        public async Task GetHistoryAsync_ComputesStintsAcrossMultipleUsersAndCargoChanges()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var tecnicoId = await service.CreateAsync("Técnico en Sistemas");
            var auxiliarId = await service.CreateAsync("Auxiliar Administrativo");

            var daniel = TestHelpers.CreateUser(db, "Daniel");
            var gabriel = TestHelpers.CreateUser(db, "Gabriel");

            // Daniel: Técnico desde 2024-01-01, luego pasa a Auxiliar desde 2025-01-01.
            db.CargoAssignments.Add(new CargoAssignment { UserId = daniel.Id, CargoId = tecnicoId, DateAsignation = new DateOnly(2024, 1, 1) });
            db.CargoAssignments.Add(new CargoAssignment { UserId = daniel.Id, CargoId = auxiliarId, DateAsignation = new DateOnly(2025, 1, 1) });
            // Gabriel: toma el cargo de Técnico justo cuando Daniel lo deja.
            db.CargoAssignments.Add(new CargoAssignment { UserId = gabriel.Id, CargoId = tecnicoId, DateAsignation = new DateOnly(2025, 1, 1) });
            db.SaveChanges();

            var tecnicoHistory = await service.GetHistoryAsync(tecnicoId);
            var auxiliarHistory = await service.GetHistoryAsync(auxiliarId);

            Assert.NotNull(tecnicoHistory);
            Assert.Equal(2, tecnicoHistory!.Items.Count);

            var danielStint = tecnicoHistory.Items.Single(i => i.UserId == daniel.Id);
            Assert.Equal(new DateOnly(2024, 1, 1), danielStint.StartDate);
            Assert.Equal(new DateOnly(2025, 1, 1), danielStint.EndDate);
            Assert.False(danielStint.IsCurrent);

            var gabrielStint = tecnicoHistory.Items.Single(i => i.UserId == gabriel.Id);
            Assert.Equal(new DateOnly(2025, 1, 1), gabrielStint.StartDate);
            Assert.Null(gabrielStint.EndDate);
            Assert.True(gabrielStint.IsCurrent);

            Assert.NotNull(auxiliarHistory);
            var danielAuxiliarStint = Assert.Single(auxiliarHistory!.Items);
            Assert.Equal(daniel.Id, danielAuxiliarStint.UserId);
            Assert.Equal(new DateOnly(2025, 1, 1), danielAuxiliarStint.StartDate);
            Assert.Null(danielAuxiliarStint.EndDate);
            Assert.True(danielAuxiliarStint.IsCurrent);
        }
    }
}
