using System.Globalization;
using System.Security.Claims;
using Gestor_Equipos.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Auth
{
    public sealed class SessionCookieEvents : CookieAuthenticationEvents
    {
        public const string SecurityStampClaimType = "SecurityStamp";
        private readonly MyDbContext _dbContext;

        public SessionCookieEvents(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var accountClaim = GetSingleClaim(context.Principal, ClaimTypes.NameIdentifier);
            var personClaim = GetSingleClaim(context.Principal, "UserId");
            var roleClaim = GetSingleClaim(context.Principal, ClaimTypes.Role);
            var stampClaim = GetSingleClaim(context.Principal, SecurityStampClaimType);

            if (!int.TryParse(accountClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var accountId) || accountId <= 0
                || !int.TryParse(personClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var personId) || personId <= 0
                || string.IsNullOrWhiteSpace(roleClaim)
                || !Guid.TryParseExact(stampClaim, "D", out var stamp) || stamp == Guid.Empty)
            {
                await RejectAsync(context);
                return;
            }

            var account = await _dbContext.UserSystems.AsNoTracking()
                .Where(candidate => candidate.Id == accountId)
                .Select(candidate => new
                {
                    candidate.UserId,
                    candidate.SecurityStamp,
                    candidate.User.Activo,
                    RoleName = candidate.Rol.Name
                })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

            if (account is null || !account.Activo || account.UserId != personId
                || account.SecurityStamp != stamp || !string.Equals(account.RoleName, roleClaim, StringComparison.Ordinal))
            {
                await RejectAsync(context);
            }
        }

        private static string? GetSingleClaim(ClaimsPrincipal? principal, string type)
        {
            var claims = principal?.FindAll(type).Take(2).ToArray();
            return claims?.Length == 1 ? claims[0].Value : null;
        }

        private static async Task RejectAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            context.ShouldRenew = false;
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
        }
    }
}
