using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.Peripheral;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
    public class PeripheralController : Controller
    {
        private readonly IPeripheralService _peripheralService;
        private readonly MyDbContext _dbContext;

        public PeripheralController(IPeripheralService peripheralService, MyDbContext dbContext)
        {
            _peripheralService = peripheralService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Create(int desktopId)
        {
            await PopulateTypesAsync();
            return View(new PeripheralCreateViewModel { DesktopId = desktopId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PeripheralCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateTypesAsync();
                return View(vm);
            }

            await _peripheralService.AddAsync(vm);
            return RedirectToAction("Details", "Desktop", new { id = vm.DesktopId });
        }

        public IActionResult AddObservation(int peripheralId, int desktopId)
        {
            ViewBag.PeripheralId = peripheralId;
            ViewBag.DesktopId = desktopId;
            return View(new PeripheralObservationCreateViewModel { Date = DateOnly.FromDateTime(DateTime.Now) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddObservation(int peripheralId, int desktopId, PeripheralObservationCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.PeripheralId = peripheralId;
                ViewBag.DesktopId = desktopId;
                return View(vm);
            }

            await _peripheralService.AddObservationAsync(peripheralId, vm);
            return RedirectToAction("Details", "Desktop", new { id = desktopId });
        }

        private async Task PopulateTypesAsync()
        {
            ViewBag.PeripheralTypes = new SelectList(await _dbContext.PeripheralTypes.OrderBy(t => t.Name).ToListAsync(), "Id", "Name");
        }
    }
}
