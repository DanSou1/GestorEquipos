using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;
        private readonly ILogger<AuthService> _logger;

        public AuthService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher, ILogger<AuthService> logger)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public async Task<AuthenticatedUser?> ValidateUserAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            _logger.LogInformation("[Login] Iniciando validacion para {Email}. Intentando conectar a la BD (DataSource={DataSource}, Database={Database})...",
                normalizedEmail,
                _dbContext.Database.GetDbConnection().DataSource,
                _dbContext.Database.GetDbConnection().Database);

            UserSystem? userSystem;
            try
            {
                // Punto de depuracion sugerido (F9 en VS): la siguiente linea es la que abre
                // la conexion real contra SQL Server. Si el problema es de conectividad
                // (servidor caido, firewall, credenciales de BD, timeout), la excepcion se
                // captura mas abajo antes de llegar al controller.
                userSystem = await _dbContext.UserSystems
                    .Include(us => us.User)
                    .Include(us => us.Rol)
                    .SingleOrDefaultAsync(us => us.User.Email.ToLower() == normalizedEmail, cancellationToken);

                _logger.LogInformation("[Login] Conexion a la BD exitosa. Usuario {Email} {Encontrado}.",
                    normalizedEmail, userSystem is null ? "NO encontrado" : "encontrado");
            }
            catch (SqlException sqlEx)
            {
                // Fallo especifico de SQL Server: red, login de BD, timeout, base inexistente, etc.
                _logger.LogError(sqlEx,
                    "[Login] FALLO DE CONEXION a SQL Server al validar {Email}. SqlErrorNumber={ErrorNumber}, Servidor={DataSource}",
                    normalizedEmail, sqlEx.Number, _dbContext.Database.GetDbConnection().DataSource);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Login] Error inesperado accediendo a la BD al validar {Email}.", normalizedEmail);
                throw;
            }

            if (userSystem is null || !userSystem.User.Activo)
            {
                _logger.LogWarning("[Login] Rechazado para {Email}: {Motivo}.",
                    normalizedEmail, userSystem is null ? "no existe la cuenta" : "la cuenta esta inactiva");
                return null;
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(
                userSystem,
                userSystem.PasswordHash,
                password);

            _logger.LogInformation("[Login] Resultado de verificacion de password para {Email}: {Resultado}.",
                normalizedEmail, verificationResult);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("[Login] Password invalido para {Email}.", normalizedEmail);
                return null;
            }

            if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                try
                {
                    userSystem.PasswordHash = _passwordHasher.HashPassword(userSystem, password);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("[Login] Rehash de password guardado en BD para {Email}.", normalizedEmail);
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogError(sqlEx, "[Login] FALLO DE CONEXION a SQL Server al guardar el rehash de password para {Email}.", normalizedEmail);
                    throw;
                }
            }

            _logger.LogInformation("[Login] Validacion exitosa para {Email}. Rol={Rol}.", normalizedEmail, userSystem.Rol.Name);

            return new AuthenticatedUser(
                userSystem.UserId,
                userSystem.Id,
                userSystem.User.Email,
                $"{userSystem.User.Name} {userSystem.User.LastName}".Trim(),
                userSystem.Rol.Name,
                userSystem.SecurityStamp);
        }
    }
}
