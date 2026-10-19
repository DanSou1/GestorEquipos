using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.UserAdmin;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class UserAdminServiceTests
    {
        private class RecordingEmailService : IEmailService
        {
            public List<(string ToEmail, string ToName, string Subject, string BodyHtml)> Sent { get; } = new();
            public bool ThrowOnSend { get; set; }

            public Task SendAsync(string toEmail, string toName, string subject, string bodyHtml,
                IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default)
            {
                if (ThrowOnSend)
                {
                    throw new InvalidOperationException("SMTP falló (simulado).");
                }

                Sent.Add((toEmail, toName, subject, bodyHtml));
                return Task.CompletedTask;
            }
        }

        private static UserAdminService CreateService(MyDbContext db, RecordingEmailService? emailService = null)
        {
            return new UserAdminService(db, new PasswordHasher<UserSystem>(), emailService ?? new RecordingEmailService());
        }

        private static int SeedColaboradorRole(MyDbContext db)
        {
            var role = new Rol { Name = "Colaborador" };
            db.Rols.Add(role);
            db.SaveChanges();
            return role.Id;
        }

        private static UserCreateViewModel NewPersonVm(string email) => new()
        {
            Name = "Daniel",
            LastName = "Sulaiman",
            Email = email
        };

        [Fact]
        public async Task CreateUserWithLoginAsync_CreatesPersonAndColaboradorLoginInOneTransaction()
        {
            using var db = TestHelpers.CreateDbContext();
            SeedColaboradorRole(db);
            var service = CreateService(db);

            var userId = await service.CreateUserWithLoginAsync(NewPersonVm("daniel@empresa.local"));

            var person = db.Users.Single(u => u.Id == userId);
            Assert.Equal("daniel@empresa.local", person.Email);

            var login = db.UserSystems.Include(us => us.Rol).Single(us => us.UserId == userId);
            Assert.Equal("daniel@empresa.local", login.Username);
            Assert.Equal("Colaborador", login.Rol.Name);
            Assert.True(login.MustChangePassword);
        }

        [Fact]
        public async Task CreateUserWithLoginAsync_SendsWelcomeEmailWithUsernameAndPassword()
        {
            using var db = TestHelpers.CreateDbContext();
            SeedColaboradorRole(db);
            var emailService = new RecordingEmailService();
            var service = CreateService(db, emailService);

            await service.CreateUserWithLoginAsync(NewPersonVm("nueva@empresa.local"));

            var sent = Assert.Single(emailService.Sent);
            Assert.Equal("nueva@empresa.local", sent.ToEmail);
            Assert.Contains("nueva@empresa.local", sent.BodyHtml);
        }

        [Fact]
        public async Task CreateUserWithLoginAsync_PropagatesExceptionWhenEmailFails()
        {
            using var db = TestHelpers.CreateDbContext();
            SeedColaboradorRole(db);
            var emailService = new RecordingEmailService { ThrowOnSend = true };
            var service = CreateService(db, emailService);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateUserWithLoginAsync(NewPersonVm("falla@empresa.local")));
        }

        [Fact]
        public async Task CreateUserWithLoginAsync_ThrowsOnDuplicateEmail()
        {
            using var db = TestHelpers.CreateDbContext();
            SeedColaboradorRole(db);
            var service = CreateService(db);
            await service.CreateUserWithLoginAsync(NewPersonVm("duplicado@empresa.local"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateUserWithLoginAsync(NewPersonVm("duplicado@empresa.local")));
        }

        [Fact]
        public async Task ChangeOwnPasswordAsync_HappyPath_ClearsMustChangePasswordAndRotatesSecurityStamp()
        {
            using var db = TestHelpers.CreateDbContext();
            SeedColaboradorRole(db);
            var hasher = new PasswordHasher<UserSystem>();
            var service = CreateService(db);

            var userId = await service.CreateUserWithLoginAsync(NewPersonVm("cambia@empresa.local"));
            var login = db.UserSystems.Single(us => us.UserId == userId);
            var originalStamp = login.SecurityStamp;

            var newStamp = await service.ChangeOwnPasswordAsync(login.Id, "NuevaClave456");

            var reloaded = db.UserSystems.Single(us => us.Id == login.Id);
            Assert.False(reloaded.MustChangePassword);
            Assert.NotEqual(originalStamp, reloaded.SecurityStamp);
            Assert.Equal(newStamp, reloaded.SecurityStamp);
            Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(reloaded, reloaded.PasswordHash, "NuevaClave456"));
        }
    }
}
