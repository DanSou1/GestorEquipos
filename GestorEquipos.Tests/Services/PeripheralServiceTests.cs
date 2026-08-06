using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Peripheral;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class PeripheralServiceTests
    {
        private static (Desktop desktop, PeripheralType type) SeedDesktopAndType(Gestor_Equipos.Data.MyDbContext db)
        {
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            var type = new PeripheralType { Name = "Teclado" };
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.PeripheralTypes.Add(type);
            db.SaveChanges();

            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();

            return (desktop, type);
        }

        [Fact]
        public async Task AddAsync_CreatesPeripheralWithEstadoActivo()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var service = new PeripheralService(db);

            var id = await service.AddAsync(new PeripheralCreateViewModel
            {
                DesktopId = desktop.Id,
                PeripheralTypeId = type.Id,
                Brand = "Logitech",
                Model = "K120"
            });

            var peripheral = db.Peripherals.Single(p => p.Id == id);
            Assert.Equal(PeripheralEstado.Activo, peripheral.Estado);
        }

        [Fact]
        public async Task AddObservationAsync_BajaRAES_SetsEstadoRaes()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M", Estado = PeripheralEstado.Activo };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            var service = new PeripheralService(db);
            await service.AddObservationAsync(peripheral.Id, new PeripheralObservationCreateViewModel
            {
                Date = new DateOnly(2026, 1, 1),
                Type = PeripheralObservationType.BajaRAES,
                Description = "Daño irreparable"
            });

            var updated = db.Peripherals.Single(p => p.Id == peripheral.Id);
            Assert.Equal(PeripheralEstado.Raes, updated.Estado);
            Assert.Single(db.PeripheralObservations);
        }

        [Fact]
        public async Task AddObservationAsync_Reparacion_SetsEstadoActivo()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M", Estado = PeripheralEstado.Inactivo };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            var service = new PeripheralService(db);
            await service.AddObservationAsync(peripheral.Id, new PeripheralObservationCreateViewModel
            {
                Date = new DateOnly(2026, 1, 1),
                Type = PeripheralObservationType.Reparacion,
                Description = "Reparado"
            });

            var updated = db.Peripherals.Single(p => p.Id == peripheral.Id);
            Assert.Equal(PeripheralEstado.Activo, updated.Estado);
        }

        [Fact]
        public async Task AddObservationAsync_Cambio_UsesResultingEstadoWhenProvided()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M", Estado = PeripheralEstado.Activo };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            var service = new PeripheralService(db);
            await service.AddObservationAsync(peripheral.Id, new PeripheralObservationCreateViewModel
            {
                Date = new DateOnly(2026, 1, 1),
                Type = PeripheralObservationType.Cambio,
                Description = "Se cambio por uno nuevo",
                ResultingEstado = PeripheralEstado.Inactivo
            });

            var updated = db.Peripherals.Single(p => p.Id == peripheral.Id);
            Assert.Equal(PeripheralEstado.Inactivo, updated.Estado);
        }

        [Fact]
        public async Task AddObservationAsync_Cambio_KeepsCurrentEstadoWhenNotProvided()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedDesktopAndType(db);
            var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralTypeId = type.Id, Brand = "B", Model = "M", Estado = PeripheralEstado.Activo };
            db.Peripherals.Add(peripheral);
            db.SaveChanges();

            var service = new PeripheralService(db);
            await service.AddObservationAsync(peripheral.Id, new PeripheralObservationCreateViewModel
            {
                Date = new DateOnly(2026, 1, 1),
                Type = PeripheralObservationType.Cambio,
                Description = "Solo bitacora"
            });

            var updated = db.Peripherals.Single(p => p.Id == peripheral.Id);
            Assert.Equal(PeripheralEstado.Activo, updated.Estado);
        }

        [Fact]
        public async Task AddObservationAsync_ThrowsWhenPeripheralNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = new PeripheralService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddObservationAsync(999, new PeripheralObservationCreateViewModel
                {
                    Date = new DateOnly(2026, 1, 1),
                    Type = PeripheralObservationType.Reparacion,
                    Description = "x"
                }));
        }
    }
}
