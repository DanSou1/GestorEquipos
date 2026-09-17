using Gestor_Equipos.Data;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.License;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class LicenseService : ILicenseService
    {
        private readonly MyDbContext _dbContext;

        public LicenseService(MyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // LOG-09: see the matching comment on DesktopService.CreateAsync.
        public async Task<int> AddAsync(LicenseCreateViewModel vm)
        {
            if (!await _dbContext.Desktops.AnyAsync(d => d.Id == vm.DesktopId))
            {
                throw new InvalidOperationException("El equipo seleccionado no existe.");
            }

            var license = new License
            {
                DesktopId = vm.DesktopId,
                SoftwareType = vm.SoftwareType,
                LicenseKey = vm.NoLicense ? null : vm.LicenseKey,
                NoLicense = vm.NoLicense
            };

            _dbContext.Licenses.Add(license);
            await _dbContext.SaveChangesAsync();
            return license.Id;
        }
    }
}
