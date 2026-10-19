using System.Security.Claims;
using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Desktop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AllStaffRoles)]
    public class DesktopController : Controller
    {
        private readonly IDesktopService _desktopService;
        private readonly IDesktopPdfService _desktopPdfService;
        private readonly IAsignationService _asignationService;
        private readonly MyDbContext _dbContext;

        public DesktopController(IDesktopService desktopService, IDesktopPdfService desktopPdfService, IAsignationService asignationService, MyDbContext dbContext)
        {
            _desktopService = desktopService;
            _desktopPdfService = desktopPdfService;
            _asignationService = asignationService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(bool? estado, string? search, string? brand, string? model)
        {
            var allDesktops = await _desktopService.GetAllAsync(HttpContext.RequestAborted);

            ViewBag.TotalCount = allDesktops.Count;
            ViewBag.ActivosCount = allDesktops.Count(d => d.Status == "Activo");
            ViewBag.InactivosCount = allDesktops.Count(d => d.Status != "Activo");

            var desktops = allDesktops.AsEnumerable();

            if (estado.HasValue)
            {
                var estadoText = estado.Value ? "Activo" : "Inactivo";
                desktops = desktops.Where(d => d.Status == estadoText);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                desktops = desktops.Where(d =>
                    d.NameDesktop.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    d.SerialNumber.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                desktops = desktops.Where(d => d.Brand == brand);
            }

            if (!string.IsNullOrWhiteSpace(model))
            {
                desktops = desktops.Where(d => d.Model == model);
            }

            var desktopTypes = await _dbContext.DesktopTypes
                .OrderBy(t => t.Brand)
                .ThenBy(t => t.Model)
                .ToListAsync(HttpContext.RequestAborted);

            ViewBag.EstadoFiltro = estado;
            ViewBag.SearchTerm = search;
            ViewBag.BrandFiltro = brand;
            ViewBag.ModelFiltro = model;
            ViewBag.BrandOptions = desktopTypes.Select(t => t.Brand).Distinct().OrderBy(b => b).ToList();
            ViewBag.ModelOptions = desktopTypes
                .Where(t => string.IsNullOrWhiteSpace(brand) || t.Brand == brand)
                .Select(t => t.Model)
                .Distinct()
                .OrderBy(m => m)
                .ToList();
            return View(desktops.ToList());
        }

        public async Task<IActionResult> Details(int id)
        {
            var detail = await _desktopService.GetDetailAsync(id, HttpContext.RequestAborted);
            if (detail is null)
            {
                return NotFound();
            }

            ViewBag.Error = TempData["Error"];
            return View(detail);
        }

        public async Task<IActionResult> DownloadPdf(int id)
        {
            var detail = await _desktopService.GetDetailAsync(id, HttpContext.RequestAborted);
            if (detail is null)
            {
                return NotFound();
            }

            var pdfBytes = _desktopPdfService.GenerateHojaDeVidaPdf(detail);
            var fileName = $"HojaDeVida-{detail.SerialNumber}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View();
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DesktopCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(vm);
            }

            int id;
            try
            {
                id = await _desktopService.CreateAsync(vm);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateDropdownsAsync();
                return View(vm);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Edit(int id)
        {
            var desktop = await _dbContext.Desktops
                .Include(d => d.Remote)
                .SingleOrDefaultAsync(d => d.Id == id);
            if (desktop is null)
            {
                return NotFound();
            }

            var vm = new DesktopEditViewModel
            {
                NameDesktop = desktop.NameDesktop,
                SerialNumber = desktop.SerialNumber,
                Processor = desktop.Processor,
                Disk = desktop.Disk,
                DesktopTypeId = desktop.DesktopTypeId,
                OSVersionId = desktop.OSVersionId,
                RamId = desktop.RamId,
                RamType = desktop.RamType,
                RemoteSelection = desktop.RemoteId.HasValue ? "keep" : ""
            };

            await PopulateDropdownsAsync();
            ViewBag.DesktopId = id;
            ViewBag.CurrentRemoteId = desktop.RemoteId;
            ViewBag.CurrentRemoteSummary = desktop.Remote is null ? null : BuildRemoteLabel(desktop.Remote);
            return View(vm);
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DesktopEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                ViewBag.DesktopId = id;
                var currentRemote = await _dbContext.Desktops
                    .Where(d => d.Id == id)
                    .Select(d => d.Remote)
                    .SingleOrDefaultAsync();
                ViewBag.CurrentRemoteId = currentRemote?.Id;
                ViewBag.CurrentRemoteSummary = currentRemote is null ? null : BuildRemoteLabel(currentRemote);
                return View(vm);
            }

            try
            {
                await _desktopService.UpdateSpecsAsync(id, vm, GetCurrentUserSystemId());
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateDropdownsAsync();
                ViewBag.DesktopId = id;
                var currentRemote = await _dbContext.Desktops
                    .Where(d => d.Id == id)
                    .Select(d => d.Remote)
                    .SingleOrDefaultAsync();
                ViewBag.CurrentRemoteId = currentRemote?.Id;
                ViewBag.CurrentRemoteSummary = currentRemote is null ? null : BuildRemoteLabel(currentRemote);
                return View(vm);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _desktopService.DeactivateAsync(id);
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _desktopService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        public async Task<IActionResult> Assign(int desktopId)
        {
            var vm = await BuildAssignViewModelAsync(desktopId);
            if (vm is null)
            {
                return NotFound();
            }
            return View(vm);
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int desktopId, int userId)
        {
            try
            {
                await _asignationService.AssignAsync(desktopId, userId);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var vm = await BuildAssignViewModelAsync(desktopId, userId);
                if (vm is null)
                {
                    return NotFound();
                }
                return View(vm);
            }

            return RedirectToAction(nameof(Details), new { id = desktopId });
        }

        // UX-06: return a single equipo to Disponible without deactivating its holder (which
        // would release every other equipo/periférico that person has too).
        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int id)
        {
            try
            {
                await _asignationService.ReturnAsync(id);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private int GetCurrentUserSystemId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(claim!);
        }

        private async Task<DesktopAssignViewModel?> BuildAssignViewModelAsync(int desktopId, int? selectedUserId = null)
        {
            var desktop = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == desktopId);
            if (desktop is null)
            {
                return null;
            }

            var users = await _dbContext.Users
                .Where(u => u.Email != AuthBootstrapper.RaesUserEmail && u.Activo)
                .OrderBy(u => u.Name)
                .ToListAsync();

            var history = await _asignationService.GetHistoryAsync(desktopId);
            var current = history.FirstOrDefault();
            var hasActiveHolder = current is not null && current.User.Activo;

            return new DesktopAssignViewModel
            {
                DesktopId = desktopId,
                DesktopLabel = $"{desktop.NameDesktop} ({desktop.SerialNumber})",
                Users = new SelectList(
                    users.Select(u => new { u.Id, FullName = $"{u.Name} {u.LastName}" }),
                    "Id",
                    "FullName",
                    selectedUserId),
                SelectedUserId = selectedUserId,
                HasActiveHolder = hasActiveHolder,
                CurrentHolderName = hasActiveHolder ? $"{current!.User.Name} {current.User.LastName}" : null,
                CurrentHolderUserId = hasActiveHolder ? current!.UserId : null
            };
        }

        private async Task PopulateDropdownsAsync()
        {
            var desktopTypes = await _dbContext.DesktopTypes
                .OrderBy(t => t.Brand)
                .ThenBy(t => t.Model)
                .ToListAsync();
            ViewBag.DesktopTypeCatalog = desktopTypes;
            ViewBag.DesktopTypeBrands = desktopTypes
                .Select(t => t.Brand)
                .Distinct()
                .OrderBy(b => b)
                .ToList();

            ViewBag.OSVersions = new SelectList(await _dbContext.OSVersions.ToListAsync(), "Id", "TypeSO");

            var rams = await _dbContext.Rams.ToListAsync();
            ViewBag.Rams = new SelectList(
                rams.OrderBy(r => ParseRamCapacity(r.Especification)),
                "Id",
                "Especification");
        }

        // Especification is a free-text catalog value seeded directly via SQL (no admin UI),
        // so it isn't guaranteed to be an exact "<number>GB" string — parse tolerantly instead
        // of throwing and taking down every Create/Edit screen on a malformed row.
        private static int ParseRamCapacity(string especification)
        {
            var digits = new string((especification ?? string.Empty).TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var value) ? value : int.MaxValue;
        }

        private static string BuildRemoteLabel(Remote remote)
        {
            return remote.ConnectionType == RemoteConnectionType.Aplicativo
                ? $"Aplicativo — {remote.AppDescription}"
                : $"RDP — {remote.IPAddress}:{remote.Port} ({remote.Username})";
        }
    }
}
