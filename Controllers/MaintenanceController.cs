using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    public class MaintenanceController : Controller
    {
        private readonly IMaintenanceService _maintenanceService;
        private readonly IMaintenancePdfService _maintenancePdfService;
        private readonly MyDbContext _dbContext;

        private static readonly (string Label, Func<MaintenanceChecklist?, bool> Selector)[] ChecklistItems =
        {
            ("Verificación de sistema mecánico", c => c?.VerificacionSistemaMecanico ?? false),
            ("Verificación de sistema eléctrico", c => c?.VerificacionSistemaElectrico ?? false),
            ("Verificación de sistema de seguridad", c => c?.VerificacionSistemaSeguridad ?? false),
            ("Soplado interno", c => c?.SopladoInterno ?? false),
            ("Lavado interno", c => c?.LavadoInterno ?? false),
            ("Lubricación", c => c?.Lubricacion ?? false),
            ("Ajuste de tornillería", c => c?.AjusteTornilleria ?? false),
            ("Cambio de accesorios y repuestos por desgaste", c => c?.CambioAccesorios ?? false)
        };

        public MaintenanceController(
            IMaintenanceService maintenanceService,
            IMaintenancePdfService maintenancePdfService,
            MyDbContext dbContext)
        {
            _maintenanceService = maintenanceService;
            _maintenancePdfService = maintenancePdfService;
            _dbContext = dbContext;
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        public IActionResult Create(int desktopId)
        {
            return View(new MaintenanceCreateViewModel { DesktopId = desktopId, Date = DateOnly.FromDateTime(DateTime.Now) });
        }

        [HttpPost]
        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MaintenanceCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _maintenanceService.CreateAsync(vm);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }

            return RedirectToAction("Details", "Desktop", new { id = vm.DesktopId });
        }

        public async Task<IActionResult> DownloadPdf(int id)
        {
            var maintenance = await _dbContext.Maintenances
                .Include(m => m.Checklist)
                .Include(m => m.Desktop).ThenInclude(d => d.DesktopType)
                .Include(m => m.EquipmentSnapshot)
                .SingleOrDefaultAsync(m => m.Id == id);

            if (maintenance is null)
            {
                return NotFound();
            }

            var vm = new MaintenancePdfViewModel
            {
                DesktopName = maintenance.Desktop.NameDesktop,
                SerialNumber = maintenance.Desktop.SerialNumber,
                Brand = maintenance.Desktop.DesktopType.Brand,
                Model = maintenance.Desktop.DesktopType.Model,
                Date = maintenance.Date,
                Category = maintenance.Category,
                Description = maintenance.Description,
                TechnicianName = maintenance.TechnicianName
            };

            if (maintenance.Category == MaintenanceCategory.Preventivo)
            {
                // LOG-08: reads the snapshot captured at creation time (MaintenanceService.CreateAsync),
                // not the desktop's current holder/peripherals — those can have changed since.
                // Records created before this snapshot existed have no data to show; showing the
                // *current* holder/equipment instead would misrepresent history, so it stays blank.
                vm.CurrentUserName = maintenance.SnapshotHolderName ?? "Sin dato histórico";
                vm.CurrentAreaName = maintenance.SnapshotHolderArea ?? "Sin dato histórico";

                vm.Equipment.AddRange(maintenance.EquipmentSnapshot.Select(s => new MaintenanceEquipmentRow
                {
                    Label = s.Label,
                    Brand = s.Brand,
                    Model = s.Model,
                    Serial = s.Serial
                }));

                vm.Checklist = ChecklistItems
                    .Select(i => new MaintenanceChecklistItem { Label = i.Label, Checked = i.Selector(maintenance.Checklist) })
                    .ToList();
            }

            var pdfBytes = _maintenancePdfService.GenerateMaintenancePdf(vm);
            var fileName = $"Mantenimiento-{maintenance.Id}-{maintenance.Desktop.SerialNumber}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}
