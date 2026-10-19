using System.Security.Claims;
using Gestor_Equipos.Data;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Auth
{
    // Fuerza a cualquier cuenta con UserSystem.MustChangePassword=true a pasar por
    // ChangePasswordController antes de poder usar cualquier otra pantalla de la app. Vive como
    // IAsyncActionFilter global (no en SessionCookieEvents) porque esto es una regla de negocio,
    // no de validez de sesión — SessionCookieEvents sigue encargándose solo de eso.
    public sealed class MustChangePasswordFilter : IAsyncActionFilter
    {
        private static readonly string[] ExemptControllers = { "ChangePassword", "Login" };

        private readonly MyDbContext _dbContext;

        public MustChangePasswordFilter(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            if (context.ActionDescriptor is ControllerActionDescriptor descriptor
                && ExemptControllers.Contains(descriptor.ControllerName))
            {
                await next();
                return;
            }

            var accountClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(accountClaim, out var accountId))
            {
                await next();
                return;
            }

            var mustChangePassword = await _dbContext.UserSystems.AsNoTracking()
                .Where(us => us.Id == accountId)
                .Select(us => us.MustChangePassword)
                .SingleOrDefaultAsync();

            if (mustChangePassword)
            {
                context.Result = new Microsoft.AspNetCore.Mvc.RedirectToActionResult("Index", "ChangePassword", null);
                return;
            }

            await next();
        }
    }
}
