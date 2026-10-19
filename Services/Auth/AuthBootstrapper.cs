using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Auth
{
    public static class AuthBootstrapper
    {
        public const string AdministradorRoleName = "Administrador";
        public const string AuditorRoleName = "Auditor";
        public const string TecnicoSistemasRoleName = "Técnico en Sistemas";
        // Colaboradores: personas del módulo Usuarios que ahora reciben login automático (ver
        // EnsureColaboradorLoginsBackfilledAsync / UserAdminService.CreateUserWithLoginAsync),
        // acotadas a crear/ver/borrar sus propios Casos TI — nunca aparecen en AdminOrTecnicoRoles.
        public const string ColaboradorRoleName = "Colaborador";
        // Roles permitidos para crear/consultar/operar (asignar, devolver, reasignar, liberar,
        // dar de baja a RAES, desactivar persona) en todos los módulos excepto Cuentas de acceso
        // y las acciones de Editar/Borrar, que siguen exigiendo AdministradorRoleName a nivel de
        // método (ver DesktopController, PeripheralController, etc.).
        public const string AdminOrTecnicoRoles = $"{AdministradorRoleName},{TecnicoSistemasRoleName}";
        // "Cualquier rol de personal" — Administrador/Técnico/Auditor, pero explícitamente NO
        // Colaborador. Se usa en los controllers que hoy caen al fallback [Authorize] (cualquier
        // autenticado) y que deben seguir vedados para Colaborador salvo una acción puntual
        // (ver MaintenanceCampaignController.Sign).
        public const string AllStaffRoles = $"{AdministradorRoleName},{TecnicoSistemasRoleName},{AuditorRoleName}";
        public const string RaesUserEmail = "raes@system.local";
        // UX-06: distinct from RaesUserEmail — this represents "returned, equipment still in
        // service" (Desktop.Estado stays true), not "retired". Activo=false so it's picked up
        // for free by every existing "current holder inactive => Disponible" query, the same
        // way the RAES trick is. No dedicated Area/Regional (unlike RAES): those columns just
        // show "-", which is accurate — an unassigned desktop isn't "in" any area.
        public const string UnassignedUserEmail = "sin-asignar@system.local";
        public const string RaesAreaAndRegionalName = "RAES";

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
            await EnsureRoleAsync(dbContext, TecnicoSistemasRoleName);
            var colaboradorRole = await EnsureRoleAsync(dbContext, ColaboradorRoleName);
            await dbContext.SaveChangesAsync();

            await EnsureRaesUserAsync(dbContext);
            await EnsureUnassignedUserAsync(dbContext);
            await dbContext.SaveChangesAsync();

            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            await EnsureColaboradorLoginsBackfilledAsync(dbContext, passwordHasher, emailService, colaboradorRole.Id);

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

        // Crea una cuenta Colaborador (login = correo, contraseña temporal, MustChangePassword=true)
        // para toda persona activa que todavía no tenga ninguna UserSystem, excluyendo las
        // pseudo-cuentas reservadas RAES/"sin asignar" (nunca deben poder iniciar sesión).
        // Idempotente "gratis": en una segunda ejecución la consulta ya no encuentra nadie sin
        // cuenta, así que nadie recibe un segundo correo de bienvenida.
        private static async Task EnsureColaboradorLoginsBackfilledAsync(
            MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher, IEmailService emailService, int colaboradorRoleId)
        {
            var personasSinCuenta = await dbContext.Users
                .Where(u => u.Activo
                    && u.Email != RaesUserEmail
                    && u.Email != UnassignedUserEmail
                    && !u.UserSystems.Any())
                .ToListAsync();

            var strategy = dbContext.Database.CreateExecutionStrategy();

            foreach (var persona in personasSinCuenta)
            {
                try
                {
                    await strategy.ExecuteAsync(async () =>
                    {
                        // No se confirma una cuenta sin que la persona haya recibido su contraseña:
                        // si el correo falla para una persona puntual, la transacción nunca se
                        // confirma y se reintenta en el próximo arranque (mismo patrón que
                        // MaintenanceCampaignService.CompleteAndSendAsync), sin bloquear al resto.
                        await using var transaction = await dbContext.Database.BeginTransactionAsync();

                        var password = TemporaryPasswordGenerator.Generate();
                        var userSystem = new UserSystem
                        {
                            Username = persona.Email,
                            UserId = persona.Id,
                            RolId = colaboradorRoleId,
                            MustChangePassword = true,
                            PasswordHash = string.Empty
                        };
                        userSystem.PasswordHash = passwordHasher.HashPassword(userSystem, password);

                        dbContext.UserSystems.Add(userSystem);
                        await dbContext.SaveChangesAsync();

                        await emailService.SendAsync(
                            persona.Email,
                            $"{persona.Name} {persona.LastName}",
                            "Tu acceso a AssetOps",
                            BuildWelcomeEmailBody(persona.Name, persona.Email, password),
                            Array.Empty<EmailAttachment>());

                        await transaction.CommitAsync();
                    });
                }
                catch (Exception)
                {
                    dbContext.ChangeTracker.Clear();
                }
            }
        }

        private static string BuildWelcomeEmailBody(string name, string username, string temporaryPassword)
        {
            return $"""
                <p>Hola {name},</p>
                <p>Se creó tu acceso a la plataforma AssetOps.</p>
                <p>Usuario: <strong>{username}</strong><br/>
                Contraseña temporal: <strong>{temporaryPassword}</strong></p>
                <p>Deberás cambiarla la primera vez que inicies sesión.</p>
                """;
        }

        private static async Task EnsureUnassignedUserAsync(MyDbContext dbContext)
        {
            if (await dbContext.Users.AnyAsync(u => u.Email == UnassignedUserEmail))
            {
                return;
            }

            dbContext.Users.Add(new Users
            {
                Name = "Disponible",
                LastName = "Sistema",
                Email = UnassignedUserEmail,
                EmailTeams = UnassignedUserEmail,
                Activo = false
            });
        }
    }
}
