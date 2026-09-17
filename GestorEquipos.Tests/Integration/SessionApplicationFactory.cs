using System.Net;
using System.Data.Common;
using System.Text.RegularExpressions;
using Gestor_Equipos.Controllers;
using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.UserAdmin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace GestorEquipos.Tests.Integration
{
    public sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GESTOREQUIPOS_TEST_SQLSERVER")))
            {
                Skip = "Set GESTOREQUIPOS_TEST_SQLSERVER to an isolated SQL Server test instance.";
            }
        }
    }

    public sealed class SessionApplicationFactory : WebApplicationFactory<HomeController>
    {
        private readonly string _databaseName = "GestorEquipos_SEG01_" + Guid.NewGuid().ToString("N");
        private bool _databaseUsed;
        public bool FailSessionQueries { get; set; }
        public SessionTestClock Clock { get; } = new();

        public string Password { get; } = "Test!" + Guid.NewGuid().ToString("N");

        public string ConnectionString
        {
            get
            {
                var configured = Environment.GetEnvironmentVariable("GESTOREQUIPOS_TEST_SQLSERVER")
                    ?? throw new InvalidOperationException("An explicit test SQL Server is required.");
                return new SqlConnectionStringBuilder(configured) { InitialCatalog = _databaseName }.ConnectionString;
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("BootstrapAdmin:Email", "");
            builder.UseSetting("BootstrapAdmin:Password", "");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MyDbContext>>();
                services.AddDbContext<MyDbContext>(options => options.UseSqlServer(ConnectionString).AddInterceptors(new SessionQueryInterceptor(this)));
                services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options => options.TimeProvider = Clock);
                _databaseUsed = true;
            });
        }

        public HttpClient CreateSession()
        {
            return CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        }

        public async Task<UserSystem> CreateAccountAsync(string roleName = AuthBootstrapper.AdministradorRoleName)
        {
            using var scope = Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
            var email = Guid.NewGuid().ToString("N") + "@session.invalid";
            var personId = await service.CreateUserAsync(new UserCreateViewModel
            {
                Name = "Session", LastName = "Test", Email = email
            });
            var roleId = await database.Rols.Where(role => role.Name == roleName).Select(role => role.Id).SingleAsync();
            await service.CreateLoginAsync(personId, email, Password, roleId);
            return await database.UserSystems.Include(account => account.User).Include(account => account.Rol)
                .SingleAsync(account => account.UserId == personId);
        }

        public async Task WithDatabaseAsync(Func<MyDbContext, IServiceProvider, Task> action)
        {
            using var scope = Services.CreateScope();
            await action(scope.ServiceProvider.GetRequiredService<MyDbContext>(), scope.ServiceProvider);
        }

        public async Task<T> ReadAsync<T>(Func<MyDbContext, Task<T>> query)
        {
            using var scope = Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<MyDbContext>());
        }

        public async Task<string> LoginAsync(HttpClient client, UserSystem account, bool rememberMe = false, string? password = null)
        {
            using var response = await PostFormAsync(client, "/Login/Index", "/Login/LoginUser", new()
            {
                ["Email"] = account.User.Email,
                ["Password"] = password ?? Password,
                ["RememberMe"] = rememberMe.ToString()
            });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal)).Split(';')[0];
        }

        public HttpClient CreateCookieReplay(string cookie)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, HandleCookies = false, BaseAddress = new Uri("https://localhost")
            });
            client.DefaultRequestHeaders.Add("Cookie", cookie);
            return client;
        }

        public static async Task<string> TokenAsync(HttpClient client, string path)
        {
            using var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(token.Success, "Missing antiforgery token at " + path);
            return WebUtility.HtmlDecode(token.Groups[1].Value);
        }

        public static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string formPath, string postPath, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = await TokenAsync(client, formPath);
            return await client.PostAsync(postPath, new FormUrlEncodedContent(fields));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _databaseUsed)
            {
                var connection = new SqlConnectionStringBuilder(ConnectionString);
                if (connection.InitialCatalog != _databaseName || !_databaseName.StartsWith("GestorEquipos_SEG01_", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Refusing to remove a database not owned by this fixture.");
                }
                using var database = new MyDbContext(new DbContextOptionsBuilder<MyDbContext>().UseSqlServer(connection.ConnectionString).Options);
                database.Database.EnsureDeleted();
            }
        }

        public sealed class SessionTestClock : TimeProvider
        {
            private DateTimeOffset _now = DateTimeOffset.UtcNow;
            public override DateTimeOffset GetUtcNow() => _now;
            public void Advance(TimeSpan duration) => _now += duration;
        }

        private sealed class SessionQueryInterceptor(SessionApplicationFactory factory) : DbCommandInterceptor
        {
            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                if (factory.FailSessionQueries && command.CommandText.Contains("[UserSystem]", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Simulated session database failure.");
                }
                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }
        }
    }
}
