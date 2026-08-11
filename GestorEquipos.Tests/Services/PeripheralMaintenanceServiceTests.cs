using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.PeripheralMaintenance;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class PeripheralMaintenanceServiceTests
    {
        private static (Peripheral peripheral, MaintenanceType type, UserSystem technician) SeedBase(Gestor_Equipos.Data.MyDbContext db)
        {
            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            var role = new Rol { Name = AuthBootstrapper.AdministradorRoleName };
            var peripheralType = new PeripheralType { Name = "Mouse" };
            var maintenanceType = new MaintenanceType { Type = "Correctivo" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.Rols.Add(role);
            db.PeripheralTypes.Add(peripheralType);
            db.MaintenanceTypes.Add(maintenanceType);
            db.SaveChanges();

            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();

            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = peripheralType.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);

            var techUser = new Users { Name = "Carlos", LastName = "Ruiz", Email = "carlos-pm@x.com", EmailTeams = "carlos-pm@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(techUser);
            db.SaveChanges();

            var technician = new UserSystem { Username = "carlos-pm", PasswordHash = "x", UserId = techUser.Id, RolId = role.Id };
            db.UserSystems.Add(technician);
            db.SaveChanges();

            return (peripheral, maintenanceType, technician);
        }

        [Fact]
        public async Task CreateAsync_AddsPeripheralMaintenance()
        {
            using var db = TestHelpers.CreateDbContext();
            var (peripheral, type, technician) = SeedBase(db);
            var service = new PeripheralMaintenanceService(db);

            var id = await service.CreateAsync(new PeripheralMaintenanceCreateViewModel
            {
                PeripheralId = peripheral.Id,
                MaintenanceTypeId = type.Id,
                Date = new DateOnly(2026, 1, 1),
                Description = "Cambio de rueda de scroll",
                TechnicianUserSystemId = technician.Id
            });

            var maintenance = db.PeripheralMaintenances.Single(m => m.Id == id);
            Assert.Equal(peripheral.Id, maintenance.PeripheralId);
            Assert.Equal(technician.Id, maintenance.TechnicianUserSystemId);
        }

        [Fact]
        public async Task GetByPeripheralAsync_ReturnsOrderedByDateDescending()
        {
            using var db = TestHelpers.CreateDbContext();
            var (peripheral, type, technician) = SeedBase(db);
            db.PeripheralMaintenances.Add(new PeripheralMaintenance { PeripheralId = peripheral.Id, MaintenanceTypeId = type.Id, Date = new DateOnly(2026, 1, 1), Description = "Primero", TechnicianUserSystemId = technician.Id });
            db.PeripheralMaintenances.Add(new PeripheralMaintenance { PeripheralId = peripheral.Id, MaintenanceTypeId = type.Id, Date = new DateOnly(2026, 6, 1), Description = "Segundo", TechnicianUserSystemId = technician.Id });
            db.SaveChanges();

            var service = new PeripheralMaintenanceService(db);
            var result = await service.GetByPeripheralAsync(peripheral.Id);

            Assert.Equal(2, result.Count);
            Assert.Equal("Segundo", result[0].Description);
            Assert.Equal("Correctivo", result[0].MaintenanceType.Type);
        }

        [Fact]
        public async Task GetByPeripheralAsync_IncludesTechnicianUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (peripheral, type, technician) = SeedBase(db);
            db.PeripheralMaintenances.Add(new PeripheralMaintenance { PeripheralId = peripheral.Id, MaintenanceTypeId = type.Id, Date = new DateOnly(2026, 1, 1), Description = "Test", TechnicianUserSystemId = technician.Id });
            db.SaveChanges();

            var service = new PeripheralMaintenanceService(db);
            var result = await service.GetByPeripheralAsync(peripheral.Id);

            var maintenance = Assert.Single(result);
            Assert.Equal("Carlos", maintenance.Technician.User.Name);
        }
    }
}
