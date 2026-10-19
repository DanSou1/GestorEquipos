using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class AuthBootstrapperTests
    {
        private class RecordingEmailService : IEmailService
        {
            public List<(string ToEmail, string ToName, string Subject)> Sent { get; } = new();

            public Task SendAsync(string toEmail, string toName, string subject, string bodyHtml,
                IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default)
            {
                Sent.Add((toEmail, toName, subject));
                return Task.CompletedTask;
            }
        }

        private static MyDbContext CreateDb(string dbName)
        {
            var options = new DbContextOptionsBuilder<MyDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new MyDbContext(options);
        }

        private static ServiceProvider BuildProvider(string dbName, IEmailService emailService)
        {
            var services = new ServiceCollection();
            services.AddDbContext<MyDbContext>(o => o
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddSingleton<IPasswordHasher<UserSystem>>(new PasswordHasher<UserSystem>());
            services.AddSingleton(emailService);
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task EnsureAdminAsync_BackfillsLoginOnlyForPersonasWithoutLogin()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var seedDb = CreateDb(dbName))
            {
                TestHelpers.CreateUser(seedDb, "SinLogin", email: "sinlogin@empresa.local");

                var role = new Rol { Name = "Administrador" };
                seedDb.Rols.Add(role);
                seedDb.SaveChanges();

                var conLogin = TestHelpers.CreateUser(seedDb, "ConLogin", email: "conlogin@empresa.local");
                var hasher = new PasswordHasher<UserSystem>();
                var existingLogin = new UserSystem { Username = conLogin.Email, UserId = conLogin.Id, RolId = role.Id, PasswordHash = string.Empty };
                existingLogin.PasswordHash = hasher.HashPassword(existingLogin, "Clave123");
                seedDb.UserSystems.Add(existingLogin);
                seedDb.SaveChanges();
            }

            var emailService = new RecordingEmailService();
            using var provider = BuildProvider(dbName, emailService);

            await AuthBootstrapper.EnsureAdminAsync(provider, new ConfigurationBuilder().Build());

            using var assertDb = CreateDb(dbName);

            var sinLoginPersona = assertDb.Users.Single(u => u.Email == "sinlogin@empresa.local");
            var newLogin = assertDb.UserSystems.Include(us => us.Rol).Single(us => us.UserId == sinLoginPersona.Id);
            Assert.Equal("Colaborador", newLogin.Rol.Name);
            Assert.True(newLogin.MustChangePassword);
            Assert.Contains(emailService.Sent, e => e.ToEmail == "sinlogin@empresa.local");

            var conLoginPersona = assertDb.Users.Single(u => u.Email == "conlogin@empresa.local");
            Assert.Equal(1, assertDb.UserSystems.Count(us => us.UserId == conLoginPersona.Id));
            Assert.DoesNotContain(emailService.Sent, e => e.ToEmail == "conlogin@empresa.local");
        }

        [Fact]
        public async Task EnsureAdminAsync_SkipsReservedPseudoUsers()
        {
            var dbName = Guid.NewGuid().ToString();
            var emailService = new RecordingEmailService();
            using var provider = BuildProvider(dbName, emailService);

            await AuthBootstrapper.EnsureAdminAsync(provider, new ConfigurationBuilder().Build());

            using var assertDb = CreateDb(dbName);
            Assert.False(assertDb.UserSystems.Any(us => us.User.Email == AuthBootstrapper.RaesUserEmail));
            Assert.False(assertDb.UserSystems.Any(us => us.User.Email == AuthBootstrapper.UnassignedUserEmail));
            Assert.DoesNotContain(emailService.Sent, e => e.ToEmail == AuthBootstrapper.RaesUserEmail);
            Assert.DoesNotContain(emailService.Sent, e => e.ToEmail == AuthBootstrapper.UnassignedUserEmail);
        }

        [Fact]
        public async Task EnsureAdminAsync_IsIdempotentOnSecondRun()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var seedDb = CreateDb(dbName))
            {
                TestHelpers.CreateUser(seedDb, "Repetido", email: "repetido@empresa.local");
            }

            var emailService = new RecordingEmailService();
            using var provider = BuildProvider(dbName, emailService);

            await AuthBootstrapper.EnsureAdminAsync(provider, new ConfigurationBuilder().Build());
            await AuthBootstrapper.EnsureAdminAsync(provider, new ConfigurationBuilder().Build());

            using var assertDb = CreateDb(dbName);
            var persona = assertDb.Users.Single(u => u.Email == "repetido@empresa.local");
            Assert.Equal(1, assertDb.UserSystems.Count(us => us.UserId == persona.Id));
            Assert.Equal(1, emailService.Sent.Count(e => e.ToEmail == "repetido@empresa.local"));
        }
    }
}
