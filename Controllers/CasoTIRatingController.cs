using Gestor_Equipos.Services;
using GestorEquipos.Models.ViewModels.CasoTI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    // La política global en Program.cs es RequireAuthenticatedUser — [AllowAnonymous]
    // explícito es obligatorio aquí, igual que en LoginController, o la petición nunca
    // llega a la acción y termina redirigida a Login.
    [AllowAnonymous]
    public class CasoTIRatingController : Controller
    {
        private readonly ICasoTIService _casoTIService;

        public CasoTIRatingController(ICasoTIService casoTIService)
        {
            _casoTIService = casoTIService;
        }

        public async Task<IActionResult> Index(Guid token)
        {
            var prompt = await _casoTIService.GetRatingPromptAsync(token, HttpContext.RequestAborted);
            if (prompt is null)
            {
                return View("TokenInvalido");
            }

            return View(prompt);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CasoTIRatingSubmitViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var prompt = await _casoTIService.GetRatingPromptAsync(vm.Token, HttpContext.RequestAborted);
                if (prompt is null)
                {
                    return View("TokenInvalido");
                }

                return View(prompt);
            }

            var result = await _casoTIService.SubmitRatingAsync(vm.Token, vm.Rating, vm.Comment, HttpContext.RequestAborted);

            if (result == CasoTIRatingSubmitResult.TokenNotFound)
            {
                return View("TokenInvalido");
            }

            return RedirectToAction(nameof(Index), new { token = vm.Token });
        }
    }
}
