using System.Text;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models.ViewModels.Desktop;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class DesktopPdfServiceTests
    {
        private static DesktopDetailViewModel BuildFullDetail() => new()
        {
            Id = 1,
            NameDesktop = "PC-001",
            SerialNumber = "SN-001",
            Brand = "Dell",
            Model = "OptiPlex",
            Processor = "i5",
            Disk = "256GB SSD",
            OSVersionName = "Windows 11",
            RamSpecification = "8GB",
            RemoteInfo = "10.0.0.1:3389",
            Estado = true,
            CurrentUserName = "Juan Perez",
            CurrentAreaName = "Sistemas",
            CurrentRegionalName = "Bogota",
            AsignationHistory = new List<AsignationHistoryItem>
            {
                new() { UserName = "Juan Perez", DateAsignation = new DateOnly(2025, 1, 10) }
            },
            Peripherals = new List<PeripheralDetailItem>
            {
                new()
                {
                    Id = 1,
                    TypeName = "Mouse",
                    Brand = "Logitech",
                    Model = "M100",
                    Serial = "MS-1",
                    Estado = "Activo",
                    Observations = new List<PeripheralObservationItem>
                    {
                        new() { Date = new DateOnly(2025, 2, 1), Type = "Reparacion", Description = "Cambio de cable" }
                    }
                }
            },
            Maintenances = new List<MaintenanceHistoryItem>
            {
                new() { Date = new DateOnly(2025, 3, 1), MaintenanceTypeName = "Preventivo", Description = "Limpieza", TechnicianName = "Ana Gomez" }
            },
            Licenses = new List<LicenseItem>
            {
                new() { SoftwareType = "Windows 11", LicenseKey = "AAAA-BBBB", NoLicense = false },
                new() { SoftwareType = "Office", LicenseKey = null, NoLicense = true }
            },
            SpecChangeLogs = new List<SpecChangeLogItem>
            {
                new() { Date = new DateOnly(2025, 4, 1), FieldName = "RAM", OldValue = "4GB", NewValue = "8GB", ChangedByName = "Admin" }
            }
        };

        private static DesktopDetailViewModel BuildEmptyDetail() => new()
        {
            Id = 2,
            NameDesktop = "PC-002",
            SerialNumber = "SN-002",
            Brand = "HP",
            Model = "ProDesk",
            Processor = "i3",
            Disk = "128GB SSD",
            OSVersionName = "Windows 10",
            RamSpecification = "4GB",
            RemoteInfo = null,
            Estado = false,
            CurrentUserName = "Sin asignar",
            CurrentAreaName = "-",
            CurrentRegionalName = "-"
        };

        [Fact]
        public void GenerateHojaDeVidaPdf_WithData_ReturnsValidPdfBytes()
        {
            var service = new DesktopPdfService();

            var bytes = service.GenerateHojaDeVidaPdf(BuildFullDetail());

            Assert.NotEmpty(bytes);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }

        [Fact]
        public void GenerateHojaDeVidaPdf_WithEmptyCollections_ReturnsValidPdfBytes()
        {
            var service = new DesktopPdfService();

            var bytes = service.GenerateHojaDeVidaPdf(BuildEmptyDetail());

            Assert.NotEmpty(bytes);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }
    }
}
