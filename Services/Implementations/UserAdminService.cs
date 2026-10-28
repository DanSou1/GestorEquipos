using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.AccessAccount;
using GestorEquipos.Models.ViewModels.UserAdmin;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class UserAdminService : IUserAdminService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;
        private readonly IEmailService _emailService;

        public UserAdminService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher, IEmailService emailService)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }

        public async Task<int> CreateUserAsync(UserCreateViewModel vm)
        {
            var normalizedEmail = vm.Email.Trim().ToLowerInvariant();

            if (await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                throw new InvalidOperationException("Ya existe un usuario con ese correo.");
            }

            var user = new Users
            {
                Name = vm.Name,
                LastName = vm.LastName,
                Email = normalizedEmail,
                EmailTeams = string.IsNullOrWhiteSpace(vm.EmailTeams)
                    ? normalizedEmail
                    : vm.EmailTeams.Trim().ToLowerInvariant(),
                AreaId = vm.AreaId,
                RegionalId = vm.RegionalId
            };

            _dbContext.Users.Add(user);

            if (vm.CargoId.HasValue)
            {
                _dbContext.CargoAssignments.Add(new CargoAssignment
                {
                    User = user,
                    CargoId = vm.CargoId.Value,
                    DateAsignation = DateOnly.FromDateTime(DateTime.Now)
                });
            }

            await _dbContext.SaveChangesAsync();
            return user.Id;
        }

        public async Task UpdateUserAsync(int id, UserCreateViewModel vm)
        {
            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == id)
                ?? throw new InvalidOperationException("Usuario no encontrado.");

            // LOG-03: the RAES pseudo-user has no UserSystem, so it never appears in the normal
            // Usuarios listing — but it's still a plain Users row, reachable by ID through this
            // generic edit route. The RAES retirement flow depends on its email staying constant.
            // UX-06's "sin asignar" pseudo-user is reserved the same way.
            if (user.Email is AuthBootstrapper.RaesUserEmail or AuthBootstrapper.UnassignedUserEmail)
            {
                throw new InvalidOperationException("Este usuario reservado del sistema no se puede editar.");
            }

            var normalizedEmail = vm.Email.Trim().ToLowerInvariant();

            if (await _dbContext.Users.AnyAsync(u => u.Id != id && u.Email.ToLower() == normalizedEmail))
            {
                throw new InvalidOperationException("Ya existe un usuario con ese correo.");
            }

            user.Name = vm.Name;
            user.LastName = vm.LastName;
            user.Email = normalizedEmail;
            user.EmailTeams = string.IsNullOrWhiteSpace(vm.EmailTeams)
                ? normalizedEmail
                : vm.EmailTeams.Trim().ToLowerInvariant();
            user.AreaId = vm.AreaId;
            user.RegionalId = vm.RegionalId;

            // El cargo es un historial append-only (ver CargoAssignment): no se "edita" el cargo
            // actual, se agrega una fila nueva solo si el valor elegido cambió. Si vm.CargoId viene
            // null (nada seleccionado) no se toca el historial — no tiene sentido un registro sin
            // CargoId, así que no hay forma de "quitar" un cargo ya asignado desde aquí.
            var currentCargoId = await _dbContext.CargoAssignments
                .Where(a => a.UserId == id)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .Select(a => (int?)a.CargoId)
                .FirstOrDefaultAsync();

            if (vm.CargoId.HasValue && vm.CargoId != currentCargoId)
            {
                _dbContext.CargoAssignments.Add(new CargoAssignment
                {
                    UserId = id,
                    CargoId = vm.CargoId.Value,
                    DateAsignation = DateOnly.FromDateTime(DateTime.Now)
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        // LOG-01: CreateUserAsync + CreateLoginAsync run as two separate SaveChangesAsync calls;
        // if the login step fails (duplicate username), the just-saved Users row is orphaned —
        // it exists in the inventory with no access account and no way to retry cleanly. This
        // adds both in a single SaveChangesAsync so a failure leaves nothing behind.
        public async Task<int> CreateAccountAsync(UserCreateViewModel personVm, string username, string password, int rolId)
        {
            var normalizedEmail = personVm.Email.Trim().ToLowerInvariant();
            if (await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                throw new InvalidOperationException("Ya existe un usuario con ese correo.");
            }

            var normalizedUsername = username.Trim().ToLowerInvariant();
            if (await _dbContext.UserSystems.AnyAsync(us => us.Username.ToLower() == normalizedUsername))
            {
                throw new InvalidOperationException("Ya existe una cuenta con ese nombre de usuario.");
            }

            var user = new Users
            {
                Name = personVm.Name,
                LastName = personVm.LastName,
                Email = normalizedEmail,
                EmailTeams = string.IsNullOrWhiteSpace(personVm.EmailTeams)
                    ? normalizedEmail
                    : personVm.EmailTeams.Trim().ToLowerInvariant(),
                AreaId = personVm.AreaId,
                RegionalId = personVm.RegionalId
            };
            _dbContext.Users.Add(user);

            if (personVm.CargoId.HasValue)
            {
                _dbContext.CargoAssignments.Add(new CargoAssignment
                {
                    User = user,
                    CargoId = personVm.CargoId.Value,
                    DateAsignation = DateOnly.FromDateTime(DateTime.Now)
                });
            }

            var userSystem = new UserSystem
            {
                Username = normalizedUsername,
                User = user,
                RolId = rolId,
                PasswordHash = string.Empty
            };
            userSystem.PasswordHash = _passwordHasher.HashPassword(userSystem, password);
            _dbContext.UserSystems.Add(userSystem);

            await _dbContext.SaveChangesAsync();
            return user.Id;
        }

        // Toda persona nueva registrada desde UserAdminController.Create recibe automáticamente
        // login (usuario = correo, contraseña temporal) con rol Colaborador y un correo de
        // bienvenida. Reutiliza CreateAccountAsync (ya crea Users+UserSystem en un solo
        // SaveChangesAsync, LOG-01) envuelto en una transacción explícita: si el correo falla, no
        // se confirma — no queremos una cuenta creada sin que la persona haya recibido su clave
        // (mismo patrón de reversión que MaintenanceCampaignService.CompleteAndSendAsync).
        public async Task<int> CreateUserWithLoginAsync(UserCreateViewModel vm)
        {
            var colaboradorRoleId = await _dbContext.Rols
                .Where(r => r.Name == AuthBootstrapper.ColaboradorRoleName)
                .Select(r => r.Id)
                .SingleAsync();

            var normalizedEmail = vm.Email.Trim().ToLowerInvariant();
            var password = TemporaryPasswordGenerator.Generate();

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                var userId = await CreateAccountAsync(vm, normalizedEmail, password, colaboradorRoleId);

                var userSystem = await _dbContext.UserSystems.SingleAsync(us => us.UserId == userId);
                userSystem.MustChangePassword = true;
                await _dbContext.SaveChangesAsync();

                await _emailService.SendAsync(
                    normalizedEmail,
                    $"{vm.Name} {vm.LastName}",
                    "Tu acceso a AssetOps",
                    $"""
                    <p>Hola {vm.Name},</p>
                    <p>Se creó tu acceso a la plataforma AssetOps.</p>
                    <p>Usuario: <strong>{normalizedEmail}</strong><br/>
                    Contraseña temporal: <strong>{password}</strong></p>
                    <p>Deberás cambiarla la primera vez que inicies sesión.</p>
                    """,
                    Array.Empty<EmailAttachment>());

                await transaction.CommitAsync();
                return userId;
            });
        }

        public async Task CreateLoginAsync(int userId, string username, string password, int rolId)
        {
            var normalizedUsername = username.Trim().ToLowerInvariant();

            if (await _dbContext.UserSystems.AnyAsync(us => us.Username.ToLower() == normalizedUsername))
            {
                throw new InvalidOperationException("Ya existe una cuenta con ese nombre de usuario.");
            }

            if (await _dbContext.UserSystems.AnyAsync(us => us.UserId == userId))
            {
                throw new InvalidOperationException("Este usuario ya tiene una cuenta de acceso asociada.");
            }

            var userSystem = new UserSystem
            {
                Username = normalizedUsername,
                UserId = userId,
                RolId = rolId,
                PasswordHash = string.Empty
            };
            userSystem.PasswordHash = _passwordHasher.HashPassword(userSystem, password);

            _dbContext.UserSystems.Add(userSystem);
            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateAccountAsync(int userId, AccessAccountEditViewModel vm, int actingAdminUserSystemId)
        {
            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId)
                ?? throw new InvalidOperationException("Usuario no encontrado.");

            var userSystem = await _dbContext.UserSystems.SingleOrDefaultAsync(us => us.UserId == userId)
                ?? throw new InvalidOperationException("Este usuario no tiene una cuenta de acceso.");

            var normalizedEmail = vm.Email.Trim().ToLowerInvariant();
            if (await _dbContext.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == normalizedEmail))
            {
                throw new InvalidOperationException("Ya existe un usuario con ese correo.");
            }

            var normalizedUsername = vm.Username.Trim().ToLowerInvariant();
            if (await _dbContext.UserSystems.AnyAsync(us => us.Id != userSystem.Id && us.Username.ToLower() == normalizedUsername))
            {
                throw new InvalidOperationException("Ya existe una cuenta con ese nombre de usuario.");
            }

            if (!string.IsNullOrEmpty(vm.NewPassword))
            {
                if (string.IsNullOrEmpty(vm.AdminPassword))
                {
                    throw new InvalidOperationException("Debes ingresar tu contraseña de administrador para cambiar la clave.");
                }

                var adminUserSystem = await _dbContext.UserSystems.SingleOrDefaultAsync(us => us.Id == actingAdminUserSystemId)
                    ?? throw new InvalidOperationException("No se pudo verificar la cuenta del administrador.");

                var verificationResult = _passwordHasher.VerifyHashedPassword(adminUserSystem, adminUserSystem.PasswordHash, vm.AdminPassword);
                if (verificationResult == PasswordVerificationResult.Failed)
                {
                    throw new InvalidOperationException("Contraseña de administrador incorrecta.");
                }

                userSystem.PasswordHash = _passwordHasher.HashPassword(userSystem, vm.NewPassword);
            }

            user.Name = vm.Name;
            user.LastName = vm.LastName;
            user.Email = normalizedEmail;
            user.EmailTeams = string.IsNullOrWhiteSpace(vm.EmailTeams)
                ? normalizedEmail
                : vm.EmailTeams.Trim().ToLowerInvariant();
            user.AreaId = vm.AreaId;
            user.RegionalId = vm.RegionalId;

            userSystem.Username = normalizedUsername;
            var roleChanged = userSystem.RolId != vm.RolId;
            if (roleChanged || !string.IsNullOrEmpty(vm.NewPassword))
            {
                userSystem.SecurityStamp = Guid.NewGuid();
            }

            await RunWithAdminInvariantAsync(async adminRoleId =>
            {
                if (roleChanged && userSystem.RolId == adminRoleId && vm.RolId != adminRoleId
                    && !await HasOtherEnabledAdminAsync(adminRoleId, userSystem.Id))
                {
                    throw new InvalidOperationException("No se puede quitar el rol de Administrador: es la única cuenta habilitada con ese rol. Crea o habilita otra cuenta de Administrador antes de continuar.");
                }

                if (roleChanged)
                {
                    var oldRoleName = await _dbContext.Rols.Where(r => r.Id == userSystem.RolId).Select(r => r.Name).SingleAsync();
                    var newRoleName = await _dbContext.Rols.Where(r => r.Id == vm.RolId).Select(r => r.Name).SingleAsync();
                    await LogAuditAsync(actingAdminUserSystemId, "RoleChanged", $"Cuenta {userSystem.Username}: {oldRoleName} -> {newRoleName}");
                }

                userSystem.RolId = vm.RolId;
                await _dbContext.SaveChangesAsync();
            });
        }

        public async Task DeleteAccountAsync(int userId, int actingAdminUserSystemId, string? adminPassword)
        {
            var userSystem = await _dbContext.UserSystems.SingleOrDefaultAsync(us => us.UserId == userId)
                ?? throw new InvalidOperationException("Este usuario no tiene una cuenta de acceso.");

            if (string.IsNullOrEmpty(adminPassword))
            {
                throw new InvalidOperationException("Debes ingresar tu contraseña de administrador para eliminar.");
            }

            var adminUserSystem = await _dbContext.UserSystems.SingleOrDefaultAsync(us => us.Id == actingAdminUserSystemId)
                ?? throw new InvalidOperationException("No se pudo verificar la cuenta del administrador.");

            var verificationResult = _passwordHasher.VerifyHashedPassword(adminUserSystem, adminUserSystem.PasswordHash, adminPassword);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                throw new InvalidOperationException("Contraseña de administrador incorrecta.");
            }

            if (userSystem.Id == actingAdminUserSystemId)
            {
                throw new InvalidOperationException("No puedes eliminar tu propia cuenta de acceso mientras tienes la sesión iniciada.");
            }

            // LOG-07: SpecChangeLog.ChangedByUserSystemId is DeleteBehavior.Restrict, so deleting
            // an account with edit history used to bubble up as a raw DbUpdateException (500).
            // Surface it as a normal validation error and point at the non-destructive option.
            if (await _dbContext.SpecChangeLogs.AnyAsync(s => s.ChangedByUserSystemId == userSystem.Id))
            {
                throw new InvalidOperationException("No se puede eliminar: esta cuenta es autora de cambios registrados en el historial de equipos. Desactívala en vez de eliminarla para conservar esa autoría.");
            }

            // MaintenanceCampaignItem.SignerUserSystemId es Restrict por el mismo motivo.
            if (await _dbContext.MaintenanceCampaignItems.AnyAsync(c => c.SignerUserSystemId == userSystem.Id))
            {
                throw new InvalidOperationException("No se puede eliminar: esta cuenta firmó actas de mantenimiento programado. Desactívala en vez de eliminarla para conservar esa autoría.");
            }

            // CasoTI.TecnicoUserSystemId es Restrict por el mismo motivo: no perder la autoría
            // de quién resolvió un caso de soporte.
            if (await _dbContext.CasosTI.AnyAsync(c => c.TecnicoUserSystemId == userSystem.Id))
            {
                throw new InvalidOperationException("No se puede eliminar: esta cuenta tiene Casos TI resueltos a su nombre. Desactívala en vez de eliminarla para conservar esa autoría.");
            }

            await RunWithAdminInvariantAsync(async adminRoleId =>
            {
                if (userSystem.RolId == adminRoleId && !await HasOtherEnabledAdminAsync(adminRoleId, userSystem.Id))
                {
                    throw new InvalidOperationException("No se puede eliminar: es la única cuenta de Administrador habilitada. Crea otra cuenta de Administrador antes de eliminar esta.");
                }

                await LogAuditAsync(actingAdminUserSystemId, "AccountDeleted", $"Cuenta {userSystem.Username}");
                _dbContext.UserSystems.Remove(userSystem);
                await _dbContext.SaveChangesAsync();
            });
        }

        // Distinto de UpdateAccountAsync/DeleteAccountAsync (un admin actuando sobre OTRA cuenta):
        // aquí el actor cambia su propia contraseña, por eso no recibe adminPassword ni valida el
        // invariante de "al menos un Administrador habilitado".
        public async Task<Guid> ChangeOwnPasswordAsync(int userSystemId, string newPassword)
        {
            var userSystem = await _dbContext.UserSystems.SingleOrDefaultAsync(us => us.Id == userSystemId)
                ?? throw new InvalidOperationException("Cuenta no encontrada.");

            userSystem.PasswordHash = _passwordHasher.HashPassword(userSystem, newPassword);
            userSystem.MustChangePassword = false;
            userSystem.SecurityStamp = Guid.NewGuid();
            await _dbContext.SaveChangesAsync();
            return userSystem.SecurityStamp;
        }

        private async Task RunWithAdminInvariantAsync(Func<int, Task> mutateAndSaveAsync)
        {
            var adminRoleId = await _dbContext.Rols
                .Where(r => r.Name == AuthBootstrapper.AdministradorRoleName)
                .Select(r => r.Id)
                .SingleAsync();

            await mutateAndSaveAsync(adminRoleId);
        }

        private Task<bool> HasOtherEnabledAdminAsync(int adminRoleId, int excludingUserSystemId)
        {
            return _dbContext.UserSystems
                .AnyAsync(us => us.Id != excludingUserSystemId && us.RolId == adminRoleId && us.User.Activo);
        }

        // SEG-09: not a FK to UserSystem on purpose — the actor row can be deleted later and the
        // audit trail must still read correctly, so it snapshots a description instead.
        private async Task LogAuditAsync(int actorUserSystemId, string action, string targetDescription)
        {
            var actorUsername = await _dbContext.UserSystems
                .Where(us => us.Id == actorUserSystemId)
                .Select(us => us.Username)
                .SingleOrDefaultAsync() ?? $"UserSystem#{actorUserSystemId}";

            _dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                ActorUserSystemId = actorUserSystemId,
                ActorDescription = actorUsername,
                Action = action,
                TargetDescription = targetDescription
            });
        }

        public async Task<List<Users>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            // Antes de que toda persona nueva recibiera login automático, "sin cuenta" bastaba
            // para no duplicar con Cuentas de acceso (GetAccountsAsync). Ahora que cada persona
            // nueva SIEMPRE tiene un login Colaborador, se excluye solo a quien tenga una cuenta
            // de personal (Admin/Técnico/Auditor) — esas sí se gestionan desde Cuentas de acceso.
            return await _dbContext.Users
                .Where(u => u.Email != AuthBootstrapper.RaesUserEmail
                    && u.Email != AuthBootstrapper.UnassignedUserEmail
                    && !u.UserSystems.Any(us => us.Rol.Name != AuthBootstrapper.ColaboradorRoleName)
                    && (includeInactive || u.Activo))
                .Include(u => u.Area)
                .Include(u => u.Regional)
                .Include(u => u.UserSystems).ThenInclude(us => us.Rol)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Users>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            // Espejo de la exclusión en GetAllAsync: un Colaborador (login automático del alta en
            // "Usuarios") no debe listarse también aquí, o parecería que la misma persona/correo
            // está duplicada entre los dos módulos.
            return await _dbContext.Users
                .Where(u => u.Email != AuthBootstrapper.RaesUserEmail
                    && u.UserSystems.Any(us => us.Rol.Name != AuthBootstrapper.ColaboradorRoleName))
                .Include(u => u.Area)
                .Include(u => u.Regional)
                .Include(u => u.UserSystems).ThenInclude(us => us.Rol)
                .ToListAsync(cancellationToken);
        }

        public async Task DeactivateAsync(int id)
        {
            var user = await _dbContext.Users.Include(u => u.UserSystems).SingleOrDefaultAsync(u => u.Id == id)
                ?? throw new InvalidOperationException("Usuario no encontrado.");

            // LOG-03: see UpdateUserAsync above — reachable by ID through this generic route too,
            // and deactivating either reserved pseudo-user would corrupt its bookkeeping (it's
            // already Activo=false by design in the "sin asignar" case).
            if (user.Email is AuthBootstrapper.RaesUserEmail or AuthBootstrapper.UnassignedUserEmail)
            {
                throw new InvalidOperationException("Este usuario reservado del sistema no se puede desactivar.");
            }

            if (!user.Activo)
            {
                return;
            }

            await RunWithAdminInvariantAsync(async adminRoleId =>
            {
                var deactivatesAnAdmin = user.UserSystems.Any(account => account.RolId == adminRoleId);
                if (deactivatesAnAdmin)
                {
                    var ownAccountIds = user.UserSystems.Select(account => account.Id).ToHashSet();
                    var hasOtherEnabledAdmin = await _dbContext.UserSystems
                        .AnyAsync(us => !ownAccountIds.Contains(us.Id) && us.RolId == adminRoleId && us.User.Activo);
                    if (!hasOtherEnabledAdmin)
                    {
                        throw new InvalidOperationException("No se puede desactivar: es el único Administrador habilitado. Habilita otra cuenta de Administrador antes de continuar.");
                    }
                }

                user.Activo = false;
                user.DeactivatedAt = DateOnly.FromDateTime(DateTime.Now);
                foreach (var account in user.UserSystems)
                {
                    account.SecurityStamp = Guid.NewGuid();
                }
                await _dbContext.SaveChangesAsync();
            });
        }

        public async Task<List<UsersByRegionalItem>> GetUsersByRegionalAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Users
                .Where(u => u.Activo && u.Email != AuthBootstrapper.RaesUserEmail)
                .GroupBy(u => u.Regional != null ? u.Regional.Name : "Sin regional")
                .Select(g => new UsersByRegionalItem { RegionalName = g.Key, Count = g.Count() })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.RegionalName)
                .ToListAsync(cancellationToken);
        }

        public async Task<Users?> GetByIdAsync(int id)
        {
            return await _dbContext.Users
                .Include(u => u.Area)
                .Include(u => u.Regional)
                .Include(u => u.UserSystems).ThenInclude(us => us.Rol)
                .SingleOrDefaultAsync(u => u.Id == id);
        }

        public async Task<UserDetailViewModel?> GetDetailAsync(int id)
        {
            var user = await _dbContext.Users
                .Include(u => u.Area)
                .Include(u => u.Regional)
                .Include(u => u.UserSystems).ThenInclude(us => us.Rol)
                .SingleOrDefaultAsync(u => u.Id == id);

            if (user is null)
            {
                return null;
            }

            var history = await _dbContext.Asignations
                .Where(a => a.UserId == id)
                .Include(a => a.Desktop)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            var desktopIds = history.Select(a => a.DesktopId).Distinct().ToList();

            var latestAssignationIdByDesktop = await _dbContext.Asignations
                .Where(a => desktopIds.Contains(a.DesktopId))
                .GroupBy(a => a.DesktopId)
                .Select(g => g.OrderByDescending(a => a.DateAsignation).ThenByDescending(a => a.Id).First().Id)
                .ToListAsync();

            var currentDesktops = !user.Activo
                ? new List<AssignedDesktopItem>()
                : history
                    .Where(a => latestAssignationIdByDesktop.Contains(a.Id))
                    .GroupBy(a => a.DesktopId)
                    .Select(g => g.First())
                    .Select(a => new AssignedDesktopItem
                    {
                        DesktopId = a.DesktopId,
                        NameDesktop = a.Desktop.NameDesktop,
                        SerialNumber = a.Desktop.SerialNumber,
                        SinceDate = a.DateAsignation
                    })
                    .ToList();

            var userSystem = user.UserSystems.FirstOrDefault();

            var cargoHistory = await _dbContext.CargoAssignments
                .Where(a => a.UserId == id)
                .Include(a => a.Cargo)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            return new UserDetailViewModel
            {
                Id = user.Id,
                Name = user.Name,
                LastName = user.LastName,
                Email = user.Email,
                EmailTeams = user.EmailTeams,
                Activo = user.Activo,
                DeactivatedAt = user.DeactivatedAt,
                AreaName = user.Area?.Name ?? "-",
                RegionalName = user.Regional?.Name ?? "-",
                Username = userSystem?.Username,
                RolName = userSystem?.Rol?.Name,
                CargoName = cargoHistory.FirstOrDefault()?.Cargo.Name,
                CurrentDesktops = currentDesktops,
                AsignationHistory = history
                    .Select(a => new UserAsignationHistoryItem
                    {
                        NameDesktop = a.Desktop.NameDesktop,
                        SerialNumber = a.Desktop.SerialNumber,
                        DateAsignation = a.DateAsignation
                    })
                    .ToList(),
                CargoHistory = cargoHistory
                    .Select(a => new UserCargoHistoryItem
                    {
                        CargoName = a.Cargo.Name,
                        DateAsignation = a.DateAsignation
                    })
                    .ToList()
            };
        }
    }
}
