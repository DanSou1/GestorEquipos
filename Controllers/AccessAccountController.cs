using System.Security.Claims;
using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.AccessAccount;
using GestorEquipos.Models.ViewModels.UserAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]
    public class AccessAccountController : Controller
    {
        private readonly IUserAdminService _userAdminService;
        private readonly MyDbContext _dbContext;

        public AccessAccountController(IUserAdminService userAdminService, MyDbContext dbContext)
        {
            _userAdminService = userAdminService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var accounts = await _userAdminService.GetAccountsAsync();
            return View(accounts);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AccessAccountCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(vm);
            }

            try
            {
                var userId = await _userAdminService.CreateUserAsync(new UserCreateViewModel
                {
                    Name = vm.Name,
                    LastName = vm.LastName,
                    Email = vm.Email,
                    EmailTeams = vm.EmailTeams,
                    AreaId = vm.AreaId,
                    RegionalId = vm.RegionalId
                });

                await _userAdminService.CreateLoginAsync(userId, vm.Username, vm.Password, vm.RolId);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateDropdownsAsync();
                return View(vm);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userAdminService.GetByIdAsync(id);
            if (user is null || !user.UserSystems.Any())
            {
                return NotFound();
            }

            var userSystem = user.UserSystems.First();
            var vm = new AccessAccountEditViewModel
            {
                Id = user.Id,
                Name = user.Name,
                LastName = user.LastName,
                Email = user.Email,
                EmailTeams = user.EmailTeams,
                AreaId = user.AreaId,
                RegionalId = user.RegionalId,
                Username = userSystem.Username,
                RolId = userSystem.RolId
            };

            await PopulateDropdownsAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AccessAccountEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(vm);
            }

            try
            {
                await _userAdminService.UpdateAccountAsync(id, vm, GetCurrentUserSystemId());
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateDropdownsAsync();
                return View(vm);
            }

            return RedirectToAction(nameof(Index));
        }

        private int GetCurrentUserSystemId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(claim!);
        }

        private async Task PopulateDropdownsAsync()
        {
            ViewBag.Areas = new SelectList(await _dbContext.Areas.OrderBy(a => a.Name).ToListAsync(), "Id", "Name");
            ViewBag.Regionals = new SelectList(await _dbContext.Regionals.OrderBy(r => r.Name).ToListAsync(), "Id", "Name");
            ViewBag.Roles = new SelectList(await _dbContext.Rols.OrderBy(r => r.Name).ToListAsync(), "Id", "Name");
        }
    }
}
