using System.Net;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.AccessAccount;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace GestorEquipos.Tests.Integration
{
    public class SessionRevocationTests
    {
        [SqlServerFact]
        public async Task SessionLifecycle_PasswordRoleAndStateChanges_RevokeEveryOldCookie()
        {
            using var factory = new SessionApplicationFactory();
            var actingAdmin = await factory.CreateAccountAsync();
            var account = await factory.CreateAccountAsync();
            var auditor = await factory.CreateAccountAsync(AuthBootstrapper.AuditorRoleName);
            using var actorSession = factory.CreateSession();
            using var session = factory.CreateSession();
            using var secondSession = factory.CreateSession();
            using var auditorSession = factory.CreateSession();
            await factory.LoginAsync(actorSession, actingAdmin);
            var originalCookie = await factory.LoginAsync(session, account, rememberMe: true);
            await factory.LoginAsync(secondSession, account);
            await factory.LoginAsync(auditorSession, auditor);
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/AccessAccount/Index")).StatusCode);
            var denied = await auditorSession.GetAsync("/AccessAccount/Index");
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("/Login/AccessDenied", denied.Headers.Location?.OriginalString);

            using var ordinaryEdit = await SessionApplicationFactory.PostFormAsync(actorSession, "/AccessAccount/Edit/" + account.UserId, "/AccessAccount/Edit/" + account.UserId, EditFields(account));
            Assert.Equal(HttpStatusCode.Redirect, ordinaryEdit.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/AccessAccount/Index")).StatusCode);

            factory.Clock.Advance(TimeSpan.FromMinutes(35));
            using var renewed = await session.GetAsync("/AccessAccount/Index");
            Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
            Assert.Contains(renewed.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Cookies="));

            var passwordFields = EditFields(account);
            var changedPassword = "Changed!" + Guid.NewGuid().ToString("N");
            passwordFields["NewPassword"] = changedPassword;
            passwordFields["ConfirmPassword"] = changedPassword;
            passwordFields["AdminPassword"] = "wrong";
            using var rejected = await SessionApplicationFactory.PostFormAsync(actorSession, "/AccessAccount/Edit/" + account.UserId, "/AccessAccount/Edit/" + account.UserId, passwordFields);
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/AccessAccount/Index")).StatusCode);
            passwordFields["AdminPassword"] = factory.Password;
            using var passwordChange = await SessionApplicationFactory.PostFormAsync(actorSession, "/AccessAccount/Edit/" + account.UserId, "/AccessAccount/Edit/" + account.UserId, passwordFields);
            Assert.Equal(HttpStatusCode.Redirect, passwordChange.StatusCode);
            AssertLoginRedirect(await session.GetAsync("/AccessAccount/Index"));
            AssertLoginRedirect(await secondSession.GetAsync("/AccessAccount/Index"));
            using var replay = factory.CreateCookieReplay(originalCookie);
            AssertLoginRedirect(await replay.GetAsync("/AccessAccount/Index"));
            Assert.Equal(HttpStatusCode.OK, (await actorSession.GetAsync("/AccessAccount/Index")).StatusCode);

            using var freshSession = factory.CreateSession();
            var freshCookie = await factory.LoginAsync(freshSession, account, password: changedPassword);
            var fields = EditFields(account);
            fields["RolId"] = auditor.RolId.ToString();
            using var demotion = await SessionApplicationFactory.PostFormAsync(actorSession, "/AccessAccount/Edit/" + account.UserId, "/AccessAccount/Edit/" + account.UserId, fields);
            Assert.Equal(HttpStatusCode.Redirect, demotion.StatusCode);
            fields["RolId"] = account.RolId.ToString();
            using var restoration = await SessionApplicationFactory.PostFormAsync(actorSession, "/AccessAccount/Edit/" + account.UserId, "/AccessAccount/Edit/" + account.UserId, fields);
            Assert.Equal(HttpStatusCode.Redirect, restoration.StatusCode);
            using var restoredRoleReplay = factory.CreateCookieReplay(freshCookie);
            AssertLoginRedirect(await restoredRoleReplay.GetAsync("/AccessAccount/Index"));

            using var activeSession = factory.CreateSession();
            var activeCookie = await factory.LoginAsync(activeSession, account, password: changedPassword);
            using var deactivated = await SessionApplicationFactory.PostFormAsync(actorSession, "/UserAdmin/Index", "/UserAdmin/Deactivate", new() { ["id"] = account.UserId.ToString() });
            Assert.Equal(HttpStatusCode.Redirect, deactivated.StatusCode);
            AssertLoginRedirect(await activeSession.GetAsync("/AccessAccount/Index"));
            using var loginAttempt = factory.CreateSession();
            using var inactiveLogin = await SessionApplicationFactory.PostFormAsync(loginAttempt, "/Login/Index", "/Login/LoginUser", new() { ["Email"] = account.User.Email, ["Password"] = changedPassword });
            Assert.Equal(HttpStatusCode.OK, inactiveLogin.StatusCode);
            AssertLoginRedirect(await loginAttempt.GetAsync("/AccessAccount/Index"));
            await factory.WithDatabaseAsync(async (database, _) =>
            {
                (await database.Users.FindAsync(account.UserId))!.Activo = true;
                await database.SaveChangesAsync();
            });
            using var reactivatedReplay = factory.CreateCookieReplay(activeCookie);
            AssertLoginRedirect(await reactivatedReplay.GetAsync("/AccessAccount/Index"));
        }

        [SqlServerFact]
        public async Task LegacyCookie_WithoutSecurityStamp_IsRejectedByRealMiddleware()
        {
            using var factory = new SessionApplicationFactory();
            var account = await factory.CreateAccountAsync();
            var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim("UserId", account.UserId.ToString()),
                new Claim(ClaimTypes.Role, account.Rol.Name)
            }, CookieAuthenticationDefaults.AuthenticationScheme);
            var properties = new AuthenticationProperties { IssuedUtc = factory.Clock.GetUtcNow(), ExpiresUtc = factory.Clock.GetUtcNow().AddHours(1) };
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, CookieAuthenticationDefaults.AuthenticationScheme);
            using var replay = factory.CreateCookieReplay(".AspNetCore.Cookies=" + options.TicketDataFormat.Protect(ticket));
            AssertLoginRedirect(await replay.GetAsync("/AccessAccount/Index"));
        }

        [SqlServerFact]
        public async Task SessionLookup_DatabaseFailure_DoesNotExecuteProtectedWrite()
        {
            using var factory = new SessionApplicationFactory();
            var account = await factory.CreateAccountAsync();
            using var session = factory.CreateSession();
            await factory.LoginAsync(session, account);
            var token = await SessionApplicationFactory.TokenAsync(session, "/UserAdmin/Create");
            factory.FailSessionQueries = true;
            try
            {
                using var response = await session.PostAsync("/UserAdmin/Create", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token, ["Name"] = "Failed", ["LastName"] = "Lookup", ["Email"] = "failed-lookup@session.invalid"
                }));
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }
            catch (InvalidOperationException exception)
            {
                Assert.Equal("Simulated session database failure.", exception.Message);
            }
            finally
            {
                factory.FailSessionQueries = false;
            }
            await factory.WithDatabaseAsync(async (database, _) => Assert.False(await database.Users.AnyAsync(person => person.Email == "failed-lookup@session.invalid")));
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/AccessAccount/Index")).StatusCode);
        }

        private static Dictionary<string, string> EditFields(UserSystem account)
        {
            return new()
            {
                ["Name"] = "Updated", ["LastName"] = account.User.LastName, ["Email"] = account.User.Email,
                ["Username"] = account.Username, ["RolId"] = account.RolId.ToString()
            };
        }

        [SqlServerFact]
        public async Task ExistingCookie_DemotedAccount_CannotRestoreItsRole()
        {
            using var factory = new SessionApplicationFactory();
            var account = await factory.CreateAccountAsync();
            // A second enabled admin so demoting `account` below doesn't trip the SEG-03
            // last-admin guard — this test is about stale-cookie rejection, not that guard.
            await factory.CreateAccountAsync();
            using var session = factory.CreateSession();
            await factory.LoginAsync(session, account);
            var token = await SessionApplicationFactory.TokenAsync(session, "/AccessAccount/Edit/" + account.UserId);
            await factory.WithDatabaseAsync(async (database, services) =>
            {
                var auditorRoleId = await database.Rols.Where(role => role.Name == AuthBootstrapper.AuditorRoleName).Select(role => role.Id).SingleAsync();
                await services.GetRequiredService<IUserAdminService>().UpdateAccountAsync(account.UserId, new AccessAccountEditViewModel
                {
                    Name = account.User.Name, LastName = account.User.LastName, Email = account.User.Email,
                    Username = account.Username, RolId = auditorRoleId
                }, account.Id);
            });
            using var response = await session.PostAsync("/AccessAccount/Edit/" + account.UserId, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["Name"] = account.User.Name, ["LastName"] = account.User.LastName,
                ["Email"] = account.User.Email, ["Username"] = account.Username, ["RolId"] = account.RolId.ToString()
            }));
            AssertLoginRedirect(response);
            await factory.WithDatabaseAsync(async (database, _) =>
                Assert.Equal(AuthBootstrapper.AuditorRoleName, (await database.UserSystems.Include(current => current.Rol).SingleAsync(current => current.Id == account.Id)).Rol.Name));
        }

        [SqlServerFact]
        public async Task ExistingCookie_DeletedAccount_CannotCreateUsers()
        {
            using var factory = new SessionApplicationFactory();
            var actingAdmin = await factory.CreateAccountAsync();
            var account = await factory.CreateAccountAsync();
            using var session = factory.CreateSession();
            await factory.LoginAsync(session, account);
            var token = await SessionApplicationFactory.TokenAsync(session, "/UserAdmin/Create");
            await factory.WithDatabaseAsync(async (_, services) =>
                await services.GetRequiredService<IUserAdminService>().DeleteAccountAsync(account.UserId, actingAdmin.Id, factory.Password));
            using var response = await session.PostAsync("/UserAdmin/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["Name"] = "Forbidden", ["LastName"] = "Write", ["Email"] = "blocked@session.invalid"
            }));
            AssertLoginRedirect(response);
            await factory.WithDatabaseAsync(async (database, _) => Assert.False(await database.Users.AnyAsync(person => person.Email == "blocked@session.invalid")));
        }

        [SqlServerFact]
        public async Task ExistingCookie_DeactivatedPerson_IsRejected()
        {
            using var factory = new SessionApplicationFactory();
            var account = await factory.CreateAccountAsync();
            // A second enabled admin so deactivating `account` below doesn't trip the SEG-03
            // last-admin guard — this test is about stale-cookie rejection, not that guard.
            await factory.CreateAccountAsync();
            using var session = factory.CreateSession();
            await factory.LoginAsync(session, account);
            await factory.WithDatabaseAsync(async (_, services) =>
                await services.GetRequiredService<IUserAdminService>().DeactivateAsync(account.UserId));
            using var response = await session.GetAsync("/AccessAccount/Index");
            AssertLoginRedirect(response);
        }

        internal static void AssertLoginRedirect(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Login/Index", response.Headers.Location?.OriginalString);
        }
    }
}
