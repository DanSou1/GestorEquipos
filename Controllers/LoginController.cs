using Gestor_Equipos.Models;
using Gestor_Equipos.Services.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Gestor_Equipos.Controllers
{
    public class LoginController : Controller
    {
        private readonly IAuthService _authService;
        
        public LoginController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost]
        public async Task<IActionResult> Login(UserLogin user)
        {
            bool access = await _authService.ValidateUserAsync(user.email, user.passwordHash);
            if (access)
            {
                Console.WriteLine("Acceso concedido");
                return View("Privacy");
            }
            else
            {
                ViewBag.Error = "Usuario o contraseña incorrecta.";
                return View("Index");
            }
        }
    }
}
