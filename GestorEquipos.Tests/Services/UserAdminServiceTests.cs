using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.UserAdmin;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class UserAdminServiceTests
    {
        private static (Area area, Regional regional, Rol role) SeedLookups(Gestor_Equipos.Data.MyDbContext db)
        {
            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var role = new Rol { Name = AuthBootstrapper.AdministradorRoleName };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.Rols.Add(role);
            db.SaveChanges();
            return (area, regional, role);
        }

        private static UserAdminService CreateService(Gestor_Equipos.Data.MyDbContext db)
        {
            return new UserAdminService(db, new PasswordHasher<UserSystem>());
        }

        [Fact]
        public async Task CreateUserAsync_CreatesUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            var id = await service.CreateUserAsync(new UserCreateViewModel
            {
                Name = "Maria",
                LastName = "Lopez",
                Email = "Maria@Empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id
            });

            var user = db.Users.Single(u => u.Id == id);
            Assert.Equal("maria@empresa.com", user.Email);
            Assert.Equal("maria@empresa.com", user.EmailTeams);
        }

        [Fact]
        public async Task CreateUserAsync_ThrowsOnDuplicateEmail()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            var vm = new UserCreateViewModel { Name = "Maria", LastName = "Lopez", Email = "maria@empresa.com", AreaId = area.Id, RegionalId = regional.Id };
            await service.CreateUserAsync(vm);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateUserAsync(vm));
        }

        [Fact]
        public async Task CreateLoginAsync_CreatesUserSystemWithHashedPassword()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Pedro", LastName = "Diaz", Email = "pedro@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(userId, "pedro", "Sup3rSecreta!", role.Id);

            var userSystem = db.UserSystems.Single(us => us.UserId == userId);
            Assert.Equal("pedro", userSystem.Username);
            Assert.NotEqual("Sup3rSecreta!", userSystem.PasswordHash);
            Assert.NotEmpty(userSystem.PasswordHash);
        }

        [Fact]
        public async Task CreateLoginAsync_ThrowsOnDuplicateUsername()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var user1 = await service.CreateUserAsync(new UserCreateViewModel { Name = "A", LastName = "B", Email = "a@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            var user2 = await service.CreateUserAsync(new UserCreateViewModel { Name = "C", LastName = "D", Email = "c@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(user1, "mismo", "Password1!", role.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateLoginAsync(user2, "mismo", "Password2!", role.Id));
        }

        [Fact]
        public async Task CreateLoginAsync_ThrowsWhenUserAlreadyHasLogin()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "A", LastName = "B", Email = "a@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(userId, "usuario1", "Password1!", role.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateLoginAsync(userId, "usuario2", "Password2!", role.Id));
        }

        [Fact]
        public async Task GetAllAsync_ExcludesRaesUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            db.Users.Add(new Users { Name = "RAES", LastName = "Sistema", Email = AuthBootstrapper.RaesUserEmail, EmailTeams = AuthBootstrapper.RaesUserEmail, AreaId = area.Id, RegionalId = regional.Id });
            db.Users.Add(new Users { Name = "Normal", LastName = "Persona", Email = "normal@empresa.com", EmailTeams = "normal@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            db.SaveChanges();

            var service = CreateService(db);
            var result = await service.GetAllAsync();

            var item = Assert.Single(result);
            Assert.Equal("normal@empresa.com", item.Email);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsUserWithIncludes()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "A", LastName = "B", Email = "a@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(userId, "ab", "Password1!", role.Id);

            var result = await service.GetByIdAsync(userId);

            Assert.NotNull(result);
            Assert.Equal(area.Name, result!.Area.Name);
            Assert.Single(result.UserSystems);
            Assert.Equal(role.Name, result.UserSystems.First().Rol.Name);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNullWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var result = await service.GetByIdAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetDetailAsync_ReturnsNullWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var result = await service.GetDetailAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetDetailAsync_ReturnsProfileAndNoLoginWhenNoUserSystem()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Ana", LastName = "Gomez", Email = "ana@empresa.com", AreaId = area.Id, RegionalId = regional.Id });

            var result = await service.GetDetailAsync(userId);

            Assert.NotNull(result);
            Assert.Equal("Ana", result!.Name);
            Assert.Equal("Gomez", result.LastName);
            Assert.Equal(area.Name, result.AreaName);
            Assert.Equal(regional.Name, result.RegionalName);
            Assert.Null(result.Username);
            Assert.Null(result.RolName);
            Assert.Empty(result.CurrentDesktops);
            Assert.Empty(result.AsignationHistory);
        }

        [Fact]
        public async Task GetDetailAsync_ReturnsLoginInfoWhenUserSystemExists()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Pedro", LastName = "Diaz", Email = "pedro@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(userId, "pedro", "Sup3rSecreta!", role.Id);

            var result = await service.GetDetailAsync(userId);

            Assert.NotNull(result);
            Assert.Equal("pedro", result!.Username);
            Assert.Equal(role.Name, result.RolName);
        }

        private static Desktop SeedDesktop(Gestor_Equipos.Data.MyDbContext db, string name)
        {
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.SaveChanges();

            var desktop = new Desktop { NameDesktop = name, SerialNumber = $"SN-{name}", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();
            return desktop;
        }

        [Fact]
        public async Task GetDetailAsync_HistoryIsOrderedMostRecentFirstAndScopedToUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Ana", LastName = "Gomez", Email = "ana@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            var otherUserId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Luis", LastName = "Diaz", Email = "luis@empresa.com", AreaId = area.Id, RegionalId = regional.Id });

            var desktop1 = SeedDesktop(db, "PC1");
            var desktop2 = SeedDesktop(db, "PC2");

            db.Asignations.Add(new Asignation { DesktopId = desktop1.Id, UserId = userId, DateAsignation = new DateOnly(2026, 1, 1) });
            db.Asignations.Add(new Asignation { DesktopId = desktop2.Id, UserId = userId, DateAsignation = new DateOnly(2026, 5, 1) });
            db.Asignations.Add(new Asignation { DesktopId = desktop1.Id, UserId = otherUserId, DateAsignation = new DateOnly(2026, 6, 1) });
            db.SaveChanges();

            var result = await service.GetDetailAsync(userId);

            Assert.NotNull(result);
            Assert.Equal(2, result!.AsignationHistory.Count);
            Assert.Equal("PC2", result.AsignationHistory[0].NameDesktop);
            Assert.Equal("PC1", result.AsignationHistory[1].NameDesktop);
        }

        [Fact]
        public async Task GetDetailAsync_CurrentDesktopsExcludeOnesReassignedToAnotherUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Ana", LastName = "Gomez", Email = "ana@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            var otherUserId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Luis", LastName = "Diaz", Email = "luis@empresa.com", AreaId = area.Id, RegionalId = regional.Id });

            var desktopStillHeld = SeedDesktop(db, "PC-Held");
            var desktopReassigned = SeedDesktop(db, "PC-Reassigned");

            db.Asignations.Add(new Asignation { DesktopId = desktopStillHeld.Id, UserId = userId, DateAsignation = new DateOnly(2026, 1, 1) });
            db.Asignations.Add(new Asignation { DesktopId = desktopReassigned.Id, UserId = userId, DateAsignation = new DateOnly(2026, 1, 1) });
            db.Asignations.Add(new Asignation { DesktopId = desktopReassigned.Id, UserId = otherUserId, DateAsignation = new DateOnly(2026, 3, 1) });
            db.SaveChanges();

            var result = await service.GetDetailAsync(userId);

            Assert.NotNull(result);
            var current = Assert.Single(result!.CurrentDesktops);
            Assert.Equal("PC-Held", current.NameDesktop);
            Assert.Equal(2, result.AsignationHistory.Count);
        }
    }
}
