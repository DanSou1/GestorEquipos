using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.PeripheralMaintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
    public class PeripheralMaintenanceController : Controller
    {
        private readonly IPeripheralMaintenanceService _peripheralMaintenanceService;
        private readonly MyDbContext _dbContext;

        public PeripheralMaintenanceController(IPeripheralMaintenanceService peripheralMaintenanceService, MyDbContext dbContext)
        {
            _peripheralMaintenanceService = peripheralMaintenanceService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Create(int peripheralId)
        {
            await PopulateDropdownsAsync();
            return View(new PeripheralMaintenanceCreateViewModel { PeripheralId = peripheralId, Date = DateOnly.FromDateTime(DateTime.Now) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PeripheralMaintenanceCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(vm);
            }

            await _peripheralMaintenanceService.CreateAsync(vm);
            return RedirectToAction("Details", "Peripheral", new { id = vm.PeripheralId });
        }

        private async Task PopulateDropdownsAsync()
        {
            ViewBag.MaintenanceTypes = new SelectList(await _dbContext.MaintenanceTypes.OrderBy(t => t.Type).ToListAsync(), "Id", "Type");

            var technicians = await _dbContext.UserSystems
                .Include(us => us.User)
                .Include(us => us.Rol)
                .Where(us => us.Rol.Name == AuthBootstrapper.AdministradorRoleName)
                .ToListAsync();

            ViewBag.Technicians = new SelectList(
                technicians.Select(t => new { t.Id, FullName = $"{t.User.Name} {t.User.LastName}" }),
                "Id",
                "FullName");
        }
    }
}
