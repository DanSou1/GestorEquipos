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

        public async Task<int> AddAsync(LicenseCreateViewModel vm)
        {
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

        public async Task<List<License>> GetByDesktopAsync(int desktopId)
        {
            return await _dbContext.Licenses
                .Where(l => l.DesktopId == desktopId)
                .ToListAsync();
        }
    }
}
