using System.Security.Claims;
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
            var all = await _remoteService.GetInventoryAsync();

            // REN-02: GetInventoryStatsAsync() internally re-materializes the whole inventory —
            // compute the same totals from the list already loaded above instead of fetching twice.
            ViewBag.Stats = new RemoteInventoryStatsViewModel
            {
                Total = all.Count,
                Asignados = all.Count(i => i.Estado == "Asignado"),
                Disponibles = all.Count(i => i.Estado == "Disponible")
            };

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
            ViewBag.Error = TempData["Error"];
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

            var linkedDesktops = await _dbContext.Desktops
                .Where(d => d.RemoteId == id)
                .OrderBy(d => d.NameDesktop)
                .ToListAsync();
            var preselectedDesktopId = linkedDesktops.Count == 1 ? linkedDesktops[0].Id : (int?)null;

            ViewBag.RemoteId = id;
            ViewBag.LinkedDesktops = linkedDesktops;
            await PopulateEditDesktopDropdownAsync(id, preselectedDesktopId);

            return View(new RemoteEditViewModel
            {
                ConnectionType = remote.ConnectionType,
                IPAddress = remote.IPAddress,
                Port = remote.Port,
                Username = remote.Username,
                Password = remote.Password,
                AppDescription = remote.AppDescription,
                DesktopId = preselectedDesktopId
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
                ViewBag.LinkedDesktops = await _dbContext.Desktops.Where(d => d.RemoteId == id).OrderBy(d => d.NameDesktop).ToListAsync();
                await PopulateEditDesktopDropdownAsync(id, vm.DesktopId);
                return View(vm);
            }

            try
            {
                await _remoteService.UpdateAsync(id, vm, GetCurrentUserSystemId());
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.RemoteId = id;
                ViewBag.LinkedDesktops = await _dbContext.Desktops.Where(d => d.RemoteId == id).OrderBy(d => d.NameDesktop).ToListAsync();
                await PopulateEditDesktopDropdownAsync(id, vm.DesktopId);
                return View(vm);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Release(int id)
        {
            try
            {
                await _remoteService.ReleaseAsync(id, GetCurrentUserSystemId());
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        public async Task<IActionResult> Assign(int remoteId)
        {
            await PopulateAssignDropdownAsync(remoteId);
            return View();
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int remoteId, int desktopId)
        {
            try
            {
                await _remoteService.AssignToDesktopAsync(remoteId, desktopId, GetCurrentUserSystemId());
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateAssignDropdownAsync(remoteId, desktopId);
                return View();
            }

            return RedirectToAction(nameof(Details), new { id = remoteId });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string adminPassword)
        {
            try
            {
                await _remoteService.DeleteAsync(id, GetCurrentUserSystemId(), adminPassword);
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

        private async Task PopulateAssignDropdownAsync(int remoteId, int? selectedDesktopId = null)
        {
            ViewBag.RemoteId = remoteId;
            var desktops = await _dbContext.Desktops
                .Where(d => d.RemoteId == null && d.Estado)
                .OrderBy(d => d.NameDesktop)
                .ToListAsync();
            ViewBag.Desktops = new SelectList(desktops, "Id", "NameDesktop", selectedDesktopId);
        }

        private async Task PopulateEditDesktopDropdownAsync(int remoteId, int? selectedDesktopId)
        {
            var desktops = await _dbContext.Desktops
                .Where(d => d.RemoteId == remoteId || (d.RemoteId == null && d.Estado))
                .OrderBy(d => d.NameDesktop)
                .ToListAsync();
            ViewBag.Desktops = new SelectList(desktops, "Id", "NameDesktop", selectedDesktopId);
        }
    }
}
