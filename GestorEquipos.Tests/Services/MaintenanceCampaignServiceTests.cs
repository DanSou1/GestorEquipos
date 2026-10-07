using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class MaintenanceCampaignServiceTests
    {
        private static readonly DateOnly WithinJuneWindow = new(2026, 6, 15);
        private static readonly DateOnly WithinDecemberWindow = new(2026, 12, 15);

        private class FakePdfService : IMaintenancePdfService
        {
            public byte[] GenerateMaintenancePdf(MaintenancePdfViewModel detail) => new byte[] { 1, 2, 3 };
            public byte[] GenerateExroActaPdf(MaintenanceExroActaViewModel detail) => new byte[] { 1, 2, 3 };
        }

        private class RecordingEmailService : IEmailService
        {
            public List<(string ToEmail, string ToName, string Subject)> Sent { get; } = new();
            public bool ThrowOnSend { get; set; }

            public Task SendAsync(string toEmail, string toName, string subject, string bodyHtml,
                IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default)
            {
                if (ThrowOnSend)
                {
                    throw new InvalidOperationException("SMTP falló (simulado).");
                }

                Sent.Add((toEmail, toName, subject));
                return Task.CompletedTask;
            }
        }

        private static MaintenanceCampaignService CreateService(MyDbContext db, RecordingEmailService? emailService = null)
        {
            return new MaintenanceCampaignService(db, new MaintenanceService(db), new FakePdfService(), emailService ?? new RecordingEmailService());
        }

        private static void Assign(MyDbContext db, Desktop desktop, Users user)
        {
            db.Asignations.Add(new Asignation { DesktopId = desktop.Id, UserId = user.Id, DateAsignation = DateOnly.FromDateTime(DateTime.Now) });
            db.SaveChanges();
        }

        [Fact]
        public async Task SyncOpenPeriodsAsync_CreatesPendingOnlyForActiveDesktops()
        {
            using var db = TestHelpers.CreateDbContext();
            var activeDesktop = TestHelpers.CreateDesktop(db, "SN-ACTIVE");
            var retiredDesktop = TestHelpers.CreateDesktop(db, "SN-RETIRED", estado: false);
            var service = CreateService(db);

            await service.SyncOpenPeriodsAsync(WithinJuneWindow);

            var items = await db.MaintenanceCampaignItems.ToListAsync();
            Assert.Single(items);
            Assert.Equal(activeDesktop.Id, items[0].DesktopId);
            Assert.Equal(6, items[0].PeriodMonth);
            Assert.Equal(MaintenanceCampaignStatus.Pendiente, items[0].Status);
        }

        [Fact]
        public async Task SyncOpenPeriodsAsync_DoesNotOpenDecemberBeforeItStarts()
        {
            using var db = TestHelpers.CreateDbContext();
            TestHelpers.CreateDesktop(db);
            var service = CreateService(db);

            await service.SyncOpenPeriodsAsync(WithinJuneWindow);

            var items = await db.MaintenanceCampaignItems.ToListAsync();
            Assert.Single(items);
            Assert.Equal(6, items[0].PeriodMonth);
        }

        [Fact]
        public async Task SyncOpenPeriodsAsync_OpensDecemberOnceItStarts_WithoutDuplicatingJune()
        {
            using var db = TestHelpers.CreateDbContext();
            TestHelpers.CreateDesktop(db);
            var service = CreateService(db);

            await service.SyncOpenPeriodsAsync(WithinJuneWindow);
            await service.SyncOpenPeriodsAsync(WithinDecemberWindow);

            var items = await db.MaintenanceCampaignItems.ToListAsync();
            Assert.Equal(2, items.Count);
            Assert.Contains(items, i => i.PeriodMonth == 6);
            Assert.Contains(items, i => i.PeriodMonth == 12);
        }

        [Fact]
        public async Task SyncOpenPeriodsAsync_IsIdempotent()
        {
            using var db = TestHelpers.CreateDbContext();
            TestHelpers.CreateDesktop(db);
            var service = CreateService(db);

            await service.SyncOpenPeriodsAsync(WithinJuneWindow);
            await service.SyncOpenPeriodsAsync(WithinJuneWindow);

            Assert.Single(await db.MaintenanceCampaignItems.ToListAsync());
        }

        [Fact]
        public async Task SyncOpenPeriodsAsync_DoesNotDuplicateAnAlreadyCompletedItem()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            db.MaintenanceCampaignItems.Add(new MaintenanceCampaignItem
            {
                DesktopId = desktop.Id,
                PeriodYear = 2026,
                PeriodMonth = 6,
                Status = MaintenanceCampaignStatus.Realizado,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            var service = CreateService(db);

            await service.SyncOpenPeriodsAsync(WithinJuneWindow);

            var items = await db.MaintenanceCampaignItems.ToListAsync();
            Assert.Single(items);
            Assert.Equal(MaintenanceCampaignStatus.Realizado, items[0].Status);
        }

        [Fact]
        public async Task GetIndexAsync_OnlyCompletedReturnsOnlyRealizado()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            db.MaintenanceCampaignItems.AddRange(
                new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Pendiente, CreatedAtUtc = DateTime.UtcNow },
                new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2025, PeriodMonth = 12, Status = MaintenanceCampaignStatus.CorreoEnviado, CreatedAtUtc = DateTime.UtcNow },
                new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2025, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Realizado, CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var service = CreateService(db);

            var auditorView = await service.GetIndexAsync(onlyCompleted: true);
            Assert.Empty(auditorView.Pendientes);
            Assert.Empty(auditorView.CorreoEnviado);
            Assert.Single(auditorView.Realizados);

            var fullView = await service.GetIndexAsync(onlyCompleted: false);
            Assert.Single(fullView.Pendientes);
            Assert.Single(fullView.CorreoEnviado);
            Assert.Single(fullView.Realizados);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsItemWithDesktopIncluded()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Pendiente, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db);

            var result = await service.GetByIdAsync(item.Id);

            Assert.NotNull(result);
            Assert.Equal(desktop.NameDesktop, result!.Desktop.NameDesktop);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNullWhenNotFound()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var result = await service.GetByIdAsync(9999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetIndexAsync_KeepsRealizadoHistoryForRetiredDesktop()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db, estado: false);
            db.MaintenanceCampaignItems.Add(new MaintenanceCampaignItem
            {
                DesktopId = desktop.Id,
                PeriodYear = 2025,
                PeriodMonth = 6,
                Status = MaintenanceCampaignStatus.Realizado,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            var service = CreateService(db);

            var result = await service.GetIndexAsync(onlyCompleted: true);

            Assert.Single(result.Realizados);
        }

        [Fact]
        public async Task GetIndexAsync_FlagsOldPendingPeriodAsAtrasado()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            db.MaintenanceCampaignItems.Add(new MaintenanceCampaignItem
            {
                DesktopId = desktop.Id,
                PeriodYear = 2020,
                PeriodMonth = 6,
                Status = MaintenanceCampaignStatus.Pendiente,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            var service = CreateService(db);

            var result = await service.GetIndexAsync(onlyCompleted: false);

            Assert.True(result.Pendientes.Single().EsAtrasado);
        }

        [Fact]
        public async Task CompleteAndSendAsync_ThrowsWhenItemNotPendiente()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.CorreoEnviado, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteAndSendAsync(item.Id, new MaintenanceCreateViewModel()));
        }

        [Fact]
        public async Task CompleteAndSendAsync_ThrowsWhenNoAssignedUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Pendiente, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteAndSendAsync(item.Id, new MaintenanceCreateViewModel { Date = WithinJuneWindow, Description = "x", TechnicianName = "Tec" }));
        }

        [Fact]
        public async Task CompleteAndSendAsync_ThrowsWhenHolderIsDisponible()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var disponible = TestHelpers.CreateReservedUser(db, "sin-asignar@system.local");
            Assign(db, desktop, disponible);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Pendiente, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteAndSendAsync(item.Id, new MaintenanceCreateViewModel { Date = WithinJuneWindow, Description = "x", TechnicianName = "Tec" }));
        }

        [Fact]
        public async Task CompleteAndSendAsync_HappyPath_CreatesMaintenanceSendsEmailAndMarksCorreoEnviado()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var user = TestHelpers.CreateUser(db, "Ana", email: "ana@empresa.local");
            Assign(db, desktop, user);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Pendiente, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var emailService = new RecordingEmailService();
            var service = CreateService(db, emailService);

            var maintenanceId = await service.CompleteAndSendAsync(item.Id, new MaintenanceCreateViewModel
            {
                Date = WithinJuneWindow,
                Description = "Mantenimiento preventivo semestral",
                TechnicianName = "Carlos Técnico"
            });

            var reloaded = await db.MaintenanceCampaignItems.SingleAsync(c => c.Id == item.Id);
            Assert.Equal(MaintenanceCampaignStatus.CorreoEnviado, reloaded.Status);
            Assert.Equal(maintenanceId, reloaded.MaintenanceId);
            Assert.Equal("ana@empresa.local", reloaded.EmailSentToAddress);
            Assert.NotNull(reloaded.EmailSentAtUtc);

            var maintenance = await db.Maintenances.SingleAsync(m => m.Id == maintenanceId);
            Assert.Equal(MaintenanceCategory.Preventivo, maintenance.Category);
            Assert.Equal(desktop.Id, maintenance.DesktopId);

            Assert.Single(emailService.Sent);
            Assert.Equal("ana@empresa.local", emailService.Sent[0].ToEmail);
        }

        [Fact]
        public async Task CompleteAndSendAsync_LeavesItemPendienteWhenEmailFails()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var user = TestHelpers.CreateUser(db, "Ana", email: "ana@empresa.local");
            Assign(db, desktop, user);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Pendiente, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var emailService = new RecordingEmailService { ThrowOnSend = true };
            var service = CreateService(db, emailService);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAndSendAsync(item.Id, new MaintenanceCreateViewModel
            {
                Date = WithinJuneWindow,
                Description = "Mantenimiento preventivo semestral",
                TechnicianName = "Carlos Técnico"
            }));

            var reloaded = await db.MaintenanceCampaignItems.SingleAsync(c => c.Id == item.Id);
            Assert.Equal(MaintenanceCampaignStatus.Pendiente, reloaded.Status);
            Assert.Null(reloaded.MaintenanceId);
            Assert.Null(reloaded.EmailSentAtUtc);
        }

        [Fact]
        public async Task UploadSignedPdfAsync_ThrowsWhenNotCorreoEnviado()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Pendiente, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db);

            using var stream = new MemoryStream(new byte[] { 1 });
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadSignedPdfAsync(item.Id, stream, "acta.pdf", 1));
        }

        [Fact]
        public async Task UploadSignedPdfAsync_HappyPath_StoresBytesAndMarksRealizado()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.CorreoEnviado, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db);
            var expectedBytes = new byte[] { 1, 2, 3, 4 };

            using (var stream = new MemoryStream(expectedBytes))
            {
                await service.UploadSignedPdfAsync(item.Id, stream, "acta-firmada.pdf", uploadedByUserSystemId: 7);
            }

            var reloaded = await db.MaintenanceCampaignItems.SingleAsync(c => c.Id == item.Id);
            Assert.Equal(MaintenanceCampaignStatus.Realizado, reloaded.Status);
            Assert.Equal("acta-firmada.pdf", reloaded.SignedPdfFileName);
            Assert.Equal(expectedBytes, reloaded.SignedPdfContent);
            Assert.Equal(7, reloaded.SignedPdfUploadedByUserSystemId);
            Assert.NotNull(reloaded.SignedPdfUploadedAtUtc);
        }

        [Fact]
        public async Task GetSignedPdfAsync_ReturnsNullWhenNeverUploaded()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.CorreoEnviado, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db);

            var result = await service.GetSignedPdfAsync(item.Id);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetSignedPdfAsync_ReturnsBytesAfterUpload()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.CorreoEnviado, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db);

            using (var stream = new MemoryStream(new byte[] { 9, 9, 9 }))
            {
                await service.UploadSignedPdfAsync(item.Id, stream, "firmado.pdf", 1);
            }

            var result = await service.GetSignedPdfAsync(item.Id);

            Assert.NotNull(result);
            Assert.Equal("firmado.pdf", result!.Value.FileName);
            Assert.Equal(new byte[] { 9, 9, 9 }, result.Value.Content);
        }
    }
}
