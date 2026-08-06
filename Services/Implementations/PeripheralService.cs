using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Peripheral;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class PeripheralService : IPeripheralService
    {
        private readonly MyDbContext _dbContext;

        public PeripheralService(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<int> AddAsync(PeripheralCreateViewModel vm)
        {
            var peripheral = new Peripheral
            {
                DesktopId = vm.DesktopId,
                PeripheralTypeId = vm.PeripheralTypeId,
                Brand = vm.Brand,
                Model = vm.Model,
                Serial = vm.Serial,
                Estado = PeripheralEstado.Activo
            };

            _dbContext.Peripherals.Add(peripheral);
            await _dbContext.SaveChangesAsync();
            return peripheral.Id;
        }

        public async Task AddObservationAsync(int peripheralId, PeripheralObservationCreateViewModel vm)
        {
            var peripheral = await _dbContext.Peripherals.SingleOrDefaultAsync(p => p.Id == peripheralId)
                ?? throw new InvalidOperationException("Periférico no encontrado.");

            _dbContext.PeripheralObservations.Add(new PeripheralObservation
            {
                PeripheralId = peripheralId,
                Date = vm.Date,
                Type = vm.Type,
                Description = vm.Description
            });

            peripheral.Estado = vm.Type switch
            {
                PeripheralObservationType.BajaRAES => PeripheralEstado.Raes,
                PeripheralObservationType.Reparacion => PeripheralEstado.Activo,
                PeripheralObservationType.Cambio => vm.ResultingEstado ?? peripheral.Estado,
                _ => peripheral.Estado
            };

            await _dbContext.SaveChangesAsync();
        }
    }
}
