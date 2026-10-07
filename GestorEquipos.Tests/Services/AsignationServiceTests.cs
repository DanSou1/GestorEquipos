using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace GestorEquipos.Tests.Services
{
    public class AsignationServiceTests
    {
        [Fact]
        public async Task AssignAsync_AddsAsignationRow()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var user = TestHelpers.CreateUser(db, "Ana");
            var service = new AsignationService(db);

            await service.AssignAsync(desktop.Id, user.Id);

            var rows = await db.Asignations.Where(a => a.DesktopId == desktop.Id).ToListAsync();
            Assert.Single(rows);
            Assert.Equal(user.Id, rows[0].UserId);
        }

        [Fact]
        public async Task AssignAsync_ThrowsWhenDesktopNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var user = TestHelpers.CreateUser(db, "Ana");
            var service = new AsignationService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(9999, user.Id));
        }

        [Fact]
        public async Task AssignAsync_ThrowsWhenDesktopIsRetired()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db, estado: false);
            var user = TestHelpers.CreateUser(db, "Ana");
            var service = new AsignationService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(desktop.Id, user.Id));
        }

        [Fact]
        public async Task AssignAsync_AllowsRaesAssignmentEvenWhenDesktopRetired()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db, estado: false);
            var raesUser = TestHelpers.CreateReservedUser(db, AuthBootstrapper.RaesUserEmail);
            var service = new AsignationService(db);

            await service.AssignAsync(desktop.Id, raesUser.Id);

            var rows = await db.Asignations.Where(a => a.DesktopId == desktop.Id).ToListAsync();
            Assert.Single(rows);
            Assert.Equal(raesUser.Id, rows[0].UserId);
        }

        [Fact]
        public async Task AssignAsync_ReassignsDirectlyFromOneActiveUserToAnother()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var userA = TestHelpers.CreateUser(db, "AdministradorDeSistemas");
            var userB = TestHelpers.CreateUser(db, "Daniel");
            var service = new AsignationService(db);

            await service.AssignAsync(desktop.Id, userA.Id);
            var firstRow = await db.Asignations.SingleAsync(a => a.DesktopId == desktop.Id);

            var exception = await Record.ExceptionAsync(() => service.AssignAsync(desktop.Id, userB.Id));

            Assert.Null(exception);
            var rows = await db.Asignations.Where(a => a.DesktopId == desktop.Id).ToListAsync();
            Assert.Equal(2, rows.Count);

            var latest = rows.OrderByDescending(a => a.DateAsignation).ThenByDescending(a => a.Id).First();
            Assert.Equal(userB.Id, latest.UserId);

            var untouchedFirstRow = rows.Single(a => a.Id == firstRow.Id);
            Assert.Equal(userA.Id, untouchedFirstRow.UserId);
            Assert.Equal(firstRow.DateAsignation, untouchedFirstRow.DateAsignation);
        }

        [Fact]
        public async Task GetHistoryAsync_ShowsBothHoldersInOrderAfterDirectReassignment()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var userA = TestHelpers.CreateUser(db, "AdministradorDeSistemas");
            var userB = TestHelpers.CreateUser(db, "Daniel");
            var service = new AsignationService(db);

            await service.AssignAsync(desktop.Id, userA.Id);
            await service.AssignAsync(desktop.Id, userB.Id);

            var history = await service.GetHistoryAsync(desktop.Id);

            Assert.Equal(2, history.Count);
            Assert.Equal(userB.Id, history[0].UserId);
            Assert.Equal(userA.Id, history[1].UserId);
        }

        [Fact]
        public async Task AssignAsync_AllowsReassignToSameCurrentUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var user = TestHelpers.CreateUser(db, "Ana");
            var service = new AsignationService(db);

            await service.AssignAsync(desktop.Id, user.Id);
            await service.AssignAsync(desktop.Id, user.Id);

            var rows = await db.Asignations.Where(a => a.DesktopId == desktop.Id).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(user.Id, r.UserId));
        }

        [Fact]
        public async Task AssignAsync_AllowsAssignWhenCurrentHolderIsInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var inactiveHolder = TestHelpers.CreateUser(db, "Ana", activo: false);
            var newUser = TestHelpers.CreateUser(db, "Daniel");
            var service = new AsignationService(db);

            db.Asignations.Add(new GestorEquipos.Models.Asignation
            {
                DesktopId = desktop.Id,
                UserId = inactiveHolder.Id,
                DateAsignation = DateOnly.FromDateTime(DateTime.Now.AddDays(-1))
            });
            await db.SaveChangesAsync();

            var exception = await Record.ExceptionAsync(() => service.AssignAsync(desktop.Id, newUser.Id));

            Assert.Null(exception);
            var latest = (await service.GetHistoryAsync(desktop.Id))[0];
            Assert.Equal(newUser.Id, latest.UserId);
        }

        [Fact]
        public async Task AssignAsync_ThrowsWhenTargetUserIsInactive()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var inactiveUser = TestHelpers.CreateUser(db, "Ana", activo: false);
            var service = new AsignationService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(desktop.Id, inactiveUser.Id));
        }

        [Fact]
        public async Task ReturnAsync_ReleasesActiveHolderWithoutDeactivatingThem()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var user = TestHelpers.CreateUser(db, "Ana");
            TestHelpers.CreateReservedUser(db, AuthBootstrapper.UnassignedUserEmail);
            var service = new AsignationService(db);

            await service.AssignAsync(desktop.Id, user.Id);
            await service.ReturnAsync(desktop.Id);

            var refreshedUser = await db.Users.SingleAsync(u => u.Id == user.Id);
            Assert.True(refreshedUser.Activo);

            var history = await service.GetHistoryAsync(desktop.Id);
            Assert.Equal(2, history.Count);
            Assert.Equal(AuthBootstrapper.UnassignedUserEmail, history[0].User.Email);
        }

        [Fact]
        public async Task ReturnAsync_ThrowsWhenAlreadyDisponible()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var service = new AsignationService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReturnAsync(desktop.Id));
        }

        [Fact]
        public async Task GetHistoryAsync_ReturnsOrderedDescendingByDate()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var userA = TestHelpers.CreateUser(db, "Ana");
            var userB = TestHelpers.CreateUser(db, "Beto");
            var service = new AsignationService(db);

            db.Asignations.Add(new GestorEquipos.Models.Asignation
            {
                DesktopId = desktop.Id,
                UserId = userA.Id,
                DateAsignation = DateOnly.FromDateTime(DateTime.Now.AddDays(-2))
            });
            db.Asignations.Add(new GestorEquipos.Models.Asignation
            {
                DesktopId = desktop.Id,
                UserId = userB.Id,
                DateAsignation = DateOnly.FromDateTime(DateTime.Now.AddDays(-1))
            });
            await db.SaveChangesAsync();

            var history = await service.GetHistoryAsync(desktop.Id);

            Assert.Equal(userB.Id, history[0].UserId);
            Assert.Equal(userA.Id, history[1].UserId);
        }
    }
}
