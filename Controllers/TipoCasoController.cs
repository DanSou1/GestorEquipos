using System.Security.Claims;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.TipoCaso;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
    public class TipoCasoController : Controller
    {
        private readonly ITipoCasoService _tipoCasoService;

        public TipoCasoController(ITipoCasoService tipoCasoService)
        {
            _tipoCasoService = tipoCasoService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Error = TempData["Error"];
            var tipos = await _tipoCasoService.GetAllAsync();
            return View(tipos);
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public IActionResult Create()
        {
            return View(new TipoCasoCreateViewModel());
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TipoCasoCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _tipoCasoService.CreateAsync(vm);
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
            var tipo = await _tipoCasoService.GetByIdAsync(id);
            if (tipo is null)
            {
                return NotFound();
            }

            return View(new TipoCasoEditViewModel { Id = tipo.Id, Nombre = tipo.Nombre });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TipoCasoEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _tipoCasoService.UpdateAsync(id, vm);
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
                await _tipoCasoService.DeleteAsync(id, GetCurrentUserSystemId(), adminPassword);
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
