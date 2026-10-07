using System.Security.Claims;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.PeripheralType;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
    public class PeripheralTypeController : Controller
    {
        private readonly IPeripheralTypeService _peripheralTypeService;

        public PeripheralTypeController(IPeripheralTypeService peripheralTypeService)
        {
            _peripheralTypeService = peripheralTypeService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Error = TempData["Error"];
            var types = await _peripheralTypeService.GetAllAsync();
            return View(types);
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public IActionResult Create()
        {
            return View(new PeripheralTypeCreateViewModel());
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PeripheralTypeCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _peripheralTypeService.CreateAsync(vm);
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
            var type = await _peripheralTypeService.GetByIdAsync(id);
            if (type is null)
            {
                return NotFound();
            }

            return View(new PeripheralTypeEditViewModel { Id = type.Id, Name = type.Name });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PeripheralTypeEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _peripheralTypeService.UpdateAsync(id, vm);
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
                await _peripheralTypeService.DeleteAsync(id, GetCurrentUserSystemId(), adminPassword);
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
