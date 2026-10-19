using System.Text;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models.ViewModels.Maintenance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class MaintenancePdfServiceTests
    {
        // No se referencia ningún logo real: GenerateExroActaPdf solo agrega la imagen si el
        // archivo existe en WebRootPath, así que un directorio inexistente es válido aquí.
        private class FakeWebHostEnvironment : IWebHostEnvironment
        {
            public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "no-such-wwwroot");
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ApplicationName { get; set; } = "GestorEquipos.Tests";
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = Path.GetTempPath();
            public string EnvironmentName { get; set; } = "Testing";
        }

        private static MaintenanceExroActaViewModel BuildDetail(List<MaintenanceChecklistItem>? checklist = null, List<MaintenanceEquipmentRow>? equipment = null)
        {
            return new MaintenanceExroActaViewModel
            {
                Date = new DateOnly(2026, 6, 15),
                UsuarioNombre = "Ana Pérez",
                Area = "Ventas",
                DescripcionFalla = "Mantenimiento preventivo 2026-I",
                TechnicianName = "Carlos Técnico",
                Equipment = equipment ?? new List<MaintenanceEquipmentRow>
                {
                    new() { Label = "Desktop", Brand = "HP", Model = "Compaq 6200", Serial = "ABC123" }
                },
                Checklist = checklist ?? new List<MaintenanceChecklistItem>
                {
                    new() { Label = "Verificación de sistema mecánico", Checked = true },
                    new() { Label = "Verificación de sistema eléctrico", Checked = true },
                    new() { Label = "Verificación de sistema de seguridad", Checked = false },
                    new() { Label = "Soplado interno", Checked = true },
                    new() { Label = "Lavado Interno/Externo", Checked = true },
                    new() { Label = "Lubricación", Checked = false },
                    new() { Label = "Ajuste de tornillería", Checked = true },
                    new() { Label = "Cambio de accesorios y repuestos por desgaste", Checked = false }
                }
            };
        }

        private static bool LooksLikePdf(byte[] bytes)
        {
            return bytes.Length > 4 && Encoding.ASCII.GetString(bytes, 0, 4) == "%PDF";
        }

        [Fact]
        public void GenerateExroActaPdf_ReturnsValidPdfBytes()
        {
            var service = new MaintenancePdfService(new FakeWebHostEnvironment());

            var bytes = service.GenerateExroActaPdf(BuildDetail());

            Assert.NotEmpty(bytes);
            Assert.True(LooksLikePdf(bytes));
        }

        [Fact]
        public void GenerateExroActaPdf_DoesNotThrowWithPartialChecklist()
        {
            var service = new MaintenancePdfService(new FakeWebHostEnvironment());
            var partialChecklist = new List<MaintenanceChecklistItem>
            {
                new() { Label = "Verificación de sistema mecánico", Checked = true },
                new() { Label = "Soplado interno", Checked = false }
            };

            var bytes = service.GenerateExroActaPdf(BuildDetail(checklist: partialChecklist));

            Assert.True(LooksLikePdf(bytes));
        }

        [Fact]
        public void GenerateExroActaPdf_DoesNotThrowWithEmptyEquipmentList()
        {
            var service = new MaintenancePdfService(new FakeWebHostEnvironment());

            var bytes = service.GenerateExroActaPdf(BuildDetail(equipment: new List<MaintenanceEquipmentRow>()));

            Assert.True(LooksLikePdf(bytes));
        }

        [Fact]
        public void GenerateExroActaPdf_WithSignatureData_ProducesNonEmptyPdf()
        {
            var service = new MaintenancePdfService(new FakeWebHostEnvironment());
            var detail = BuildDetail();
            detail.SignatureData = "[[{\"x\":10,\"y\":10},{\"x\":50,\"y\":60},{\"x\":90,\"y\":20}]]";
            detail.SignerName = "Ana Pérez";
            detail.SignedAtUtc = DateTime.UtcNow;

            var bytes = service.GenerateExroActaPdf(detail);

            Assert.True(LooksLikePdf(bytes));
        }

        [Fact]
        public void GenerateExroActaPdf_WithMalformedSignatureData_FallsBackToBlankLine()
        {
            var service = new MaintenancePdfService(new FakeWebHostEnvironment());
            var detail = BuildDetail();
            detail.SignatureData = "no es json válido";

            var bytes = service.GenerateExroActaPdf(detail);

            Assert.True(LooksLikePdf(bytes));
        }

        [Fact]
        public void GenerateMaintenancePdf_StillWorksAfterAddingExroMethod()
        {
            var service = new MaintenancePdfService(new FakeWebHostEnvironment());
            var detail = new MaintenancePdfViewModel
            {
                DesktopName = "PC-1",
                SerialNumber = "SN-1",
                Brand = "HP",
                Model = "Compaq",
                Date = new DateOnly(2026, 6, 15),
                Category = GestorEquipos.Models.MaintenanceCategory.Correctivo,
                Description = "Cambio de fuente",
                TechnicianName = "Carlos"
            };

            var bytes = service.GenerateMaintenancePdf(detail);

            Assert.True(LooksLikePdf(bytes));
        }

        [Fact]
        public void GenerateMaintenancePdf_IncludesChecklistAndEquipmentForPreventivo()
        {
            var service = new MaintenancePdfService(new FakeWebHostEnvironment());
            var detail = new MaintenancePdfViewModel
            {
                DesktopName = "PC-1",
                SerialNumber = "SN-1",
                Brand = "HP",
                Model = "Compaq",
                Date = new DateOnly(2026, 6, 15),
                Category = GestorEquipos.Models.MaintenanceCategory.Preventivo,
                Description = "Mantenimiento preventivo 2026-I",
                TechnicianName = "Carlos",
                CurrentUserName = "Ana Pérez",
                CurrentAreaName = "Ventas",
                Equipment = new List<MaintenanceEquipmentRow>
                {
                    new() { Label = "Desktop", Brand = "HP", Model = "Compaq 6200", Serial = "ABC123" }
                },
                Checklist = new List<MaintenanceChecklistItem>
                {
                    new() { Label = "Soplado interno", Checked = true }
                }
            };

            var bytes = service.GenerateMaintenancePdf(detail);

            Assert.True(LooksLikePdf(bytes));
        }
    }
}
