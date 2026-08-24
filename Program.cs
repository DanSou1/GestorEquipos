using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}
builder.Services.AddScoped<IAuthService, AuthService>();
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
        options.LoginPath = "/Login/Index";
        options.AccessDeniedPath = "/Login/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
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

var app = builder.Build();

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

app.UseAuthentication();
app.UseAuthorization();

await AuthBootstrapper.EnsureAdminAsync(app.Services, app.Configuration);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
