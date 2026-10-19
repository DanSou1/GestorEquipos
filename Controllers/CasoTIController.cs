using System.Security.Claims;
using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.CasoTI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize]
    public class CasoTIController : Controller
    {
        private readonly ICasoTIService _casoTIService;
        private readonly IUserAdminService _userAdminService;
        private readonly MyDbContext _dbContext;

        public CasoTIController(ICasoTIService casoTIService, IUserAdminService userAdminService, MyDbContext dbContext)
        {
            _casoTIService = casoTIService;
            _userAdminService = userAdminService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index([FromQuery] CasoTIIndexFilterViewModel filter)
        {
            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];
            var vm = await _casoTIService.GetIndexAsync(GetCurrentUserSystemId(), GetCurrentUsersId(), GetCurrentRole(), filter, HttpContext.RequestAborted);

            if (CanFilterByTecnico())
            {
                ViewBag.TecnicoOptions = await GetTecnicoSelectListAsync();
            }

            ViewBag.TipoCasoOptions = new SelectList(await _dbContext.TiposCaso.OrderBy(t => t.Nombre).ToListAsync(), "Id", "Nombre");
            ViewBag.Filter = filter;
            return View(vm);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateCreateDropdownsAsync();
            return View(new CasoTICreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CasoTICreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCreateDropdownsAsync();
                return View(vm);
            }

            try
            {
                var id = await _casoTIService.CreateAsync(vm, GetCurrentUserSystemId(), GetCurrentUsersId(), GetCurrentRole(), HttpContext.RequestAborted);
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateCreateDropdownsAsync();
                return View(vm);
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            ViewBag.Error = TempData["Error"];
            var vm = await _casoTIService.GetDetailAsync(id, GetCurrentUserSystemId(), GetCurrentUsersId(), GetCurrentRole(), HttpContext.RequestAborted);
            if (vm is null)
            {
                return NotFound();
            }

            return View(vm);
        }

        [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id, string notas)
        {
            try
            {
                await _casoTIService.ResolveAsync(
                    id,
                    notas,
                    GetCurrentUserSystemId(),
                    GetCurrentRole(),
                    token => Url.Action("Index", "CasoTIRating", new { token }, Request.Scheme)!,
                    HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // Cualquier rol puede llegar aquí (un Colaborador debe poder borrar su propio caso) —
        // la verificación real de dueño/estado vive en CasoTIService.DeleteAsync.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _casoTIService.DeleteAsync(id, GetCurrentUsersId(), HttpContext.RequestAborted);
                TempData["Success"] = "Caso eliminado.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Estadisticas(CasoTIGranularidad granularidad = CasoTIGranularidad.Mes, int? mes = null, int? anio = null, int? tecnicoId = null)
        {
            var vm = await _casoTIService.GetStatsAsync(
                GetCurrentUserSystemId(), GetCurrentUsersId(), GetCurrentRole(), tecnicoId, granularidad, mes, anio, HttpContext.RequestAborted);

            if (CanFilterByTecnico())
            {
                ViewBag.TecnicoFiltro = tecnicoId;
                ViewBag.TecnicoOptions = await GetTecnicoSelectListAsync();
            }

            return View(vm);
        }

        public async Task<IActionResult> Calificaciones(int? tecnicoId = null)
        {
            var vm = await _casoTIService.GetRatingsAsync(GetCurrentUserSystemId(), GetCurrentUsersId(), GetCurrentRole(), tecnicoId, HttpContext.RequestAborted);

            if (CanFilterByTecnico())
            {
                ViewBag.TecnicoFiltro = tecnicoId;
                ViewBag.TecnicoOptions = await GetTecnicoSelectListAsync();
            }

            return View(vm);
        }

        private bool CanFilterByTecnico() =>
            User.IsInRole(AuthBootstrapper.AdministradorRoleName) || User.IsInRole(AuthBootstrapper.TecnicoSistemasRoleName);

        private async Task PopulateCreateDropdownsAsync()
        {
            if (User.IsInRole(AuthBootstrapper.ColaboradorRoleName))
            {
                // El Colaborador no elige persona: el servicio la fuerza a sí mismo.
                ViewBag.TipoCasoOptions = new SelectList(
                    await _dbContext.TiposCaso.OrderBy(t => t.Nombre).ToListAsync(), "Id", "Nombre");
                return;
            }

            // Solo personas "normales" (sin cuenta de personal, sin contar RAES/"sin asignar"):
            // misma definición canónica que UserAdminService.GetAllAsync.
            var personas = await _userAdminService.GetAllAsync(includeInactive: false, HttpContext.RequestAborted);
            ViewBag.PersonaOptions = new SelectList(
                personas
                    .OrderBy(u => u.Name).ThenBy(u => u.LastName)
                    .Select(u => new { u.Id, Nombre = $"{u.Name} {u.LastName}" }),
                "Id", "Nombre");

            ViewBag.TipoCasoOptions = new SelectList(
                await _dbContext.TiposCaso.OrderBy(t => t.Nombre).ToListAsync(),
                "Id", "Nombre");
        }

        private async Task<SelectList> GetTecnicoSelectListAsync()
        {
            var tecnicos = await _dbContext.UserSystems
                .Include(us => us.Rol)
                .Include(us => us.User)
                .Where(us => us.Rol.Name == AuthBootstrapper.AdministradorRoleName || us.Rol.Name == AuthBootstrapper.TecnicoSistemasRoleName)
                .OrderBy(us => us.User.Name)
                .Select(us => new { us.Id, Nombre = us.User.Name + " " + us.User.LastName })
                .ToListAsync();

            return new SelectList(tecnicos, "Id", "Nombre");
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

        private string GetCurrentRole()
        {
            if (User.IsInRole(AuthBootstrapper.AdministradorRoleName)) return AuthBootstrapper.AdministradorRoleName;
            if (User.IsInRole(AuthBootstrapper.TecnicoSistemasRoleName)) return AuthBootstrapper.TecnicoSistemasRoleName;
            if (User.IsInRole(AuthBootstrapper.ColaboradorRoleName)) return AuthBootstrapper.ColaboradorRoleName;
            return AuthBootstrapper.AuditorRoleName;
        }
    }
}
