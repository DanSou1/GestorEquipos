using System.Security.Claims;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.Location;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
    public class LocationController : Controller
    {
        private readonly ILocationService _locationService;

        public LocationController(ILocationService locationService)
        {
            _locationService = locationService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Areas = await _locationService.GetAllAreasAsync();
            ViewBag.Regionals = await _locationService.GetAllRegionalsAsync();
            ViewBag.Error = TempData["Error"];
            return View();
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public IActionResult CreateArea()
        {
            return View(new LocationFormViewModel());
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateArea(LocationFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _locationService.CreateAreaAsync(vm.Name);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> EditArea(int id)
        {
            var area = await _locationService.GetAreaByIdAsync(id);
            if (area is null)
            {
                return NotFound();
            }

            return View(new LocationFormViewModel { Id = area.Id, Name = area.Name });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditArea(int id, LocationFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _locationService.UpdateAreaAsync(id, vm.Name);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteArea(int id, string adminPassword)
        {
            try
            {
                await _locationService.DeleteAreaAsync(id, GetCurrentUserSystemId(), adminPassword);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public IActionResult CreateRegional()
        {
            return View(new LocationFormViewModel());
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRegional(LocationFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _locationService.CreateRegionalAsync(vm.Name);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> EditRegional(int id)
        {
            var regional = await _locationService.GetRegionalByIdAsync(id);
            if (regional is null)
            {
                return NotFound();
            }

            return View(new LocationFormViewModel { Id = regional.Id, Name = regional.Name });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRegional(int id, LocationFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _locationService.UpdateRegionalAsync(id, vm.Name);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRegional(int id, string adminPassword)
        {
            try
            {
                await _locationService.DeleteRegionalAsync(id, GetCurrentUserSystemId(), adminPassword);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private int GetCurrentUserSystemId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(claim!);
        }
    }
}
