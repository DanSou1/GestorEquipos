using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly MyDbContext _dbContext;

        // Kept in sync with the equipment table on the PDF (Desktop plus these peripheral
        // types) — see MaintenanceEquipmentSnapshotItem for why this is captured once here
        // rather than queried live at download time.
        private static readonly string[] EquipmentPeripheralTypeNames = { "Mouse", "Teclado", "Monitor" };

        public MaintenanceService(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // LOG-09: see the matching comment on DesktopService.CreateAsync.
        public async Task<int> CreateAsync(MaintenanceCreateViewModel vm)
        {
            if (!await _dbContext.Desktops.AnyAsync(d => d.Id == vm.DesktopId))
            {
                throw new InvalidOperationException("El equipo seleccionado no existe.");
            }

            if (!await _dbContext.MaintenanceTypes.AnyAsync(t => t.Id == vm.MaintenanceTypeId))
            {
                throw new InvalidOperationException("El tipo de mantenimiento seleccionado no existe.");
            }

            var maintenance = new Maintenance
            {
                DesktopId = vm.DesktopId,
                MaintenanceTypeId = vm.MaintenanceTypeId,
                Category = vm.Category,
                Date = vm.Date,
                Description = vm.Description,
                TechnicianName = vm.TechnicianName
            };

            if (vm.Category == MaintenanceCategory.Preventivo)
            {
                maintenance.Checklist = new MaintenanceChecklist
                {
                    VerificacionSistemaMecanico = vm.ChecklistVerificacionSistemaMecanico,
                    VerificacionSistemaElectrico = vm.ChecklistVerificacionSistemaElectrico,
                    VerificacionSistemaSeguridad = vm.ChecklistVerificacionSistemaSeguridad,
                    SopladoInterno = vm.ChecklistSopladoInterno,
                    LavadoInterno = vm.ChecklistLavadoInterno,
                    Lubricacion = vm.ChecklistLubricacion,
                    AjusteTornilleria = vm.ChecklistAjusteTornilleria,
                    CambioAccesorios = vm.ChecklistCambioAccesorios
                };

                var currentAsignation = await _dbContext.Asignations
                    .Where(a => a.DesktopId == vm.DesktopId)
                    .Include(a => a.User).ThenInclude(u => u.Area)
                    .OrderByDescending(a => a.DateAsignation)
                    .ThenByDescending(a => a.Id)
                    .FirstOrDefaultAsync();
                var isDisponible = currentAsignation is not null && !currentAsignation.User.Activo;
                maintenance.SnapshotHolderName = currentAsignation is null
                    ? "Sin asignar"
                    : isDisponible
                        ? "Disponible"
                        : $"{currentAsignation.User.Name} {currentAsignation.User.LastName}";
                maintenance.SnapshotHolderArea = currentAsignation?.User.Area?.Name ?? "-";

                var desktop = await _dbContext.Desktops.SingleAsync(d => d.Id == vm.DesktopId);
                maintenance.EquipmentSnapshot.Add(new MaintenanceEquipmentSnapshotItem
                {
                    Label = "Desktop",
                    Brand = desktop.Brand,
                    Model = desktop.Model,
                    Serial = desktop.SerialNumber
                });

                var peripherals = await _dbContext.Peripherals
                    .Include(p => p.PeripheralType)
                    .Where(p => p.DesktopId == vm.DesktopId
                        && p.Estado
                        && EquipmentPeripheralTypeNames.Contains(p.PeripheralType.Name))
                    .OrderBy(p => p.PeripheralType.Name)
                    .ToListAsync();

                foreach (var peripheral in peripherals)
                {
                    maintenance.EquipmentSnapshot.Add(new MaintenanceEquipmentSnapshotItem
                    {
                        Label = peripheral.PeripheralType.Name,
                        Brand = peripheral.Brand,
                        Model = peripheral.Model,
                        Serial = peripheral.Serial
                    });
                }
            }

            _dbContext.Maintenances.Add(maintenance);
            await _dbContext.SaveChangesAsync();
            return maintenance.Id;
        }
    }
}
