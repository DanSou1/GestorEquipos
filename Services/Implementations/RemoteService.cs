using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Remote;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class RemoteService : IRemoteService
    {
        private readonly MyDbContext _dbContext;
        private readonly IPasswordHasher<UserSystem> _passwordHasher;

        public RemoteService(MyDbContext dbContext, IPasswordHasher<UserSystem> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        public async Task<List<RemoteInventoryItem>> GetInventoryAsync()
        {
            var remotes = await _dbContext.Remotes.AsNoTracking().ToListAsync();
            var assignedDesktops = await _dbContext.Desktops
                .AsNoTracking()
                .Where(d => d.RemoteId != null)
                .Select(d => new { d.Id, d.NameDesktop, d.RemoteId })
                .ToListAsync();

            var desktopsByRemote = assignedDesktops.ToLookup(d => d.RemoteId!.Value);

            return remotes
                .Select(r =>
                {
                    var assigned = desktopsByRemote[r.Id]
                        .Select(d => new AssignedDesktopItem { Id = d.Id, Name = d.NameDesktop })
                        .ToList();

                    return new RemoteInventoryItem
                    {
                        Id = r.Id,
                        ConnectionType = r.ConnectionType,
                        IPAddress = r.IPAddress,
                        Port = r.Port,
                        Username = r.Username,
                        AppDescription = r.AppDescription,
                        Estado = assigned.Count > 0 ? "Asignado" : "Disponible",
                        AssignedDesktops = assigned
                    };
                })
                .ToList();
        }

        public async Task<RemoteInventoryStatsViewModel> GetInventoryStatsAsync()
        {
            var items = await GetInventoryAsync();

            return new RemoteInventoryStatsViewModel
            {
                Total = items.Count,
                Asignados = items.Count(i => i.Estado == "Asignado"),
                Disponibles = items.Count(i => i.Estado == "Disponible")
            };
        }

        public async Task<RemoteDetailViewModel?> GetDetailAsync(int id)
        {
            var remote = await _dbContext.Remotes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
            if (remote is null)
            {
                return null;
            }

            var assigned = await _dbContext.Desktops
                .AsNoTracking()
                .Where(d => d.RemoteId == id)
                .Select(d => new AssignedDesktopItem { Id = d.Id, Name = d.NameDesktop })
                .ToListAsync();

            return new RemoteDetailViewModel
            {
                Id = remote.Id,
                ConnectionType = remote.ConnectionType,
                IPAddress = remote.IPAddress,
                Port = remote.Port,
                Username = remote.Username,
                Password = remote.Password,
                AppDescription = remote.AppDescription,
                Estado = assigned.Count > 0 ? "Asignado" : "Disponible",
                AssignedDesktops = assigned
            };
        }

        public async Task<Remote?> GetByIdAsync(int id)
        {
            return await _dbContext.Remotes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
        }

        // LOG-05: the "is this desktop already linked to another Remote" check and the write
        // that follows used to run unprotected — two concurrent requests could both pass the
        // check before either commits. Serializable isolation closes that window.
        public async Task UpdateAsync(int id, RemoteEditViewModel vm, int actingUserSystemId)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                var remote = await _dbContext.Remotes.SingleOrDefaultAsync(r => r.Id == id)
                    ?? throw new InvalidOperationException("Acceso remoto no encontrado.");

                Desktop? target = null;
                if (vm.DesktopId is not null)
                {
                    target = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == vm.DesktopId)
                        ?? throw new InvalidOperationException("Equipo no encontrado.");

                    if (!target.Estado)
                    {
                        throw new InvalidOperationException("No se puede vincular un acceso remoto a un equipo dado de baja (RAES).");
                    }

                    if (target.RemoteId is not null && target.RemoteId != id)
                    {
                        throw new InvalidOperationException("Este equipo ya tiene otro acceso remoto asignado. Libéralo primero desde ese registro.");
                    }
                }

                var isRdp = vm.ConnectionType == RemoteConnectionType.EscritorioRemotoWindows;
                remote.ConnectionType = vm.ConnectionType;
                remote.IPAddress = isRdp ? vm.IPAddress : null;
                remote.Port = isRdp ? vm.Port : null;
                remote.Username = isRdp ? vm.Username : null;
                remote.Password = isRdp ? vm.Password : null;
                remote.AppDescription = isRdp ? null : vm.AppDescription;

                // Remote has no back-collection to Desktop, and nothing stops more than one
                // Desktop.RemoteId pointing at the same row (see CLAUDE.md) — capture every
                // previously-linked id before unlinking, rather than assuming there's only one.
                var currentlyLinked = await _dbContext.Desktops.Where(d => d.RemoteId == id).ToListAsync();
                var previouslyLinkedIds = currentlyLinked.Select(d => d.Id).ToList();
                foreach (var desktop in currentlyLinked)
                {
                    if (target is null || desktop.Id != target.Id)
                    {
                        desktop.RemoteId = null;
                    }
                }

                if (target is not null)
                {
                    target.RemoteId = id;
                }

                // SEG-09: only log an actual reassignment/release, not every metadata edit.
                var linkChanged = target is null
                    ? previouslyLinkedIds.Count > 0
                    : previouslyLinkedIds.Count != 1 || previouslyLinkedIds[0] != target.Id;
                if (linkChanged)
                {
                    var description = target is null
                        ? $"Acceso remoto #{id} liberado (antes en equipo(s) {string.Join(", ", previouslyLinkedIds.Select(d => "#" + d))})"
                        : $"Acceso remoto #{id} reasignado a equipo #{target.Id}";
                    await LogAuditAsync(actingUserSystemId, "RemoteReassigned", description);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            });
        }

        public async Task ReleaseAsync(int id, int actingUserSystemId)
        {
            var exists = await _dbContext.Remotes.AnyAsync(r => r.Id == id);
            if (!exists)
            {
                throw new InvalidOperationException("Acceso remoto no encontrado.");
            }

            var linkedDesktops = await _dbContext.Desktops.Where(d => d.RemoteId == id).ToListAsync();
            if (linkedDesktops.Count == 0)
            {
                return;
            }

            foreach (var desktop in linkedDesktops)
            {
                desktop.RemoteId = null;
            }

            await LogAuditAsync(actingUserSystemId, "RemoteReleased",
                $"Acceso remoto #{id} liberado de equipo(s) {string.Join(", ", linkedDesktops.Select(d => "#" + d.Id))}");
            await _dbContext.SaveChangesAsync();
        }

        // LOG-05: see the matching comment on UpdateAsync above.
        public async Task AssignToDesktopAsync(int remoteId, int desktopId, int actingUserSystemId)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                var remoteExists = await _dbContext.Remotes.AnyAsync(r => r.Id == remoteId);
                if (!remoteExists)
                {
                    throw new InvalidOperationException("Acceso remoto no encontrado.");
                }

                var desktop = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == desktopId)
                    ?? throw new InvalidOperationException("Equipo no encontrado.");

                if (!desktop.Estado)
                {
                    throw new InvalidOperationException("No se puede vincular un acceso remoto a un equipo dado de baja (RAES).");
                }

                if (desktop.RemoteId is not null)
                {
                    throw new InvalidOperationException("Este equipo ya tiene un acceso remoto asignado. Reemplázalo desde Editar equipo.");
                }

                desktop.RemoteId = remoteId;
                await LogAuditAsync(actingUserSystemId, "RemoteReassigned", $"Acceso remoto #{remoteId} asignado a equipo #{desktopId}");
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            });
        }

        public async Task DeleteAsync(int id, int actingAdminUserSystemId, string? adminPassword)
        {
            var remote = await _dbContext.Remotes.SingleOrDefaultAsync(r => r.Id == id)
                ?? throw new InvalidOperationException("Acceso remoto no encontrado.");

            if (string.IsNullOrEmpty(adminPassword))
            {
                throw new InvalidOperationException("Debes ingresar tu contraseña de administrador para eliminar.");
            }

            var adminUserSystem = await _dbContext.UserSystems.SingleOrDefaultAsync(us => us.Id == actingAdminUserSystemId)
                ?? throw new InvalidOperationException("No se pudo verificar la cuenta del administrador.");

            var verificationResult = _passwordHasher.VerifyHashedPassword(adminUserSystem, adminUserSystem.PasswordHash, adminPassword);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                throw new InvalidOperationException("Contraseña de administrador incorrecta.");
            }

            if (await _dbContext.Desktops.AnyAsync(d => d.RemoteId == id))
            {
                throw new InvalidOperationException("No se puede eliminar: este acceso remoto está asignado a uno o más equipos. Libéralo primero.");
            }

            _dbContext.Remotes.Remove(remote);
            await _dbContext.SaveChangesAsync();
        }

        // SEG-09: see the matching comment on UserAdminService.LogAuditAsync.
        private async Task LogAuditAsync(int actorUserSystemId, string action, string targetDescription)
        {
            var actorUsername = await _dbContext.UserSystems
                .Where(us => us.Id == actorUserSystemId)
                .Select(us => us.Username)
                .SingleOrDefaultAsync() ?? $"UserSystem#{actorUserSystemId}";

            _dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                ActorUserSystemId = actorUserSystemId,
                ActorDescription = actorUsername,
                Action = action,
                TargetDescription = targetDescription
            });
        }
    }
}
