using System.Security.Claims;
using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GestorEquipos.Tests.Services
{
    public class SessionCookieEventsTests
    {
        [Theory]
        [InlineData("missing-account")]
        [InlineData("missing-person")]
        [InlineData("missing-role")]
        [InlineData("missing-stamp")]
        [InlineData("duplicate-account")]
        [InlineData("duplicate-person")]
        [InlineData("duplicate-role")]
        [InlineData("duplicate-stamp")]
        [InlineData("invalid-account")]
        [InlineData("negative-person")]
        [InlineData("invalid-stamp")]
        [InlineData("empty-stamp")]
        [InlineData("different-person")]
        [InlineData("different-role")]
        [InlineData("different-stamp")]
        [InlineData("unknown-account")]
        [InlineData("inactive")]
        public async Task ValidatePrincipal_InvalidSession_RejectsAndSignsOut(string scenario)
        {
            using var database = TestHelpers.CreateDbContext();
            var account = SeedAccount(database);
            var claims = ClaimsFor(account);
            var claimTypes = new Dictionary<string, string>
            {
                ["account"] = ClaimTypes.NameIdentifier, ["person"] = "UserId",
                ["role"] = ClaimTypes.Role, ["stamp"] = SessionCookieEvents.SecurityStampClaimType
            };
            if (scenario.StartsWith("missing-"))
            {
                claims.RemoveAll(claim => claim.Type == claimTypes[scenario[8..]]);
            }
            else if (scenario.StartsWith("duplicate-"))
            {
                claims.Add(claims.Single(claim => claim.Type == claimTypes[scenario[10..]]));
            }
            else
            {
                switch (scenario)
                {
                    case "invalid-account": ReplaceClaim(claims, ClaimTypes.NameIdentifier, "invalid"); break;
                    case "negative-person": ReplaceClaim(claims, "UserId", "-1"); break;
                    case "invalid-stamp": ReplaceClaim(claims, SessionCookieEvents.SecurityStampClaimType, "invalid"); break;
                    case "empty-stamp": ReplaceClaim(claims, SessionCookieEvents.SecurityStampClaimType, Guid.Empty.ToString("D")); break;
                    case "different-person": ReplaceClaim(claims, "UserId", "9999"); break;
                    case "different-role": ReplaceClaim(claims, ClaimTypes.Role, "Auditor"); break;
                    case "different-stamp": ReplaceClaim(claims, SessionCookieEvents.SecurityStampClaimType, Guid.NewGuid().ToString("D")); break;
                    case "unknown-account": ReplaceClaim(claims, ClaimTypes.NameIdentifier, "9999"); break;
                    case "inactive": account.User.Activo = false; database.SaveChanges(); break;
                }
            }
            var authentication = new RecordingAuthenticationService();
            using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
            var context = CreateContext(claims, services);
            context.ShouldRenew = true;

            await new SessionCookieEvents(database).ValidatePrincipal(context);

            Assert.Null(context.Principal);
            Assert.False(context.ShouldRenew);
            Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, authentication.SignedOutScheme);
        }

        [Fact]
        public async Task ValidatePrincipal_CurrentSession_PreservesPrincipalAndRenewal()
        {
            using var database = TestHelpers.CreateDbContext();
            var account = SeedAccount(database);
            var authentication = new RecordingAuthenticationService();
            using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
            var context = CreateContext(ClaimsFor(account), services);
            var principal = context.Principal;
            context.ShouldRenew = true;
            await new SessionCookieEvents(database).ValidatePrincipal(context);
            Assert.Same(principal, context.Principal);
            Assert.True(context.ShouldRenew);
            Assert.Null(authentication.SignedOutScheme);
        }

        [Fact]
        public async Task ValidatePrincipal_DatabaseFailure_PropagatesInsteadOfAccepting()
        {
            var database = TestHelpers.CreateDbContext();
            var account = SeedAccount(database);
            var authentication = new RecordingAuthenticationService();
            using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
            var context = CreateContext(ClaimsFor(account), services);
            await database.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => new SessionCookieEvents(database).ValidatePrincipal(context));
            Assert.Null(authentication.SignedOutScheme);
        }

        internal static UserSystem SeedAccount(MyDbContext database)
        {
            var account = new UserSystem
            {
                Username = "session@unit.invalid", PasswordHash = "unused",
                User = new Users { Name = "Session", LastName = "Test", Email = "session@unit.invalid" },
                Rol = new Rol { Name = AuthBootstrapper.AdministradorRoleName }
            };
            database.UserSystems.Add(account);
            database.SaveChanges();
            return account;
        }

        private static List<Claim> ClaimsFor(UserSystem account)
        {
            return new()
            {
                new(ClaimTypes.NameIdentifier, account.Id.ToString()), new("UserId", account.UserId.ToString()),
                new(ClaimTypes.Role, account.Rol.Name), new(SessionCookieEvents.SecurityStampClaimType, account.SecurityStamp.ToString("D"))
            };
        }

        private static void ReplaceClaim(List<Claim> claims, string type, string value)
        {
            claims.RemoveAll(claim => claim.Type == type);
            claims.Add(new Claim(type, value));
        }

        private static CookieValidatePrincipalContext CreateContext(List<Claim> claims, IServiceProvider services)
        {
            var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, scheme.Name));
            var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), scheme.Name);
            return new CookieValidatePrincipalContext(new DefaultHttpContext { RequestServices = services }, scheme, new CookieAuthenticationOptions(), ticket);
        }

        private sealed class RecordingAuthenticationService : IAuthenticationService
        {
            public string? SignedOutScheme { get; private set; }
            public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw new NotSupportedException();
            public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
            public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
            public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();
            public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            {
                SignedOutScheme = scheme;
                return Task.CompletedTask;
            }
        }
    }
}
