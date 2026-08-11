using System.Security.Claims;
using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.Peripheral;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize]
    public class PeripheralController : Controller
    {
        private readonly IPeripheralService _peripheralService;
        private readonly IPeripheralAssignmentService _peripheralAssignmentService;
        private readonly MyDbContext _dbContext;

        public PeripheralController(IPeripheralService peripheralService, IPeripheralAssignmentService peripheralAssignmentService, MyDbContext dbContext)
        {
            _peripheralService = peripheralService;
            _peripheralAssignmentService = peripheralAssignmentService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(int? peripheralTypeId, string? estado, string? search)
        {
            var stats = await _peripheralService.GetInventoryStatsAsync();
            var all = await _peripheralService.GetInventoryAsync();

            ViewBag.Stats = stats;

            var items = all.AsEnumerable();

            if (peripheralTypeId.HasValue)
            {
                items = items.Where(p => p.PeripheralTypeId == peripheralTypeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                items = items.Where(p => p.Estado == estado);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                items = items.Where(p =>
                    p.Brand.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Model.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.DesktopName.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            ViewBag.PeripheralTypeFiltro = peripheralTypeId;
            ViewBag.EstadoFiltro = estado;
            ViewBag.SearchTerm = search;
            ViewBag.PeripheralTypes = new SelectList(await _dbContext.PeripheralTypes.OrderBy(t => t.Name).ToListAsync(), "Id", "Name", peripheralTypeId);

            return View(items.ToList());
        }

        public async Task<IActionResult> Details(int id)
        {
            var detail = await _peripheralService.GetDetailAsync(id);
            if (detail is null)
            {
                return NotFound();
            }

            ViewBag.Error = TempData["Error"];
            return View(detail);
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Create(int desktopId)
        {
            await PopulateTypesAsync();
            return View(new PeripheralCreateViewModel { DesktopId = desktopId });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PeripheralCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateTypesAsync();
                return View(vm);
            }

            await _peripheralService.AddAsync(vm);
            return RedirectToAction("Details", "Desktop", new { id = vm.DesktopId });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Edit(int id)
        {
            var peripheral = await _peripheralService.GetByIdAsync(id);
            if (peripheral is null)
            {
                return NotFound();
            }

            await PopulateTypesAsync();
            return View(new PeripheralEditViewModel
            {
                Id = peripheral.Id,
                DesktopId = peripheral.DesktopId,
                PeripheralTypeId = peripheral.PeripheralTypeId,
                Brand = peripheral.Brand,
                Model = peripheral.Model,
                Serial = peripheral.Serial
            });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PeripheralEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateTypesAsync();
                return View(vm);
            }

            try
            {
                await _peripheralService.UpdateAsync(id, vm);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateTypesAsync();
                return View(vm);
            }

            return RedirectToAction("Details", "Desktop", new { id = vm.DesktopId });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int desktopId, string adminPassword)
        {
            try
            {
                await _peripheralService.DeleteAsync(id, GetCurrentUserSystemId(), adminPassword);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", "Desktop", new { id = desktopId });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Reassign(int peripheralId)
        {
            await PopulateReassignDropdownAsync(peripheralId);
            return View();
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reassign(int peripheralId, int userId)
        {
            try
            {
                await _peripheralAssignmentService.AssignAsync(peripheralId, userId);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateReassignDropdownAsync(peripheralId, userId);
                return View();
            }

            return RedirectToAction(nameof(Details), new { id = peripheralId });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetireToRaes(int id)
        {
            await _peripheralService.RetireToRaesAsync(id);
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task PopulateTypesAsync()
        {
            ViewBag.PeripheralTypes = new SelectList(await _dbContext.PeripheralTypes.OrderBy(t => t.Name).ToListAsync(), "Id", "Name");
        }

        private async Task PopulateReassignDropdownAsync(int peripheralId, int? selectedUserId = null)
        {
            ViewBag.PeripheralId = peripheralId;
            var users = await _dbContext.Users
                .Where(u => u.Email != AuthBootstrapper.RaesUserEmail && u.Activo)
                .OrderBy(u => u.Name).ThenBy(u => u.LastName)
                .Select(u => new { u.Id, FullName = u.Name + " " + u.LastName })
                .ToListAsync();

            ViewBag.Users = new SelectList(users, "Id", "FullName", selectedUserId);
        }

        private int GetCurrentUserSystemId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(claim!);
        }
    }
}
