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

        public UserAdminService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
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
            await _dbContext.SaveChangesAsync();
            return user.Id;
        }

        public async Task UpdateUserAsync(int id, UserCreateViewModel vm)
        {
            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == id)
                ?? throw new InvalidOperationException("Usuario no encontrado.");

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

            await _dbContext.SaveChangesAsync();
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
            userSystem.RolId = vm.RolId;

            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<Users>> GetAllAsync(bool includeInactive = false)
        {
            return await _dbContext.Users
                .Where(u => u.Email != AuthBootstrapper.RaesUserEmail
                    && !u.UserSystems.Any()
                    && (includeInactive || u.Activo))
                .Include(u => u.Area)
                .Include(u => u.Regional)
                .Include(u => u.UserSystems).ThenInclude(us => us.Rol)
                .ToListAsync();
        }

        public async Task<List<Users>> GetAccountsAsync()
        {
            return await _dbContext.Users
                .Where(u => u.Email != AuthBootstrapper.RaesUserEmail && u.UserSystems.Any())
                .Include(u => u.Area)
                .Include(u => u.Regional)
                .Include(u => u.UserSystems).ThenInclude(us => us.Rol)
                .ToListAsync();
        }

        public async Task DeactivateAsync(int id)
        {
            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == id)
                ?? throw new InvalidOperationException("Usuario no encontrado.");

            if (!user.Activo)
            {
                return;
            }

            user.Activo = false;
            user.DeactivatedAt = DateOnly.FromDateTime(DateTime.Now);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<UsersByRegionalItem>> GetUsersByRegionalAsync()
        {
            return await _dbContext.Users
                .Where(u => u.Activo && u.Email != AuthBootstrapper.RaesUserEmail)
                .GroupBy(u => u.Regional != null ? u.Regional.Name : "Sin regional")
                .Select(g => new UsersByRegionalItem { RegionalName = g.Key, Count = g.Count() })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.RegionalName)
                .ToListAsync();
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
                CurrentDesktops = currentDesktops,
                AsignationHistory = history
                    .Select(a => new UserAsignationHistoryItem
                    {
                        NameDesktop = a.Desktop.NameDesktop,
                        SerialNumber = a.Desktop.SerialNumber,
                        DateAsignation = a.DateAsignation
                    })
                    .ToList()
            };
        }
    }
}
