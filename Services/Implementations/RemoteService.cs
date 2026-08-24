using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Remote;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class RemoteService : IRemoteService
    {
        private readonly MyDbContext _dbContext;

        public RemoteService(MyDbContext dbContext)
        {
            _dbContext = dbContext;
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

        public async Task UpdateAsync(int id, RemoteEditViewModel vm)
        {
            var remote = await _dbContext.Remotes.SingleOrDefaultAsync(r => r.Id == id)
                ?? throw new InvalidOperationException("Acceso remoto no encontrado.");

            var isRdp = vm.ConnectionType == RemoteConnectionType.EscritorioRemotoWindows;
            remote.ConnectionType = vm.ConnectionType;
            remote.IPAddress = isRdp ? vm.IPAddress : null;
            remote.Port = isRdp ? vm.Port : null;
            remote.Username = isRdp ? vm.Username : null;
            remote.Password = isRdp ? vm.Password : null;
            remote.AppDescription = isRdp ? null : vm.AppDescription;

            await _dbContext.SaveChangesAsync();
        }

        public async Task ReleaseAsync(int id)
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

            await _dbContext.SaveChangesAsync();
        }

        public async Task AssignToDesktopAsync(int remoteId, int desktopId)
        {
            var remoteExists = await _dbContext.Remotes.AnyAsync(r => r.Id == remoteId);
            if (!remoteExists)
            {
                throw new InvalidOperationException("Acceso remoto no encontrado.");
            }

            var desktop = await _dbContext.Desktops.SingleOrDefaultAsync(d => d.Id == desktopId)
                ?? throw new InvalidOperationException("Equipo no encontrado.");

            if (desktop.RemoteId is not null)
            {
                throw new InvalidOperationException("Este equipo ya tiene un acceso remoto asignado. Reemplázalo desde Editar equipo.");
            }

            desktop.RemoteId = remoteId;
            await _dbContext.SaveChangesAsync();
        }
    }
}
