using System.Security.Claims;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.MaintenanceType;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
    public class MaintenanceTypeController : Controller
    {
        private readonly IMaintenanceTypeService _maintenanceTypeService;

        public MaintenanceTypeController(IMaintenanceTypeService maintenanceTypeService)
        {
            _maintenanceTypeService = maintenanceTypeService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Error = TempData["Error"];
            var types = await _maintenanceTypeService.GetAllAsync();
            return View(types);
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public IActionResult Create()
        {
            return View(new MaintenanceTypeCreateViewModel());
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MaintenanceTypeCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _maintenanceTypeService.CreateAsync(vm);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Edit(int id)
        {
            var type = await _maintenanceTypeService.GetByIdAsync(id);
            if (type is null)
            {
                return NotFound();
            }

            return View(new MaintenanceTypeEditViewModel { Id = type.Id, Type = type.Type });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MaintenanceTypeEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _maintenanceTypeService.UpdateAsync(id, vm);
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
        public async Task<IActionResult> Delete(int id, string adminPassword)
        {
            try
            {
                await _maintenanceTypeService.DeleteAsync(id, GetCurrentUserSystemId(), adminPassword);
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
