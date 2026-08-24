using System.Text;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class MaintenancePdfServiceTests
    {
        private static MaintenancePdfViewModel BuildCorrectivo() => new()
        {
            DesktopName = "PC-001",
            SerialNumber = "SN-001",
            Brand = "Dell",
            Model = "OptiPlex",
            Date = new DateOnly(2026, 1, 15),
            Category = MaintenanceCategory.Correctivo,
            MaintenanceTypeName = "Correctivo",
            Description = "Cambio de fuente de poder",
            TechnicianName = "Carlos Ruiz"
        };

        private static MaintenancePdfViewModel BuildPreventivoConChecklistCompleto() => new()
        {
            DesktopName = "PC-002",
            SerialNumber = "SN-002",
            Brand = "HP",
            Model = "ProDesk",
            Date = new DateOnly(2026, 2, 1),
            Category = MaintenanceCategory.Preventivo,
            MaintenanceTypeName = "Preventivo",
            Description = "Mantenimiento preventivo trimestral",
            TechnicianName = "Ana Gomez",
            CurrentUserName = "Juan Perez",
            CurrentAreaName = "Sistemas",
            Equipment = new List<MaintenanceEquipmentRow>
            {
                new() { Label = "Desktop", Brand = "HP", Model = "ProDesk", Serial = "SN-002" },
                new() { Label = "Mouse", Brand = "Logitech", Model = "M100", Serial = "MS-1" },
                new() { Label = "Teclado", Brand = "Logitech", Model = "K120", Serial = "KB-1" }
            },
            Checklist = new List<MaintenanceChecklistItem>
            {
                new() { Label = "Verificación de sistema mecánico", Checked = true },
                new() { Label = "Verificación de sistema eléctrico", Checked = true },
                new() { Label = "Verificación de sistema de seguridad", Checked = true },
                new() { Label = "Soplado interno", Checked = true },
                new() { Label = "Lavado interno", Checked = true },
                new() { Label = "Lubricación", Checked = true },
                new() { Label = "Ajuste de tornillería", Checked = true },
                new() { Label = "Cambio de accesorios y repuestos por desgaste", Checked = true }
            }
        };

        private static MaintenancePdfViewModel BuildPreventivoConChecklistVacio() => new()
        {
            DesktopName = "PC-003",
            SerialNumber = "SN-003",
            Brand = "Lenovo",
            Model = "ThinkCentre",
            Date = new DateOnly(2026, 3, 1),
            Category = MaintenanceCategory.Preventivo,
            MaintenanceTypeName = "Preventivo",
            Description = "Mantenimiento preventivo sin hallazgos que marcar",
            TechnicianName = "Maria Lopez",
            CurrentUserName = "Sin asignar",
            CurrentAreaName = "-",
            Equipment = new List<MaintenanceEquipmentRow>
            {
                new() { Label = "Desktop", Brand = "Lenovo", Model = "ThinkCentre", Serial = "SN-003" }
            },
            Checklist = new List<MaintenanceChecklistItem>
            {
                new() { Label = "Verificación de sistema mecánico", Checked = false },
                new() { Label = "Verificación de sistema eléctrico", Checked = false },
                new() { Label = "Verificación de sistema de seguridad", Checked = false },
                new() { Label = "Soplado interno", Checked = false },
                new() { Label = "Lavado interno", Checked = false },
                new() { Label = "Lubricación", Checked = false },
                new() { Label = "Ajuste de tornillería", Checked = false },
                new() { Label = "Cambio de accesorios y repuestos por desgaste", Checked = false }
            }
        };

        private static MaintenancePdfViewModel BuildPreventivoSinPerifericos() => new()
        {
            DesktopName = "PC-004",
            SerialNumber = "SN-004",
            Brand = "Acer",
            Model = "Veriton",
            Date = new DateOnly(2026, 4, 1),
            Category = MaintenanceCategory.Preventivo,
            MaintenanceTypeName = "Preventivo",
            Description = "Mantenimiento preventivo sin periféricos activos",
            TechnicianName = "Pedro Diaz",
            CurrentUserName = "Sin asignar",
            CurrentAreaName = "-",
            Equipment = new List<MaintenanceEquipmentRow>(),
            Checklist = new List<MaintenanceChecklistItem>()
        };

        [Fact]
        public void GenerateMaintenancePdf_Correctivo_ReturnsValidPdfBytes()
        {
            var service = new MaintenancePdfService();

            var bytes = service.GenerateMaintenancePdf(BuildCorrectivo());

            Assert.NotEmpty(bytes);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }

        [Fact]
        public void GenerateMaintenancePdf_PreventivoConChecklistCompleto_ReturnsValidPdfBytes()
        {
            var service = new MaintenancePdfService();

            var bytes = service.GenerateMaintenancePdf(BuildPreventivoConChecklistCompleto());

            Assert.NotEmpty(bytes);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }

        [Fact]
        public void GenerateMaintenancePdf_PreventivoConChecklistVacio_ReturnsValidPdfBytes()
        {
            var service = new MaintenancePdfService();

            var bytes = service.GenerateMaintenancePdf(BuildPreventivoConChecklistVacio());

            Assert.NotEmpty(bytes);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }

        [Fact]
        public void GenerateMaintenancePdf_PreventivoSinPerifericos_ReturnsValidPdfBytes()
        {
            var service = new MaintenancePdfService();

            var bytes = service.GenerateMaintenancePdf(BuildPreventivoSinPerifericos());

            Assert.NotEmpty(bytes);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }
    }
}
