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
        private static readonly Func<int, string> BuildSignUrl = id => $"https://x/sign/{id}";

        private class FakePdfService : IMaintenancePdfService
        {
            public MaintenanceExroActaViewModel? LastExroActaDetail { get; private set; }

            public byte[] GenerateMaintenancePdf(MaintenancePdfViewModel detail) => new byte[] { 1, 2, 3 };

            public byte[] GenerateExroActaPdf(MaintenanceExroActaViewModel detail)
            {
                LastExroActaDetail = detail;
                return new byte[] { 1, 2, 3 };
            }
        }

        private class RecordingEmailService : IEmailService
        {
            public List<(string ToEmail, string ToName, string Subject, string BodyHtml)> Sent { get; } = new();
            public bool ThrowOnSend { get; set; }

            public Task SendAsync(string toEmail, string toName, string subject, string bodyHtml,
                IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default)
            {
                if (ThrowOnSend)
                {
                    throw new InvalidOperationException("SMTP falló (simulado).");
                }

                Sent.Add((toEmail, toName, subject, bodyHtml));
                return Task.CompletedTask;
            }
        }

        private static FakePdfService _lastPdfService = new();

        private static MaintenanceCampaignService CreateService(MyDbContext db, RecordingEmailService? emailService = null, FakePdfService? pdfService = null)
        {
            _lastPdfService = pdfService ?? new FakePdfService();
            return new MaintenanceCampaignService(db, new MaintenanceService(db), _lastPdfService, emailService ?? new RecordingEmailService());
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
                () => service.CompleteAndSendAsync(item.Id, new MaintenanceCreateViewModel(), BuildSignUrl));
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
                () => service.CompleteAndSendAsync(item.Id, new MaintenanceCreateViewModel { Date = WithinJuneWindow, Description = "x", TechnicianName = "Tec" }, BuildSignUrl));
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
                () => service.CompleteAndSendAsync(item.Id, new MaintenanceCreateViewModel { Date = WithinJuneWindow, Description = "x", TechnicianName = "Tec" }, BuildSignUrl));
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
            }, BuildSignUrl);

            var reloaded = await db.MaintenanceCampaignItems.SingleAsync(c => c.Id == item.Id);
            Assert.Equal(MaintenanceCampaignStatus.CorreoEnviado, reloaded.Status);
            Assert.Equal(maintenanceId, reloaded.MaintenanceId);
            Assert.Equal("ana@empresa.local", reloaded.EmailSentToAddress);
            Assert.NotNull(reloaded.EmailSentAtUtc);
            Assert.Equal(user.Id, reloaded.AssignedSignerUsersId);

            var maintenance = await db.Maintenances.SingleAsync(m => m.Id == maintenanceId);
            Assert.Equal(MaintenanceCategory.Preventivo, maintenance.Category);
            Assert.Equal(desktop.Id, maintenance.DesktopId);

            Assert.Single(emailService.Sent);
            Assert.Equal("ana@empresa.local", emailService.Sent[0].ToEmail);
            Assert.Contains($"https://x/sign/{item.Id}", emailService.Sent[0].BodyHtml);
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
            }, BuildSignUrl));

            var reloaded = await db.MaintenanceCampaignItems.SingleAsync(c => c.Id == item.Id);
            Assert.Equal(MaintenanceCampaignStatus.Pendiente, reloaded.Status);
            Assert.Null(reloaded.MaintenanceId);
            Assert.Null(reloaded.EmailSentAtUtc);
            Assert.Null(reloaded.AssignedSignerUsersId);
        }

        private async Task<(MyDbContext Db, MaintenanceCampaignService Service, MaintenanceCampaignItem Item, Users Signer)> SeedCorreoEnviadoItemAsync(RecordingEmailService? emailService = null, FakePdfService? pdfService = null)
        {
            var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var user = TestHelpers.CreateUser(db, "Ana", email: "ana@empresa.local");
            Assign(db, desktop, user);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Pendiente, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db, emailService, pdfService);

            await service.CompleteAndSendAsync(item.Id, new MaintenanceCreateViewModel
            {
                Date = WithinJuneWindow,
                Description = "Mantenimiento preventivo semestral",
                TechnicianName = "Carlos Técnico"
            }, BuildSignUrl);

            var reloaded = await db.MaintenanceCampaignItems.SingleAsync(c => c.Id == item.Id);
            return (db, service, reloaded, user);
        }

        [Fact]
        public async Task SignAsync_HappyPath_MarksRealizadoStoresSignatureAndMethod()
        {
            var (db, service, item, signer) = await SeedCorreoEnviadoItemAsync();
            const string signatureData = "[[{\"x\":1,\"y\":1}]]";

            await service.SignAsync(item.Id, signer.Id, actingUserSystemId: 42, signatureData, MaintenanceSignatureMethod.Manuscrita, saveSignature: false);

            var reloaded = await db.MaintenanceCampaignItems.SingleAsync(c => c.Id == item.Id);
            Assert.Equal(MaintenanceCampaignStatus.Realizado, reloaded.Status);
            Assert.Equal(signatureData, reloaded.SignatureData);
            Assert.Equal(MaintenanceSignatureMethod.Manuscrita, reloaded.SignatureMethod);
            Assert.Equal(42, reloaded.SignerUserSystemId);
            Assert.NotNull(reloaded.SignedAtUtc);
            db.Dispose();
        }

        [Fact]
        public async Task SignAsync_ThrowsWhenActingUsersIdIsNotAssignedSigner()
        {
            var (db, service, item, _) = await SeedCorreoEnviadoItemAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SignAsync(item.Id, actingUsersId: 999999, actingUserSystemId: 1, "[[{\"x\":1,\"y\":1}]]", MaintenanceSignatureMethod.Manuscrita, false));
            db.Dispose();
        }

        [Fact]
        public async Task SignAsync_ThrowsWhenStatusIsNotCorreoEnviado()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var user = TestHelpers.CreateUser(db, "Ana", email: "ana@empresa.local");
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Pendiente, CreatedAtUtc = DateTime.UtcNow, AssignedSignerUsersId = user.Id };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SignAsync(item.Id, user.Id, 1, "[[{\"x\":1,\"y\":1}]]", MaintenanceSignatureMethod.Manuscrita, false));
        }

        [Fact]
        public async Task SignAsync_WithSaveSignatureTrue_UpsertsUserSavedSignature()
        {
            var (db, service, item, signer) = await SeedCorreoEnviadoItemAsync();
            const string signatureData = "[[{\"x\":1,\"y\":1}]]";

            await service.SignAsync(item.Id, signer.Id, 1, signatureData, MaintenanceSignatureMethod.Manuscrita, saveSignature: true);

            var saved = await db.UserSavedSignatures.SingleAsync(s => s.UserId == signer.Id);
            Assert.Equal(signatureData, saved.SignatureData);
            db.Dispose();
        }

        [Fact]
        public async Task SignAsync_WithFirmaGuardadaMethod_ReusesStoredSignatureData()
        {
            var (db, service, item, signer) = await SeedCorreoEnviadoItemAsync();
            const string savedData = "[[{\"x\":5,\"y\":5}]]";
            db.UserSavedSignatures.Add(new UserSavedSignature { UserId = signer.Id, SignatureData = savedData, SavedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();

            await service.SignAsync(item.Id, signer.Id, 1, savedData, MaintenanceSignatureMethod.FirmaGuardada, saveSignature: false);

            var reloaded = await db.MaintenanceCampaignItems.SingleAsync(c => c.Id == item.Id);
            Assert.Equal(savedData, reloaded.SignatureData);
            Assert.Equal(MaintenanceSignatureMethod.FirmaGuardada, reloaded.SignatureMethod);
            db.Dispose();
        }

        [Fact]
        public async Task GetSignPromptAsync_ReturnsNullWhenActingUsersIdIsNotTheAssignedSigner()
        {
            var (db, service, item, _) = await SeedCorreoEnviadoItemAsync();

            var result = await service.GetSignPromptAsync(item.Id, actingUsersId: 999999);

            Assert.Null(result);
            db.Dispose();
        }

        [Fact]
        public async Task GetSignPromptAsync_ReportsSavedSignatureWhenPresent()
        {
            var (db, service, item, signer) = await SeedCorreoEnviadoItemAsync();
            db.UserSavedSignatures.Add(new UserSavedSignature { UserId = signer.Id, SignatureData = "[[{\"x\":1,\"y\":1}]]", SavedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var result = await service.GetSignPromptAsync(item.Id, signer.Id);

            Assert.NotNull(result);
            Assert.True(result!.HasSavedSignature);
            db.Dispose();
        }

        [Fact]
        public async Task RegeneratePdfAsync_ReturnsNullBeforeMaintenanceExists()
        {
            using var db = TestHelpers.CreateDbContext();
            var desktop = TestHelpers.CreateDesktop(db);
            var item = new MaintenanceCampaignItem { DesktopId = desktop.Id, PeriodYear = 2026, PeriodMonth = 6, Status = MaintenanceCampaignStatus.Pendiente, CreatedAtUtc = DateTime.UtcNow };
            db.MaintenanceCampaignItems.Add(item);
            await db.SaveChangesAsync();
            var service = CreateService(db);

            var result = await service.RegeneratePdfAsync(item.Id);

            Assert.Null(result);
        }

        [Fact]
        public async Task RegeneratePdfAsync_RendersSignatureAfterSigned()
        {
            var pdfService = new FakePdfService();
            var (db, service, item, signer) = await SeedCorreoEnviadoItemAsync(pdfService: pdfService);
            const string signatureData = "[[{\"x\":1,\"y\":1}]]";
            await service.SignAsync(item.Id, signer.Id, 1, signatureData, MaintenanceSignatureMethod.Manuscrita, false);

            var bytes = await service.RegeneratePdfAsync(item.Id);

            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes!);
            Assert.NotNull(pdfService.LastExroActaDetail);
            Assert.Equal(signatureData, pdfService.LastExroActaDetail!.SignatureData);
            Assert.Equal($"{signer.Name} {signer.LastName}", pdfService.LastExroActaDetail.SignerName);
            db.Dispose();
        }
    }
}
