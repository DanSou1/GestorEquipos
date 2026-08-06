using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.License;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
    public class LicenseController : Controller
    {
        private readonly ILicenseService _licenseService;

        public LicenseController(ILicenseService licenseService)
        {
            _licenseService = licenseService;
        }

        public IActionResult Create(int desktopId)
        {
            return View(new LicenseCreateViewModel { DesktopId = desktopId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LicenseCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            await _licenseService.AddAsync(vm);
            return RedirectToAction("Details", "Desktop", new { id = vm.DesktopId });
        }
    }
}
