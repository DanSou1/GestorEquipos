using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
    public class MaintenanceController : Controller
    {
        private readonly IMaintenanceService _maintenanceService;
        private readonly MyDbContext _dbContext;

        public MaintenanceController(IMaintenanceService maintenanceService, MyDbContext dbContext)
        {
            _maintenanceService = maintenanceService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Create(int desktopId)
        {
            await PopulateDropdownsAsync();
            return View(new MaintenanceCreateViewModel { DesktopId = desktopId, Date = DateOnly.FromDateTime(DateTime.Now) });
        }

        [HttpPost]
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
