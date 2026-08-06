using System.Security.Claims;
using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.Desktop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize]
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

        public async Task<IActionResult> Index(bool? estado, string? search)
        {
            var allDesktops = await _desktopService.GetAllAsync();

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

            ViewBag.EstadoFiltro = estado;
            ViewBag.SearchTerm = search;
            return View(desktops.ToList());
        }

        public async Task<IActionResult> Details(int id)
        {
            var detail = await _desktopService.GetDetailAsync(id);
            if (detail is null)
            {
                return NotFound();
            }

            return View(detail);
        }

        public async Task<IActionResult> DownloadPdf(int id)
        {
            var detail = await _desktopService.GetDetailAsync(id);
            if (detail is null)
            {
                return NotFound();
            }

            var pdfBytes = _desktopPdfService.GenerateHojaDeVidaPdf(detail);
            var fileName = $"HojaDeVida-{detail.SerialNumber}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View();
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DesktopCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(vm);
            }

            var id = await _desktopService.CreateAsync(vm);
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Edit(int id)
        {
            var desktop = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == id);
            if (desktop is null)
            {
                return NotFound();
            }

            var vm = new DesktopEditViewModel
            {
                NameDesktop = desktop.NameDesktop,
                SerialNumber = desktop.SerialNumber,
                Brand = desktop.Brand,
                Model = desktop.Model,
                Processor = desktop.Processor,
                Disk = desktop.Disk,
                OSVersionId = desktop.OSVersionId,
                RamId = desktop.RamId,
                RemoteId = desktop.RemoteId
            };

            await PopulateDropdownsAsync();
            ViewBag.DesktopId = id;
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
                return View(vm);
            }

            await _desktopService.UpdateSpecsAsync(id, vm, GetCurrentUserSystemId());
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _desktopService.DeactivateAsync(id);
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Assign(int desktopId)
        {
            ViewBag.DesktopId = desktopId;
            var users = await _dbContext.Users
                .Where(u => u.Email != AuthBootstrapper.RaesUserEmail)
                .OrderBy(u => u.Name)
                .ToListAsync();
            ViewBag.Users = new SelectList(
                users.Select(u => new { u.Id, FullName = $"{u.Name} {u.LastName}" }),
                "Id",
                "FullName");
            return View();
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int desktopId, int userId)
        {
            await _asignationService.AssignAsync(desktopId, userId);
            return RedirectToAction(nameof(Details), new { id = desktopId });
        }

        private int GetCurrentUserSystemId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(claim!);
        }

        private async Task PopulateDropdownsAsync()
        {
            ViewBag.OSVersions = new SelectList(await _dbContext.OSVersions.ToListAsync(), "Id", "TypeSO");
            ViewBag.Rams = new SelectList(await _dbContext.Rams.ToListAsync(), "Id", "Especification");
            ViewBag.Remotes = new SelectList(await _dbContext.Remotes.ToListAsync(), "Id", "IPAddress");
        }
    }
}
