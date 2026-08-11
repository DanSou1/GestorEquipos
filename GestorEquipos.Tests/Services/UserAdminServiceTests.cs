using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.AccessAccount;
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
        public async Task UpdateUserAsync_UpdatesFields()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var otherArea = new Area { Name = "B" };
            var otherRegional = new Regional { Name = "S" };
            db.Areas.Add(otherArea);
            db.Regionals.Add(otherRegional);
            db.SaveChanges();
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Maria", LastName = "Lopez", Email = "maria2@empresa.com", AreaId = area.Id, RegionalId = regional.Id });

            await service.UpdateUserAsync(userId, new UserCreateViewModel
            {
                Name = "Mariana",
                LastName = "Lopez Diaz",
                Email = "mariana@empresa.com",
                EmailTeams = "mariana.teams@empresa.com",
                AreaId = otherArea.Id,
                RegionalId = otherRegional.Id
            });

            var user = db.Users.Single(u => u.Id == userId);
            Assert.Equal("Mariana", user.Name);
            Assert.Equal("Lopez Diaz", user.LastName);
            Assert.Equal("mariana@empresa.com", user.Email);
            Assert.Equal("mariana.teams@empresa.com", user.EmailTeams);
            Assert.Equal(otherArea.Id, user.AreaId);
            Assert.Equal(otherRegional.Id, user.RegionalId);
        }

        [Fact]
        public async Task UpdateUserAsync_ThrowsWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateUserAsync(999, new UserCreateViewModel { Name = "X", LastName = "Y", Email = "x@empresa.com" }));
        }

        [Fact]
        public async Task UpdateUserAsync_ThrowsOnDuplicateEmailFromAnotherUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            await service.CreateUserAsync(new UserCreateViewModel { Name = "A", LastName = "B", Email = "taken@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "C", LastName = "D", Email = "free@empresa.com", AreaId = area.Id, RegionalId = regional.Id });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateUserAsync(userId, new UserCreateViewModel { Name = "C", LastName = "D", Email = "taken@empresa.com", AreaId = area.Id, RegionalId = regional.Id }));
        }

        [Fact]
        public async Task UpdateUserAsync_AllowsSavingWithOwnUnchangedEmail()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "A", LastName = "B", Email = "same@empresa.com", AreaId = area.Id, RegionalId = regional.Id });

            await service.UpdateUserAsync(userId, new UserCreateViewModel { Name = "A2", LastName = "B", Email = "same@empresa.com", AreaId = area.Id, RegionalId = regional.Id });

            var user = db.Users.Single(u => u.Id == userId);
            Assert.Equal("A2", user.Name);
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
        public async Task UpdateAccountAsync_UpdatesFieldsWithoutChangingPasswordWhenNewPasswordEmpty()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Pedro", LastName = "Diaz", Email = "pedro-upd@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(userId, "pedro-upd", "Sup3rSecreta!", role.Id);
            var originalHash = db.UserSystems.Single(us => us.UserId == userId).PasswordHash;

            await service.UpdateAccountAsync(userId, new AccessAccountEditViewModel
            {
                Name = "Pedro Actualizado",
                LastName = "Diaz",
                Email = "pedro-upd@empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id,
                Username = "pedro-upd",
                RolId = role.Id
            }, actingAdminUserSystemId: -1);

            var user = db.Users.Single(u => u.Id == userId);
            var userSystem = db.UserSystems.Single(us => us.UserId == userId);
            Assert.Equal("Pedro Actualizado", user.Name);
            Assert.Equal(originalHash, userSystem.PasswordHash);
        }

        [Fact]
        public async Task UpdateAccountAsync_ChangesPasswordWhenAdminPasswordCorrect()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var adminId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Admin", LastName = "User", Email = "admin@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(adminId, "admin", "AdminPass1!", role.Id);
            var adminUserSystemId = db.UserSystems.Single(us => us.UserId == adminId).Id;

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Pedro", LastName = "Diaz", Email = "pedro-pw@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(userId, "pedro-pw", "OldPass1!", role.Id);
            var originalHash = db.UserSystems.Single(us => us.UserId == userId).PasswordHash;

            await service.UpdateAccountAsync(userId, new AccessAccountEditViewModel
            {
                Name = "Pedro",
                LastName = "Diaz",
                Email = "pedro-pw@empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id,
                Username = "pedro-pw",
                RolId = role.Id,
                NewPassword = "NewPass1!",
                ConfirmPassword = "NewPass1!",
                AdminPassword = "AdminPass1!"
            }, adminUserSystemId);

            var newHash = db.UserSystems.Single(us => us.UserId == userId).PasswordHash;
            Assert.NotEqual(originalHash, newHash);
            Assert.NotEqual("NewPass1!", newHash);
        }

        [Fact]
        public async Task UpdateAccountAsync_ThrowsWhenAdminPasswordIncorrect_AndPersistsNothing()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var adminId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Admin", LastName = "User", Email = "admin2@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(adminId, "admin2", "AdminPass1!", role.Id);
            var adminUserSystemId = db.UserSystems.Single(us => us.UserId == adminId).Id;

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Pedro", LastName = "Diaz", Email = "pedro-wrong@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(userId, "pedro-wrong", "OldPass1!", role.Id);
            var originalHash = db.UserSystems.Single(us => us.UserId == userId).PasswordHash;

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAccountAsync(userId, new AccessAccountEditViewModel
            {
                Name = "Pedro Cambiado",
                LastName = "Diaz",
                Email = "pedro-wrong@empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id,
                Username = "pedro-wrong",
                RolId = role.Id,
                NewPassword = "NewPass1!",
                ConfirmPassword = "NewPass1!",
                AdminPassword = "IncorrectPassword!"
            }, adminUserSystemId));

            var user = db.Users.Single(u => u.Id == userId);
            var userSystem = db.UserSystems.Single(us => us.UserId == userId);
            Assert.Equal("Pedro", user.Name);
            Assert.Equal(originalHash, userSystem.PasswordHash);
        }

        [Fact]
        public async Task UpdateAccountAsync_ThrowsWhenNewPasswordProvidedButAdminPasswordMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Pedro", LastName = "Diaz", Email = "pedro-noadmin@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(userId, "pedro-noadmin", "OldPass1!", role.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAccountAsync(userId, new AccessAccountEditViewModel
            {
                Name = "Pedro",
                LastName = "Diaz",
                Email = "pedro-noadmin@empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id,
                Username = "pedro-noadmin",
                RolId = role.Id,
                NewPassword = "NewPass1!",
                ConfirmPassword = "NewPass1!",
                AdminPassword = null
            }, actingAdminUserSystemId: -1));
        }

        [Fact]
        public async Task UpdateAccountAsync_ThrowsOnDuplicateUsername()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var user1Id = await service.CreateUserAsync(new UserCreateViewModel { Name = "A", LastName = "B", Email = "dupuser1@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(user1Id, "usuariouno", "Password1!", role.Id);
            var user2Id = await service.CreateUserAsync(new UserCreateViewModel { Name = "C", LastName = "D", Email = "dupuser2@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(user2Id, "usuariodos", "Password2!", role.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAccountAsync(user2Id, new AccessAccountEditViewModel
            {
                Name = "C",
                LastName = "D",
                Email = "dupuser2@empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id,
                Username = "usuariouno",
                RolId = role.Id
            }, actingAdminUserSystemId: -1));
        }

        [Fact]
        public async Task UpdateAccountAsync_ThrowsOnDuplicateEmail()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var user1Id = await service.CreateUserAsync(new UserCreateViewModel { Name = "A", LastName = "B", Email = "dupemail1@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(user1Id, "dupemailuno", "Password1!", role.Id);
            var user2Id = await service.CreateUserAsync(new UserCreateViewModel { Name = "C", LastName = "D", Email = "dupemail2@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(user2Id, "dupemaildos", "Password2!", role.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAccountAsync(user2Id, new AccessAccountEditViewModel
            {
                Name = "C",
                LastName = "D",
                Email = "dupemail1@empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id,
                Username = "dupemaildos",
                RolId = role.Id
            }, actingAdminUserSystemId: -1));
        }

        [Fact]
        public async Task UpdateAccountAsync_AllowsSavingWithOwnUnchangedUsernameAndEmail()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Ana", LastName = "Gomez", Email = "ana-unchanged@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(userId, "ana-unchanged", "Password1!", role.Id);

            await service.UpdateAccountAsync(userId, new AccessAccountEditViewModel
            {
                Name = "Ana Maria",
                LastName = "Gomez",
                Email = "ana-unchanged@empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id,
                Username = "ana-unchanged",
                RolId = role.Id
            }, actingAdminUserSystemId: -1);

            var user = db.Users.Single(u => u.Id == userId);
            Assert.Equal("Ana Maria", user.Name);
        }

        [Fact]
        public async Task UpdateAccountAsync_ThrowsWhenUserNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAccountAsync(999, new AccessAccountEditViewModel
            {
                Name = "X",
                LastName = "Y",
                Email = "x@empresa.com",
                Username = "x"
            }, actingAdminUserSystemId: -1));
        }

        [Fact]
        public async Task UpdateAccountAsync_ThrowsWhenUserHasNoAccessAccount()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Sin", LastName = "Cuenta", Email = "sincuenta@empresa.com", AreaId = area.Id, RegionalId = regional.Id });

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAccountAsync(userId, new AccessAccountEditViewModel
            {
                Name = "Sin",
                LastName = "Cuenta",
                Email = "sincuenta@empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id,
                Username = "sincuenta",
                RolId = role.Id
            }, actingAdminUserSystemId: -1));
        }

        [Fact]
        public async Task UpdateAccountAsync_ChangesRolId()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var otherRole = new Rol { Name = "Auditor" };
            db.Rols.Add(otherRole);
            db.SaveChanges();
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Ana", LastName = "Gomez", Email = "ana-role@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(userId, "ana-role", "Password1!", role.Id);

            await service.UpdateAccountAsync(userId, new AccessAccountEditViewModel
            {
                Name = "Ana",
                LastName = "Gomez",
                Email = "ana-role@empresa.com",
                AreaId = area.Id,
                RegionalId = regional.Id,
                Username = "ana-role",
                RolId = otherRole.Id
            }, actingAdminUserSystemId: -1);

            var userSystem = db.UserSystems.Single(us => us.UserId == userId);
            Assert.Equal(otherRole.Id, userSystem.RolId);
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
        public async Task GetAllAsync_ExcludesUsersWithAccessAccount()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            var service = CreateService(db);

            var equipmentUserId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Equipo", LastName = "Persona", Email = "equipo@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            var staffUserId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Staff", LastName = "Tecnico", Email = "staff@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(staffUserId, "staff", "Password1!", role.Id);

            var result = await service.GetAllAsync();

            var item = Assert.Single(result);
            Assert.Equal(equipmentUserId, item.Id);
        }

        [Fact]
        public async Task GetAllAsync_ExcludesInactiveByDefaultAndIncludesWhenRequested()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            var activeId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Activo", LastName = "Persona", Email = "activo@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            var inactiveId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Inactivo", LastName = "Persona", Email = "inactivo@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.DeactivateAsync(inactiveId);

            var defaultResult = await service.GetAllAsync();
            var withInactive = await service.GetAllAsync(includeInactive: true);

            var item = Assert.Single(defaultResult);
            Assert.Equal(activeId, item.Id);
            Assert.Equal(2, withInactive.Count);
            Assert.Contains(withInactive, u => u.Id == inactiveId);
        }

        [Fact]
        public async Task GetAccountsAsync_ReturnsOnlyUsersWithLoginAndExcludesRaes()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, role) = SeedLookups(db);
            db.Users.Add(new Users { Name = "RAES", LastName = "Sistema", Email = AuthBootstrapper.RaesUserEmail, EmailTeams = AuthBootstrapper.RaesUserEmail, AreaId = area.Id, RegionalId = regional.Id });
            db.SaveChanges();
            var service = CreateService(db);

            var equipmentUserId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Equipo", LastName = "Persona", Email = "equipo2@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            var staffUserId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Staff", LastName = "Tecnico", Email = "staff2@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.CreateLoginAsync(staffUserId, "staff2", "Password1!", role.Id);

            var result = await service.GetAccountsAsync();

            var item = Assert.Single(result);
            Assert.Equal(staffUserId, item.Id);
            Assert.NotEqual(equipmentUserId, item.Id);
        }

        [Fact]
        public async Task DeactivateAsync_SetsActivoFalseAndDeactivatedAt()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Ana", LastName = "Gomez", Email = "ana2@empresa.com", AreaId = area.Id, RegionalId = regional.Id });

            await service.DeactivateAsync(userId);

            var user = db.Users.Single(u => u.Id == userId);
            Assert.False(user.Activo);
            Assert.Equal(DateOnly.FromDateTime(DateTime.Now), user.DeactivatedAt);
        }

        [Fact]
        public async Task DeactivateAsync_ThrowsWhenUserNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeactivateAsync(999));
        }

        [Fact]
        public async Task DeactivateAsync_IsIdempotentWhenAlreadyInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Ana", LastName = "Gomez", Email = "ana3@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.DeactivateAsync(userId);
            var firstDeactivatedAt = db.Users.Single(u => u.Id == userId).DeactivatedAt;

            await service.DeactivateAsync(userId);

            var user = db.Users.Single(u => u.Id == userId);
            Assert.False(user.Activo);
            Assert.Equal(firstDeactivatedAt, user.DeactivatedAt);
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

        [Fact]
        public async Task GetUsersByRegionalAsync_GroupsActiveUsersByRegionalDescending()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regionalA, _) = SeedLookups(db);
            var regionalB = new Regional { Name = "Otra" };
            db.Regionals.Add(regionalB);
            db.SaveChanges();
            var service = CreateService(db);

            await service.CreateUserAsync(new UserCreateViewModel { Name = "A1", LastName = "X", Email = "a1@empresa.com", AreaId = area.Id, RegionalId = regionalA.Id });
            await service.CreateUserAsync(new UserCreateViewModel { Name = "A2", LastName = "X", Email = "a2@empresa.com", AreaId = area.Id, RegionalId = regionalA.Id });
            await service.CreateUserAsync(new UserCreateViewModel { Name = "B1", LastName = "X", Email = "b1@empresa.com", AreaId = area.Id, RegionalId = regionalB.Id });

            var result = await service.GetUsersByRegionalAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal(regionalA.Name, result[0].RegionalName);
            Assert.Equal(2, result[0].Count);
            Assert.Equal(regionalB.Name, result[1].RegionalName);
            Assert.Equal(1, result[1].Count);
        }

        [Fact]
        public async Task GetUsersByRegionalAsync_ExcludesInactiveAndRaesUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            db.Users.Add(new Users { Name = "RAES", LastName = "Sistema", Email = AuthBootstrapper.RaesUserEmail, EmailTeams = AuthBootstrapper.RaesUserEmail, AreaId = area.Id, RegionalId = regional.Id });
            db.SaveChanges();
            var service = CreateService(db);

            var activeId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Activo", LastName = "X", Email = "activo-reg@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            var inactiveId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Inactivo", LastName = "X", Email = "inactivo-reg@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            await service.DeactivateAsync(inactiveId);

            var result = await service.GetUsersByRegionalAsync();

            var item = Assert.Single(result);
            Assert.Equal(regional.Name, item.RegionalName);
            Assert.Equal(1, item.Count);
        }

        [Fact]
        public async Task GetUsersByRegionalAsync_ReturnsEmptyWhenNoActiveUsers()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var result = await service.GetUsersByRegionalAsync();

            Assert.Empty(result);
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

        [Fact]
        public async Task GetDetailAsync_CurrentDesktopsEmptyWhenUserIsInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var (area, regional, _) = SeedLookups(db);
            var service = CreateService(db);

            var userId = await service.CreateUserAsync(new UserCreateViewModel { Name = "Ana", LastName = "Gomez", Email = "ana4@empresa.com", AreaId = area.Id, RegionalId = regional.Id });
            var desktop = SeedDesktop(db, "PC-Held");
            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = userId, DateAsignation = new DateOnly(2026, 1, 1) });
            db.SaveChanges();

            await service.DeactivateAsync(userId);

            var result = await service.GetDetailAsync(userId);

            Assert.NotNull(result);
            Assert.False(result!.Activo);
            Assert.NotNull(result.DeactivatedAt);
            Assert.Empty(result.CurrentDesktops);
            Assert.Single(result.AsignationHistory);
        }
    }
}
