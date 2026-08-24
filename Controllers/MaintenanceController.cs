using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    public class MaintenanceController : Controller
    {
        private readonly IMaintenanceService _maintenanceService;
        private readonly IMaintenancePdfService _maintenancePdfService;
        private readonly IAsignationService _asignationService;
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

        private static readonly string[] EquipmentPeripheralTypeNames = { "Mouse", "Teclado", "Monitor" };

        public MaintenanceController(
            IMaintenanceService maintenanceService,
            IMaintenancePdfService maintenancePdfService,
            IAsignationService asignationService,
            MyDbContext dbContext)
        {
            _maintenanceService = maintenanceService;
            _maintenancePdfService = maintenancePdfService;
            _asignationService = asignationService;
            _dbContext = dbContext;
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Create(int desktopId)
        {
            await PopulateDropdownsAsync();
            return View(new MaintenanceCreateViewModel { DesktopId = desktopId, Date = DateOnly.FromDateTime(DateTime.Now) });
        }

        [HttpPost]
        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MaintenanceCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(vm);
            }

            await _maintenanceService.CreateAsync(vm);
            return RedirectToAction("Details", "Desktop", new { id = vm.DesktopId });
        }

        public async Task<IActionResult> DownloadPdf(int id)
        {
            var maintenance = await _dbContext.Maintenances
                .Include(m => m.MaintenanceType)
                .Include(m => m.Checklist)
                .Include(m => m.Desktop)
                .SingleOrDefaultAsync(m => m.Id == id);

            if (maintenance is null)
            {
                return NotFound();
            }

            var vm = new MaintenancePdfViewModel
            {
                DesktopName = maintenance.Desktop.NameDesktop,
                SerialNumber = maintenance.Desktop.SerialNumber,
                Brand = maintenance.Desktop.Brand,
                Model = maintenance.Desktop.Model,
                Date = maintenance.Date,
                Category = maintenance.Category,
                MaintenanceTypeName = maintenance.MaintenanceType.Type,
                Description = maintenance.Description,
                TechnicianName = maintenance.TechnicianName
            };

            if (maintenance.Category == MaintenanceCategory.Preventivo)
            {
                var asignationHistory = await _asignationService.GetHistoryAsync(maintenance.DesktopId);
                var currentAsignation = asignationHistory.FirstOrDefault();
                var isDisponible = currentAsignation is not null && !currentAsignation.User.Activo;
                vm.CurrentUserName = currentAsignation is null
                    ? "Sin asignar"
                    : isDisponible
                        ? "Disponible"
                        : $"{currentAsignation.User.Name} {currentAsignation.User.LastName}";
                vm.CurrentAreaName = currentAsignation?.User.Area?.Name ?? "-";

                vm.Equipment.Add(new MaintenanceEquipmentRow
                {
                    Label = "Desktop",
                    Brand = maintenance.Desktop.Brand,
                    Model = maintenance.Desktop.Model,
                    Serial = maintenance.Desktop.SerialNumber
                });

                var peripherals = await _dbContext.Peripherals
                    .Include(p => p.PeripheralType)
                    .Where(p => p.DesktopId == maintenance.DesktopId
                        && p.Estado
                        && EquipmentPeripheralTypeNames.Contains(p.PeripheralType.Name))
                    .OrderBy(p => p.PeripheralType.Name)
                    .ToListAsync();

                vm.Equipment.AddRange(peripherals.Select(p => new MaintenanceEquipmentRow
                {
                    Label = p.PeripheralType.Name,
                    Brand = p.Brand,
                    Model = p.Model,
                    Serial = p.Serial
                }));

                vm.Checklist = ChecklistItems
                    .Select(i => new MaintenanceChecklistItem { Label = i.Label, Checked = i.Selector(maintenance.Checklist) })
                    .ToList();
            }

            var pdfBytes = _maintenancePdfService.GenerateMaintenancePdf(vm);
            var fileName = $"Mantenimiento-{maintenance.Id}-{maintenance.Desktop.SerialNumber}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task PopulateDropdownsAsync()
        {
            ViewBag.MaintenanceTypes = new SelectList(await _dbContext.MaintenanceTypes.OrderBy(t => t.Type).ToListAsync(), "Id", "Type");
        }
    }
}
