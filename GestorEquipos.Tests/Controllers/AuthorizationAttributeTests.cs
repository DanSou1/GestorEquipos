using System.Reflection;
using Gestor_Equipos.Controllers;
using Gestor_Equipos.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace GestorEquipos.Tests.Controllers
{
    public class AuthorizationAttributeTests
    {
        private static AuthorizeAttribute? GetClassAuthorize(Type controllerType)
            => controllerType.GetCustomAttribute<AuthorizeAttribute>();

        private static IEnumerable<MethodInfo> GetActionOverloads(Type controllerType, string methodName)
            => controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == methodName);

        // --- DesktopController: lectura abierta a cualquier usuario autenticado, escritura solo Administrador ---

        [Fact]
        public void DesktopController_ClassLevel_RequiresAuthenticatedUser_WithoutRoleRestriction()
        {
            var classAttr = GetClassAuthorize(typeof(DesktopController));
            Assert.NotNull(classAttr);
            Assert.True(string.IsNullOrEmpty(classAttr!.Roles));
        }

        [Theory]
        [InlineData(nameof(DesktopController.Index))]
        [InlineData(nameof(DesktopController.Details))]
        public void DesktopController_ReadActions_HaveNoRoleRestriction(string methodName)
        {
            foreach (var method in GetActionOverloads(typeof(DesktopController), methodName))
            {
                var methodAttr = method.GetCustomAttribute<AuthorizeAttribute>();
                Assert.True(methodAttr is null || string.IsNullOrEmpty(methodAttr.Roles));
            }
        }

        [Theory]
        [InlineData(nameof(DesktopController.Create))]
        [InlineData(nameof(DesktopController.Edit))]
        [InlineData(nameof(DesktopController.Deactivate))]
        [InlineData(nameof(DesktopController.Assign))]
        public void DesktopController_WriteActions_RequireAdministrador(string methodName)
        {
            var overloads = GetActionOverloads(typeof(DesktopController), methodName).ToList();
            Assert.NotEmpty(overloads);

            foreach (var method in overloads)
            {
                var methodAttr = method.GetCustomAttribute<AuthorizeAttribute>();
                Assert.NotNull(methodAttr);
                Assert.Equal(AuthBootstrapper.AdministradorRoleName, methodAttr!.Roles);
            }
        }

        // --- Controladores 100% Admin-only: deben tener [Authorize(Roles = "Administrador")] a nivel de clase ---

        [Theory]
        [InlineData(typeof(PeripheralController))]
        [InlineData(typeof(MaintenanceController))]
        [InlineData(typeof(LicenseController))]
        [InlineData(typeof(PeripheralTypeController))]
        [InlineData(typeof(UserAdminController))]
        public void AdminOnlyControllers_HaveClassLevelAdministradorAuthorize(Type controllerType)
        {
            var classAttr = GetClassAuthorize(controllerType);
            Assert.NotNull(classAttr);
            Assert.Equal(AuthBootstrapper.AdministradorRoleName, classAttr!.Roles);
        }

        // --- HomeController: sigue requiriendo autenticación, ya no expone Register ---

        [Fact]
        public void HomeController_ClassLevel_RequiresAuthenticatedUser()
        {
            var classAttr = GetClassAuthorize(typeof(HomeController));
            Assert.NotNull(classAttr);
            Assert.True(string.IsNullOrEmpty(classAttr!.Roles));
        }

        [Fact]
        public void HomeController_DoesNotExposeRegisterAction()
        {
            var overloads = GetActionOverloads(typeof(HomeController), "Register");
            Assert.Empty(overloads);
        }

        // --- LoginController (definido en Controllers/UserController.cs): anónimo salvo Logout ---

        [Fact]
        public void LoginController_ClassLevel_AllowsAnonymous()
        {
            var allowAnonymous = typeof(LoginController).GetCustomAttribute<AllowAnonymousAttribute>();
            Assert.NotNull(allowAnonymous);
        }

        [Fact]
        public void LoginController_Logout_RequiresAuthenticatedUser()
        {
            var overloads = GetActionOverloads(typeof(LoginController), nameof(LoginController.Logout)).ToList();
            Assert.NotEmpty(overloads);
            foreach (var method in overloads)
            {
                var methodAttr = method.GetCustomAttribute<AuthorizeAttribute>();
                Assert.NotNull(methodAttr);
            }
        }
    }
}
