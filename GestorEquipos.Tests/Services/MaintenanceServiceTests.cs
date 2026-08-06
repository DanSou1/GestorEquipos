using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class MaintenanceServiceTests
    {
        private static (Desktop desktop, MaintenanceType type, UserSystem technician) SeedBase(Gestor_Equipos.Data.MyDbContext db)
        {
            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            var role = new Rol { Name = AuthBootstrapper.AdministradorRoleName };
            var maintenanceType = new MaintenanceType { Type = "Correctivo" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.Rols.Add(role);
            db.MaintenanceTypes.Add(maintenanceType);
            db.SaveChanges();

            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);

            var techUser = new Users { Name = "Carlos", LastName = "Ruiz", Email = "carlos@x.com", EmailTeams = "carlos@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(techUser);
            db.SaveChanges();

            var technician = new UserSystem { Username = "carlos", PasswordHash = "x", UserId = techUser.Id, RolId = role.Id };
            db.UserSystems.Add(technician);
            db.SaveChanges();

            return (desktop, maintenanceType, technician);
        }

        [Fact]
        public async Task CreateAsync_AddsMaintenance()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type, technician) = SeedBase(db);
            var service = new MaintenanceService(db);

            var id = await service.CreateAsync(new MaintenanceCreateViewModel
            {
                DesktopId = desktop.Id,
                MaintenanceTypeId = type.Id,
                Date = new DateOnly(2026, 1, 1),
                Description = "Cambio de fuente",
                TechnicianUserSystemId = technician.Id
            });

            var maintenance = db.Maintenances.Single(m => m.Id == id);
            Assert.Equal(desktop.Id, maintenance.DesktopId);
            Assert.Equal(technician.Id, maintenance.TechnicianUserSystemId);
        }

        [Fact]
        public async Task GetByDesktopAsync_ReturnsOrderedByDateDescending()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type, technician) = SeedBase(db);
            db.Maintenances.Add(new Maintenance { DesktopId = desktop.Id, MaintenanceTypeId = type.Id, Date = new DateOnly(2026, 1, 1), Description = "Primero", TechnicianUserSystemId = technician.Id });
            db.Maintenances.Add(new Maintenance { DesktopId = desktop.Id, MaintenanceTypeId = type.Id, Date = new DateOnly(2026, 6, 1), Description = "Segundo", TechnicianUserSystemId = technician.Id });
            db.SaveChanges();

            var service = new MaintenanceService(db);
            var result = await service.GetByDesktopAsync(desktop.Id);

            Assert.Equal(2, result.Count);
            Assert.Equal("Segundo", result[0].Description);
            Assert.Equal("Correctivo", result[0].MaintenanceType.Type);
        }
    }
}
