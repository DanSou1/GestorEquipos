using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class MaintenanceServiceTests
    {
        private static (Desktop desktop, MaintenanceType type) SeedBase(Gestor_Equipos.Data.MyDbContext db)
        {
            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            var maintenanceType = new MaintenanceType { Type = "Correctivo" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.MaintenanceTypes.Add(maintenanceType);
            db.SaveChanges();

            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();

            return (desktop, maintenanceType);
        }

        [Fact]
        public async Task CreateAsync_AddsMaintenance()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedBase(db);
            var service = new MaintenanceService(db);

            var id = await service.CreateAsync(new MaintenanceCreateViewModel
            {
                DesktopId = desktop.Id,
                MaintenanceTypeId = type.Id,
                Date = new DateOnly(2026, 1, 1),
                Description = "Cambio de fuente",
                TechnicianName = "Carlos Ruiz"
            });

            var maintenance = db.Maintenances.Single(m => m.Id == id);
            Assert.Equal(desktop.Id, maintenance.DesktopId);
            Assert.Equal("Carlos Ruiz", maintenance.TechnicianName);
        }

        [Fact]
        public async Task CreateAsync_Preventivo_ConChecklistParcial_CreaFilaChecklistConValoresCorrectos()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedBase(db);
            var service = new MaintenanceService(db);

            var id = await service.CreateAsync(new MaintenanceCreateViewModel
            {
                DesktopId = desktop.Id,
                MaintenanceTypeId = type.Id,
                Category = MaintenanceCategory.Preventivo,
                Date = new DateOnly(2026, 1, 1),
                Description = "Mantenimiento preventivo",
                TechnicianName = "Carlos Ruiz",
                ChecklistVerificacionSistemaMecanico = true,
                ChecklistSopladoInterno = true,
                ChecklistLubricacion = true
            });

            var maintenance = db.Maintenances.Single(m => m.Id == id);
            Assert.Equal(MaintenanceCategory.Preventivo, maintenance.Category);

            var checklist = db.MaintenanceChecklists.Single(c => c.MaintenanceId == id);
            Assert.True(checklist.VerificacionSistemaMecanico);
            Assert.True(checklist.SopladoInterno);
            Assert.True(checklist.Lubricacion);
            Assert.False(checklist.VerificacionSistemaElectrico);
            Assert.False(checklist.VerificacionSistemaSeguridad);
            Assert.False(checklist.LavadoInterno);
            Assert.False(checklist.AjusteTornilleria);
            Assert.False(checklist.CambioAccesorios);
        }

        [Fact]
        public async Task CreateAsync_Correctivo_IgnoraChecklistPosteado_NoCreaFilaChecklist()
        {
            using var db = TestHelpers.CreateDbContext();
            var (desktop, type) = SeedBase(db);
            var service = new MaintenanceService(db);

            var id = await service.CreateAsync(new MaintenanceCreateViewModel
            {
                DesktopId = desktop.Id,
                MaintenanceTypeId = type.Id,
                Category = MaintenanceCategory.Correctivo,
                Date = new DateOnly(2026, 1, 1),
                Description = "Cambio de fuente",
                TechnicianName = "Carlos Ruiz",
                ChecklistVerificacionSistemaMecanico = true,
                ChecklistSopladoInterno = true,
                ChecklistLubricacion = true
            });

            var maintenance = db.Maintenances.Single(m => m.Id == id);
            Assert.Equal(MaintenanceCategory.Correctivo, maintenance.Category);
            Assert.False(db.MaintenanceChecklists.Any(c => c.MaintenanceId == id));
        }
    }
}
