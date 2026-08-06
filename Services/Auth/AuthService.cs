using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;

        public AuthService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        public async Task<AuthenticatedUser?> ValidateUserAsync(string email, string password)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var userSystem = await _dbContext.UserSystems
                .Include(us => us.User)
                .Include(us => us.Rol)
                .SingleOrDefaultAsync(us => us.User.Email.ToLower() == normalizedEmail);

            if (userSystem is null)
            {
                return null;
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(
                userSystem,
                userSystem.PasswordHash,
                password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return null;
            }

            if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                userSystem.PasswordHash = _passwordHasher.HashPassword(userSystem, password);
                await _dbContext.SaveChangesAsync();
            }

            return new AuthenticatedUser(
                userSystem.UserId,
                userSystem.Id,
                userSystem.User.Email,
                $"{userSystem.User.Name} {userSystem.User.LastName}".Trim(),
                userSystem.Rol.Name);
        }
    }
}
