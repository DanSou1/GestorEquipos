using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.UserAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdminOrTecnicoRoles)]
    public class UserAdminController : Controller
    {
        private readonly IUserAdminService _userAdminService;
        private readonly MyDbContext _dbContext;

        public UserAdminController(IUserAdminService userAdminService, MyDbContext dbContext)
        {
            _userAdminService = userAdminService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(string? search, bool? showInactive)
        {
            var users = await _userAdminService.GetAllAsync(includeInactive: showInactive == true, cancellationToken: HttpContext.RequestAborted);

            ViewBag.Error = TempData["Error"];
            ViewBag.TotalCount = users.Count;
            ViewBag.SearchTerm = search;
            ViewBag.ShowInactive = showInactive == true;

            if (!string.IsNullOrWhiteSpace(search))
            {
                users = users.Where(u =>
                    u.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.LastName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return View(users);
        }

        public async Task<IActionResult> Details(int id)
        {
            var detail = await _userAdminService.GetDetailAsync(id);
            if (detail is null)
            {
                return NotFound();
            }

            return View(detail);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(vm);
            }

            try
            {
                await _userAdminService.CreateUserAsync(vm);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateDropdownsAsync();
                return View(vm);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userAdminService.GetByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            var currentCargoId = await _dbContext.CargoAssignments
                .Where(a => a.UserId == id)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .Select(a => (int?)a.CargoId)
                .FirstOrDefaultAsync();

            var vm = new UserCreateViewModel
            {
                Id = user.Id,
                Name = user.Name,
                LastName = user.LastName,
                Email = user.Email,
                EmailTeams = user.EmailTeams,
                AreaId = user.AreaId,
                RegionalId = user.RegionalId,
                CargoId = currentCargoId
            };

            await PopulateDropdownsAsync();
            ViewBag.HasCargoAssigned = currentCargoId.HasValue;
            return View(vm);
        }

        [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                ViewBag.HasCargoAssigned = vm.CargoId.HasValue;
                return View(vm);
            }

            try
            {
                await _userAdminService.UpdateUserAsync(id, vm);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateDropdownsAsync();
                ViewBag.HasCargoAssigned = vm.CargoId.HasValue;
                return View(vm);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            try
            {
                await _userAdminService.DeactivateAsync(id);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync()
        {
            ViewBag.Areas = new SelectList(await _dbContext.Areas.OrderBy(a => a.Name).ToListAsync(), "Id", "Name");
            ViewBag.Regionals = new SelectList(await _dbContext.Regionals.OrderBy(r => r.Name).ToListAsync(), "Id", "Name");
            ViewBag.Cargos = new SelectList(await _dbContext.Cargos.OrderBy(c => c.Name).ToListAsync(), "Id", "Name");
        }
    }
}
