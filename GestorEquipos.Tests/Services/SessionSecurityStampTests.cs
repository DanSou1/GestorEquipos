using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.AccessAccount;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GestorEquipos.Tests.Services
{
    public class SessionSecurityStampTests
    {
        [Theory]
        [InlineData("role", true)]
        [InlineData("password", true)]
        [InlineData("inactive", true)]
        [InlineData("profile", false)]
        [InlineData("rejected", false)]
        public async Task AccountChange_RotatesOnlyAfterSecurityChange(string change, bool shouldRotate)
        {
            using var database = TestHelpers.CreateDbContext();
            var account = SessionCookieEventsTests.SeedAccount(database);
            var hasher = new PasswordHasher<UserSystem>();
            account.PasswordHash = hasher.HashPassword(account, "Existing!Password123");
            var auditor = new Rol { Name = "Auditor" };
            database.Rols.Add(auditor);
            // A second enabled admin so the "role"/"inactive" cases below don't trip the
            // SEG-03 last-admin guard — this test is about security-stamp rotation, not that guard.
            database.UserSystems.Add(new UserSystem
            {
                Username = "session-admin2@unit.invalid", PasswordHash = "unused",
                User = new Users { Name = "Session2", LastName = "Test", Email = "session-admin2@unit.invalid" },
                Rol = account.Rol
            });
            await database.SaveChangesAsync();
            var original = account.SecurityStamp;
            var service = new UserAdminService(database, hasher);
            var model = new AccessAccountEditViewModel
            {
                Name = "Updated", LastName = account.User.LastName, Email = account.User.Email,
                Username = account.Username, RolId = account.RolId
            };
            if (change == "role") model.RolId = auditor.Id;
            if (change == "password" || change == "rejected")
            {
                model.NewPassword = "Changed!Password456";
                model.AdminPassword = change == "password" ? "Existing!Password123" : "wrong";
            }
            if (change == "inactive") await service.DeactivateAsync(account.UserId);
            else if (change == "rejected") await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAccountAsync(account.UserId, model, account.Id));
            else await service.UpdateAccountAsync(account.UserId, model, account.Id);

            Assert.Equal(shouldRotate, original != account.SecurityStamp);
            database.ChangeTracker.Clear();
            Assert.Equal(shouldRotate, original != (await database.UserSystems.FindAsync(account.Id))!.SecurityStamp);
        }

        [Fact]
        public async Task ValidateUserAsync_Rehash_ReturnsStampWithoutRotating()
        {
            using var database = TestHelpers.CreateDbContext();
            var account = SessionCookieEventsTests.SeedAccount(database);
            var oldHasher = new PasswordHasher<UserSystem>(Options.Create(new PasswordHasherOptions { IterationCount = 10000 }));
            var hasher = new PasswordHasher<UserSystem>();
            account.PasswordHash = oldHasher.HashPassword(account, "Rehash!Password123");
            await database.SaveChangesAsync();
            var originalHash = account.PasswordHash;
            var originalStamp = account.SecurityStamp;
            var result = await new AuthService(database, hasher).ValidateUserAsync(account.User.Email, "Rehash!Password123");
            Assert.NotNull(result);
            Assert.Equal(originalStamp, result.SecurityStamp);
            Assert.Equal(originalStamp, account.SecurityStamp);
            Assert.NotEqual(originalHash, account.PasswordHash);
        }

        [Theory]
        [InlineData("inactive")]
        [InlineData("password")]
        [InlineData("unknown")]
        public async Task ValidateUserAsync_IneligibleCredentials_ReturnsNull(string reason)
        {
            using var database = TestHelpers.CreateDbContext();
            var account = SessionCookieEventsTests.SeedAccount(database);
            var hasher = new PasswordHasher<UserSystem>();
            account.PasswordHash = hasher.HashPassword(account, "Valid!Password123");
            if (reason == "inactive") account.User.Activo = false;
            await database.SaveChangesAsync();
            var result = await new AuthService(database, hasher).ValidateUserAsync(
                reason == "unknown" ? "missing@unit.invalid" : account.User.Email,
                reason == "password" ? "wrong" : "Valid!Password123");
            Assert.Null(result);
        }
    }
}
