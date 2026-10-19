using System.Security.Claims;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    [Authorize]
    public class ChangePasswordController : Controller
    {
        private readonly IUserAdminService _userAdminService;

        public ChangePasswordController(IUserAdminService userAdminService)
        {
            _userAdminService = userAdminService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ChangePasswordViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var userSystemId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var roleName = User.FindFirstValue(ClaimTypes.Role);

            Guid newStamp;
            try
            {
                newStamp = await _userAdminService.ChangeOwnPasswordAsync(userSystemId, vm.NewPassword);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }

            // MustChangePasswordFilter y SessionCookieEvents comprueban la BD en cada request —
            // hay que reemitir la cookie con el SecurityStamp nuevo, si no la sesión actual queda
            // rechazada en el siguiente request por desincronización de stamp.
            var claims = User.Claims
                .Where(c => c.Type != SessionCookieEvents.SecurityStampClaimType)
                .Append(new Claim(SessionCookieEvents.SecurityStampClaimType, newStamp.ToString("D")))
                .ToList();

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            TempData["Success"] = "Contraseña actualizada correctamente.";

            if (roleName == Gestor_Equipos.Services.Auth.AuthBootstrapper.ColaboradorRoleName)
            {
                return RedirectToAction("Index", "CasoTI");
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
