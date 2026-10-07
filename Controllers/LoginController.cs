using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Diagnostics;
using GestorEquipos.Models.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Gestor_Equipos.Controllers
{
    [AllowAnonymous]
    public class LoginController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILogger<LoginController> _logger;

        public LoginController(IAuthService authService, ILogger<LoginController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> LoginUser(LoginViewModel model)
        {
            _logger.LogInformation("[Login] POST recibido para {Email} desde {Ip}.",
                model.Email, HttpContext.Connection.RemoteIpAddress);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("[Login] ModelState invalido para {Email}.", model.Email);
                return View("Index", model);
            }

            AuthenticatedUser? authenticatedUser;
            try
            {
                // Punto de depuracion sugerido (F9 en VS): entra aqui para seguir paso a paso
                // la llamada que abre la conexion a la BD dentro de AuthService.ValidateUserAsync.
                authenticatedUser = await _authService.ValidateUserAsync(model.Email, model.Password, HttpContext.RequestAborted);
            }
            catch (Exception exception) when (DatabaseAvailability.IsUnavailable(exception))
            {
                _logger.LogWarning("[Login] Base de datos no disponible. RequestId={RequestId}", HttpContext.TraceIdentifier);
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                Response.Headers.CacheControl = "no-store";
                Response.Headers.RetryAfter = "10";
                ModelState.AddModelError(string.Empty, "La base de datos no esta disponible o tardo demasiado en responder. Intenta nuevamente en unos momentos.");
                return View("Index", model);
            }

            if (authenticatedUser is null)
            {
                _logger.LogWarning("[Login] Credenciales invalidas para {Email}.", model.Email);
                ModelState.AddModelError(string.Empty, "Correo o contrasena invalidos.");
                return View("Index", model);
            }

            _logger.LogInformation("[Login] Autenticacion exitosa para {Email} (UserSystemId={UserSystemId}, Rol={Rol}).",
                model.Email, authenticatedUser.UserSystemId, authenticatedUser.RoleName);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, authenticatedUser.UserSystemId.ToString()),
                new(ClaimTypes.Email, authenticatedUser.Email),
                new(ClaimTypes.Name, authenticatedUser.FullName),
                new(ClaimTypes.Role, authenticatedUser.RoleName),
                new("UserId", authenticatedUser.UserId.ToString()),
                new(SessionCookieEvents.SecurityStampClaimType, authenticatedUser.SecurityStamp.ToString("D"))
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);

            _logger.LogInformation("[Login] Cookie de sesion emitida para {Email}. Redirigiendo...", model.Email);

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
