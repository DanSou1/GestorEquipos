using Gestor_Equipos.Data;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Auth
{
    public static class AuthBootstrapper
    {
        public const string AdministradorRoleName = "Administrador";
        public const string AuditorRoleName = "Auditor";
        public const string RaesUserEmail = "raes@system.local";
        private const string RaesAreaAndRegionalName = "RAES";

        public static async Task EnsureAdminAsync(IServiceProvider services, IConfiguration configuration)
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserSystem>>();

            if (dbContext.Database.IsRelational())
            {
                await dbContext.Database.MigrateAsync();
            }

            var adminRole = await EnsureRoleAsync(dbContext, AdministradorRoleName);
            await EnsureRoleAsync(dbContext, AuditorRoleName);
            await dbContext.SaveChangesAsync();

            await EnsureRaesUserAsync(dbContext);
            await dbContext.SaveChangesAsync();

            var email = configuration["BootstrapAdmin:Email"];
            var password = configuration["BootstrapAdmin:Password"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            var area = await dbContext.Areas.FirstOrDefaultAsync(a => a.Name != RaesAreaAndRegionalName);
            if (area is null)
            {
                area = new Area { Name = "Administracion" };
                dbContext.Areas.Add(area);
            }

            var regional = await dbContext.Regionals.FirstOrDefaultAsync(r => r.Name != RaesAreaAndRegionalName);
            if (regional is null)
            {
                regional = new Regional { Name = "Principal" };
                dbContext.Regionals.Add(regional);
            }

            await dbContext.SaveChangesAsync();

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var user = await dbContext.Users
                .Include(existingUser => existingUser.UserSystems)
                .SingleOrDefaultAsync(existingUser => existingUser.Email.ToLower() == normalizedEmail);

            if (user is null)
            {
                user = new Users
                {
                    Name = "Administrador",
                    LastName = "Sistema",
                    Email = normalizedEmail,
                    EmailTeams = normalizedEmail,
                    AreaId = area.Id,
                    RegionalId = regional.Id
                };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();
            }

            if (await dbContext.UserSystems.AnyAsync(account => account.UserId == user.Id))
            {
                return;
            }

            var userSystem = new UserSystem
            {
                Username = normalizedEmail,
                UserId = user.Id,
                RolId = adminRole.Id,
                PasswordHash = string.Empty
            };
            userSystem.PasswordHash = passwordHasher.HashPassword(userSystem, password);

            dbContext.UserSystems.Add(userSystem);
            await dbContext.SaveChangesAsync();
        }

        private static async Task<Rol> EnsureRoleAsync(MyDbContext dbContext, string roleName)
        {
            var role = await dbContext.Rols.SingleOrDefaultAsync(r => r.Name == roleName);
            if (role is null)
            {
                role = new Rol { Name = roleName };
                dbContext.Rols.Add(role);
            }

            return role;
        }

        private static async Task EnsureRaesUserAsync(MyDbContext dbContext)
        {
            var existing = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == RaesUserEmail);
            if (existing is not null)
            {
                return;
            }

            var raesArea = await dbContext.Areas.SingleOrDefaultAsync(a => a.Name == RaesAreaAndRegionalName);
            if (raesArea is null)
            {
                raesArea = new Area { Name = RaesAreaAndRegionalName };
                dbContext.Areas.Add(raesArea);
            }

            var raesRegional = await dbContext.Regionals.SingleOrDefaultAsync(r => r.Name == RaesAreaAndRegionalName);
            if (raesRegional is null)
            {
                raesRegional = new Regional { Name = RaesAreaAndRegionalName };
                dbContext.Regionals.Add(raesRegional);
            }

            await dbContext.SaveChangesAsync();

            var raesUser = new Users
            {
                Name = "RAES",
                LastName = "Sistema",
                Email = RaesUserEmail,
                EmailTeams = RaesUserEmail,
                AreaId = raesArea.Id,
                RegionalId = raesRegional.Id
            };
            dbContext.Users.Add(raesUser);
        }
    }
}
