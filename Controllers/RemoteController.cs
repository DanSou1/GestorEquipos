using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.Remote;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize]
    public class RemoteController : Controller
    {
        private readonly IRemoteService _remoteService;
        private readonly MyDbContext _dbContext;

        public RemoteController(IRemoteService remoteService, MyDbContext dbContext)
        {
            _remoteService = remoteService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(string? estado, string? search)
        {
            var stats = await _remoteService.GetInventoryStatsAsync();
            var all = await _remoteService.GetInventoryAsync();

            ViewBag.Stats = stats;

            var items = all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(estado))
            {
                items = items.Where(r => r.Estado == estado);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                items = items.Where(r =>
                    (r.IPAddress ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (r.Username ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (r.AppDescription ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.AssignedDesktops.Any(d => d.Name.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            ViewBag.EstadoFiltro = estado;
            ViewBag.SearchTerm = search;
            return View(items.ToList());
        }

        public async Task<IActionResult> Details(int id)
        {
            var detail = await _remoteService.GetDetailAsync(id);
            if (detail is null)
            {
                return NotFound();
            }

            ViewBag.Error = TempData["Error"];
            return View(detail);
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Edit(int id)
        {
            var remote = await _remoteService.GetByIdAsync(id);
            if (remote is null)
            {
                return NotFound();
            }

            ViewBag.RemoteId = id;
            return View(new RemoteEditViewModel
            {
                ConnectionType = remote.ConnectionType,
                IPAddress = remote.IPAddress,
                Port = remote.Port,
                Username = remote.Username,
                Password = remote.Password,
                AppDescription = remote.AppDescription
            });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RemoteEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.RemoteId = id;
                return View(vm);
            }

            try
            {
                await _remoteService.UpdateAsync(id, vm);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.RemoteId = id;
                return View(vm);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Release(int id)
        {
            try
            {
                await _remoteService.ReleaseAsync(id);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Assign(int remoteId)
        {
            await PopulateAssignDropdownAsync(remoteId);
            return View();
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int remoteId, int desktopId)
        {
            try
            {
                await _remoteService.AssignToDesktopAsync(remoteId, desktopId);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateAssignDropdownAsync(remoteId, desktopId);
                return View();
            }

            return RedirectToAction(nameof(Details), new { id = remoteId });
        }

        private async Task PopulateAssignDropdownAsync(int remoteId, int? selectedDesktopId = null)
        {
            ViewBag.RemoteId = remoteId;
            var desktops = await _dbContext.Desktops
                .Where(d => d.RemoteId == null && d.Estado)
                .OrderBy(d => d.NameDesktop)
                .ToListAsync();
            ViewBag.Desktops = new SelectList(desktops, "Id", "NameDesktop", selectedDesktopId);
        }
    }
}
