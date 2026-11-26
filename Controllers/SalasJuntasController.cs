using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models.ViewModels.SalasJuntas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Gestor_Equipos.Controllers
{
    [Authorize(Roles = AuthBootstrapper.AllRolesExceptAuditor)]
    public class SalasJuntasController : Controller
    {
        private readonly IConfiguration _configuration;

        public SalasJuntasController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            var model = new List<SalaJuntaLinkViewModel>
            {
                new() { Nombre = "Sala 1", Url = _configuration["SalasDeJuntas:Sala1Url"]! },
                new() { Nombre = "Sala 2", Url = _configuration["SalasDeJuntas:Sala2Url"]! }
            };

            return View(model);
        }
    }
}
