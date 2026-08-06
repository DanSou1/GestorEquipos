using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class AsignationServiceTests
    {
        [Fact]
        public async Task AssignAsync_AddsAsignationRow()
        {
            using var db = TestHelpers.CreateDbContext();
            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.SaveChanges();

            var user = new Users { Name = "Ana", LastName = "Gomez", Email = "ana@x.com", EmailTeams = "ana@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();

            var service = new AsignationService(db);
            await service.AssignAsync(desktop.Id, user.Id);

            var asignation = Assert.Single(db.Asignations);
            Assert.Equal(desktop.Id, asignation.DesktopId);
            Assert.Equal(user.Id, asignation.UserId);
        }

        [Fact]
        public async Task GetHistoryAsync_ReturnsOrderedDescendingByDate()
        {
            using var db = TestHelpers.CreateDbContext();
            var area = new Area { Name = "A" };
            var regional = new Regional { Name = "R" };
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.SaveChanges();

            var user1 = new Users { Name = "Ana", LastName = "Gomez", Email = "ana@x.com", EmailTeams = "ana@x.com", AreaId = area.Id, RegionalId = regional.Id };
            var user2 = new Users { Name = "Luis", LastName = "Diaz", Email = "luis@x.com", EmailTeams = "luis@x.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.AddRange(user1, user2);
            var desktop = new Desktop { NameDesktop = "PC", SerialNumber = "SN", Brand = "B", Model = "M", Processor = "P", Disk = "D", OSVersionId = os.Id, RamId = ram.Id };
            db.Desktops.Add(desktop);
            db.SaveChanges();

            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user1.Id, DateAsignation = new DateOnly(2026, 1, 1) });
            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user2.Id, DateAsignation = new DateOnly(2026, 5, 1) });
            db.SaveChanges();

            var service = new AsignationService(db);
            var history = await service.GetHistoryAsync(desktop.Id);

            Assert.Equal(2, history.Count);
            Assert.Equal(user2.Id, history[0].UserId);
            Assert.Equal(user1.Id, history[1].UserId);
        }
    }
}
