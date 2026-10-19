using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class MaintenanceCampaignService : IMaintenanceCampaignService
    {
        // Año en que se despliega esta función — evita generar histórico falso hacia atrás
        // para desktops creados antes de que el módulo existiera.
        private const int CampaignEpochYear = 2026;

        private readonly MyDbContext _dbContext;
        private readonly IMaintenanceService _maintenanceService;
        private readonly IMaintenancePdfService _pdfService;
        private readonly IEmailService _emailService;

        public MaintenanceCampaignService(
            MyDbContext dbContext,
            IMaintenanceService maintenanceService,
            IMaintenancePdfService pdfService,
            IEmailService emailService)
        {
            _dbContext = dbContext;
            _maintenanceService = maintenanceService;
            _pdfService = pdfService;
            _emailService = emailService;
        }

        // Perezoso e idempotente: se llama desde GET Index. No hay background job en el
        // repo — un ítem Pendiente nunca se borra ni se duplica, por eso un período que no
        // se completó sigue apareciendo "atrasado" en los meses siguientes sin más lógica.
        public async Task SyncOpenPeriodsAsync(DateOnly? referenceDate = null, CancellationToken cancellationToken = default)
        {
            var today = referenceDate ?? DateOnly.FromDateTime(DateTime.Now);
            var openPeriods = GetOpenPeriods(today);

            if (openPeriods.Count == 0)
            {
                return;
            }

            var activeDesktopIds = await _dbContext.Desktops
                .Where(d => d.Estado)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            if (activeDesktopIds.Count == 0)
            {
                return;
            }

            var existingKeys = (await _dbContext.MaintenanceCampaignItems
                .Where(c => activeDesktopIds.Contains(c.DesktopId))
                .Select(c => new { c.DesktopId, c.PeriodYear, c.PeriodMonth })
                .ToListAsync(cancellationToken))
                .Select(k => (k.DesktopId, k.PeriodYear, k.PeriodMonth))
                .ToHashSet();

            var now = DateTime.UtcNow;
            foreach (var desktopId in activeDesktopIds)
            {
                foreach (var (year, month) in openPeriods)
                {
                    if (existingKeys.Contains((desktopId, year, month)))
                    {
                        continue;
                    }

                    _dbContext.MaintenanceCampaignItems.Add(new MaintenanceCampaignItem
                    {
                        DesktopId = desktopId,
                        PeriodYear = year,
                        PeriodMonth = month,
                        Status = MaintenanceCampaignStatus.Pendiente,
                        CreatedAtUtc = now
                    });
                }
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Dos requests concurrentes a Index sincronizando al mismo tiempo — el índice
                // único (DesktopId, PeriodYear, PeriodMonth) ya evitó el duplicado real.
            }
        }

        private static List<(int Year, int Month)> GetOpenPeriods(DateOnly today)
        {
            var periods = new List<(int, int)>();
            for (var year = CampaignEpochYear; year <= today.Year; year++)
            {
                if (new DateOnly(year, 6, 1) <= today)
                {
                    periods.Add((year, 6));
                }
                if (new DateOnly(year, 12, 1) <= today)
                {
                    periods.Add((year, 12));
                }
            }
            return periods;
        }

        public async Task<MaintenanceCampaignIndexViewModel> GetIndexAsync(bool onlyCompleted, CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            var query = _dbContext.MaintenanceCampaignItems
                .AsNoTracking()
                .Include(c => c.Desktop)
                .Include(c => c.Maintenance)
                .AsQueryable();

            if (onlyCompleted)
            {
                query = query.Where(c => c.Status == MaintenanceCampaignStatus.Realizado);
            }

            var items = await query
                .OrderBy(c => c.PeriodYear).ThenBy(c => c.PeriodMonth).ThenBy(c => c.Desktop.NameDesktop)
                .ToListAsync(cancellationToken);

            var result = new MaintenanceCampaignIndexViewModel();
            foreach (var item in items)
            {
                var periodEnd = new DateOnly(item.PeriodYear, item.PeriodMonth, DateTime.DaysInMonth(item.PeriodYear, item.PeriodMonth));
                var listItem = new MaintenanceCampaignListItem
                {
                    Id = item.Id,
                    DesktopId = item.DesktopId,
                    DesktopName = item.Desktop.NameDesktop,
                    SerialNumber = item.Desktop.SerialNumber,
                    PeriodYear = item.PeriodYear,
                    PeriodMonth = item.PeriodMonth,
                    EsAtrasado = item.Status != MaintenanceCampaignStatus.Realizado && today > periodEnd,
                    HolderName = item.Maintenance?.SnapshotHolderName,
                    EmailSentAtUtc = item.EmailSentAtUtc,
                    SignedAtUtc = item.SignedAtUtc
                };

                switch (item.Status)
                {
                    case MaintenanceCampaignStatus.Pendiente:
                        result.Pendientes.Add(listItem);
                        break;
                    case MaintenanceCampaignStatus.CorreoEnviado:
                        result.CorreoEnviado.Add(listItem);
                        break;
                    case MaintenanceCampaignStatus.Realizado:
                        result.Realizados.Add(listItem);
                        break;
                }
            }

            return result;
        }

        public async Task<MaintenanceCampaignItem?> GetByIdAsync(int campaignItemId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.MaintenanceCampaignItems
                .Include(c => c.Desktop)
                .SingleOrDefaultAsync(c => c.Id == campaignItemId, cancellationToken);
        }

        public async Task<int> CompleteAndSendAsync(int campaignItemId, MaintenanceCreateViewModel maintenanceVm, Func<int, string> buildSignUrl)
        {
            var item = await _dbContext.MaintenanceCampaignItems
                .Include(c => c.Desktop)
                .SingleOrDefaultAsync(c => c.Id == campaignItemId)
                ?? throw new InvalidOperationException("El ítem de campaña no existe.");

            if (item.Status != MaintenanceCampaignStatus.Pendiente)
            {
                throw new InvalidOperationException("Este mantenimiento ya fue completado.");
            }

            var currentAsignation = await _dbContext.Asignations
                .Where(a => a.DesktopId == item.DesktopId)
                .Include(a => a.User).ThenInclude(u => u.Area)
                .OrderByDescending(a => a.DateAsignation)
                .ThenByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            var isDisponible = currentAsignation is not null && !currentAsignation.User.Activo;
            if (currentAsignation is null || isDisponible || string.IsNullOrWhiteSpace(currentAsignation.User.Email))
            {
                throw new InvalidOperationException(
                    "Este equipo no tiene un usuario asignado con correo válido; no se puede enviar el acta de mantenimiento.");
            }

            // Nunca confiar en lo que mande el cliente para estos dos campos.
            maintenanceVm.DesktopId = item.DesktopId;
            maintenanceVm.Category = MaintenanceCategory.Preventivo;

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                var createdMaintenanceId = await _maintenanceService.CreateAsync(maintenanceVm);
                var usuarioNombre = $"{currentAsignation.User.Name} {currentAsignation.User.LastName}";

                // El correo se envía antes de mutar/guardar el ítem: si falla, no queda nada
                // para revertir y el ítem permanece Pendiente (ver CasoTIService.ResolveAsync,
                // mismo criterio).
                await _emailService.SendAsync(
                    currentAsignation.User.Email,
                    usuarioNombre,
                    "Mantenimiento preventivo programado",
                    BuildEmailBody(usuarioNombre, item.Desktop.NameDesktop, buildSignUrl(item.Id)),
                    Array.Empty<EmailAttachment>());

                item.MaintenanceId = createdMaintenanceId;
                item.Status = MaintenanceCampaignStatus.CorreoEnviado;
                item.EmailSentAtUtc = DateTime.UtcNow;
                item.EmailSentToAddress = currentAsignation.User.Email;
                // Snapshot de quién puede firmar — ver comentario en MaintenanceCampaignItem.
                item.AssignedSignerUsersId = currentAsignation.User.Id;
                await _dbContext.SaveChangesAsync();

                await transaction.CommitAsync();
                return createdMaintenanceId;
            });
        }

        public async Task<MaintenanceSignViewModel?> GetSignPromptAsync(int campaignItemId, int actingUsersId, CancellationToken cancellationToken = default)
        {
            var item = await _dbContext.MaintenanceCampaignItems
                .AsNoTracking()
                .Include(c => c.Desktop)
                .SingleOrDefaultAsync(c => c.Id == campaignItemId, cancellationToken);

            if (item is null || item.Status != MaintenanceCampaignStatus.CorreoEnviado || item.AssignedSignerUsersId != actingUsersId)
            {
                return null;
            }

            var savedSignature = await _dbContext.UserSavedSignatures
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.UserId == actingUsersId, cancellationToken);

            return new MaintenanceSignViewModel
            {
                CampaignItemId = item.Id,
                DesktopName = item.Desktop.NameDesktop,
                HasSavedSignature = savedSignature is not null,
                SavedSignatureData = savedSignature?.SignatureData
            };
        }

        public async Task SignAsync(
            int campaignItemId, int actingUsersId, int actingUserSystemId, string signatureData,
            MaintenanceSignatureMethod method, bool saveSignature, CancellationToken cancellationToken = default)
        {
            var item = await _dbContext.MaintenanceCampaignItems.SingleOrDefaultAsync(c => c.Id == campaignItemId, cancellationToken)
                ?? throw new InvalidOperationException("El ítem de campaña no existe.");

            if (item.Status != MaintenanceCampaignStatus.CorreoEnviado)
            {
                throw new InvalidOperationException("Este mantenimiento no está esperando firma.");
            }

            if (item.AssignedSignerUsersId != actingUsersId)
            {
                throw new InvalidOperationException("No puedes firmar un acta que no te corresponde.");
            }

            if (string.IsNullOrWhiteSpace(signatureData))
            {
                throw new InvalidOperationException("Debes dibujar tu firma o usar tu firma guardada.");
            }

            item.SignatureData = signatureData;
            item.SignatureMethod = method;
            item.SignedAtUtc = DateTime.UtcNow;
            item.SignerUserSystemId = actingUserSystemId;
            item.Status = MaintenanceCampaignStatus.Realizado;

            if (saveSignature)
            {
                var saved = await _dbContext.UserSavedSignatures.SingleOrDefaultAsync(s => s.UserId == actingUsersId, cancellationToken);
                if (saved is null)
                {
                    _dbContext.UserSavedSignatures.Add(new UserSavedSignature
                    {
                        UserId = actingUsersId,
                        SignatureData = signatureData,
                        SavedAtUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    saved.SignatureData = signatureData;
                    saved.SavedAtUtc = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<byte[]?> RegeneratePdfAsync(int campaignItemId, CancellationToken cancellationToken = default)
        {
            var item = await _dbContext.MaintenanceCampaignItems
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == campaignItemId, cancellationToken);

            if (item?.MaintenanceId is null)
            {
                return null;
            }

            var actaVm = await BuildActaViewModelAsync(item, cancellationToken);
            return _pdfService.GenerateExroActaPdf(actaVm);
        }

        private async Task<MaintenanceExroActaViewModel> BuildActaViewModelAsync(MaintenanceCampaignItem item, CancellationToken ct)
        {
            var maintenance = await _dbContext.Maintenances
                .Include(m => m.Checklist)
                .Include(m => m.EquipmentSnapshot)
                .SingleAsync(m => m.Id == item.MaintenanceId!.Value, ct);

            var signer = await _dbContext.Users
                .Include(u => u.Area)
                .SingleAsync(u => u.Id == item.AssignedSignerUsersId, ct);
            var usuarioNombre = $"{signer.Name} {signer.LastName}";

            return new MaintenanceExroActaViewModel
            {
                Date = maintenance.Date,
                UsuarioNombre = usuarioNombre,
                Area = signer.Area?.Name ?? "-",
                DescripcionFalla = maintenance.Description,
                TechnicianName = maintenance.TechnicianName,
                Equipment = maintenance.EquipmentSnapshot
                    .Select(s => new MaintenanceEquipmentRow { Label = s.Label, Brand = s.Brand, Model = s.Model, Serial = s.Serial })
                    .ToList(),
                Checklist = BuildChecklist(maintenance.Checklist),
                SignatureData = item.SignatureData,
                SignerName = usuarioNombre,
                SignedAtUtc = item.SignedAtUtc
            };
        }

        private static List<MaintenanceChecklistItem> BuildChecklist(MaintenanceChecklist? checklist)
        {
            return new List<MaintenanceChecklistItem>
            {
                new() { Label = "Verificación de sistema mecánico", Checked = checklist?.VerificacionSistemaMecanico ?? false },
                new() { Label = "Verificación de sistema eléctrico", Checked = checklist?.VerificacionSistemaElectrico ?? false },
                new() { Label = "Verificación de sistema de seguridad", Checked = checklist?.VerificacionSistemaSeguridad ?? false },
                new() { Label = "Soplado interno", Checked = checklist?.SopladoInterno ?? false },
                new() { Label = "Lavado Interno/Externo", Checked = checklist?.LavadoInterno ?? false },
                new() { Label = "Lubricación", Checked = checklist?.Lubricacion ?? false },
                new() { Label = "Ajuste de tornillería", Checked = checklist?.AjusteTornilleria ?? false },
                new() { Label = "Cambio de accesorios y repuestos por desgaste", Checked = checklist?.CambioAccesorios ?? false }
            };
        }

        private static string BuildEmailBody(string userName, string desktopName, string signUrl)
        {
            return $"""
                <p>Hola {userName},</p>
                <p>Se realizó el mantenimiento preventivo programado de tu equipo <strong>{desktopName}</strong>.</p>
                <p>Ingresa a la plataforma para firmar el acta de conformidad (necesitas iniciar sesión con tu cuenta):
                <a href="{signUrl}">firmar el acta</a>.</p>
                <p>Gracias.</p>
                """;
        }
    }
}
