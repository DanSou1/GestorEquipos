using System.Net;
using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<SessionCookieEvents>();
builder.Services.AddScoped<IPasswordHasher<UserSystem>, PasswordHasher<UserSystem>>();
builder.Services.AddScoped<IDesktopService, DesktopService>();
builder.Services.AddScoped<IDesktopPdfService, DesktopPdfService>();
builder.Services.AddScoped<IAsignationService, AsignationService>();
builder.Services.AddScoped<IPeripheralService, PeripheralService>();
builder.Services.AddScoped<IPeripheralTypeService, PeripheralTypeService>();
builder.Services.AddScoped<IPeripheralAssignmentService, PeripheralAssignmentService>();
builder.Services.AddScoped<IPeripheralMaintenanceService, PeripheralMaintenanceService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IMaintenanceTypeService, MaintenanceTypeService>();
builder.Services.AddScoped<IMaintenancePdfService, MaintenancePdfService>();
builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IRemoteService, RemoteService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.EventsType = typeof(SessionCookieEvents);
        options.LoginPath = "/Login/Index";
        options.AccessDeniedPath = "/Login/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // SameAsRequest only in Development (plain http://localhost without TLS). Everywhere
        // else the cookie must never leave the browser over an unencrypted connection, even on
        // a private/internal network — see SEG-07 in REVISION_PROYECTO.md.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// SEG-08: the login form has no other brute-force protection (no account lockout, no CAPTCHA).
// Partition by client IP so one attacker can't exhaust the budget for everyone else, and reject
// immediately (QueueLimit 0) rather than queuing — a queued login attempt is still an attempt.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsync(
            "Demasiados intentos. Intenta de nuevo en un minuto.", cancellationToken);
    };
    // The integration test suite (Testing environment, see SessionApplicationFactory) logs in
    // dozens of times per run from the same loopback address — a real limit there would fail
    // tests, not attackers, so it's effectively disabled rather than the production value.
    var permitLimit = builder.Environment.EnvironmentName == "Testing" ? int.MaxValue : 10;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = permitLimit,
            QueueLimit = 0
        }));
});

builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions
            .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)
            .CommandTimeout(60)));

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// Only relevant if this deployment sits behind a reverse proxy that terminates TLS
// (e.g. IIS/nginx on the company server). Left at the framework default (trust only
// loopback) unless the proxy's address is explicitly configured — never trust
// X-Forwarded-* from an unknown source, since that would let anyone reaching the
// container directly spoof "https" and bypass the Secure-cookie/HTTPS-redirect checks below.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(proxy, out var ip))
        {
            options.KnownProxies.Add(ip);
        }
    }
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseResponseCompression();

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // /lib (vendored, unversioned third-party assets) and /css, /js (carry a content-hash
        // query string via asp-append-version) are safe to cache for a year: a real content
        // change always yields a new URL. Everything else (favicon, /Images) is not
        // version-stamped, so it gets a short cache instead of risking a year-long stale asset.
        var path = ctx.Context.Request.Path;
        var isLongLived = path.StartsWithSegments("/lib") || path.StartsWithSegments("/css") || path.StartsWithSegments("/js");
        ctx.Context.Response.Headers.CacheControl = isLongLived
            ? "public,max-age=31536000,immutable"
            : "public,max-age=86400";
    }
});

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

await AuthBootstrapper.EnsureAdminAsync(app.Services, app.Configuration);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
