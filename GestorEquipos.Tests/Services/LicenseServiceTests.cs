using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.License;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class LicenseServiceTests
    {
        private static Desktop SeedDesktop(Gestor_Equipos.Data.MyDbContext db)
        {
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.SaveChanges();

            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();
            return desktop;
        }

        [Fact]
        public async Task AddAsync_NoLicenseTrue_ForcesKeyNull()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = SeedDesktop(db);
            var service = new LicenseService(db);

            var id = await service.AddAsync(new LicenseCreateViewModel
            {
                DesktopId = desktop.Id,
                SoftwareType = "Windows 10",
                LicenseKey = "AAAA-BBBB",
                NoLicense = true
            });

            var license = db.Licenses.Single(l => l.Id == id);
            Assert.Null(license.LicenseKey);
            Assert.True(license.NoLicense);
        }

        [Fact]
        public async Task AddAsync_NoLicenseFalse_KeepsKey()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = SeedDesktop(db);
            var service = new LicenseService(db);

            var id = await service.AddAsync(new LicenseCreateViewModel
            {
                DesktopId = desktop.Id,
                SoftwareType = "Office 2016",
                LicenseKey = "CCCC-DDDD",
                NoLicense = false
            });

            var license = db.Licenses.Single(l => l.Id == id);
            Assert.Equal("CCCC-DDDD", license.LicenseKey);
        }

        [Fact]
        public async Task GetByDesktopAsync_ReturnsOnlyForThatDesktop()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop1 = SeedDesktop(db);
            var desktop2 = SeedDesktop(db);
            db.Licenses.Add(new License { DesktopId = desktop1.Id, SoftwareType = "A", NoLicense = false });
            db.Licenses.Add(new License { DesktopId = desktop2.Id, SoftwareType = "B", NoLicense = false });
            db.SaveChanges();

            var service = new LicenseService(db);
            var result = await service.GetByDesktopAsync(desktop1.Id);

            var item = Assert.Single(result);
            Assert.Equal("A", item.SoftwareType);
        }
    }
}
