using System.Diagnostics;
using Gestor_Equipos.Services;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IDesktopService _desktopService;
        private readonly IUserAdminService _userAdminService;
        private readonly IPeripheralService _peripheralService;

        public HomeController(ILogger<HomeController> logger, IDesktopService desktopService, IUserAdminService userAdminService, IPeripheralService peripheralService)
        {
            _logger = logger;
            _desktopService = desktopService;
            _userAdminService = userAdminService;
            _peripheralService = peripheralService;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel
            {
                EquipmentStats = await _desktopService.GetEquipmentStatsAsync(),
                PeripheralStats = await _peripheralService.GetInventoryStatsAsync(),
                UsersByRegional = await _userAdminService.GetUsersByRegionalAsync()
            };
            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
