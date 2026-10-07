using System.Reflection;
using System.Security.Claims;
using Gestor_Equipos.Middleware;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Gestor_Equipos.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Linq.Expressions;

namespace GestorEquipos.Tests.Services;

public class AvailabilityAndSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoginOutageKeepsFormAndDoesNotIssueCookie(bool wrapped)
    {
        var sql = SqlError(-2);
        Exception error = wrapped
            ? new Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException("Retries exhausted", sql)
            : sql;
        var http = new DefaultHttpContext();
        var controller = new Gestor_Equipos.Controllers.LoginController(new FailingAuth(error),
            NullLogger<Gestor_Equipos.Controllers.LoginController>.Instance)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };
        var model = new GestorEquipos.Models.Auth.LoginViewModel { Email = "test@example.com", Password = "unused" };

        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await controller.LoginUser(model));

        Assert.Equal("Index", result.ViewName);
        Assert.Same(model, result.Model);
        Assert.Equal(503, http.Response.StatusCode);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        Assert.False(http.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.Contains("tardo demasiado", Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task LoginSchemaErrorIsNotReportedAsConnectionFailure()
    {
        var controller = new Gestor_Equipos.Controllers.LoginController(new FailingAuth(SqlError(207)),
            NullLogger<Gestor_Equipos.Controllers.LoginController>.Instance)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        await Assert.ThrowsAsync<SqlException>(() => controller.LoginUser(new GestorEquipos.Models.Auth.LoginViewModel()));
    }

    private sealed class FailingAuth(Exception exception) : IAuthService
    {
        public Task<AuthenticatedUser?> ValidateUserAsync(string email, string password, CancellationToken cancellationToken = default)
            => Task.FromException<AuthenticatedUser?>(exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqlOutageReturns503WithoutDeletingCookie(bool wrapped)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = ".AspNetCore.Cookies=existing-ticket";
        context.Response.Body = new MemoryStream();
        var error = SqlError(-2);
        var middleware = new DatabaseAvailabilityMiddleware(_ =>
            throw (wrapped ? new InvalidOperationException("Retry exhausted", error) : error),
            NullLogger<DatabaseAvailabilityMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
        context.Response.Body.Position = 0;
        var html = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Servicio temporalmente no disponible", html);
        Assert.DoesNotContain("private SQL details", html);
    }

    [Fact]
    public async Task SqlProgrammingErrorsAreNotMisreportedAsOutages()
    {
        var middleware = new DatabaseAvailabilityMiddleware(_ => throw SqlError(207),
            NullLogger<DatabaseAvailabilityMiddleware>.Instance);
        await Assert.ThrowsAsync<SqlException>(() => middleware.InvokeAsync(new DefaultHttpContext()));
    }

    [Fact]
    public async Task ClientCancellationDoesNotRenderUnavailablePage()
    {
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(true) };
        context.Response.Body = new MemoryStream();
        var middleware = new DatabaseAvailabilityMiddleware(_ => throw new OperationCanceledException(),
            NullLogger<DatabaseAvailabilityMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task SessionValidationOutageBlocksProtectedWorkAndRecoversWithSameTicket()
    {
        var outage = new QueryOutage();
        using var db = new MyDbContext(new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(outage).Options);
        var user = TestHelpers.CreateUser(db, "Recovery");
        var account = new UserSystem { UserId = user.Id, Rol = new Rol { Name = "Administrador" },
            Username = "recovery", PasswordHash = "unused", SecurityStamp = Guid.NewGuid() };
        db.UserSystems.Add(account);
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim("UserId", user.Id.ToString()),
            new Claim(ClaimTypes.Role, "Administrador"),
            new Claim(SessionCookieEvents.SecurityStampClaimType, account.SecurityStamp.ToString("D"))
        }, "Cookies"));
        var auth = new RecordingAuthentication();
        using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
        var protectedWorkReached = false;
        var middleware = new DatabaseAvailabilityMiddleware(async http =>
        {
            var scheme = new AuthenticationScheme("Cookies", null, typeof(CookieAuthenticationHandler));
            var validation = new CookieValidatePrincipalContext(http, scheme, new CookieAuthenticationOptions(),
                new AuthenticationTicket(principal, "Cookies"));
            await new SessionCookieEvents(db).ValidatePrincipal(validation);
            Assert.NotNull(validation.Principal);
            protectedWorkReached = true;
        }, NullLogger<DatabaseAvailabilityMiddleware>.Instance);
        var failed = new DefaultHttpContext { RequestServices = services };
        failed.Response.Body = new MemoryStream();
        outage.Unavailable = true;
        await middleware.InvokeAsync(failed);
        Assert.Equal(503, failed.Response.StatusCode);
        Assert.False(protectedWorkReached);
        Assert.False(auth.SignedOut);
        Assert.False(failed.Response.Headers.ContainsKey("Set-Cookie"));

        outage.Unavailable = false;
        var recovered = new DefaultHttpContext { RequestServices = services };
        await middleware.InvokeAsync(recovered);
        Assert.Equal(200, recovered.Response.StatusCode);
        Assert.True(protectedWorkReached);
        Assert.False(auth.SignedOut);
    }

    [Theory]
    [InlineData("valid", false)]
    [InlineData("inactive", true)]
    [InlineData("role", true)]
    [InlineData("stamp", true)]
    [InlineData("deleted", true)]
    public async Task SessionRevocationStillWorks(string change, bool rejected)
    {
        using var db = TestHelpers.CreateDbContext();
        var user = TestHelpers.CreateUser(db, "Session");
        var role = new Rol { Name = "Administrador" };
        var account = new UserSystem { UserId = user.Id, Rol = role, Username = "session", PasswordHash = "unused", SecurityStamp = Guid.NewGuid() };
        db.UserSystems.Add(account);
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim("UserId", user.Id.ToString()),
            new Claim(ClaimTypes.Role, role.Name),
            new Claim(SessionCookieEvents.SecurityStampClaimType, account.SecurityStamp.ToString("D"))
        }, "Cookies"));
        switch (change)
        {
            case "inactive": user.Activo = false; break;
            case "role": role.Name = "Auditor"; break;
            case "stamp": account.SecurityStamp = Guid.NewGuid(); break;
            case "deleted": db.UserSystems.Remove(account); break;
        }
        await db.SaveChangesAsync();
        var auth = new RecordingAuthentication();
        using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var scheme = new AuthenticationScheme("Cookies", null, typeof(CookieAuthenticationHandler));
        var validation = new CookieValidatePrincipalContext(http, scheme, new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, "Cookies"));
        await new SessionCookieEvents(db).ValidatePrincipal(validation);
        Assert.Equal(rejected, validation.Principal is null);
        Assert.Equal(rejected, auth.SignedOut);
    }

    // SqlClient exposes no public exception constructor. Build a provider exception
    // without depending on a live server or stopping the user's SQL instance.
    private static SqlException SqlError(int number)
    {
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .First(c => c.GetParameters().Length == 9);
        var error = constructor.Invoke(new object?[] { number, (byte)0, (byte)20, "server", "private SQL details", "procedure", 0, 0, null });
        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(collection, new[] { error });
        var factory = typeof(SqlException).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .First(m => m.Name == "CreateException" && m.GetParameters().Length == 2);
        return (SqlException)factory.Invoke(null, new object[] { collection, "17.0" })!;
    }

    private sealed class RecordingAuthentication : IAuthenticationService
    {
        public bool SignedOut { get; private set; }
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        { SignedOut = true; return Task.CompletedTask; }
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw new NotSupportedException();
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();
    }

    private sealed class QueryOutage : IQueryExpressionInterceptor
    {
        public bool Unavailable { get; set; }
        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
        {
            if (Unavailable) throw SqlError(-2);
            return queryExpression;
        }
    }
}
