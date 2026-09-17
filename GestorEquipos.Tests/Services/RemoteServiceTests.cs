using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Remote;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class RemoteServiceTests
    {
        private static (OSVersion os, Ram ram) SeedLookups(Gestor_Equipos.Data.MyDbContext db)
        {
            var os = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "8GB" };
            db.OSVersions.Add(os);
            db.Rams.Add(ram);
            db.SaveChanges();
            return (os, ram);
        }

        private static int SeedAdminUserSystem(Gestor_Equipos.Data.MyDbContext db, string username, string password)
        {
            var area = new Area { Name = $"A-{username}" };
            var regional = new Regional { Name = $"R-{username}" };
            var role = new Rol { Name = "Administrador" };
            db.Areas.Add(area);
            db.Regionals.Add(regional);
            db.Rols.Add(role);
            db.SaveChanges();

            var user = new Users { Name = "Admin", LastName = "User", Email = $"{username}@empresa.com", EmailTeams = $"{username}@empresa.com", AreaId = area.Id, RegionalId = regional.Id };
            db.Users.Add(user);
            db.SaveChanges();

            var hasher = new PasswordHasher<UserSystem>();
            var userSystem = new UserSystem { Username = username, UserId = user.Id, RolId = role.Id, PasswordHash = string.Empty };
            userSystem.PasswordHash = hasher.HashPassword(userSystem, password);
            db.UserSystems.Add(userSystem);
            db.SaveChanges();

            return userSystem.Id;
        }

        private static Desktop SeedDesktop(Gestor_Equipos.Data.MyDbContext db, OSVersion os, Ram ram, string name, int? remoteId = null)
        {
            var desktop = new Desktop
            {
                NameDesktop = name,
                SerialNumber = $"SN-{name}",
                Brand = "Dell",
                Model = "OptiPlex",
                Processor = "i5",
                Disk = "256GB SSD",
                OSVersionId = os.Id,
                RamId = ram.Id,
                RamType = RamType.DDR4,
                Estado = true,
                RemoteId = remoteId
            };
            db.Desktops.Add(desktop);
            db.SaveChanges();
            return desktop;
        }

        private static RemoteService CreateService(Gestor_Equipos.Data.MyDbContext db) => new(db, new PasswordHasher<UserSystem>());

        [Fact]
        public async Task GetInventoryAsync_RemoteWithNoDesktop_IsDisponible()
        {
            using var db = TestHelpers.CreateDbContext();
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();

            var service = CreateService(db);
            var items = await service.GetInventoryAsync();

            var item = Assert.Single(items);
            Assert.Equal("Disponible", item.Estado);
            Assert.Empty(item.AssignedDesktops);
        }

        [Fact]
        public async Task GetInventoryAsync_RemoteWithOneDesktop_IsAsignadoWithDesktopName()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.EscritorioRemotoWindows, IPAddress = "10.0.0.5", Port = "3389", Username = "PC01\\jperez", Password = "clave123" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            SeedDesktop(db, os, ram, "PC-001", remote.Id);

            var service = CreateService(db);
            var items = await service.GetInventoryAsync();

            var item = Assert.Single(items);
            Assert.Equal("Asignado", item.Estado);
            var desktop = Assert.Single(item.AssignedDesktops);
            Assert.Equal("PC-001", desktop.Name);
        }

        [Fact]
        public async Task GetInventoryAsync_RemoteSharedByTwoDesktops_ListsBothDesktopNames()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "Anydesk" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            SeedDesktop(db, os, ram, "PC-001", remote.Id);
            SeedDesktop(db, os, ram, "PC-002", remote.Id);

            var service = CreateService(db);
            var items = await service.GetInventoryAsync();

            var item = Assert.Single(items);
            Assert.Equal("Asignado", item.Estado);
            Assert.Equal(2, item.AssignedDesktops.Count);
            Assert.Contains(item.AssignedDesktops, d => d.Name == "PC-001");
            Assert.Contains(item.AssignedDesktops, d => d.Name == "PC-002");
        }

        [Fact]
        public async Task GetInventoryStatsAsync_ComputesCorrectCounts()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var assignedRemote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            var freeRemote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "Zoom" };
            db.Remotes.AddRange(assignedRemote, freeRemote);
            db.SaveChanges();
            SeedDesktop(db, os, ram, "PC-001", assignedRemote.Id);

            var service = CreateService(db);
            var stats = await service.GetInventoryStatsAsync();

            Assert.Equal(2, stats.Total);
            Assert.Equal(1, stats.Asignados);
            Assert.Equal(1, stats.Disponibles);
        }

        [Fact]
        public async Task GetDetailAsync_ReturnsNullForMissingId()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var detail = await service.GetDetailAsync(999);

            Assert.Null(detail);
        }

        [Fact]
        public async Task GetDetailAsync_ReturnsFullDetailIncludingAllLinkedDesktops()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.EscritorioRemotoWindows, IPAddress = "10.0.0.5", Port = "3389", Username = "PC01\\jperez", Password = "clave123" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            SeedDesktop(db, os, ram, "PC-001", remote.Id);
            SeedDesktop(db, os, ram, "PC-002", remote.Id);

            var service = CreateService(db);
            var detail = await service.GetDetailAsync(remote.Id);

            Assert.NotNull(detail);
            Assert.Equal("clave123", detail!.Password);
            Assert.Equal("Asignado", detail.Estado);
            Assert.Equal(2, detail.AssignedDesktops.Count);
        }

        [Fact]
        public async Task UpdateAsync_ChangesAplicativoToRdp_ClearsAppDescriptionAndSetsRdpFields()
        {
            using var db = TestHelpers.CreateDbContext();
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();

            var service = CreateService(db);
            var vm = new RemoteEditViewModel
            {
                ConnectionType = RemoteConnectionType.EscritorioRemotoWindows,
                IPAddress = "10.0.0.9",
                Port = "3389",
                Username = "PC09\\ana",
                Password = "clave9"
            };

            await service.UpdateAsync(remote.Id, vm, -1);

            var updated = db.Remotes.Single(r => r.Id == remote.Id);
            Assert.Equal(RemoteConnectionType.EscritorioRemotoWindows, updated.ConnectionType);
            Assert.Null(updated.AppDescription);
            Assert.Equal("10.0.0.9", updated.IPAddress);
            Assert.Equal("3389", updated.Port);
            Assert.Equal("PC09\\ana", updated.Username);
            Assert.Equal("clave9", updated.Password);
        }

        [Fact]
        public async Task UpdateAsync_ChangesRdpToAplicativo_ClearsRdpFieldsAndSetsAppDescription()
        {
            using var db = TestHelpers.CreateDbContext();
            var remote = new Remote { ConnectionType = RemoteConnectionType.EscritorioRemotoWindows, IPAddress = "10.0.0.5", Port = "3389", Username = "PC01\\jperez", Password = "clave123" };
            db.Remotes.Add(remote);
            db.SaveChanges();

            var service = CreateService(db);
            var vm = new RemoteEditViewModel
            {
                ConnectionType = RemoteConnectionType.Aplicativo,
                AppDescription = "Anydesk"
            };

            await service.UpdateAsync(remote.Id, vm, -1);

            var updated = db.Remotes.Single(r => r.Id == remote.Id);
            Assert.Equal(RemoteConnectionType.Aplicativo, updated.ConnectionType);
            Assert.Equal("Anydesk", updated.AppDescription);
            Assert.Null(updated.IPAddress);
            Assert.Null(updated.Port);
            Assert.Null(updated.Username);
            Assert.Null(updated.Password);
        }

        [Fact]
        public async Task UpdateAsync_MissingRemote_ThrowsInvalidOperationException()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var vm = new RemoteEditViewModel { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "Zoom" };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(999, vm, -1));
        }

        [Fact]
        public async Task UpdateAsync_WithDesktopId_AssignsRemoteToDesktop()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram, "PC-001");

            var service = CreateService(db);
            var vm = new RemoteEditViewModel { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI", DesktopId = desktop.Id };

            await service.UpdateAsync(remote.Id, vm, -1);

            Assert.Equal(remote.Id, db.Desktops.Single(d => d.Id == desktop.Id).RemoteId);
        }

        [Fact]
        public async Task UpdateAsync_TargetDesktopRetired_ThrowsAndDoesNotLink()
        {
            // LOG-04: assigning a Remote to a RAES-retired desktop should be rejected server-side.
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram, "PC-Retired");
            desktop.Estado = false;
            db.SaveChanges();

            var service = CreateService(db);
            var vm = new RemoteEditViewModel { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI", DesktopId = desktop.Id };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(remote.Id, vm, -1));
            Assert.Null(db.Desktops.Single(d => d.Id == desktop.Id).RemoteId);
        }

        [Fact]
        public async Task UpdateAsync_WithNullDesktopId_ReleasesCurrentlyLinkedDesktops()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram, "PC-001", remote.Id);

            var service = CreateService(db);
            var vm = new RemoteEditViewModel { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI", DesktopId = null };

            await service.UpdateAsync(remote.Id, vm, -1);

            Assert.Null(db.Desktops.Single(d => d.Id == desktop.Id).RemoteId);
        }

        [Fact]
        public async Task UpdateAsync_SwitchingDesktopId_ReleasesPreviousAndAssignsNew()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            var oldDesktop = SeedDesktop(db, os, ram, "PC-001", remote.Id);
            var newDesktop = SeedDesktop(db, os, ram, "PC-002");

            var service = CreateService(db);
            var vm = new RemoteEditViewModel { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI", DesktopId = newDesktop.Id };

            await service.UpdateAsync(remote.Id, vm, -1);

            Assert.Null(db.Desktops.Single(d => d.Id == oldDesktop.Id).RemoteId);
            Assert.Equal(remote.Id, db.Desktops.Single(d => d.Id == newDesktop.Id).RemoteId);
        }

        [Fact]
        public async Task UpdateAsync_DesktopAlreadyLinkedToAnotherRemote_ThrowsAndPersistsNoChanges()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var otherRemote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "Zoom" };
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.AddRange(otherRemote, remote);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram, "PC-001", otherRemote.Id);

            var service = CreateService(db);
            var vm = new RemoteEditViewModel { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI", DesktopId = desktop.Id };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(remote.Id, vm, -1));

            Assert.Equal(otherRemote.Id, db.Desktops.Single(d => d.Id == desktop.Id).RemoteId);
            Assert.Equal("SAP GUI", db.Remotes.Single(r => r.Id == remote.Id).AppDescription);
        }

        [Fact]
        public async Task UpdateAsync_DesktopIdNotFound_ThrowsInvalidOperationException()
        {
            using var db = TestHelpers.CreateDbContext();
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();

            var service = CreateService(db);
            var vm = new RemoteEditViewModel { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI", DesktopId = 999 };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(remote.Id, vm, -1));
        }

        [Fact]
        public async Task ReleaseAsync_ClearsRemoteIdOnAllLinkedDesktops()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            var d1 = SeedDesktop(db, os, ram, "PC-001", remote.Id);
            var d2 = SeedDesktop(db, os, ram, "PC-002", remote.Id);

            var service = CreateService(db);
            await service.ReleaseAsync(remote.Id, -1);

            Assert.Null(db.Desktops.Single(d => d.Id == d1.Id).RemoteId);
            Assert.Null(db.Desktops.Single(d => d.Id == d2.Id).RemoteId);
            Assert.Contains(db.Remotes, r => r.Id == remote.Id);
            // SEG-09: releasing a remote must leave an audit trail entry.
            Assert.Contains(db.AuditLogEntries, e => e.Action == "RemoteReleased");
        }

        [Fact]
        public async Task ReleaseAsync_NoLinkedDesktops_IsNoop()
        {
            using var db = TestHelpers.CreateDbContext();
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();

            var service = CreateService(db);
            await service.ReleaseAsync(remote.Id, -1);

            Assert.Contains(db.Remotes, r => r.Id == remote.Id);
        }

        [Fact]
        public async Task ReleaseAsync_MissingRemote_ThrowsInvalidOperationException()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReleaseAsync(999, -1));
        }

        [Fact]
        public async Task AssignToDesktopAsync_SetsRemoteIdOnUnassignedDesktop()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram, "PC-001");

            var service = CreateService(db);
            await service.AssignToDesktopAsync(remote.Id, desktop.Id, -1);

            Assert.Equal(remote.Id, db.Desktops.Single(d => d.Id == desktop.Id).RemoteId);
        }

        [Fact]
        public async Task AssignToDesktopAsync_TargetDesktopRetired_ThrowsAndDoesNotLink()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram, "PC-Retired");
            desktop.Estado = false;
            db.SaveChanges();

            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignToDesktopAsync(remote.Id, desktop.Id, -1));
            Assert.Null(db.Desktops.Single(d => d.Id == desktop.Id).RemoteId);
        }

        [Fact]
        public async Task AssignToDesktopAsync_DesktopAlreadyHasRemote_ThrowsInvalidOperationException()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var existingRemote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "Zoom" };
            var newRemote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.AddRange(existingRemote, newRemote);
            db.SaveChanges();
            var desktop = SeedDesktop(db, os, ram, "PC-001", existingRemote.Id);

            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignToDesktopAsync(newRemote.Id, desktop.Id, -1));
        }

        [Fact]
        public async Task AssignToDesktopAsync_MissingDesktop_ThrowsInvalidOperationException()
        {
            using var db = TestHelpers.CreateDbContext();
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();

            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignToDesktopAsync(remote.Id, 999, -1));
        }

        [Fact]
        public async Task AssignToDesktopAsync_MissingRemote_ThrowsInvalidOperationException()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var desktop = SeedDesktop(db, os, ram, "PC-001");

            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignToDesktopAsync(999, desktop.Id, -1));
        }

        [Fact]
        public async Task DeleteAsync_RemovesRemote_WhenPasswordCorrectAndUnassigned()
        {
            using var db = TestHelpers.CreateDbContext();
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-ok", "AdminPass1!");

            var service = CreateService(db);
            await service.DeleteAsync(remote.Id, adminUserSystemId, "AdminPass1!");

            Assert.Empty(db.Remotes.Where(r => r.Id == remote.Id));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAssignedToDesktop_AndPersistsRemote()
        {
            using var db = TestHelpers.CreateDbContext();
            var (os, ram) = SeedLookups(db);
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            SeedDesktop(db, os, ram, "PC-001", remote.Id);
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-inuse", "AdminPass1!");

            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(remote.Id, adminUserSystemId, "AdminPass1!"));

            Assert.NotNull(db.Remotes.SingleOrDefault(r => r.Id == remote.Id));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordIncorrect_AndPersistsRemote()
        {
            using var db = TestHelpers.CreateDbContext();
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "Zoom" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-wrongpw", "AdminPass1!");

            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(remote.Id, adminUserSystemId, "IncorrectPassword!"));

            Assert.NotNull(db.Remotes.SingleOrDefault(r => r.Id == remote.Id));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenAdminPasswordMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var remote = new Remote { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "Anydesk" };
            db.Remotes.Add(remote);
            db.SaveChanges();
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-nopw", "AdminPass1!");

            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(remote.Id, adminUserSystemId, null));
        }

        [Fact]
        public async Task DeleteAsync_MissingRemote_ThrowsInvalidOperationException()
        {
            using var db = TestHelpers.CreateDbContext();
            var adminUserSystemId = SeedAdminUserSystem(db, "admin-missing", "AdminPass1!");
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteAsync(999, adminUserSystemId, "AdminPass1!"));
        }
    }
}
