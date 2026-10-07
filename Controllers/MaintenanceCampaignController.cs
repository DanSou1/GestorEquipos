using System.Security.Claims;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    [Authorize]
    public class MaintenanceCampaignController : Controller
    {
        private const long MaxSignedPdfBytes = 10 * 1024 * 1024;

        private readonly IMaintenanceCampaignService _campaignService;

        public MaintenanceCampaignController(IMaintenanceCampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            await _campaignService.SyncOpenPeriodsAsync();
            var vm = await _campaignService.GetIndexAsync(onlyCompleted: User.IsInRole(AuthBootstrapper.AuditorRoleName));
            return View(vm);
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        public async Task<IActionResult> Complete(int id)
        {
            var item = await _campaignService.GetByIdAsync(id);
            if (item is null)
            {
                return NotFound();
            }

            if (item.Status != MaintenanceCampaignStatus.Pendiente)
            {
                TempData["Error"] = "Este mantenimiento ya fue completado.";
                return RedirectToAction(nameof(Index));
            }

            return View(new MaintenanceCampaignCompleteViewModel
            {
                CampaignItemId = item.Id,
                DesktopName = item.Desktop.NameDesktop,
                SerialNumber = item.Desktop.SerialNumber,
                Date = DateOnly.FromDateTime(DateTime.Now)
            });
        }

        [HttpPost]
        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(MaintenanceCampaignCompleteViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var maintenanceVm = new MaintenanceCreateViewModel
            {
                Date = vm.Date,
                Description = vm.Description,
                TechnicianName = vm.TechnicianName,
                ChecklistVerificacionSistemaMecanico = vm.ChecklistVerificacionSistemaMecanico,
                ChecklistVerificacionSistemaElectrico = vm.ChecklistVerificacionSistemaElectrico,
                ChecklistVerificacionSistemaSeguridad = vm.ChecklistVerificacionSistemaSeguridad,
                ChecklistSopladoInterno = vm.ChecklistSopladoInterno,
                ChecklistLavadoInterno = vm.ChecklistLavadoInterno,
                ChecklistLubricacion = vm.ChecklistLubricacion,
                ChecklistAjusteTornilleria = vm.ChecklistAjusteTornilleria,
                ChecklistCambioAccesorios = vm.ChecklistCambioAccesorios
            };

            try
            {
                await _campaignService.CompleteAndSendAsync(vm.CampaignItemId, maintenanceVm);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }

            TempData["Success"] = "Mantenimiento completado. Se envió el acta por correo al usuario asignado.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        public async Task<IActionResult> Upload(int id)
        {
            var item = await _campaignService.GetByIdAsync(id);
            if (item is null)
            {
                return NotFound();
            }

            if (item.Status != MaintenanceCampaignStatus.CorreoEnviado)
            {
                TempData["Error"] = "Este mantenimiento no está esperando la subida del acta firmada.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.DesktopName = item.Desktop.NameDesktop;
            ViewBag.CampaignItemId = item.Id;
            return View();
        }

        [HttpPost]
        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int id, IFormFile? signedPdf)
        {
            if (signedPdf is null || signedPdf.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Selecciona el PDF firmado para subir.");
            }
            else if (!string.Equals(Path.GetExtension(signedPdf.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(signedPdf.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "El archivo debe ser un PDF.");
            }
            else if (signedPdf.Length > MaxSignedPdfBytes)
            {
                ModelState.AddModelError(string.Empty, "El PDF no puede superar los 10 MB.");
            }

            if (!ModelState.IsValid)
            {
                var item = await _campaignService.GetByIdAsync(id);
                if (item is null)
                {
                    return NotFound();
                }

                ViewBag.DesktopName = item.Desktop.NameDesktop;
                ViewBag.CampaignItemId = item.Id;
                return View();
            }

            try
            {
                await using var stream = signedPdf!.OpenReadStream();
                await _campaignService.UploadSignedPdfAsync(id, stream, signedPdf.FileName, GetCurrentUserSystemId());
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var item = await _campaignService.GetByIdAsync(id);
                ViewBag.DesktopName = item?.Desktop.NameDesktop;
                ViewBag.CampaignItemId = id;
                return View();
            }

            TempData["Success"] = "Acta firmada subida. El mantenimiento quedó marcado como realizado.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> DownloadSignedPdf(int id)
        {
            var result = await _campaignService.GetSignedPdfAsync(id);
            if (result is null)
            {
                return NotFound();
            }

            return File(result.Value.Content, "application/pdf", result.Value.FileName);
        }

        public async Task<IActionResult> PreviewSignedPdf(int id)
        {
            var result = await _campaignService.GetSignedPdfAsync(id);
            if (result is null)
            {
                return NotFound();
            }

            return File(result.Value.Content, "application/pdf");
        }

        private int GetCurrentUserSystemId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(claim!);
        }
    }
}
