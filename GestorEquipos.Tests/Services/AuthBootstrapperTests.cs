using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class AuthBootstrapperTests
    {
        private static IServiceProvider BuildServices(string dbName)
        {
            var services = new ServiceCollection();
            services.AddDbContext<MyDbContext>(options => options.UseInMemoryDatabase(dbName));
            services.AddScoped<IPasswordHasher<UserSystem>, PasswordHasher<UserSystem>>();
            return services.BuildServiceProvider();
        }

        private static IConfiguration BuildConfiguration(string? email = null, string? password = null)
        {
            var dict = new Dictionary<string, string?>();
            if (email is not null)
            {
                dict["BootstrapAdmin:Email"] = email;
            }
            if (password is not null)
            {
                dict["BootstrapAdmin:Password"] = password;
            }
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        [Fact]
        public async Task EnsureAdminAsync_SeedsRolesAndRaesUser_EvenWithoutBootstrapConfig()
        {
            var services = BuildServices(Guid.NewGuid().ToString());
            var configuration = BuildConfiguration();

            await AuthBootstrapper.EnsureAdminAsync(services, configuration);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();

            Assert.True(await db.Rols.AnyAsync(r => r.Name == AuthBootstrapper.AdministradorRoleName));
            Assert.True(await db.Rols.AnyAsync(r => r.Name == AuthBootstrapper.AuditorRoleName));

            var raesUser = await db.Users.SingleOrDefaultAsync(u => u.Email == AuthBootstrapper.RaesUserEmail);
            Assert.NotNull(raesUser);
            Assert.False(await db.UserSystems.AnyAsync(us => us.UserId == raesUser!.Id));
        }

        [Fact]
        public async Task EnsureAdminAsync_CreatesAdminLogin_WhenBootstrapConfigProvided()
        {
            var services = BuildServices(Guid.NewGuid().ToString());
            var configuration = BuildConfiguration("admin@empresa.com", "Sup3rSecreta!");

            await AuthBootstrapper.EnsureAdminAsync(services, configuration);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();

            var adminUser = await db.Users.SingleOrDefaultAsync(u => u.Email == "admin@empresa.com");
            Assert.NotNull(adminUser);

            var userSystem = await db.UserSystems.Include(us => us.Rol).SingleOrDefaultAsync(us => us.UserId == adminUser!.Id);
            Assert.NotNull(userSystem);
            Assert.Equal(AuthBootstrapper.AdministradorRoleName, userSystem!.Rol.Name);
        }

        [Fact]
        public async Task EnsureAdminAsync_DoesNotCreateLogin_WhenBootstrapConfigMissing()
        {
            var services = BuildServices(Guid.NewGuid().ToString());
            var configuration = BuildConfiguration();

            await AuthBootstrapper.EnsureAdminAsync(services, configuration);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();

            Assert.Equal(0, await db.UserSystems.CountAsync());
        }

        [Fact]
        public async Task EnsureAdminAsync_IsIdempotent()
        {
            var services = BuildServices(Guid.NewGuid().ToString());
            var configuration = BuildConfiguration("admin@empresa.com", "Sup3rSecreta!");

            await AuthBootstrapper.EnsureAdminAsync(services, configuration);
            await AuthBootstrapper.EnsureAdminAsync(services, configuration);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();

            Assert.Equal(1, await db.Rols.CountAsync(r => r.Name == AuthBootstrapper.AdministradorRoleName));
            Assert.Equal(1, await db.Users.CountAsync(u => u.Email == AuthBootstrapper.RaesUserEmail));
            Assert.Equal(1, await db.Users.CountAsync(u => u.Email == "admin@empresa.com"));
            Assert.Equal(1, await db.UserSystems.CountAsync());
        }
    }
}
