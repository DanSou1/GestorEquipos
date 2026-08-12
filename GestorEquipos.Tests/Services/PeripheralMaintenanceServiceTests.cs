using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.PeripheralMaintenance;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class PeripheralMaintenanceServiceTests
    {
        private static (Peripheral peripheral, MaintenanceType type) SeedBase(Gestor_Equipos.Data.MyDbContext db)
        {
            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            var peripheralType = new PeripheralType { Name = "Mouse" };
            var maintenanceType = new MaintenanceType { Type = "Correctivo" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.PeripheralTypes.Add(peripheralType);
            db.MaintenanceTypes.Add(maintenanceType);
            db.SaveChanges();

            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();

            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = peripheralType.Id, Brand = "B", Model = "M" };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            return (peripheral, maintenanceType);
        }

        [Fact]
        public async Task CreateAsync_AddsPeripheralMaintenance()
        {
            using var db = TestHelpers.CreateDbContext();
            var (peripheral, type) = SeedBase(db);
            var service = new PeripheralMaintenanceService(db);

            var id = await service.CreateAsync(new PeripheralMaintenanceCreateViewModel
            {
                PeripheralId = peripheral.Id,
                MaintenanceTypeId = type.Id,
                Date = new DateOnly(2026, 1, 1),
                Description = "Cambio de rueda de scroll",
                TechnicianName = "Carlos Ruiz"
            });

            var maintenance = db.PeripheralMaintenances.Single(m => m.Id == id);
            Assert.Equal(peripheral.Id, maintenance.PeripheralId);
            Assert.Equal("Carlos Ruiz", maintenance.TechnicianName);
        }

        [Fact]
        public async Task GetByPeripheralAsync_ReturnsOrderedByDateDescending()
        {
            using var db = TestHelpers.CreateDbContext();
            var (peripheral, type) = SeedBase(db);
            db.PeripheralMaintenances.Add(new PeripheralMaintenance { PeripheralId = peripheral.Id, MaintenanceTypeId = type.Id, Date = new DateOnly(2026, 1, 1), Description = "Primero", TechnicianName = "Carlos Ruiz" });
            db.PeripheralMaintenances.Add(new PeripheralMaintenance { PeripheralId = peripheral.Id, MaintenanceTypeId = type.Id, Date = new DateOnly(2026, 6, 1), Description = "Segundo", TechnicianName = "Carlos Ruiz" });
            db.SaveChanges();

            var service = new PeripheralMaintenanceService(db);
            var result = await service.GetByPeripheralAsync(peripheral.Id);

            Assert.Equal(2, result.Count);
            Assert.Equal("Segundo", result[0].Description);
            Assert.Equal("Correctivo", result[0].MaintenanceType.Type);
        }

        [Fact]
        public async Task GetByPeripheralAsync_IncludesTechnicianName()
        {
            using var db = TestHelpers.CreateDbContext();
            var (peripheral, type) = SeedBase(db);
            db.PeripheralMaintenances.Add(new PeripheralMaintenance { PeripheralId = peripheral.Id, MaintenanceTypeId = type.Id, Date = new DateOnly(2026, 1, 1), Description = "Test", TechnicianName = "Carlos Ruiz" });
            db.SaveChanges();

            var service = new PeripheralMaintenanceService(db);
            var result = await service.GetByPeripheralAsync(peripheral.Id);

            var maintenance = Assert.Single(result);
            Assert.Equal("Carlos Ruiz", maintenance.TechnicianName);
        }
    }
}
