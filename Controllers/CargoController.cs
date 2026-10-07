using System.Security.Claims;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.Cargo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
    public class CargoController : Controller
    {
        private readonly ICargoService _cargoService;

        public CargoController(ICargoService cargoService)
        {
            _cargoService = cargoService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Error = TempData["Error"];
            var cargos = await _cargoService.GetAllAsync();
            return View(cargos);
        }

        public async Task<IActionResult> History(int id)
        {
            var history = await _cargoService.GetHistoryAsync(id);
            if (history is null)
            {
                return NotFound();
            }

            return View(history);
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public IActionResult Create()
        {
            return View(new CargoCreateViewModel());
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CargoCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _cargoService.CreateAsync(vm.Name);
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
            var cargo = await _cargoService.GetByIdAsync(id);
            if (cargo is null)
            {
                return NotFound();
            }

            return View(new CargoEditViewModel { Id = cargo.Id, Name = cargo.Name });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CargoEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _cargoService.UpdateAsync(id, vm.Name);
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
                await _cargoService.DeleteAsync(id, GetCurrentUserSystemId(), adminPassword);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // Usado por el botón "+" en Nuevo/Editar usuario para agregar un cargo sin salir del
        // formulario. Solo Administrador (ver AuthBootstrapper.AdminOrTecnicoRoles en la clase:
        // el técnico puede ver/elegir cargos existentes, pero no crear uno nuevo).
        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickCreate(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("El nombre del cargo es obligatorio.");
            }

            try
            {
                var id = await _cargoService.CreateAsync(name);
                var cargo = await _cargoService.GetByIdAsync(id);
                return Json(new { id = cargo!.Id, name = cargo.Name });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private int GetCurrentUserSystemId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(claim!);
        }
    }
}
