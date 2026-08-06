using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
    public class PeripheralTypeController : Controller
    {
        private readonly MyDbContext _dbContext;

        public PeripheralTypeController(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var types = await _dbContext.PeripheralTypes.OrderBy(t => t.Name).ToListAsync();
            return View(types);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name")] PeripheralType model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _dbContext.PeripheralTypes.Add(new PeripheralType { Name = model.Name });
            await _dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
