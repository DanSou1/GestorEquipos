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
        private readonly IMaintenanceCampaignService _campaignService;

        public MaintenanceCampaignController(IMaintenanceCampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        [Authorize(Roles = AuthBootstrapper.AllStaffRoles)]
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
                await _campaignService.CompleteAndSendAsync(
                    vm.CampaignItemId,
                    maintenanceVm,
                    campaignItemId => Url.Action("Sign", "MaintenanceCampaign", new { id = campaignItemId }, Request.Scheme)!);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }

            TempData["Success"] = "Mantenimiento completado. Se envió el enlace de firma por correo al usuario asignado.";
            return RedirectToAction(nameof(Index));
        }

        // Cualquier rol puede llegar aquí — es una excepción puntual para que un Colaborador
        // (cuyo nav solo muestra Casos TI) pueda abrir el link de firma que recibió por correo.
        // La verificación real de dueño/estado vive en MaintenanceCampaignService.GetSignPromptAsync.
        [Authorize]
        public async Task<IActionResult> Sign(int id)
        {
            var vm = await _campaignService.GetSignPromptAsync(id, GetCurrentUsersId(), HttpContext.RequestAborted);
            if (vm is null)
            {
                return NotFound();
            }

            return View(vm);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignSubmit(MaintenanceSignSubmitViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var prompt = await _campaignService.GetSignPromptAsync(vm.CampaignItemId, GetCurrentUsersId(), HttpContext.RequestAborted);
                if (prompt is null)
                {
                    return NotFound();
                }

                return View(nameof(Sign), prompt);
            }

            try
            {
                await _campaignService.SignAsync(
                    vm.CampaignItemId, GetCurrentUsersId(), GetCurrentUserSystemId(),
                    vm.SignatureData, vm.Method, vm.SaveSignature, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var prompt = await _campaignService.GetSignPromptAsync(vm.CampaignItemId, GetCurrentUsersId(), HttpContext.RequestAborted);
                if (prompt is null)
                {
                    return NotFound();
                }

                return View(nameof(Sign), prompt);
            }

            TempData["Success"] = "Firma registrada. El mantenimiento quedó marcado como realizado.";
            // No se redirige a Home: un Colaborador no tiene acceso a ese controller (su nav
            // solo muestra Casos TI), y esta acción debe ser alcanzable por cualquier rol.
            return RedirectToAction("Index", "CasoTI");
        }

        [Authorize(Roles = AuthBootstrapper.AllStaffRoles)]
        public async Task<IActionResult> DownloadSignedPdf(int id)
        {
            var item = await _campaignService.GetByIdAsync(id);
            var pdfBytes = await _campaignService.RegeneratePdfAsync(id);
            if (item is null || pdfBytes is null)
            {
                return NotFound();
            }

            var fileName = $"E-FGA-07 Control Mantenimiento V04 - {SanitizeFileName(item.Desktop.NameDesktop)}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [Authorize(Roles = AuthBootstrapper.AllStaffRoles)]
        public async Task<IActionResult> PreviewSignedPdf(int id)
        {
            var pdfBytes = await _campaignService.RegeneratePdfAsync(id);
            if (pdfBytes is null)
            {
                return NotFound();
            }

            return File(pdfBytes, "application/pdf");
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "Equipo" : sanitized;
        }

        private int GetCurrentUserSystemId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(claim!);
        }

        private int GetCurrentUsersId()
        {
            var claim = User.FindFirst("UserId")?.Value;
            return int.Parse(claim!);
        }
    }
}
